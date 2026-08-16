// ============================================================
//  DeepSeek Harness 启动器 - WPF 重构版 (代码式 WPF, 无 XAML 编译链)
//  v0.1 骨架: 深色主题 + WindowChrome 无边框窗(原生缩放/吸附) + 侧栏导航 + 切页淡入动画
//  编译: build.bat (仅用系统自带 csc + GAC WPF 程序集)
// ============================================================
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shell;
using System.Windows.Threading;

namespace DeepSeekHarness
{
    // ---------- 调色板 (深色科技蓝 / 浅色护眼高质感 完整色彩系统) ----------
    static class Palette
    {
        public static bool IsDark = true;

        public static Color BgDeep { get { return IsDark ? Color.FromRgb(9, 13, 23) : Color.FromRgb(238, 242, 248); } }
        public static Color Bg { get { return IsDark ? Color.FromRgb(13, 18, 32) : Color.FromRgb(244, 247, 252); } }
        public static Color BgCard { get { return IsDark ? Color.FromRgb(19, 27, 46) : Color.FromRgb(255, 255, 255); } }
        public static Color BgCardHover { get { return IsDark ? Color.FromRgb(25, 35, 58) : Color.FromRgb(240, 244, 252); } }
        public static Color BgInput { get { return IsDark ? Color.FromRgb(20, 28, 48) : Color.FromRgb(235, 240, 248); } }
        public static Color Blue = Color.FromRgb(77, 107, 254);
        public static Color BlueLight = Color.FromRgb(110, 136, 255);
        public static Color Cyan = Color.FromRgb(0, 210, 255);
        public static Color Text { get { return IsDark ? Color.FromRgb(235, 238, 245) : Color.FromRgb(15, 23, 42); } }
        public static Color TextDim { get { return IsDark ? Color.FromRgb(156, 168, 196) : Color.FromRgb(51, 65, 85); } }
        public static Color TextFaint { get { return IsDark ? Color.FromRgb(90, 104, 141) : Color.FromRgb(100, 116, 139); } }
        public static Color Success { get { return IsDark ? Color.FromRgb(34, 197, 94) : Color.FromRgb(5, 150, 105); } }
        public static Color Warn { get { return IsDark ? Color.FromRgb(245, 158, 11) : Color.FromRgb(217, 119, 6); } }
        public static Color WarnLight { get { return IsDark ? Color.FromRgb(255, 178, 44) : Color.FromRgb(245, 158, 11); } }
        public static Color Error { get { return IsDark ? Color.FromRgb(239, 68, 68) : Color.FromRgb(220, 38, 38); } }
        public static Color Border { get { return IsDark ? Color.FromArgb(28, 255, 255, 255) : Color.FromArgb(45, 0, 0, 0); } }
        public static Color BorderSoft { get { return IsDark ? Color.FromArgb(16, 255, 255, 255) : Color.FromArgb(28, 0, 0, 0); } }

        public static Brush Brush(Color c) { return new SolidColorBrush(c); }
        public static Brush BrushA(Color c, byte a) { return new SolidColorBrush(Color.FromArgb(a, c.R, c.G, c.B)); }

        // 微渐变卡片背景
        public static LinearGradientBrush CardGradient()
        {
            return IsDark
                ? new LinearGradientBrush(Color.FromRgb(22, 31, 52), Color.FromRgb(16, 23, 40), new Point(0, 0), new Point(0, 1))
                : new LinearGradientBrush(Color.FromRgb(255, 255, 255), Color.FromRgb(255, 255, 255), new Point(0, 0), new Point(0, 1));
        }

        // 品牌主色高光渐变
        public static LinearGradientBrush BlueGradient()
        {
            return new LinearGradientBrush(
                Color.FromRgb(89, 119, 254),
                Color.FromRgb(67, 97, 245),
                new Point(0, 0),
                new Point(0, 1)
            );
        }

        // 卡片 1px 倒角高光边框 (浅色下为精致细线，消除晃眼光晕)
        public static LinearGradientBrush CardBorderBrush()
        {
            var b = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
            if (IsDark)
            {
                b.GradientStops.Add(new GradientStop(Color.FromArgb(36, 255, 255, 255), 0.0));
                b.GradientStops.Add(new GradientStop(Color.FromArgb(12, 255, 255, 255), 1.0));
            }
            else
            {
                b.GradientStops.Add(new GradientStop(Color.FromArgb(40, 0, 0, 0), 0.0));
                b.GradientStops.Add(new GradientStop(Color.FromArgb(20, 0, 0, 0), 1.0));
            }
            return b;
        }

        // 柔和自然微投影 (浅色下采用自然中性投影，消除蓝色发散眩光)
        public static DropShadowEffect CardShadow()
        {
            return new DropShadowEffect
            {
                BlurRadius = IsDark ? 16 : 8,
                ShadowDepth = IsDark ? 2 : 1,
                Direction = 270,
                Color = Colors.Black,
                Opacity = IsDark ? 0.25 : 0.06
            };
        }

        // 品牌发光投影
        public static DropShadowEffect GlowEffect(Color c, double opacity = 0.35)
        {
            return new DropShadowEffect
            {
                BlurRadius = IsDark ? 16 : 10,
                ShadowDepth = 0,
                Color = c,
                Opacity = IsDark ? opacity : opacity * 0.5
            };
        }
    }

    // ---------- 程序入口 ----------
    static class App
    {
        static Mutex singletonMutex;

        [STAThread]
        static void Main()
        {
            // 单实例: 已在运行则发信号让旧窗口弹出, 自己退出
            // 隐藏参数 --sandbox: 换独立 Mutex 名, 允许与生产实例并存 (仅供隔离测试)
            bool sandbox = false;
            bool diagTest = false;
            try
            {
                foreach (string a in Environment.GetCommandLineArgs())
                {
                    if (a == "--sandbox") sandbox = true;
                    if (a == "--diag-test") diagTest = true;
                }
            }
            catch { }
            bool createdNew;
            singletonMutex = new Mutex(true, "DeepSeekHarness.Launcher.WPF.v1" + (sandbox ? ".sandbox" : ""), out createdNew);
            if (sandbox) Dsh.SandboxMode = true;
            if (diagTest) Dsh.DiagTestMode = true;
            if (!createdNew)
            {
                try { File.WriteAllText(Proc.ReopenFlagPath(), "1"); } catch { }
                return;
            }
            try { Proc.DebugMode = Environment.GetEnvironmentVariable("DSH_LAUNCHER_DEBUG") == "1"; } catch { }
            // 全局未捕获异常 → crash.log
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
            {
                try { File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + e.ExceptionObject + "\r\n"); } catch { }
            };
            var app = new Application();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;   // 托盘常驻, 退出走托盘菜单
            ApplyGlobalButtonStyle(app);   // 全局圆角按钮模板, 统一所有按钮外观

            var splash = new SplashWindow();
            splash.Show();
            var win = new MainWindow();
            win.Loaded += delegate { splash.FadeOut(); };
            app.Run(win);
            GC.KeepAlive(singletonMutex);
        }

        // 全局控件样式: 圆角深色/浅色自适应模板, 弹性内边距, 聚焦高光, 极简 Slim 滚动条
        static void ApplyGlobalButtonStyle(Application app)
        {
            // 按钮全局模板 (带微物理回弹与悬浮平滑变色)
            var style = new Style(typeof(Button));
            style.Setters.Add(new Setter(Control.FocusVisualStyleProperty, null));
            style.Setters.Add(new Setter(Control.CursorProperty, Cursors.Hand));
            style.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(0)));
            var template = new ControlTemplate(typeof(Button));
            var border = new FrameworkElementFactory(typeof(Border));
            border.Name = "BtnBorder";
            border.SetValue(Border.CornerRadiusProperty, new CornerRadius(10));
            border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
            border.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            border.SetValue(Border.BorderBrushProperty, Palette.Brush(Palette.BorderSoft));
            var cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            border.AppendChild(cp);
            template.VisualTree = border;
            style.Setters.Add(new Setter(Control.TemplateProperty, template));
            app.Resources[typeof(Button)] = style;

            // 文本框全局模板: 聚焦发光焦点环
            var tbStyle = new Style(typeof(System.Windows.Controls.TextBox));
            tbStyle.Setters.Add(new Setter(Control.FocusVisualStyleProperty, null));
            var tbTemplate = new ControlTemplate(typeof(System.Windows.Controls.TextBox));
            var tbBorder = new FrameworkElementFactory(typeof(Border));
            tbBorder.Name = "TbBorder";
            tbBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            tbBorder.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
            tbBorder.SetValue(Border.BorderBrushProperty, Palette.Brush(Palette.BorderSoft));
            tbBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            tbBorder.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
            var sv = new FrameworkElementFactory(typeof(ScrollViewer));
            sv.Name = "PART_ContentHost";
            sv.SetValue(ScrollViewer.VerticalAlignmentProperty, new TemplateBindingExtension(Control.VerticalContentAlignmentProperty));
            tbBorder.AppendChild(sv);
            tbTemplate.VisualTree = tbBorder;

            var focusTrigger = new Trigger { Property = UIElement.IsFocusedProperty, Value = true };
            focusTrigger.Setters.Add(new Setter(Border.BorderBrushProperty, Palette.Brush(Palette.Blue), "TbBorder"));
            focusTrigger.Setters.Add(new Setter(Border.EffectProperty, Palette.GlowEffect(Palette.Blue, 0.35), "TbBorder"));
            tbTemplate.Triggers.Add(focusTrigger);

            tbStyle.Setters.Add(new Setter(Control.TemplateProperty, tbTemplate));
            app.Resources[typeof(System.Windows.Controls.TextBox)] = tbStyle;

            // 现代极简 Slim 滚动条样式
            var sbStyle = new Style(typeof(System.Windows.Controls.Primitives.ScrollBar));
            sbStyle.Setters.Add(new Setter(FrameworkElement.WidthProperty, 7.0));
            sbStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            app.Resources[typeof(System.Windows.Controls.Primitives.ScrollBar)] = sbStyle;
        }
    }

    // ---------- 启动闪屏 (品牌鲸鱼 + 加载动画, 淡出后关闭) ----------
    class SplashWindow : Window
    {
        public SplashWindow()
        {
            Width = 460;
            Height = 300;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;

            var card = new Border
            {
                CornerRadius = new CornerRadius(18),
                Background = Palette.Brush(Palette.BgCard),
                BorderBrush = Palette.Brush(Palette.BorderSoft),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(28)
            };
            var stack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };

            // 官方 DeepSeek 纯白透明鲸鱼 Logo (深海科技感极致对比)
            ImageSource whaleWhite = MainWindow.LoadEmbeddedPng("DeepSeekHarness.whale-white.png");
            if (whaleWhite == null) whaleWhite = MainWindow.LoadEmbeddedPng("DeepSeekHarness.logo.png");

            UIElement logoEl;
            if (whaleWhite != null)
            {
                var whaleHalo = new Border
                {
                    Width = 96, Height = 96,
                    CornerRadius = new CornerRadius(24),
                    Background = Palette.BlueGradient(),
                    Effect = Palette.GlowEffect(Palette.Blue, 0.55),
                    HorizontalAlignment = HorizontalAlignment.Center
                };
                whaleHalo.Child = new System.Windows.Controls.Image
                {
                    Source = whaleWhite,
                    Width = 64, Height = 64,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                logoEl = whaleHalo;
            }
            else
            {
                var logoBox = new Border { Width = 92, Height = 92, CornerRadius = new CornerRadius(20), Background = Palette.Brush(Palette.Blue), HorizontalAlignment = HorizontalAlignment.Center };
                logoBox.Child = new TextBlock { Text = "🐋", FontSize = 44, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                logoEl = logoBox;
            }
            stack.Children.Add(logoEl);
            stack.Children.Add(new TextBlock { Text = "DeepSeek Harness Launcher", Foreground = Palette.Brush(Palette.Text), FontSize = 19, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 16, 0, 0) });
            stack.Children.Add(new TextBlock { Text = Lang.T("DSH 启动器 · WPF 旗舰版"), Foreground = Palette.Brush(Palette.TextDim), FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) });
            stack.Children.Add(new TextBlock { Text = "v" + Dsh.LauncherVersion + " · by loudMore", Foreground = Palette.Brush(Palette.TextFaint), FontSize = 11, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) });
            // 加载动画条
            var bar = new Border { Height = 4, CornerRadius = new CornerRadius(2), Background = Palette.Brush(Palette.BgInput), Margin = new Thickness(30, 16, 30, 0) };
            var fill = new Border { Width = 60, CornerRadius = new CornerRadius(2), Background = Palette.Brush(Palette.Blue), HorizontalAlignment = HorizontalAlignment.Left };
            bar.Child = fill;
            stack.Children.Add(bar);
            // 流光动画
            var anim = new DoubleAnimation(-60, 460, TimeSpan.FromMilliseconds(1400)) { RepeatBehavior = RepeatBehavior.Forever };
            fill.BeginAnimation(FrameworkElement.WidthProperty, anim);

            card.Child = stack;
            Content = card;
        }

        public void FadeOut()
        {
            var fade = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(280));
            fade.Completed += delegate { Close(); };
            BeginAnimation(UIElement.OpacityProperty, fade);
        }
    }

    // ---------- 侧栏导航项 (自绘圆角, 悬停/选中动画, 平滑阻尼过渡) ----------
    class NavItem : Border
    {
        public int Index;
        public string Label;
        bool active;
        bool hover;
        TextBlock icon, text;
        Border indicator;
        TranslateTransform textShift;

        public bool Active
        {
            get { return active; }
            set { active = value; ApplyState(); }
        }

        public NavItem(int index, string iconGlyph, string label)
        {
            Index = index;
            Label = label;
            Height = 44;
            Margin = new Thickness(12, 4, 12, 4);
            CornerRadius = new CornerRadius(10);
            Background = Brushes.Transparent;
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(6) });   // 左侧指示条
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });  // 图标
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            indicator = new Border
            {
                Width = 3.5,
                Height = 0,
                CornerRadius = new CornerRadius(2),
                Background = Palette.Brush(Palette.Cyan),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Effect = Palette.GlowEffect(Palette.Cyan, 0.6)
            };
            icon = new TextBlock { Text = iconGlyph, FontSize = 15, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Foreground = Palette.Brush(Palette.TextDim) };
            text = new TextBlock { Text = label, Foreground = Palette.Brush(Palette.TextDim), FontSize = 14, VerticalAlignment = VerticalAlignment.Center };
            textShift = new TranslateTransform(0, 0);
            text.RenderTransform = textShift;

            Grid.SetColumn(indicator, 0);
            Grid.SetColumn(icon, 1);
            Grid.SetColumn(text, 2);
            g.Children.Add(indicator);
            g.Children.Add(icon);
            g.Children.Add(text);
            Child = g;
            MouseEnter += delegate { hover = true; ApplyState(); };
            MouseLeave += delegate { hover = false; ApplyState(); };
        }

        void ApplyState()
        {
            var animDur = TimeSpan.FromMilliseconds(160);
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };

            if (active)
            {
                Background = Palette.BrushA(Palette.Blue, 55);
                BorderBrush = Palette.BrushA(Palette.BlueLight, 70);
                BorderThickness = new Thickness(1);
                text.Foreground = Palette.Brush(Palette.Text);
                icon.Foreground = Palette.Brush(Palette.Cyan);
                // 指示条平滑展开
                var hAnim = new DoubleAnimation(indicator.Height, 22, animDur) { EasingFunction = ease };
                indicator.BeginAnimation(FrameworkElement.HeightProperty, hAnim);
                // 文字微右移 2px 反馈
                var sAnim = new DoubleAnimation(textShift.X, 3, animDur) { EasingFunction = ease };
                textShift.BeginAnimation(TranslateTransform.XProperty, sAnim);
            }
            else if (hover)
            {
                Background = Palette.BrushA(Palette.Text, 22);
                BorderBrush = Palette.Brush(Palette.BorderSoft);
                BorderThickness = new Thickness(1);
                text.Foreground = Palette.Brush(Palette.Text);
                icon.Foreground = Palette.Brush(Palette.TextDim);
                var hAnim = new DoubleAnimation(indicator.Height, 0, animDur) { EasingFunction = ease };
                indicator.BeginAnimation(FrameworkElement.HeightProperty, hAnim);
                var sAnim = new DoubleAnimation(textShift.X, 2, animDur) { EasingFunction = ease };
                textShift.BeginAnimation(TranslateTransform.XProperty, sAnim);
            }
            else
            {
                Background = Brushes.Transparent;
                BorderThickness = new Thickness(0);
                text.Foreground = Palette.Brush(Palette.TextDim);
                icon.Foreground = Palette.Brush(Palette.TextFaint);
                var hAnim = new DoubleAnimation(indicator.Height, 0, animDur) { EasingFunction = ease };
                indicator.BeginAnimation(FrameworkElement.HeightProperty, hAnim);
                var sAnim = new DoubleAnimation(textShift.X, 0, animDur) { EasingFunction = ease };
                textShift.BeginAnimation(TranslateTransform.XProperty, sAnim);
            }
        }
    }

    // ---------- 主窗口 ----------
    class MainWindow : Window
    {
        Grid host;                      // 内容页容器 (切页淡入)
        List<NavItem> navs = new List<NavItem>();
        List<Grid> pages = new List<Grid>();
        TextBlock sbText, sbRight;
        Dsh dsh = new Dsh();
        TextBlock ovStatus, ovSub;
        // 启动步骤指示器
        StackPanel launchSteps;
        Border[] launchStepDots = new Border[3];
        TextBlock[] launchStepTbs = new TextBlock[3];
        bool launchAnimActive = false;
        Button ovPrimary, ovStop, ovRestart;
        WrapPanel ovChips;
        TextBlock ovLog;
        ProgressBar busy;
        string lastProxy = "";
        TextBlock[] envName = new TextBlock[4], envVer = new TextBlock[4], envPath = new TextBlock[4];
        TextBlock sbDot;
        // 页面渲染缓存: 数据未变化时切页不重建, 直接复用已渲染的视觉树 (GPU 合成, 瞬时呈现)
        bool[] pageReady = new bool[6];
        bool[] pageDirty = new bool[6];
        int curPage = 0;
        Dictionary<string, string> pluginHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // 环境页
        StackPanel envRows;
        Button envRedetect, envInstall;
        Border envGuideCard;
        TextBlock envGuideDetail;
        // 概览页环境缺失警告条
        Border ovWarnBar;
        TextBlock ovWarnText;
        Button ovWarnBtn;
        // 插件页
        StackPanel pluginRows;
        TextBlock pluginSummary;
        // 更新页
        TextBlock upLupCur, upLupLatest, upLupNote;
        TextBlock upDshCur, upDshLatest, upDshNote;
        TextBlock upPluginNote;
        Button upLupGo, upDshUp, upPluginUp;
        System.Windows.Controls.ProgressBar upLupProg;
        TextBlock upLupStatus;
        string lupLatestStr = "";

        // 语义化版本比较: 当前启动器版本 vs 远端版本 (避免字符串硬编码误判)
        bool IsLauncherNewer()
        {
            Version cur, latest;
            if (!Version.TryParse(Dsh.LauncherVersion, out cur)) return false;
            if (!Version.TryParse(lupLatestStr, out latest)) return false;
            return latest > cur;
        }
        // 日志页
        ModernDropdown logKind;
        System.Windows.Controls.TextBox logBox;
        System.Windows.Controls.CheckBox logAuto;
        DispatcherTimer logTimer;
        // 设置页
        Dictionary<string, System.Windows.Controls.TextBox> setBoxes = new Dictionary<string, System.Windows.Controls.TextBox>();
        // 托盘 / 单实例
        System.Windows.Forms.NotifyIcon tray;
        bool quitting;

        public MainWindow()
        {
            Title = "DeepSeek Harness 启动器";
            Width = 1080;
            Height = 720;
            MinWidth = 860;
            MinHeight = 600;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResize;
            Background = Palette.Brush(Palette.Bg);
            dsh.Cfg = LauncherConfig.Load();   // 页面构建就要读配置, 提前加载
            Palette.IsDark = (dsh.Cfg.Theme != "light");
            Lang.Set(dsh.Cfg.Language);
            Background = Palette.Brush(Palette.Bg);
            try { Environment.SetEnvironmentVariable("DSH_HOME", dsh.Cfg.DshHome); } catch { }
            // WindowChrome: 原生无边框体验 —— 四边缩放指针、最大化、Aero 吸附全是系统级, GPU 合成丝滑
            var chrome = new WindowChrome();
            chrome.CaptionHeight = 44;
            chrome.ResizeBorderThickness = new Thickness(6);
            chrome.GlassFrameThickness = new Thickness(0, 0, 0, 1);
            chrome.CornerRadius = new CornerRadius(0);
            chrome.UseAeroCaptionButtons = false;
            WindowChrome.SetWindowChrome(this, chrome);

            // 最小化→恢复后强制重新合成 (Win11 22H2 合成器缓存失效的防御)
            StateChanged += delegate
            {
                if (WindowState == WindowState.Normal && !IsVisible)
                {
                    // 从最小化恢复时确保重绘
                    try
                    {
                        var ui = Content as UIElement;
                        if (ui != null)
                        {
                            Dispatcher.BeginInvoke(new Action(delegate
                            {
                                UpdateLayout();
                                InvalidateVisual();
                                ui.RenderTransform = new TranslateTransform(0, 0.01);
                                ui.RenderTransform = null;
                                UpdateLayout();
                            }), System.Windows.Threading.DispatcherPriority.Background);
                        }
                    }
                    catch { }
                }
            };

            BuildUi();
            // 启动过渡动画: 窗口淡入 (WPF 合成器 GPU 播放)
            Opacity = 0.0;
            var fade = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(220));
            fade.EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            // 动画完成后释放动画句柄: 否则 Opacity 被动画值永久"钉住",
            // 最小化/恢复时合成器缓存失效导致部分区域(侧边栏/标题栏)不重绘(点交互才恢复)
            fade.Completed += delegate { BeginAnimation(UIElement.OpacityProperty, null); Opacity = 1.0; };
            BeginAnimation(UIElement.OpacityProperty, fade);
            Loaded += delegate
            {
                SwitchPage(0);
                InitTray();
                StartDetect();
                StartReopenWatch();
                StartServiceWatch();   // 常驻服务状态监控: UI 永远与真实端口状态同步
                // 测试钩子: --page N 启动后自动切到指定页 (非侵入式验证用)
                try
                {
                    string[] args = Environment.GetCommandLineArgs();
                    for (int i = 1; i < args.Length; i++)
                    {
                        if (args[i] == "--page" && i + 1 < args.Length)
                        {
                            int n;
                            if (int.TryParse(args[i + 1], out n) && n >= 0 && n < pages.Count)
                                SwitchPage(n);
                        }
                        if (args[i] == "--store") OpenStore();
                        if (args[i] == "--selftest") Selftest();
                        if (args[i] == "--action" && i + 1 < args.Length)
                        {
                            string act = args[i + 1];
                            if (act == "start") { dsh.StartServiceAsync(); PollServiceState(true); }
                            else if (act == "stop") { dsh.StopServiceAsync(); PollServiceState(false); }
                            else if (act == "restart") { dsh.RestartServiceAsync(); PollServiceState(true); }
                            else if (act == "updatenow") RunUpdateCheck();
                            else if (act == "install" && i + 2 < args.Length)
                            {
                                string url = args[i + 2];
                                var tt = new Thread(delegate()
                                {
                                    string err = dsh.InstallPluginFromUrl(url);
                                    Proc.DLog("action", "install " + url + " -> " + (err.Length == 0 ? "OK" : err));
                                    File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "actions.log"), DateTime.Now.ToString("HH:mm:ss") + " install " + url + " -> " + (err.Length == 0 ? "OK" : err) + "\r\n");
                                });
                                tt.IsBackground = true;
                                tt.Start();
                                i += 2;
                            }
                            else if (act == "toggle" && i + 2 < args.Length)
                            {
                                string name = args[i + 2];
                                var plugins = dsh.ScanPlugins();
                                foreach (var pp in plugins)
                                {
                                    if (string.Equals(pp.Name, name, StringComparison.OrdinalIgnoreCase))
                                    {
                                        string err = dsh.TogglePlugin(pp);
                                        Proc.DLog("action", "toggle " + name + " -> " + (err.Length == 0 ? "OK" : err));
                                        break;
                                    }
                                }
                                i += 2;
                            }
                            else if (act == "uninstall" && i + 2 < args.Length)
                            {
                                string name = args[i + 2];
                                var plugins = dsh.ScanPlugins();
                                foreach (var pp in plugins)
                                {
                                    if (string.Equals(pp.Name, name, StringComparison.OrdinalIgnoreCase))
                                    {
                                        string err = dsh.UninstallPlugin(pp);
                                        Proc.DLog("action", "uninstall " + name + " -> " + (err.Length == 0 ? "OK" : err));
                                        break;
                                    }
                                }
                                i += 2;
                            }
                            else i++;
                        }
                    }
                }
                catch (Exception ex) { Proc.DLog("test", "hook err " + ex); }
            };
        }

        // ---------- 全链路自检 (非侵入回归: 输出 selftest.txt) ----------
        void Selftest()
        {
            var t = new Thread(delegate()
            {
                var sb = new System.Text.StringBuilder();
                try
                {
                    sb.AppendLine("cfg: port=" + dsh.Cfg.Port + " plugins=" + dsh.Cfg.PluginsRoot + " home=" + dsh.Cfg.DshHome);
                    string proxy = null;
                    try { proxy = dsh.ResolveProxy(); } catch { }
                    sb.AppendLine("proxy: " + (proxy ?? "none"));
                    var env = dsh.DetectEnvironment();
                    sb.AppendLine("env: node=" + (env.NodePath == "" ? "MISSING" : env.NodePath + " v" + env.NodeVersion)
                        + " | npm=" + (env.NpmPath == "" ? "MISSING" : env.NpmVersion)
                        + " | git=" + (env.GitPath == "" ? "MISSING" : env.GitVersion)
                        + " | dsh=" + (env.DshPath == "" ? "MISSING" : env.DshPath + " v" + env.DshVersion));
                    var plugins = dsh.ScanPlugins();
                    sb.AppendLine("plugins scanned: " + plugins.Count + (plugins.Count > 0 ? " (first: " + plugins[0].Name + " git=" + plugins[0].IsGit + ")" : ""));
                    var info = dsh.CheckUpdates(env);
                    sb.AppendLine("updates: dshUpdate=" + info.DshUpdate + " cur=" + info.DshCurrent + " latest=" + info.DshLatest + " plugins=" + info.PluginCount);
                    string lup = dsh.CheckLauncherUpdate();
                    sb.AppendLine("launcher latest: " + (lup ?? "none"));
                    var store = Dsh.FetchStore(proxy);
                    sb.AppendLine("store fetched: " + store.Count + " items" + (store.Count > 0 ? " (first: " + store[0].FullName + " stars=" + store[0].Stars + ")" : ""));
                    if (store.Count > 0) StoreCache.SaveList(store);
                    long age;
                    var cached = StoreCache.LoadList(out age);
                    sb.AppendLine("cache roundtrip: " + (cached == null ? 0 : cached.Count) + " items, age=" + age);
                    sb.AppendLine("port8099: " + Dsh.IsPortOpen(8099));
                    string inst = dsh.InstallNpmPlugin("dsh-does-not-exist-zz");
                    sb.AppendLine("npm bad-pkg handled: " + (inst.Length > 0 ? "rejected(" + inst + ")" : "???"));
                    bool ok = dsh.Cfg.Save();
                    sb.AppendLine("cfg save: " + ok);
                    sb.AppendLine("SELFTEST PASS");
                }
                catch (Exception ex) { sb.AppendLine("EXCEPTION: " + ex); }
                try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "selftest.txt"), sb.ToString()); } catch { }
                Dispatcher.BeginInvoke(new Action(delegate { sbText.Text = "自检完成，详见 selftest.txt"; }));
            });
            t.IsBackground = true;
            t.Start();
        }

        void BuildUi()
        {
            Background = Palette.Brush(Palette.Bg);
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44) });   // 标题栏
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(30) });   // 状态栏

            var titleBar = BuildTitleBar();
            Grid.SetRow(titleBar, 0);
            root.Children.Add(titleBar);
            var body = BuildBody();
            Grid.SetRow(body, 1);
            root.Children.Add(body);
            var statusBar = BuildStatusBar();
            Grid.SetRow(statusBar, 2);
            root.Children.Add(statusBar);

            Content = root;
        }

        // 嵌入 PNG → ImageSource (pack URI)
        public static ImageSource LoadEmbeddedPng(string name)
        {
            try
            {
                var uri = new Uri("pack://application:,,,/" + name);
                var s = Application.GetResourceStream(uri);
                if (s == null) return null;
                using (s.Stream)
                {
                    var bmp = new System.Windows.Media.Imaging.BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = s.Stream;
                    bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    return bmp;
                }
            }
            catch { return null; }
        }

        // ---------- 标题栏 (WindowChrome 拖拽区, 极简一体化) ----------
        Grid BuildTitleBar()
        {
            var bar = new Grid { Background = Palette.Brush(Palette.BgDeep) };
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });

            var brand = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
            // 官方 DeepSeek 图标样式: 按主题搭配不同 logo
            //   深色模式: 品牌蓝渐变底块 + 白色鲸鱼 (白鲸在蓝底/深底上醒目)
            //   浅色模式: 纯白底块 + 蓝色鲸鱼 (蓝鲸在白底上醒目, 更符合浅色清爽感)
            ImageSource whaleImg = LoadEmbeddedPng(Palette.IsDark ? "DeepSeekHarness.whale-white.png" : "DeepSeekHarness.whale-blue.png");
            if (whaleImg == null) whaleImg = LoadEmbeddedPng(Palette.IsDark ? "DeepSeekHarness.whale-white.png" : "DeepSeekHarness.logo.png");

            if (whaleImg != null)
            {
                var logoBox = new Border
                {
                    Width = 34,
                    Height = 34,
                    CornerRadius = new CornerRadius(9),
                    Background = Palette.IsDark ? (Brush)Palette.BlueGradient() : (Brush)Palette.Brush(Color.FromRgb(255, 255, 255)),
                    BorderBrush = Palette.IsDark ? null : Palette.Brush(Palette.BorderSoft),
                    BorderThickness = Palette.IsDark ? new Thickness(0) : new Thickness(1),
                    VerticalAlignment = VerticalAlignment.Center,
                    Effect = Palette.IsDark ? Palette.GlowEffect(Palette.Blue, 0.5) : Palette.CardShadow(),
                    Margin = new Thickness(0, 0, 4, 0)
                };
                logoBox.Child = new System.Windows.Controls.Image
                {
                    Source = whaleImg,
                    Width = 22,
                    Height = 22,
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                brand.Children.Add(logoBox);
            }
            else
            {
                var dot = new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(8), Background = Palette.BlueGradient(), VerticalAlignment = VerticalAlignment.Center };
                dot.Child = new TextBlock { Text = "🐋", FontSize = 15, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                brand.Children.Add(dot);
            }

            var title = new TextBlock { Text = "DeepSeek Harness Launcher", Foreground = Palette.Brush(Palette.Text), FontSize = 13, FontWeight = FontWeights.SemiBold, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            var badge = new Border
            {
                Background = Palette.BrushA(Palette.Blue, 35),
                BorderBrush = Palette.BrushA(Palette.Cyan, (byte)(Palette.IsDark ? 80 : 120)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            badge.Child = new TextBlock { Text = "v" + Dsh.LauncherVersion, Foreground = Palette.Brush(Palette.IsDark ? Palette.Cyan : Palette.Blue), FontSize = 10, FontWeight = FontWeights.Bold };
            brand.Children.Add(title);
            brand.Children.Add(badge);

            var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            btns.Children.Add(TitleBtn("─", delegate { WindowState = WindowState.Minimized; }));
            btns.Children.Add(TitleBtn("▢", delegate
            {
                WindowState = (WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized;
            }));
            btns.Children.Add(TitleBtn("✕", delegate { Close(); }, true));

            Grid.SetColumn(btns, 1);
            bar.Children.Add(brand);
            bar.Children.Add(btns);
            return bar;
        }

        Button TitleBtn(string glyph, Action onClick, bool danger = false)
        {
            var b = new Button
            {
                Content = glyph,
                Width = 48,
                FontSize = 13,
                Foreground = Palette.Brush(Palette.TextDim),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            WindowChrome.SetIsHitTestVisibleInChrome(b, true);
            b.MouseEnter += delegate { b.Background = danger ? Palette.BrushA(Palette.Error, 60) : Palette.BrushA(Palette.Text, 24); };
            b.MouseLeave += delegate { b.Background = Brushes.Transparent; };
            b.Click += delegate { onClick(); };
            return b;
        }

        // ---------- 主体: 侧栏 + 内容 ----------
        Grid BuildBody()
        {
            var body = new Grid();
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // 侧栏 (精简一体化，去除冗余Logo打架)
            var sidebar = new Grid { Background = Palette.Brush(Palette.BgDeep) };
            sidebar.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) });  // 顶部呼吸留白
            sidebar.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            sidebar.RowDefinitions.Add(new RowDefinition { Height = new GridLength(68) });  // 底部优雅外链卡片

            var navHost = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
            string[] names = { Lang.T("概览"), Lang.T("环境"), Lang.T("插件"), Lang.T("更新"), Lang.T("日志"), Lang.T("设置") };
            string[] icons = { "🏠", "🎚", "📦", "🔄", "📄", "⚙" };
            for (int i = 0; i < names.Length; i++)
            {
                int idx = i;
                var nav = new NavItem(i, icons[i], names[i]);
                nav.MouseLeftButtonUp += delegate { SwitchPage(idx); };
                navs.Add(nav);
                navHost.Children.Add(nav);
            }

            var foot = new Border
            {
                Background = Palette.BrushA(Palette.BgCard, 80),
                BorderBrush = Palette.Brush(Palette.BorderSoft),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(12, 0, 12, 10),
                Padding = new Thickness(10, 8, 10, 8),
                Cursor = Cursors.Hand
            };
            var footStack = new StackPanel();
            var ghRow = new StackPanel { Orientation = Orientation.Horizontal };
            ghRow.Children.Add(new TextBlock { Text = "⭐ ", Foreground = Palette.Brush(Palette.Warn), FontSize = 11 });
            ghRow.Children.Add(new TextBlock { Text = "dsh-launcher", Foreground = Palette.Brush(Palette.TextDim), FontSize = 11, FontWeight = FontWeights.SemiBold });
            footStack.Children.Add(ghRow);
            footStack.Children.Add(new TextBlock { Text = "by loudMore", Foreground = Palette.Brush(Palette.TextFaint), FontSize = 10, Margin = new Thickness(0, 2, 0, 0) });
            foot.Child = footStack;
            foot.MouseEnter += delegate { foot.Background = Palette.Brush(Palette.BgCardHover); };
            foot.MouseLeave += delegate { foot.Background = Palette.BrushA(Palette.BgCard, 80); };
            foot.MouseLeftButtonUp += delegate { try { Process.Start("https://github.com/loudMore/dsh-launcher"); } catch { } };

            Grid.SetRow(navHost, 1);
            Grid.SetRow(foot, 2);
            sidebar.Children.Add(navHost);
            sidebar.Children.Add(foot);

            // 内容页容器
            var content = new Grid { Background = Palette.Brush(Palette.Bg) };
            host = new Grid { Margin = new Thickness(24, 20, 24, 20) };
            content.Children.Add(host);
            busy = new ProgressBar
            {
                IsIndeterminate = true,
                Height = 4,
                VerticalAlignment = VerticalAlignment.Top,
                Foreground = Palette.Brush(Palette.Blue),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Visibility = Visibility.Collapsed
            };
            content.Children.Add(busy);

            // 6 个页面: 全部真实内容
            pages.Add(BuildOverviewPage());
            pages.Add(BuildEnvPage());
            pages.Add(BuildPluginsPage());
            pages.Add(BuildUpdatePage());
            pages.Add(BuildLogsPage());
            pages.Add(BuildSettingsPage());
            Grid.SetColumn(sidebar, 0);
            Grid.SetColumn(content, 1);
            body.Children.Add(sidebar);
            body.Children.Add(content);
            return body;
        }

        // ---------- 状态栏 ----------
        Grid BuildStatusBar()
        {
            var bar = new Grid { Background = Palette.Brush(Palette.BgDeep) };
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(240) });

            var leftStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
            sbDot = new TextBlock
            {
                Text = "●",
                Foreground = Palette.Brush(Palette.TextDim),
                FontSize = 11,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            sbText = new TextBlock
            {
                Text = Lang.T("准备就绪"),
                Foreground = Palette.Brush(Palette.TextDim),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            leftStack.Children.Add(sbDot);
            leftStack.Children.Add(sbText);

            sbRight = new TextBlock
            {
                Text = string.Format(Lang.T("端口 {0} · 启动器 v{1} (WPF)"), dsh.Cfg.Port, Dsh.LauncherVersion),
                Foreground = Palette.Brush(Palette.TextFaint),
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 0, 16, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(sbRight, 1);
            bar.Children.Add(leftStack);
            bar.Children.Add(sbRight);
            return bar;
        }

        // ---------- 概览页 (真实内容: 服务状态 + 运行环境) ----------
        Grid BuildOverviewPage()
        {
            var pg = new Grid();
            pg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) });
            pg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var head = new TextBlock
            {
                Text = Lang.T("概览"),
                Foreground = Palette.Brush(Palette.Text),
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };

            var stack = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };

            // 环境缺失警告条 (新手醒目引导, 非技术人员也能看懂)
            var warnBg = new SolidColorBrush(Palette.IsDark ? Color.FromArgb(60, 245, 158, 11) : Color.FromArgb(38, 245, 158, 11));
            var warnGrid = new Grid();
            warnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            warnGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var warnStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            ovWarnText = new TextBlock
            {
                Foreground = Palette.Brush(Palette.IsDark ? Color.FromRgb(251, 191, 36) : Color.FromRgb(180, 83, 9)),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap
            };
            warnStack.Children.Add(ovWarnText);
            warnStack.Children.Add(new TextBlock
            {
                Text = "不用懂技术，点右侧按钮，软件会自动帮你装好一切（约 1~2 分钟）",
                Foreground = Palette.Brush(Palette.TextFaint),
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetColumn(warnStack, 0);
            warnGrid.Children.Add(warnStack);
            ovWarnBtn = new Button
            {
                Content = "⚠ " + Lang.T("一键安装"),
                Height = 40,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Background = Palette.Brush(Palette.Warn),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(18, 0, 18, 0),
                Margin = new Thickness(14, 0, 0, 0),
                Effect = Palette.GlowEffect(Palette.Warn, 0.45)
            };
            ovWarnBtn.MouseEnter += delegate { ovWarnBtn.Background = Palette.Brush(Palette.WarnLight); ovWarnBtn.Effect = Palette.GlowEffect(Palette.Warn, 0.7); };
            ovWarnBtn.MouseLeave += delegate { ovWarnBtn.Background = Palette.Brush(Palette.Warn); ovWarnBtn.Effect = Palette.GlowEffect(Palette.Warn, 0.45); };
            ovWarnBtn.Click += delegate { RunInstall(); };
            Grid.SetColumn(ovWarnBtn, 1);
            warnGrid.Children.Add(ovWarnBtn);
            ovWarnBar = new Border
            {
                Background = warnBg,
                BorderBrush = Palette.Brush(Palette.Warn),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 0, 12),
                Visibility = Visibility.Collapsed
            };
            ovWarnBar.Child = warnGrid;
            stack.Children.Add(ovWarnBar);

            // 状态主卡 (微渐变面板 + 顶高光 + 悬浮状态灯)
            var hero = new Border
            {
                Background = Palette.CardGradient(),
                BorderBrush = Palette.CardBorderBrush(),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(22, 18, 22, 16),
                Effect = Palette.CardShadow()
            };
            var heroGrid = new Grid();
            heroGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            heroGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var heroTop = new Grid();
            heroTop.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            heroTop.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });   // 弹性: 按钮组自适应, 不挤压
            var statusCol = new StackPanel();
            ovStatus = new TextBlock { Text = "● " + Lang.T("检测中…"), Foreground = Palette.Brush(Palette.TextDim), FontSize = 22, FontWeight = FontWeights.SemiBold };
            ovSub = new TextBlock { Text = Lang.T("正在检测环境与服务状态…"), Foreground = Palette.Brush(Palette.TextFaint), FontSize = 13, Margin = new Thickness(0, 6, 0, 0) };
            statusCol.Children.Add(ovStatus);
            statusCol.Children.Add(ovSub);

            // 启动步骤指示器: 一键启动时逐步骤交代当前在干什么
            launchSteps = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0), Visibility = Visibility.Collapsed };
            for (int s = 0; s < 3; s++)
            {
                var step = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 14, 0) };
                var dot = new Border
                {
                    Width = 16, Height = 16, CornerRadius = new CornerRadius(8),
                    Background = Palette.Brush(Palette.BgInput),
                    BorderBrush = Palette.Brush(Palette.BorderSoft),
                    BorderThickness = new Thickness(1),
                    VerticalAlignment = VerticalAlignment.Center
                };
                var dotTb = new TextBlock { Text = (s + 1).ToString(), Foreground = Palette.Brush(Palette.TextFaint), FontSize = 10, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                dot.Child = dotTb;
                step.Children.Add(dot);
                var stepTb = new TextBlock
                {
                    Text = s == 0 ? Lang.T("检查插件") : (s == 1 ? Lang.T("启动服务") : Lang.T("就绪")),
                    Foreground = Palette.Brush(Palette.TextFaint),
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, 0, 0, 0)
                };
                step.Children.Add(stepTb);
                launchSteps.Children.Add(step);
                launchStepDots[s] = dot;
                launchStepTbs[s] = stepTb;
            }
            statusCol.Children.Add(launchSteps);
            ovPrimary = new Button
            {
                Content = Lang.T("一键启动"),
                Height = 38,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Background = Palette.BlueGradient(),
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(18, 0, 18, 0),
                Effect = Palette.GlowEffect(Palette.Blue, 0.4)
            };
            ovPrimary.MouseEnter += delegate { ovPrimary.Background = Palette.Brush(Palette.BlueLight); ovPrimary.Effect = Palette.GlowEffect(Palette.Blue, 0.65); };
            ovPrimary.MouseLeave += delegate { ovPrimary.Background = Palette.BlueGradient(); ovPrimary.Effect = Palette.GlowEffect(Palette.Blue, 0.4); };
            ovPrimary.Click += delegate { PrimaryAction(); };
            ovStop = new Button
            {
                Content = Lang.T("停止服务"),
                Height = 38,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = Palette.Brush(Palette.IsDark ? Palette.Error : Color.FromRgb(220, 38, 38)),
                Background = Palette.Brush(Palette.BgInput),
                BorderThickness = new Thickness(1),
                BorderBrush = Palette.Brush(Palette.BorderSoft),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(16, 0, 16, 0),
                Margin = new Thickness(0, 0, 10, 0),
                Visibility = Visibility.Collapsed
            };
            ovStop.MouseEnter += delegate { ovStop.Background = Palette.Brush(Palette.BgCardHover); };
            ovStop.MouseLeave += delegate { ovStop.Background = Palette.Brush(Palette.BgInput); };
            ovStop.Click += delegate { StopService(); };
            ovRestart = new Button
            {
                Content = Lang.T("重启服务"),
                Height = 38,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = Palette.Brush(Palette.IsDark ? Palette.Text : Palette.TextDim),
                Background = Palette.Brush(Palette.BgInput),
                BorderThickness = new Thickness(1),
                BorderBrush = Palette.Brush(Palette.BorderSoft),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(16, 0, 16, 0),
                Margin = new Thickness(0, 0, 10, 0),
                Visibility = Visibility.Collapsed
            };
            ovRestart.MouseEnter += delegate { ovRestart.Background = Palette.Brush(Palette.BgCardHover); };
            ovRestart.MouseLeave += delegate { ovRestart.Background = Palette.Brush(Palette.BgInput); };
            ovRestart.Click += delegate { RestartService(); };
            var heroBtns = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Right };
            heroBtns.Children.Add(ovStop);
            heroBtns.Children.Add(ovRestart);
            heroBtns.Children.Add(ovPrimary);
            Grid.SetColumn(heroBtns, 1);
            heroTop.Children.Add(statusCol);
            heroTop.Children.Add(heroBtns);
            Grid.SetRow(heroTop, 0);
            heroGrid.Children.Add(heroTop);
            // 芯片行 (卡片底部)
            ovChips = new WrapPanel { Margin = new Thickness(0, 14, 0, 0) };
            Grid.SetRow(ovChips, 1);
            heroGrid.Children.Add(ovChips);
            hero.Child = heroGrid;
            stack.Children.Add(hero);

            // 运行环境卡
            var envCard = new Border
            {
                Background = Palette.CardGradient(),
                BorderBrush = Palette.CardBorderBrush(),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(22, 16, 22, 16),
                Margin = new Thickness(0, 14, 0, 0),
                Effect = Palette.CardShadow()
            };
            var envStack = new StackPanel();
            envStack.Children.Add(new TextBlock { Text = Lang.T("运行环境"), Foreground = Palette.Brush(Palette.Text), FontSize = 15, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 10) });
            string[] tools = { "Node.js", "npm", "Git", "dsh" };
            for (int i = 0; i < 4; i++)
            {
                var row = new Grid { Margin = new Thickness(0, 5, 0, 5) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                envName[i] = new TextBlock { Text = tools[i], Foreground = Palette.Brush(Palette.TextDim), FontSize = 13 };
                envVer[i] = new TextBlock { Text = "检测中…", Foreground = Palette.Brush(Palette.TextFaint), FontSize = 13, HorizontalAlignment = HorizontalAlignment.Left, TextTrimming = TextTrimming.CharacterEllipsis };
                envPath[i] = new TextBlock { Text = "", Foreground = Palette.Brush(Palette.TextFaint), FontSize = 12, HorizontalAlignment = HorizontalAlignment.Left, TextTrimming = TextTrimming.CharacterEllipsis };
                Grid.SetColumn(envVer[i], 1);
                Grid.SetColumn(envPath[i], 2);
                row.Children.Add(envName[i]);
                row.Children.Add(envVer[i]);
                row.Children.Add(envPath[i]);
                envStack.Children.Add(row);
            }
            envCard.Child = envStack;
            stack.Children.Add(envCard);

            // 最近日志 (控制台式: 标题 + 提示 + 等宽日志区)
            var logCard = new Border
            {
                Background = Palette.CardGradient(),
                BorderBrush = Palette.CardBorderBrush(),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(22, 16, 22, 16),
                Margin = new Thickness(0, 14, 0, 0),
                Effect = Palette.CardShadow()
            };
            var logStack = new StackPanel();
            var logHead = new Grid();
            logHead.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            logHead.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            logHead.Children.Add(new TextBlock { Text = Lang.T("最近日志") + " (launcher.log)", Foreground = Palette.Brush(Palette.Text), FontSize = 15, FontWeight = FontWeights.SemiBold });
            logHead.Children.Add(new TextBlock { Text = Lang.T("滚轮滚动 · 完整日志在「日志」页"), Foreground = Palette.Brush(Palette.TextFaint), FontSize = 11, VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(logHead.Children[1] as UIElement, 1);
            logStack.Children.Add(logHead);
            var console = new Border
            {
                Background = Palette.Brush(Palette.BgDeep),
                BorderBrush = Palette.Brush(Palette.BorderSoft),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 10, 0, 0)
            };
            ovLog = new TextBlock { Text = "", Foreground = Palette.Brush(Palette.TextDim), FontSize = 11, FontFamily = new FontFamily("Consolas"), TextWrapping = TextWrapping.NoWrap, MinHeight = 190, MaxHeight = 280 };
            console.Child = ovLog;
            logStack.Children.Add(console);
            logCard.Child = logStack;
            stack.Children.Add(logCard);

            var scroll = new ScrollViewer
            {
                Content = stack,
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            Grid.SetRow(head, 0);
            Grid.SetRow(scroll, 1);
            pg.Children.Add(head);
            pg.Children.Add(scroll);
            return pg;
        }

        // 版本号清理: "git version 2.53.0" → "2.53.0"
        static string CleanVer(string v)
        {
            if (!string.IsNullOrEmpty(v) && v.StartsWith("git version ", StringComparison.OrdinalIgnoreCase))
                return v.Substring("git version ".Length);
            return v;
        }

        // 状态芯片 (圆角微发光药丸)
        UIElement Chip(string text, Color c)
        {
            var b = new Border
            {
                CornerRadius = new CornerRadius(12),
                Background = Palette.Brush(Palette.BgInput),
                BorderBrush = Palette.BrushA(c, 40),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 4, 10, 4),
                Margin = new Thickness(0, 0, 8, 8)
            };
            var t = new TextBlock
            {
                Text = text,
                Foreground = Palette.Brush(c),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            b.Child = t;
            return b;
        }

        void SetBusy(bool on)
        {
            if (busy != null) busy.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        }

        // ---------- 启动流程: 代理并行检测 + 环境检测 + 更新检查 ----------
        void StartDetect()
        {
            if (dsh.Cfg == null) dsh.Cfg = LauncherConfig.Load();
            try { Environment.SetEnvironmentVariable("DSH_HOME", dsh.Cfg.DshHome); } catch { }
            dsh.OnStatus = delegate(string s) { Dispatcher.BeginInvoke(new Action(delegate { sbText.Text = s; })); };
            dsh.OnLog = delegate(string s) { };
            var t = new Thread(delegate()
            {
                string p = null;
                try { p = dsh.ResolveProxy(); } catch { }          // 后台: 端口扫描可能耗时
                var env = dsh.DetectEnvironment();
                dsh.Env = env;
                // 启动环境摘要写日志 (诊断包关键内容)
                dsh.AppendLog("[env] Node=" + (string.IsNullOrEmpty(env.NodePath) ? "MISSING" : env.NodeVersion + " @" + env.NodePath)
                    + " | npm=" + (string.IsNullOrEmpty(env.NpmPath) ? "MISSING" : env.NpmVersion)
                    + " | Git=" + (string.IsNullOrEmpty(env.GitPath) ? "MISSING" : CleanVer(env.GitVersion))
                    + " | dsh=" + (string.IsNullOrEmpty(env.DshPath) ? "MISSING" : env.DshVersion + " @" + env.DshPath)
                    + " | port=" + dsh.Cfg.Port + (Dsh.IsPortOpen(dsh.Cfg.Port) ? " LISTENING" : " closed")
                    + " | proxy=" + (string.IsNullOrEmpty(dsh.Cfg.Proxy) ? "(none)" : dsh.Cfg.Proxy));
                Dispatcher.BeginInvoke(new Action(delegate { lastProxy = p; RenderOverview(); RenderEnv(); }));
                if (dsh.Cfg.CheckUpdatesOnStart)
                {
                    var info = dsh.CheckUpdates(env);
                    dsh.Update = info;
                    Dispatcher.BeginInvoke(new Action(delegate { RenderOverview(); RenderUpdate(); }));
                    // 启动器自更新状态也自动检查
                    string lupLatest = dsh.CheckLauncherUpdate();
                    if (lupLatest != null)
                    {
                        Dispatcher.BeginInvoke(new Action(delegate
                        {
                            upLupLatest.Text = "最新 " + lupLatest;
                            lupLatestStr = lupLatest;
                            bool newer = IsLauncherNewer();
                            upLupNote.Text = newer ? "发现新版本，点击「立即升级」即可自动更新" : "已是最新版本";
                            upLupNote.Foreground = Palette.Brush(newer ? Palette.Warn : Palette.TextFaint);
                            RenderUpdate();
                        }));
                    }
                }
                // 启动服务前: 坏插件自动隔离 (修不好就先禁用, 保证服务能跑, 并提示玩家去 dsh 修复)
                List<string> quarantined = null;
                try { quarantined = dsh.QuarantineBrokenPlugins(); } catch { }

                // 启动时自动启动服务 (与旧版一致, 按配置) — 用端口轮询让 UI 与真实状态保持一致
                // 注意: DispatcherTimer 必须在 UI 线程创建, 所以先 StartServiceAsync 再通过 Dispatcher 回到 UI 线程轮询
                bool autoStarted = false;
                if (dsh.Cfg.AutoStartService && !Dsh.IsPortOpen(dsh.Cfg.Port))
                {
                    try { dsh.StartServiceAsync(); } catch { }
                    Dispatcher.BeginInvoke(new Action(delegate { PollServiceState(true); }));
                    autoStarted = true;
                }

                // 第二/三道防线: 服务就绪后延迟巡检 (等 dsh 加载完插件树) — 日志巡检 + 前端 bundle 探测
                // 覆盖: 插件代码 bug / API 不存在 / 版本不兼容 / UI bundle 加载失败 等静态预检查不出的问题
                if (autoStarted || Dsh.IsPortOpen(dsh.Cfg.Port))
                {
                    try
                    {
                        var scanT = new Thread(delegate()
                        {
                            try { Thread.Sleep(8000); } catch { }   // 等插件树加载
                            if (!Dsh.IsPortOpen(dsh.Cfg.Port)) return;
                            var extra = new List<string>();
                            try { extra.AddRange(dsh.QuarantineByLogScan()); } catch { }
                            try { extra.AddRange(dsh.QuarantineByBundleProbe()); } catch { }
                            if (extra.Count == 0) return;
                            Dispatcher.BeginInvoke(new Action(delegate
                            {
                                var hint = new StringBuilder();
                                hint.AppendLine(Lang.T("以下插件在启动后仍报错，已被自动禁用："));
                                hint.AppendLine();
                                foreach (string q in extra) hint.AppendLine("  ⚠️ " + q);
                                hint.AppendLine();
                                hint.AppendLine(Lang.T("可尝试：在「插件」页修复依赖后启用；或复制完整日志到「日志」页，让 dsh 排查。"));
                                ShowModernWarn(this, Lang.T("插件已被隔离"), hint.ToString());
                                pageDirty[2] = true;
                                RenderPlugins();
                            }));
                        });
                        scanT.IsBackground = true;
                        scanT.Start();
                    }
                    catch { }
                }

                // 有被隔离的坏插件 → 提示玩家并附 dsh 修复提示词
                if (quarantined != null && quarantined.Count > 0)
                {
                    var hint = new StringBuilder();
                    hint.AppendLine(Lang.T("以下插件因缺少依赖被暂时禁用，服务已正常启动："));
                    hint.AppendLine();
                    foreach (string q in quarantined) hint.AppendLine("  ⚠️ " + q);
                    hint.AppendLine();
                    hint.AppendLine(Lang.T("修复后重启服务即可恢复。可在「插件」页一键修复依赖，或在 dsh 终端执行:"));
                    hint.AppendLine("  npm install -g <缺失依赖>");
                    hint.AppendLine("  cd <插件目录> && npm install");
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        ShowModernWarn(this, Lang.T("插件已被隔离"), hint.ToString());
                        pageDirty[2] = true;   // 插件页下次进入刷新
                    }));
                }
            });
            t.IsBackground = true;
            t.Start();
            // 每 3 小时静默检查更新
            var updTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(3) };
            updTimer.Tick += delegate { RunUpdateCheck(); };
            updTimer.Start();
            // 后台预热插件本地版本 (异步, 不阻塞 UI)
            RefreshPluginHashes();
            // 自动化测试钩子 (--diag-test): 等启动检测完成后自动导出诊断包并退出
            if (Dsh.DiagTestMode)
            {
                var dt = new Thread(delegate()
                {
                    Thread.Sleep(15000);
                    Dispatcher.BeginInvoke(new Action(delegate { ExportDiag(); }));
                });
                dt.IsBackground = true;
                dt.Start();
            }
        }

        void RenderOverview()
        {
            try
            {
                if (dsh == null || dsh.Env == null || ovStatus == null || ovPrimary == null) return;
                var env = dsh.Env;
                bool running = Dsh.IsPortOpen(dsh.Cfg.Port);
                bool nodeOk = !string.IsNullOrEmpty(env.NodePath);
                bool dshOk = !string.IsNullOrEmpty(env.DshPath);
                if (!nodeOk || !dshOk)
                {
                    bool gitOk = !string.IsNullOrEmpty(env.GitPath);
                    ovStatus.Text = "⚠ " + Lang.T("环境未安装");
                    ovStatus.Foreground = Palette.Brush(Palette.Warn);
                    string missing = (!nodeOk ? "Node.js" : "") + (gitOk ? "" : (nodeOk ? "" : "、") + "Git") + (!dshOk ? (nodeOk && gitOk ? "" : "、") + "dsh" : "");
                    ovSub.Text = "缺少: " + (missing.Length == 0 ? "必要组件" : missing) + " · " + Lang.T("首次使用请点击「一键安装」");
                    ovPrimary.Content = "⚠ " + Lang.T("一键安装");
                    if (ovWarnBar != null && ovWarnText != null)
                    {
                        ovWarnBar.Visibility = Visibility.Visible;
                        ovWarnText.Text = "⚠ 还差 " + (missing.Length == 0 ? "环境组件" : missing) + " 就装好了";
                    }
                }
                else
                {
                    if (ovWarnBar != null) ovWarnBar.Visibility = Visibility.Collapsed;
                    if (running)
                    {
                        ovStatus.Text = "● " + Lang.T("服务运行中");
                        ovStatus.Foreground = Palette.Brush(Palette.Success);
                        ovSub.Text = string.Format("http://127.0.0.1:{0} · dsh {1}", dsh.Cfg.Port, string.IsNullOrEmpty(env.DshVersion) ? "-" : env.DshVersion);
                        ovPrimary.Content = "▶ " + Lang.T("打开浏览器");
                    }
                    else
                    {
                        ovStatus.Text = "● " + Lang.T("服务未启动");
                        ovStatus.Foreground = Palette.Brush(Palette.TextDim);
                        ovSub.Text = Lang.T("环境已就绪，点击「一键启动」开始使用");
                        ovPrimary.Content = Lang.T("一键启动");
                    }
                }
            string[] vers = { env.NodeVersion, env.NpmVersion, CleanVer(env.GitVersion), env.DshVersion };
            string[] paths = { env.NodePath, env.NpmPath, env.GitPath, env.DshPath };
            for (int i = 0; i < 4; i++)
            {
                if (string.IsNullOrEmpty(paths[i]))
                {
                    envVer[i].Text = Lang.T("未检测到");
                    envVer[i].Foreground = Palette.Brush(Palette.Warn);
                    envPath[i].Text = "";
                }
                else
                {
                    envVer[i].Text = string.IsNullOrEmpty(vers[i]) ? "" : vers[i];
                    envVer[i].Foreground = Palette.Brush(Palette.TextDim);
                    envPath[i].Text = paths[i];
                    envPath[i].ToolTip = paths[i];
                    envPath[i].Foreground = Palette.Brush(Palette.TextFaint);
                }
            }
            // 状态芯片 (卡片底部, 旧版布局)
            if (ovChips != null)
            {
                ovChips.Children.Clear();
                ovChips.Children.Add(Chip("dsh " + (dshOk ? env.DshVersion : "✗"), dshOk ? Palette.Success : Palette.Warn));
                ovChips.Children.Add(Chip(string.Format("http://127.0.0.1:{0}", dsh.Cfg.Port), Palette.TextDim));
                ovChips.Children.Add(Chip("Node " + (nodeOk ? "✓" : "✗"), nodeOk ? Palette.Success : Palette.Warn));
                ovChips.Children.Add(Chip("npm " + (string.IsNullOrEmpty(env.NpmPath) ? "✗" : "✓"), string.IsNullOrEmpty(env.NpmPath) ? Palette.Warn : Palette.Success));
                ovChips.Children.Add(Chip("git " + (string.IsNullOrEmpty(env.GitPath) ? "✗" : "✓"), string.IsNullOrEmpty(env.GitPath) ? Palette.Warn : Palette.Success));
                ovChips.Children.Add(Chip(string.Format(Lang.T("{0} 个插件"), env.PluginDirs), Palette.TextDim));
                ovChips.Children.Add(Chip(string.IsNullOrEmpty(lastProxy) ? Lang.T("直连") : Lang.T("代理") + " " + lastProxy, string.IsNullOrEmpty(lastProxy) ? Palette.TextFaint : Palette.BlueLight));
            }
            // 停止/重启按钮: 仅服务运行时显示
            if (ovStop != null) ovStop.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
            if (ovRestart != null) ovRestart.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
            // 最近日志预览
            if (ovLog != null)
            {
                string tail = Dsh.ReadTail(Path.Combine(dsh.Cfg.LogDir, "launcher.log"), 12);
                ovLog.Text = string.IsNullOrEmpty(tail) ? Lang.T("暂无日志") : tail;
            }
            // 状态栏圆点 + 右侧
            if (sbDot != null)
            {
                sbDot.Foreground = Palette.Brush(running ? Palette.Success : Palette.TextDim);
                sbDot.Effect = running ? Palette.GlowEffect(Palette.Success, 0.7) : null;
                sbText.Text = running ? Lang.T("服务已在运行") : Lang.T("服务未启动");
            }
            sbRight.Text = string.Format(Lang.T("端口 {0} · 启动器 v{1} (WPF)"), dsh.Cfg.Port, Dsh.LauncherVersion);
            }
            catch { }
        }

        // 主按钮: 未安装→安装; 未启动→启动; 运行中→打开浏览器
        void PrimaryAction()
        {
            bool running = Dsh.IsPortOpen(dsh.Cfg.Port);
            // 环境检测可能尚未完成 (异步), 未就绪时先提示并重新检测, 避免误判为"未安装"
            if (dsh.Env == null || string.IsNullOrEmpty(dsh.Env.NodePath) || string.IsNullOrEmpty(dsh.Env.DshPath))
            {
                if (!running) { RunDetect(); sbText.Text = "正在检测环境，请稍候…"; return; }
                // 服务在跑但环境信息缺失: 打开浏览器并同步 UI
                dsh.OpenBrowser();
                RenderOverview();
                return;
            }
            bool nodeOk = !string.IsNullOrEmpty(dsh.Env.NodePath);
            bool dshOk = !string.IsNullOrEmpty(dsh.Env.DshPath);
            if (!nodeOk || !dshOk) { RunInstall(); return; }
            if (running) { dsh.OpenBrowser(); RenderOverview(); return; }
            // 启动步骤动画: ①检查插件 → ②启动服务 → ③就绪
            BeginLaunchSteps();
            dsh.StartServiceAsync();
            PollServiceState(true);
        }

        // 启动步骤动画驱动
        void BeginLaunchSteps()
        {
            launchAnimActive = true;
            if (launchSteps == null) return;
            launchSteps.Visibility = Visibility.Visible;
            SetLaunchStep(0, false);   // ① 检查插件 (进行中)
            SetLaunchStep(1, false);
            SetLaunchStep(2, false);
            ovStatus.Text = "● " + Lang.T("正在启动…");
            ovStatus.Foreground = Palette.Brush(Palette.BlueLight);
            ovSub.Text = Lang.T("正在检查插件兼容性…");
            // 步骤 ① 旋转动画 (进行中)
            var rot = new RotateTransform(0);
            launchStepDots[0].RenderTransform = rot;
            launchStepDots[0].RenderTransformOrigin = new Point(0.5, 0.5);
            var spin = new DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(900)) { RepeatBehavior = RepeatBehavior.Forever };
            rot.BeginAnimation(RotateTransform.AngleProperty, spin);
        }

        void SetLaunchStep(int idx, bool done)
        {
            if (launchSteps == null || idx < 0 || idx > 2) return;
            var dot = launchStepDots[idx];
            var tb = launchStepTbs[idx];
            if (dot == null || tb == null) return;
            dot.RenderTransform = null;
            if (done)
            {
                dot.Background = Palette.Brush(Palette.Success);
                dot.BorderBrush = Palette.Brush(Palette.Success);
                ((TextBlock)dot.Child).Text = "✓";
                ((TextBlock)dot.Child).Foreground = Brushes.White;
                tb.Foreground = Palette.Brush(Palette.Success);
                tb.FontWeight = FontWeights.SemiBold;
            }
            else
            {
                dot.Background = Palette.Brush(Palette.BgInput);
                dot.BorderBrush = Palette.Brush(Palette.BorderSoft);
                ((TextBlock)dot.Child).Text = (idx + 1).ToString();
                ((TextBlock)dot.Child).Foreground = Palette.Brush(Palette.TextFaint);
                tb.Foreground = Palette.Brush(Palette.TextFaint);
                tb.FontWeight = FontWeights.Normal;
            }
        }

        // 步骤 ① 完成 → 进入 ② 启动服务
        void AdvanceLaunchStep(int toStep)
        {
            if (!launchAnimActive) return;
            if (toStep >= 1) SetLaunchStep(0, true);
            if (toStep >= 2)
            {
                SetLaunchStep(1, true);
                SetLaunchStep(2, true);
                FinishLaunchSteps();
                return;
            }
            // ② 启动服务 (进行中旋转)
            ovSub.Text = Lang.T("正在启动服务，请稍候…");
            var rot = new RotateTransform(0);
            launchStepDots[1].RenderTransform = rot;
            launchStepDots[1].RenderTransformOrigin = new Point(0.5, 0.5);
            var spin = new DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(900)) { RepeatBehavior = RepeatBehavior.Forever };
            rot.BeginAnimation(RotateTransform.AngleProperty, spin);
        }

        void FinishLaunchSteps()
        {
            if (!launchAnimActive) return;
            launchAnimActive = false;
            if (launchSteps == null) return;
            // 停留片刻展示 ✓ 再隐藏
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
            timer.Tick += delegate
            {
                timer.Stop();
                launchSteps.Visibility = Visibility.Collapsed;
            };
            timer.Start();
        }

        void RestartService()
        {
            dsh.RestartServiceAsync();
            PollServiceState(true);
        }

        void StopService()
        {
            dsh.StopServiceAsync();
            PollServiceState(false);
        }

        // 轮询服务端口状态直到稳定 (或超时兜底), 彻底取代脆弱的"文本关键词"停止判断
        void PollServiceState(bool waitRunning)
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            int stable = 0;
            int ticks = 0;
            const int maxTicks = 60;   // 最多轮询 36 秒兜底
            timer.Tick += delegate
            {
                ticks++;
                bool running = Dsh.IsPortOpen(dsh.Cfg.Port);
                RenderOverview();
                stable = (running == waitRunning) ? stable + 1 : 0;   // 端口状态连续 3 次(约1.8s)稳定才收敛
                // 启动步骤动画联动: 端口一开就推进步骤
                if (launchAnimActive && waitRunning && running)
                    AdvanceLaunchStep(2);
                if (stable >= 3 || ticks >= maxTicks)
                {
                    timer.Stop();
                    if (launchAnimActive && waitRunning)
                    {
                        if (running) AdvanceLaunchStep(2);
                        else FinishLaunchSteps();   // 启动失败也收起动画, 状态栏会显示失败
                    }
                    RenderOverview();
                }
            };
            timer.Start();
        }

        // ---------- 常驻服务状态监控器: UI 永远跟随真实端口状态 ----------
        DispatcherTimer svcWatch;
        bool lastSvcRunning = false;
        bool svcWatchStarted = false;

        void StartServiceWatch()
        {
            if (svcWatchStarted) return;
            svcWatchStarted = true;
            lastSvcRunning = Dsh.IsPortOpen(dsh.Cfg.Port);
            svcWatch = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            svcWatch.Tick += delegate
            {
                try
                {
                    bool running = Dsh.IsPortOpen(dsh.Cfg.Port);
                    if (running != lastSvcRunning)
                    {
                        lastSvcRunning = running;
                        RenderOverview();
                    }
                }
                catch { }
            };
            svcWatch.Start();
        }

        // ---------- 通用构建辅助 ----------
        static Border Card(UIElement child)
        {
            return new Border
            {
                Background = Palette.CardGradient(),
                BorderBrush = Palette.CardBorderBrush(),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(20, 16, 20, 16),
                Margin = new Thickness(0, 12, 0, 0),
                Effect = Palette.CardShadow(),
                Child = child
            };
        }

        static TextBlock PageHead(string t)
        {
            return new TextBlock
            {
                Text = t,
                Foreground = Palette.Brush(Palette.Text),
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        static Grid PageShell(string title, out ScrollViewer scroll)
        {
            var pg = new Grid();
            pg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(36) });
            pg.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            Grid.SetRow(scroll, 1);
            pg.Children.Add(PageHead(Lang.T(title)));
            pg.Children.Add(scroll);
            return pg;
        }

        Button Btn(string text, Action onClick, bool primary)
        {
            var b = new Button
            {
                Content = text,
                Height = 34,
                FontSize = 13,
                FontWeight = primary ? FontWeights.SemiBold : FontWeights.Normal,
                Foreground = primary ? Brushes.White : Palette.Brush(Palette.IsDark ? Palette.Text : Palette.TextDim),
                Background = primary ? (Brush)Palette.BlueGradient() : (Brush)Palette.Brush(Palette.BgInput),
                BorderThickness = new Thickness(primary ? 0 : 1),
                BorderBrush = primary ? null : Palette.Brush(Palette.BorderSoft),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 10, 0),
                Padding = new Thickness(16, 0, 16, 0)
            };
            if (primary) b.Effect = Palette.GlowEffect(Palette.Blue, 0.35);
            b.MouseEnter += delegate
            {
                b.Background = primary ? (Brush)Palette.Brush(Palette.BlueLight) : (Brush)Palette.Brush(Palette.BgCardHover);
                if (primary) b.Effect = Palette.GlowEffect(Palette.Blue, 0.55);
            };
            b.MouseLeave += delegate
            {
                b.Background = primary ? (Brush)Palette.BlueGradient() : (Brush)Palette.Brush(Palette.BgInput);
                if (primary) b.Effect = Palette.GlowEffect(Palette.Blue, 0.35);
            };
            b.Click += delegate { onClick(); };
            return b;
        }

        // ---------- 环境页 ----------
        Grid BuildEnvPage()
        {
            Grid pg;
            ScrollViewer scroll;
            pg = PageShell("环境", out scroll);
            var stack = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
            var toolbar = new StackPanel { Orientation = Orientation.Horizontal };
            envRedetect = Btn("↻ " + Lang.T("重新检测"), delegate { RunDetect(); }, false);
            envInstall = Btn("↓ " + Lang.T("一键安装 / 修复环境"), delegate { RunInstall(); }, true);
            toolbar.Children.Add(envRedetect);
            toolbar.Children.Add(envInstall);
            stack.Children.Add(toolbar);

            // 新手引导卡 (醒目橙色边框): 环境缺失时用大白话引导, 一键自动补齐
            var guideCard = new Border
            {
                Background = Palette.CardGradient(),
                BorderBrush = Palette.Brush(Palette.Warn),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(22, 18, 22, 18),
                Margin = new Thickness(0, 12, 0, 0),
                Effect = Palette.GlowEffect(Palette.Warn, 0.18),
                Visibility = Visibility.Collapsed
            };
            var guideStack = new StackPanel();
            guideStack.Children.Add(new TextBlock
            {
                Text = "🚀 " + Lang.T("还差一步就装好了"),
                Foreground = Palette.Brush(Palette.Warn),
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 6)
            });
            guideStack.Children.Add(new TextBlock
            {
                Text = "软件需要 4 样东西才能运行：Node.js（运行引擎）、npm（自带的下载器）、Git（下载工具）、dsh（主程序）。" +
                       "下面缺哪样，点「一键安装」都会自动补上，全程不用你操作，也不会弹管理员密码。",
                Foreground = Palette.Brush(Palette.TextDim),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            });
            // 缺失清单 (RenderEnv 动态更新)
            envGuideDetail = new TextBlock
            {
                Foreground = Palette.Brush(Palette.Text),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            };
            guideStack.Children.Add(envGuideDetail);
            var guideBtns = new StackPanel { Orientation = Orientation.Horizontal };
            var installBig = Btn("🆕 " + Lang.T("一键安装"), delegate { RunInstall(); }, true);
            installBig.Height = 44;
            installBig.FontSize = 16;
            guideBtns.Children.Add(installBig);
            guideBtns.Children.Add(Btn("✅ " + Lang.T("我已安装 dsh"), delegate { SmartLocateDsh(); }, false));
            guideStack.Children.Add(guideBtns);
            // 高级定位: 精确文件 / 模糊目录
            var advBtns = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
            advBtns.Children.Add(Btn("📂 " + Lang.T("手动选择 dsh 文件"), delegate { PickDshFile(); }, false));
            advBtns.Children.Add(Btn("🗂 " + Lang.T("指定目录自动查找"), delegate { ScanDirForDsh(); }, false));
            guideStack.Children.Add(advBtns);
            guideCard.Child = guideStack;
            envGuideCard = guideCard;
            stack.Children.Add(guideCard);

            envRows = new StackPanel();
            stack.Children.Add(Card(envRows));
            scroll.Content = stack;
            return pg;
        }

        // 智能定位 dsh: 深度扫描 → 找到则应用并重新检测, 找不到提示手动方式
        void SmartLocateDsh()
        {
            sbText.Text = "正在智能查找 dsh…";
            var t = new Thread(delegate()
            {
                string found = dsh.DeepFindDsh();
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (found.Length > 0)
                    {
                        dsh.Cfg.DshCommand = found;
                        dsh.Cfg.Save();
                        sbText.Text = "已找到 dsh: " + found;
                        RunDetect();
                        ShowModernInfo(this, Lang.T("已找到 dsh"), "已定位到 dsh：\n" + found);
                    }
                    else
                    {
                        sbText.Text = "未找到 dsh";
                        ShowModernWarn(this, Lang.T("未找到 dsh"),
                            "自动扫描没有找到 dsh。\n\n你可以：\n1. 点「手动选择 dsh 文件」直接指定 dsh.cmd 的位置\n2. 点「指定目录自动查找」告诉软件去哪个文件夹里找\n3. 如果确实没装过，点「一键安装」");
                    }
                }));
            });
            t.IsBackground = true;
            t.Start();
        }

        // 手动选择 dsh 可执行文件 (文件选择器)
        void PickDshFile()
        {
            try
            {
                var dlg = new Microsoft.Win32.OpenFileDialog
                {
                    Title = Lang.T("选择 dsh 可执行文件 (dsh.cmd / dsh.exe)"),
                    Filter = "dsh 可执行文件|dsh.cmd;dsh.exe;dsh|所有文件|*.*",
                    CheckFileExists = true
                };
                if (dlg.ShowDialog(this) == true)
                {
                    dsh.Cfg.DshCommand = dlg.FileName;
                    dsh.Cfg.Save();
                    sbText.Text = "已指定 dsh: " + dlg.FileName;
                    RunDetect();
                }
            }
            catch { }
        }

        // 指定一个目录, 自动递归查找 dsh
        void ScanDirForDsh()
        {
            try
            {
                var dlg = new System.Windows.Forms.FolderBrowserDialog
                {
                    Description = Lang.T("选择包含 dsh 的文件夹（软件会自动搜索）"),
                    ShowNewFolderButton = false
                };
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    string root = dlg.SelectedPath;
                    sbText.Text = "正在搜索 " + root + " …";
                    var t = new Thread(delegate()
                    {
                        string found = Dsh.FindDshInTree(root, 5);
                        Dispatcher.BeginInvoke(new Action(delegate
                        {
                            if (found.Length > 0)
                            {
                                dsh.Cfg.DshCommand = found;
                                dsh.Cfg.Save();
                                sbText.Text = "已找到 dsh: " + found;
                                RunDetect();
                                ShowModernInfo(this, Lang.T("已找到 dsh"), "已定位到 dsh：\n" + found);
                            }
                            else
                            {
                                sbText.Text = "该目录未找到 dsh";
                                ShowModernWarn(this, Lang.T("未找到 dsh"), "在所选目录中未找到 dsh，请确认路径正确或尝试其他方式。");
                            }
                        }));
                    });
                    t.IsBackground = true;
                    t.Start();
                }
            }
            catch { }
        }

        void RenderEnv()
        {
            if (envRows == null) return;
            envRows.Children.Clear();
            var env = dsh.Env;
            bool dshMissing = string.IsNullOrEmpty(env.DshPath);
            // 引导卡: 四项中任一缺失都展示 (Node/npm/Git/dsh), 让新手一眼看清缺什么
            bool anyMissing = string.IsNullOrEmpty(env.NodePath) || string.IsNullOrEmpty(env.NpmPath)
                || string.IsNullOrEmpty(env.GitPath) || dshMissing;
            if (envGuideCard != null)
                envGuideCard.Visibility = anyMissing ? Visibility.Visible : Visibility.Collapsed;
            if (envGuideDetail != null && anyMissing)
            {
                string[] gnm = { "Node.js", "npm", "Git", "dsh" };
                string[] gpp = { env.NodePath, env.NpmPath, env.GitPath, env.DshPath };
                var glines = new List<string>();
                for (int gi = 0; gi < 4; gi++)
                {
                    bool gok = !string.IsNullOrEmpty(gpp[gi]);
                    glines.Add((gok ? "✅ " : "❌ ") + gnm[gi] + (gok ? "" : "（未安装）"));
                }
                envGuideDetail.Text = string.Join("　", glines.ToArray());
            }
            string[] names = { "Node.js", "npm", "Git", "dsh" };
            string[] vers = { env.NodeVersion, env.NpmVersion, CleanVer(env.GitVersion), env.DshVersion };
            string[] paths = { env.NodePath, env.NpmPath, env.GitPath, env.DshPath };
            for (int i = 0; i < 4; i++)
            {
                bool ok = !string.IsNullOrEmpty(paths[i]);
                var row = new Grid { Margin = new Thickness(0, 6, 0, 6) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.Children.Add(new TextBlock { Text = ok ? "●" : "○", Foreground = Palette.Brush(ok ? Palette.Success : Palette.Warn), FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
                var nameTb = new TextBlock { Text = names[i], Foreground = Palette.Brush(Palette.Text), FontSize = 14, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(nameTb, 1);
                row.Children.Add(nameTb);
                var verTb = new TextBlock
                {
                    Text = ok && !string.IsNullOrEmpty(vers[i]) ? vers[i] : "",
                    Foreground = Palette.Brush(ok ? Palette.TextDim : Palette.Warn),
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(verTb, 2);
                row.Children.Add(verTb);
                var pathTb = new TextBlock
                {
                    Text = ok ? paths[i] : Lang.T("未检测到（可点击上方「一键安装」）"),
                    Foreground = Palette.Brush(ok ? Palette.TextFaint : Palette.Warn),
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    ToolTip = ok ? paths[i] : null
                };
                Grid.SetColumn(pathTb, 3);
                row.Children.Add(pathTb);
                envRows.Children.Add(row);
            }
            envRows.Children.Add(new TextBlock
            {
                Text = Lang.T("数据目录") + ": " + dsh.Cfg.DshHome + (env.DshHomeExists ? "" : "  (不存在)"),
                Foreground = Palette.Brush(env.DshHomeExists ? Palette.TextFaint : Palette.Warn),
                FontSize = 12,
                Margin = new Thickness(0, 8, 0, 0)
            });
        }

        void RunDetect()
        {
            sbText.Text = "正在检测环境…";
            var t = new Thread(delegate()
            {
                try { dsh.ResolveProxy(); } catch { }
                var env = dsh.DetectEnvironment();
                dsh.Env = env;
                // 环境摘要写日志 (诊断包关键内容)
                dsh.AppendLog("[env] Node=" + (string.IsNullOrEmpty(env.NodePath) ? "MISSING" : env.NodeVersion + " @" + env.NodePath)
                    + " | npm=" + (string.IsNullOrEmpty(env.NpmPath) ? "MISSING" : env.NpmVersion)
                    + " | Git=" + (string.IsNullOrEmpty(env.GitPath) ? "MISSING" : CleanVer(env.GitVersion))
                    + " | dsh=" + (string.IsNullOrEmpty(env.DshPath) ? "MISSING" : env.DshVersion + " @" + env.DshPath)
                    + " | port=" + dsh.Cfg.Port + (Dsh.IsPortOpen(dsh.Cfg.Port) ? " LISTENING" : " closed")
                    + " | proxy=" + (string.IsNullOrEmpty(dsh.Cfg.Proxy) ? "(none)" : dsh.Cfg.Proxy));
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    RenderEnv();
                    RenderOverview();
                    sbText.Text = "环境检测完成";
                }));
            });
            t.IsBackground = true;
            t.Start();
        }

        void RunInstall()
        {
            // 沙盒模式(隔离测试): 硬禁止真实安装, 防止测试污染用户环境
            if (Dsh.SandboxMode)
            {
                sbText.Text = "沙盒模式不执行安装";
                ShowModernWarn(this, "一键安装", "当前为沙盒测试模式（--sandbox），不执行真实安装。\n正常使用时不会出现此提示。");
                return;
            }
            // 支持选择是否自定义安装路径
            string defaultPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "node");
            string customPath = Prompt(
                "一键安装 / 修复环境",
                "软件会自动检查并补齐缺的东西（缺什么装什么，已装的不动）：\n\n" +
                "① Node.js —— 运行引擎（缺才装，约 30MB）\n" +
                "② Git —— 下载工具，插件安装/更新要用（缺才装，约 45MB）\n" +
                "③ dsh —— DeepSeek Harness 主程序（缺才装）\n\n" +
                "全部走国内镜像，不用代理、不用管理员密码，装完即用。\n" +
                "如需自定义安装目录，请在下方修改目标文件夹；直接点「确定安装」用默认目录：\n" + defaultPath,
                defaultPath
            );
            if (string.IsNullOrEmpty(customPath)) customPath = defaultPath;   // 用户清空输入 → 用默认目录, 不静默取消
            if (string.IsNullOrEmpty(customPath)) return;

            sbText.Text = "正在安装环境…";
            envInstall.IsEnabled = false;
            SetBusy(true);
            var t = new Thread(delegate()
            {
                string error;
                bool ok = dsh.InstallDshNow(out error, customPath);
                var env = dsh.DetectEnvironment();
                dsh.Env = env;
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    envInstall.IsEnabled = true;
                    SetBusy(false);
                    RenderEnv();
                    RenderOverview();
                    if (ok)
                    {
                        sbText.Text = "环境安装完成";
                        ShowModernInfo(this, "一键安装", "全部装好了！\n" +
                            "· Node.js ✓\n· Git ✓\n· dsh ✓\n\n" +
                            "已自动写入系统 PATH，点击「一键启动」即可开始使用。");
                    }
                    else { sbText.Text = "安装未完成"; ShowModernWarn(this, "一键安装", error + "\n\n详细信息见 launcher.log。"); }
                }));
            });
            t.IsBackground = true;
            t.Start();
        }

        // ---------- 插件页 ----------
        Grid BuildPluginsPage()
        {
            Grid pg;
            ScrollViewer scroll;
            pg = PageShell("插件管理", out scroll);
            var stack = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
            var toolbar = new StackPanel { Orientation = Orientation.Horizontal };
            toolbar.Children.Add(Btn("↻ " + Lang.T("刷新列表"), delegate { RefreshPluginHashes(); pageDirty[2] = true; RenderPlugins(); }, false));
            toolbar.Children.Add(Btn("↓ " + Lang.T("安装插件"), delegate { InstallPluginPrompt(); }, false));
            toolbar.Children.Add(Btn("🛍 " + Lang.T("插件商城"), delegate { OpenStore(); }, true));
            // 全部更新已并入「一键维护」(更新+修依赖一键搞定), 避免功能重复 
            toolbar.Children.Add(Btn(Lang.T("一键维护"), delegate { MaintainPlugins(); }, false));
            toolbar.Children.Add(Btn(Lang.T("打开插件目录"), delegate { OpenPluginsDir(); }, false));
            stack.Children.Add(toolbar);
            pluginSummary = new TextBlock { Foreground = Palette.Brush(Palette.TextFaint), FontSize = 12, Margin = new Thickness(2, 8, 0, 0) };
            stack.Children.Add(pluginSummary);
            pluginRows = new StackPanel();
            stack.Children.Add(Card(pluginRows));
            scroll.Content = stack;
            return pg;
        }

        void OpenStore()
        {
            try
            {
                if (!IsVisible) ShowMain();   // 主窗口隐藏时先弹出, 避免 owned window 异常
                var w = new StoreWindow(dsh) { Owner = this };
                w.Show();
            }
            catch { }
        }

        void OpenPluginsDir()
        {
            try { Process.Start("explorer.exe", "\"" + dsh.Cfg.PluginsRoot + "\""); } catch { }
        }

        void RenderPlugins()
        {
            if (pluginRows == null) return;
            pluginRows.Children.Clear();
            var plugins = dsh.ScanPlugins();
            pluginSummary.Text = string.Format(Lang.T("共 {0} 个目录 · {1} 个 git 仓库"), plugins.Count, CountGit(plugins));
            if (plugins.Count == 0)
            {
                pluginRows.Children.Add(new TextBlock { Text = "未发现任何插件。\n插件目录: " + dsh.Cfg.PluginsRoot, Foreground = Palette.Brush(Palette.TextDim), FontSize = 13, Margin = new Thickness(2, 6, 0, 6) });
                return;
            }
            foreach (var p in plugins)
            {
                var row = new Grid { Margin = new Thickness(0, 5, 0, 5) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(330) });
                var nameCol = new StackPanel();
                nameCol.Children.Add(new TextBlock { Text = p.Name, Foreground = Palette.Brush(Palette.Text), FontSize = 14, FontWeight = FontWeights.SemiBold });
                nameCol.Children.Add(new TextBlock
                {
                    Text = p.Disabled ? "● " + Lang.T("已禁用") : (p.IsGit ? "● git" : "● " + Lang.T("普通目录")),
                    Foreground = Palette.Brush(p.Disabled ? Palette.Warn : (p.IsGit ? Palette.Success : Palette.TextDim)),
                    FontSize = 11
                });
                // 依赖状态徽章: 缺依赖的插件是高危信号 (缺依赖会让整个服务插件树崩溃)
                if (!p.Disabled && p.DepsChecked)
                {
                    if (p.DepsOk)
                    {
                        nameCol.Children.Add(new TextBlock { Text = "✓ " + Lang.T("依赖完整"), Foreground = Palette.Brush(Palette.Success), FontSize = 11 });
                    }
                    else
                    {
                        nameCol.Children.Add(new TextBlock
                        {
                            Text = "⚠️ " + Lang.T("缺依赖") + ": " + p.MissingDeps,
                            Foreground = Palette.Brush(Palette.Warn),
                            FontSize = 11,
                            TextTrimming = TextTrimming.CharacterEllipsis,
                            ToolTip = "缺少: " + p.MissingDeps + "\n点击「修复依赖」自动补齐"
                        });
                    }
                }
                if (p.IsGit)
                {
                    string h;
                    if (pluginHashes.TryGetValue(p.Name, out h) && h.Length > 0)
                        nameCol.Children.Add(new TextBlock { Text = "本地 " + h, Foreground = Palette.Brush(Palette.TextFaint), FontSize = 11 });
                }
                row.Children.Add(nameCol);
                var urlLbl = new TextBlock
                {
                    Text = string.IsNullOrEmpty(p.RemoteUrl) ? p.Path : p.RemoteUrl,
                    Foreground = Palette.Brush(string.IsNullOrEmpty(p.RemoteUrl) ? Palette.TextFaint : Palette.BlueLight),
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Cursor = string.IsNullOrEmpty(p.RemoteUrl) ? Cursors.Arrow : Cursors.Hand
                };
                if (!string.IsNullOrEmpty(p.RemoteUrl))
                    urlLbl.MouseLeftButtonUp += delegate { try { Process.Start(p.RemoteUrl); } catch { } };
                Grid.SetColumn(urlLbl, 1);
                row.Children.Add(urlLbl);
                var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
                PluginItem pp = p;
                if (p.IsGit)
                {
                    var upBtn = Btn(Lang.T("检查更新"), delegate
                    {
                        var t = new Thread(delegate()
                        {
                            string pullRes = dsh.PullPlugin(pp);
                            Dispatcher.BeginInvoke(new Action(delegate
                            {
                                RenderPlugins();
                                ShowModernInfo(this, "插件更新", pp.Name + ": " + pullRes);
                            }));
                        });
                        t.IsBackground = true;
                        t.Start();
                    }, false);
                    btns.Children.Add(upBtn);
                }
                btns.Children.Add(Btn(Lang.T("目录"), delegate { try { Process.Start("explorer.exe", "\"" + pp.Path + "\""); } catch { } }, false));
                // 缺依赖的插件显示"修复依赖"按钮 (高危: 缺依赖会让整个服务插件树崩溃)
                if (!pp.Disabled && pp.DepsChecked && !pp.DepsOk)
                {
                    btns.Children.Add(Btn("🔧 " + Lang.T("修复依赖"), delegate
                    {
                        var t = new Thread(delegate()
                        {
                            string res = dsh.FixPluginDeps(pp);
                            Dispatcher.BeginInvoke(new Action(delegate
                            {
                                RenderPlugins();
                                ShowModernInfo(this, Lang.T("修复依赖"), pp.Name + ": " + res);
                            }));
                        });
                        t.IsBackground = true;
                        t.Start();
                    }, true));
                }
                btns.Children.Add(Btn(Lang.T("卸载"), delegate { ConfirmUninstall(pp); }, false));
                btns.Children.Add(Btn(p.Disabled ? Lang.T("启用") : Lang.T("禁用"), delegate { Op(pp, dsh.TogglePlugin); }, false));
                Grid.SetColumn(btns, 2);
                row.Children.Add(btns);
                pluginRows.Children.Add(row);
            }
        }

        static int CountGit(List<PluginItem> list)
        {
            int n = 0;
            foreach (var p in list) if (p.IsGit) n++;
            return n;
        }

        void Op(PluginItem p, Func<PluginItem, string> op)
        {
            var t = new Thread(delegate()
            {
                string err = "";
                try { err = op(p); } catch (Exception ex) { err = ex.Message; }
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    RenderPlugins();
                    if (err.Length > 0) ShowModernWarn(this, "操作失败", err);
                    else { RenderPlugins(); RunDetect(); }
                }));
            });
            t.IsBackground = true;
            t.Start();
        }

        void ConfirmUninstall(PluginItem p)
        {
            bool ok = ShowModernConfirm(this, "卸载插件", "确定卸载插件「" + p.Name + "」？\n\n将删除目录:\n" + p.Path + "\n\n（此操作不可撤销）");
            if (ok) Op(p, dsh.UninstallPlugin);
        }

        void InstallPluginPrompt()
        {
            string input = Prompt("安装插件", "支持两种方式安装:\n\n  1. git 仓库地址 (克隆到插件目录)\n  2. npm 包名 (全局安装)\n\n示例: https://github.com/user/plugin.git", "");
            if (string.IsNullOrEmpty(input)) return;
            bool isUrl = input.IndexOf("://") >= 0 || input.StartsWith("git@") || input.StartsWith("http");
            var t = new Thread(delegate()
            {
                string err = isUrl ? dsh.InstallPluginFromUrl(input) : dsh.InstallNpmPlugin(input);
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    RenderPlugins();
                    if (err.Length > 0) ShowModernWarn(this, "插件安装失败", err);
                    else { MarkDirty(2); MarkDirty(0); sbText.Text = "插件已安装"; RenderPlugins(); RunDetect(); }
                }));
            });
            t.IsBackground = true;
            t.Start();
        }

        void UpdateAllPlugins()
        {
            sbText.Text = "正在更新所有插件…";
            SetBusy(true);
            var t = new Thread(delegate()
            {
                string[] results = dsh.PullAllPlugins();
                // 重新检查更新状态, 让更新页按钮立即变为"✓ 已是最新"
                try { dsh.Update = dsh.CheckUpdates(dsh.Env); } catch { }
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    SetBusy(false);
                    MarkDirty(2); MarkDirty(0); MarkDirty(3); sbText.Text = "插件更新完成";
                    ShowModernInfo(this, "全部更新", string.Join("\n", results));
                    RenderPlugins();
                    RenderUpdate();
                }));
            });
            t.IsBackground = true;
            t.Start();
        }

        void MaintainPlugins()
        {
            sbText.Text = "一键维护中…";
            SetBusy(true);
            var t = new Thread(delegate()
            {
                var lines = new List<string>();
                lines.AddRange(dsh.PullAllPlugins());
                lines.AddRange(dsh.RepairPlugins());
                // 重新检查更新状态, 让更新页按钮立即变为"✓ 已是最新"
                try { dsh.Update = dsh.CheckUpdates(dsh.Env); } catch { }
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    SetBusy(false);
                    MarkDirty(2); MarkDirty(0); MarkDirty(3); sbText.Text = "一键维护完成";
                    ShowModernInfo(this, "一键维护", string.Join("\n", lines.ToArray()));
                    RenderPlugins();
                    RenderUpdate();
                }));
            });
            t.IsBackground = true;
            t.Start();
        }

        // ---------- 更新页 ----------
        TextBlock updSpinner;
        Button updCheckButton;

        Grid BuildUpdatePage()
        {
            Grid pg;
            ScrollViewer scroll;
            pg = PageShell("更新与升级", out scroll);
            var stack = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };

            // 顶部: 检查更新按钮 (带旋转加载动画)
            updCheckButton = Btn(Lang.T("检查更新"), delegate { RunUpdateCheck(); }, true);
            updCheckButton.Margin = new Thickness(0, 0, 10, 0);
            var updBtnRow = new StackPanel { Orientation = Orientation.Horizontal };
            updBtnRow.Children.Add(updCheckButton);
            // 旋转加载指示 (检查中显示, 完成后隐藏) — 用 ⟳ 字符旋转, 无需额外图片资源
            updSpinner = new TextBlock
            {
                Text = "⟳",
                FontSize = 20,
                Foreground = Palette.Brush(Palette.BlueLight),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 2, 0),
                Visibility = Visibility.Collapsed
            };
            updBtnRow.Children.Add(updSpinner);
            stack.Children.Add(updBtnRow);

            // 启动器
            var lup = new Grid();
            lup.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            lup.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            lup.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            var lupCol = new StackPanel();
            var lupTitleRow = new StackPanel { Orientation = Orientation.Horizontal };
            lupTitleRow.Children.Add(new TextBlock { Text = "🚀 ", FontSize = 13, VerticalAlignment = VerticalAlignment.Center });
            lupTitleRow.Children.Add(new TextBlock { Text = Lang.T("启动器") + " (Launcher)", Foreground = Palette.Brush(Palette.Text), FontSize = 12, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            lupCol.Children.Add(lupTitleRow);
            upLupCur = new TextBlock { Text = Lang.T("当前") + " v" + Dsh.LauncherVersion, Foreground = Palette.Brush(Palette.Text), FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 0) };
            upLupLatest = new TextBlock { Text = Lang.T("未检查"), Foreground = Palette.Brush(Palette.TextFaint), FontSize = 12, Margin = new Thickness(0, 2, 0, 0) };
            upLupNote = new TextBlock { Text = "", Foreground = Palette.Brush(Palette.TextFaint), FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };
            lupCol.Children.Add(upLupCur);
            lupCol.Children.Add(upLupLatest);
            lupCol.Children.Add(upLupNote);
            // 自更新进度条 + 状态 (默认隐藏)
            upLupProg = new System.Windows.Controls.ProgressBar { Height = 6, Minimum = 0, Maximum = 100, Value = 0, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 6, 0, 0) };
            lupCol.Children.Add(upLupProg);
            upLupStatus = new TextBlock { Text = "", Foreground = Palette.Brush(Palette.BlueLight), FontSize = 12, TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed, Margin = new Thickness(0, 3, 0, 0) };
            lupCol.Children.Add(upLupStatus);
            lup.Children.Add(lupCol);
            upLupGo = Btn("🚀 " + Lang.T("立即升级"), delegate { UpgradeLauncher(); }, false);
            Grid.SetColumn(upLupGo, 2);
            lup.Children.Add(upLupGo);
            stack.Children.Add(Card(lup));

            // dsh (Harness 核心)
            var dshG = new Grid();
            dshG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            dshG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            dshG.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
            var dshCol = new StackPanel();
            var dshTitleRow = new StackPanel { Orientation = Orientation.Horizontal };
            dshTitleRow.Children.Add(new TextBlock { Text = "🐋 ", FontSize = 13, VerticalAlignment = VerticalAlignment.Center });
            dshTitleRow.Children.Add(new TextBlock { Text = "Harness (dsh)", Foreground = Palette.Brush(Palette.Text), FontSize = 12, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            dshCol.Children.Add(dshTitleRow);
            upDshCur = new TextBlock { Text = Lang.T("当前") + " -", Foreground = Palette.Brush(Palette.Text), FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 0) };
            upDshLatest = new TextBlock { Text = Lang.T("未检查"), Foreground = Palette.Brush(Palette.TextFaint), FontSize = 12, Margin = new Thickness(0, 2, 0, 0) };
            upDshNote = new TextBlock { Text = "", Foreground = Palette.Brush(Palette.TextFaint), FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };
            dshCol.Children.Add(upDshCur);
            dshCol.Children.Add(upDshLatest);
            dshCol.Children.Add(upDshNote);
            dshG.Children.Add(dshCol);
            upDshUp = Btn(Lang.T("立即升级 dsh"), delegate { UpgradeDsh(); }, true);
            Grid.SetColumn(upDshUp, 2);
            dshG.Children.Add(upDshUp);
            stack.Children.Add(Card(dshG));

            // 插件
            var plg = new StackPanel();
            var plgTitleRow = new StackPanel { Orientation = Orientation.Horizontal };
            plgTitleRow.Children.Add(new TextBlock { Text = "📦 ", FontSize = 13, VerticalAlignment = VerticalAlignment.Center });
            plgTitleRow.Children.Add(new TextBlock { Text = Lang.T("插件"), Foreground = Palette.Brush(Palette.Text), FontSize = 12, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
            plg.Children.Add(plgTitleRow);
            upPluginNote = new TextBlock { Text = Lang.T("插件更新") + ": " + Lang.T("未检查"), Foreground = Palette.Brush(Palette.Text), FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 6, 0, 0) };
            plg.Children.Add(upPluginNote);
            upPluginUp = Btn(Lang.T("全部更新插件"), delegate { UpdateAllPlugins(); }, false);
            upPluginUp.Margin = new Thickness(0, 8, 0, 0);
            plg.Children.Add(upPluginUp);
            stack.Children.Add(Card(plg));

            scroll.Content = stack;
            return pg;
        }

        // 检查中: 显示旋转动画 + 禁用按钮; 完成: 隐藏动画 + 恢复
        void SetUpdateChecking(bool checking)
        {
            if (updCheckButton != null)
            {
                updCheckButton.IsEnabled = !checking;
                updCheckButton.Content = checking ? Lang.T("正在检查…") : Lang.T("检查更新");
                updCheckButton.Opacity = checking ? 0.7 : 1.0;
            }
            if (updSpinner != null)
            {
                updSpinner.Visibility = checking ? Visibility.Visible : Visibility.Collapsed;
                if (checking)
                {
                    var rot = new RotateTransform(0);
                    updSpinner.RenderTransform = rot;
                    updSpinner.RenderTransformOrigin = new Point(0.5, 0.5);
                    var spin = new DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(900)) { RepeatBehavior = RepeatBehavior.Forever };
                    rot.BeginAnimation(RotateTransform.AngleProperty, spin);
                }
                else
                {
                    updSpinner.RenderTransform = null;
                }
            }
            // 检查期间各栏显示"检查中…"占位, 避免"偷偷摸摸查完瞬间刷新"
            if (upLupNote != null && checking) upLupNote.Text = "⏳ " + Lang.T("正在检查…");
            if (upDshNote != null && checking) upDshNote.Text = "⏳ " + Lang.T("正在检查…");
            if (upPluginNote != null && checking) upPluginNote.Text = Lang.T("插件更新") + ": ⏳ " + Lang.T("正在检查…");
        }

        void RenderUpdate()
        {
            if (upDshCur == null) return;
            var u = dsh.Update;
            upDshCur.Text = Lang.T("当前") + " " + (string.IsNullOrEmpty(u.DshCurrent) ? "-" : u.DshCurrent);
            upDshLatest.Text = "最新 " + (string.IsNullOrEmpty(u.DshLatest) ? "-" : u.DshLatest);
            upDshNote.Text = u.DshUpdate ? "发现新版本！" : "已是最新版本";
            upDshNote.Foreground = Palette.Brush(u.DshUpdate ? Palette.Warn : Palette.TextFaint);
            upPluginNote.Text = u.PluginCount > 0
                ? "插件更新: " + u.PluginCount + " 个可更新（" + u.PluginNames + "）"
                : "插件更新: 全部最新";
            upPluginNote.Foreground = Palette.Brush(u.PluginCount > 0 ? Palette.Warn : Palette.Text);
            // 版本状态 → 按钮状态: 已最新则按钮置灰显示 ✓, 有更新才可点击
            if (upDshUp != null)
                SetBtnState(upDshUp, u.DshUpdate, u.DshUpdate ? Lang.T("立即升级 dsh") : "✓ " + Lang.T("已是最新"));
            if (upPluginUp != null)
                SetBtnState(upPluginUp, u.PluginCount > 0, u.PluginCount > 0 ? Lang.T("全部更新插件") + " (" + u.PluginCount + ")" : "✓ " + Lang.T("已是最新"));
            if (upLupGo != null)
            {
                bool lupNewer = IsLauncherNewer();
                // 有新版 → "立即升级" (点按钮直接自更新, 不再把用户赶去 GitHub)
                SetBtnState(upLupGo, lupNewer, lupNewer ? "🚀 " + Lang.T("立即升级") + " v" + lupLatestStr : "✓ " + Lang.T("已是最新"));
            }
        }

        static void SetBtnState(Button b, bool enabled, string text)
        {
            if (b == null) return;
            b.IsEnabled = enabled;
            b.Content = text;
            b.Opacity = enabled ? 1.0 : 0.55;
        }

        void RunUpdateCheck()
        {
            sbText.Text = "正在检查更新…";
            SetUpdateChecking(true);
            var t = new Thread(delegate()
            {
                string lupLatest = dsh.CheckLauncherUpdate();
                var info = dsh.CheckUpdates(dsh.Env);
                dsh.Update = info;
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    bool newer = false;
                    if (lupLatest != null)
                    {
                        upLupLatest.Text = "最新 " + lupLatest;
                        lupLatestStr = lupLatest;
                        newer = IsLauncherNewer();
                    }
                    upLupNote.Text = newer ? "发现新版本，点击「立即升级」即可自动更新" : "已是最新版本";
                    upLupNote.Foreground = Palette.Brush(newer ? Palette.Warn : Palette.TextFaint);
                    SetUpdateChecking(false);
                    RenderUpdate();
                    MarkDirty(3); sbText.Text = "检查更新完成";
                }));
            });
            t.IsBackground = true;
            t.Start();
        }

        // ---------- 启动器自更新: 下载(带进度) → 校验 → 守护替换 → 自动重启 ----------
        void UpgradeLauncher()
        {
            string ver = lupLatestStr;
            if (string.IsNullOrEmpty(ver))
            {
                ShowModernInfo(this, "升级启动器", "请先点击「检查更新」，确认有新版后再升级。");
                return;
            }
            if (!IsLauncherNewer())
            {
                ShowModernInfo(this, "升级启动器", "已是最新版本，无需升级。");
                return;
            }
            if (Dsh.SandboxMode)
            {
                // 沙盒: 只做"下载+校验"演练(写入 %TEMP% 临时文件), 绝不替换真实 exe
                sbText.Text = "正在下载新版启动器（沙盒演练）…";
                upLupGo.IsEnabled = false;
                upLupProg.Visibility = Visibility.Visible;
                upLupStatus.Visibility = Visibility.Visible;
                upLupStatus.Text = "正在下载并校验 v" + ver + " …";
                SetBusy(true);
                var st = new Thread(delegate()
                {
                    string destPath;
                    string error;
                    bool ok2 = dsh.DownloadLauncherUpdate(ver, out destPath, out error, delegate(int pct)
                    {
                        Dispatcher.BeginInvoke(new Action(delegate
                        {
                            upLupProg.Value = pct;
                            upLupStatus.Text = "正在下载并校验 v" + ver + " … " + pct + "%";
                        }));
                    });
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        SetBusy(false);
                        upLupGo.IsEnabled = true;
                        if (ok2)
                        {
                            upLupProg.Value = 100;
                            upLupStatus.Text = "沙盒演练：下载校验通过（" + destPath + "）";
                            ShowModernInfo(this, "升级启动器（沙盒演练）", "下载与校验成功！\n" + destPath + "\n\n沙盒模式不执行真实替换重启。");
                        }
                        else
                        {
                            upLupProg.Visibility = Visibility.Collapsed;
                            upLupStatus.Text = "";
                            ShowModernWarn(this, "升级启动器", "下载失败：\n" + error + "\n\n可稍后重试，或到 GitHub 手动下载。");
                        }
                    }));
                });
                st.IsBackground = true;
                st.Start();
                return;
            }
            bool ok = ShowModernConfirm(this, "升级启动器",
                "将自动下载 启动器 v" + ver + " 并替换升级，全程不用你操作。\n\n" +
                "升级完软件会自动重启，不影响 dsh 服务运行。\n\n确定开始升级？");
            if (!ok) return;

            sbText.Text = "正在下载新版启动器…";
            upLupGo.IsEnabled = false;
            upLupProg.Visibility = Visibility.Visible;
            upLupStatus.Visibility = Visibility.Visible;
            upLupStatus.Text = "正在下载 v" + ver + " …";
            SetBusy(true);
            var t = new Thread(delegate()
            {
                string destPath;
                string error;
                bool ok2 = dsh.DownloadLauncherUpdate(ver, out destPath, out error, delegate(int pct)
                {
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        upLupProg.Value = pct;
                        upLupStatus.Text = "正在下载 v" + ver + " … " + pct + "%";
                    }));
                });
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (!ok2)
                    {
                        SetBusy(false);
                        upLupProg.Visibility = Visibility.Collapsed;
                        upLupStatus.Visibility = Visibility.Collapsed;
                        upLupGo.IsEnabled = true;
                        upLupStatus.Text = "";
                        ShowModernWarn(this, "升级启动器", "下载失败：\n" + error + "\n\n可稍后重试，或到 GitHub 手动下载。");
                        return;
                    }
                    upLupProg.Value = 100;
                    upLupStatus.Text = "下载完成，正在重启升级…";
                    sbText.Text = "升级完成，正在重启…";
                    // 调用守护替换 (spawn updater), 然后正常退出让 updater 接管重启
                    dsh.ApplyLauncherUpdate(destPath);
                    try { Thread.Sleep(1500); } catch { }   // 给 updater 一点启动时间
                    quitting = true;
                    Close();
                    Application.Current.Shutdown();
                }));
            });
            t.IsBackground = true;
            t.Start();
        }

        void UpgradeDsh()
        {
            if (Dsh.SandboxMode)
            {
                sbText.Text = "沙盒模式不执行升级";
                ShowModernWarn(this, "升级 dsh", "当前为沙盒测试模式（--sandbox），不执行真实升级。");
                return;
            }
            sbText.Text = "正在升级 dsh…";
            SetBusy(true);
            var t = new Thread(delegate()
            {
                string detail;
                string r = dsh.NpmInstallGlobal(dsh.Cfg.NpmPackage, 300000, out detail);
                var env = dsh.DetectEnvironment();
                dsh.Env = env;
                // 重新检查更新状态, 让"立即升级 dsh"按钮立即变为"✓ 已是最新"
                try { dsh.Update = dsh.CheckUpdates(env); } catch { }
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    SetBusy(false);
                    MarkDirty(3);
                    RenderUpdate();
                    RenderOverview();
                    sbText.Text = r == null ? "升级失败" : "dsh 升级完成";
                    if (r == null) ShowModernWarn(this, "升级 dsh", "升级失败（已尝试官方源与国内镜像）。\n" + detail + "\n\n详见 launcher.log。");
                    else ShowModernInfo(this, "升级 dsh", "dsh 已升级到最新版。");
                }));
            });
            t.IsBackground = true;
            t.Start();
        }

        // ---------- 日志页 (专业极客终端 + 左右均衡工具栏 + 快捷工具) ----------
        System.Windows.Controls.TextBox logSearch;

        Grid BuildLogsPage()
        {
            Grid pg;
            ScrollViewer scroll;
            pg = PageShell("日志查看", out scroll);
            var stack = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };

            // 顶部工具栏: 左右均衡分布
            var toolbarGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            toolbarGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // 左侧: 日志源切换 + 实时过滤
            var leftBar = new StackPanel { Orientation = Orientation.Horizontal };
            logKind = new ModernDropdown
            {
                Width = 150,
                Height = 34,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };
            logKind.SetItems(new string[] { "📄 launcher.log", "🤖 dsh.log" }, 0);
            logKind.SelectionChanged += delegate { RefreshLog(); };
            leftBar.Children.Add(logKind);

            logSearch = new System.Windows.Controls.TextBox
            {
                Width = 180,
                Height = 34,
                FontSize = 12,
                Margin = new Thickness(0, 0, 10, 0),
                Background = Palette.Brush(Palette.BgInput),
                Foreground = Palette.Brush(Palette.Text),
                BorderThickness = new Thickness(1),
                BorderBrush = Palette.Brush(Palette.BorderSoft),
                Padding = new Thickness(10, 0, 10, 0),
                VerticalContentAlignment = VerticalAlignment.Center,
                ToolTip = "🔍 " + Lang.T("搜索过滤…")
            };
            logSearch.TextChanged += delegate { FilterLog(); };
            leftBar.Children.Add(logSearch);

            logAuto = new System.Windows.Controls.CheckBox
            {
                Content = "实时监听", Foreground = Palette.Brush(Palette.TextDim), FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center, IsChecked = true, Margin = new Thickness(4, 0, 0, 0)
            };
            leftBar.Children.Add(logAuto);
            toolbarGrid.Children.Add(leftBar);

            // 右侧: 快捷操作按钮组
            var rightBar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            rightBar.Children.Add(Btn("↻ " + Lang.T("刷新列表"), delegate { RefreshLog(); }, false));
            rightBar.Children.Add(Btn("📋 " + Lang.T("复制日志"), delegate { CopyLog(); }, false));
            rightBar.Children.Add(Btn("🧹 " + Lang.T("清空界面"), delegate { ClearLog(); }, false));
            rightBar.Children.Add(Btn("📂 " + Lang.T("打开日志目录"), delegate { try { Process.Start("explorer.exe", "\"" + dsh.Cfg.LogDir + "\""); } catch { } }, false));
            Grid.SetColumn(rightBar, 1);
            toolbarGrid.Children.Add(rightBar);
            stack.Children.Add(toolbarGrid);

            // 控制台卡片 (浅色下采用纯白精致终端卡片 + 深色清晰代码字体)
            logBox = new System.Windows.Controls.TextBox
            {
                IsReadOnly = true,
                TextWrapping = TextWrapping.NoWrap,
                FontFamily = new FontFamily("Consolas, Courier New, monospace"),
                FontSize = 12,
                Background = Palette.Brush(Palette.IsDark ? Palette.BgDeep : Palette.BgCard),
                Foreground = Palette.Brush(Palette.IsDark ? Palette.Text : Palette.TextDim),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(14, 12, 14, 12),
                VerticalContentAlignment = VerticalAlignment.Top,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Height = 490
            };
            stack.Children.Add(Card(logBox));
            scroll.Content = stack;
            logTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            logTimer.Tick += delegate { if (logAuto.IsChecked == true) RefreshLog(); };
            logTimer.Start();
            return pg;
        }

        string rawLogContent = "";

        void RefreshLog()
        {
            string name = logKind.SelectedIndex == 1 ? "dsh.log" : "launcher.log";
            rawLogContent = Dsh.ReadTail(Path.Combine(dsh.Cfg.LogDir, name), 800);
            FilterLog();
        }

        void FilterLog()
        {
            if (string.IsNullOrEmpty(rawLogContent))
            {
                logBox.Text = Lang.T("暂无日志");
                return;
            }
            string q = logSearch != null ? logSearch.Text.Trim() : "";
            if (string.IsNullOrEmpty(q))
            {
                logBox.Text = rawLogContent;
            }
            else
            {
                var sb = new StringBuilder();
                foreach (string line in rawLogContent.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (line.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0) sb.AppendLine(line);
                }
                logBox.Text = sb.Length > 0 ? sb.ToString() : "（未找到包含「" + q + "」的日志）";
            }
            logBox.ScrollToEnd();
        }

        void CopyLog()
        {
            try
            {
                if (!string.IsNullOrEmpty(logBox.Text))
                {
                    Clipboard.SetText(logBox.Text);
                    sbText.Text = Lang.T("已复制到剪贴板");
                }
            }
            catch { }
        }

        void ClearLog()
        {
            rawLogContent = "";
            logBox.Text = "";
        }

        // ---------- 设置页 (模块化分组 + 精致卡片) ----------
        Grid BuildSettingsPage()
        {
            Grid pg;
            ScrollViewer scroll;
            pg = PageShell("设置", out scroll);
            var stack = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };

            // 基础与路径配置卡片
            var baseCardStack = new StackPanel();
            baseCardStack.Children.Add(new TextBlock { Text = Lang.T("核心服务与路径"), Foreground = Palette.Brush(Palette.Text), FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            int rr = 0;
            AddSettingRow(grid, rr++, "服务端口", dsh.Cfg.Port.ToString(), "port", "默认 8099");
            AddSettingRow(grid, rr++, "DSH_HOME", dsh.Cfg.DshHome, "home", "dsh 数据目录");
            AddSettingRow(grid, rr++, "插件目录", dsh.Cfg.PluginsRoot, "plugins", "plugins 存放目录");
            AddSettingRow(grid, rr++, "日志目录", dsh.Cfg.LogDir, "log", "日志保存目录");
            baseCardStack.Children.Add(grid);
            stack.Children.Add(Card(baseCardStack));

            // 网络与代理卡片
            var netCardStack = new StackPanel();
            netCardStack.Children.Add(new TextBlock { Text = Lang.T("网络、包源与更新"), Foreground = Palette.Brush(Palette.Text), FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
            var netGrid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            netGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            netGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            int nrr = 0;
            AddSettingRow(netGrid, nrr++, "npm 包名", dsh.Cfg.NpmPackage, "npm", "@deepseek-ai/dsh");
            AddSettingRow(netGrid, nrr++, Lang.T("npm 镜像"), dsh.Cfg.NpmRegistry, "npmreg", "例如 https://registry.npmmirror.com (留空默认)");
            AddSettingRow(netGrid, nrr++, "代理地址", dsh.Cfg.Proxy, "proxy", "例如 http://127.0.0.1:7890 (留空自动检测)");
            AddSettingRow(netGrid, nrr++, "启动器更新源", dsh.Cfg.LauncherUpdateUrl, "lup", "版本检测 URL");

            // 界面外观与多语言卡片
            var appCardStack = new StackPanel();
            appCardStack.Children.Add(new TextBlock { Text = "🌐 " + Lang.T("界面外观与个性化"), Foreground = Palette.Brush(Palette.Text), FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
            var appGrid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            appGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            appGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // 主题切换 (深色 / 浅色)
            var themeLbl = new TextBlock { Text = "🎨 " + Lang.T("界面主题"), Foreground = Palette.Brush(Palette.TextDim), FontSize = 13, VerticalAlignment = VerticalAlignment.Center };
            setTheme = new ModernDropdown
            {
                Width = 220,
                Height = 34,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 5, 0, 5)
            };
            setTheme.SetItems(new string[] { "🌙 " + Lang.T("深色模式 (Dark)"), "☀️ " + Lang.T("浅色模式 (Light)") }, Palette.IsDark ? 0 : 1);
            setTheme.SelectionChanged += delegate
            {
                bool dark = (setTheme.SelectedIndex == 0);
                if (Palette.IsDark != dark)
                {
                    Palette.IsDark = dark;
                    dsh.Cfg.Theme = dark ? "dark" : "light";
                    dsh.Cfg.Save();
                    RebuildAllPages();
                }
            };
            appGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(themeLbl, 0);
            Grid.SetRow(setTheme, 0);
            appGrid.Children.Add(themeLbl);
            appGrid.Children.Add(setTheme);
            Grid.SetColumn(setTheme, 1);

            // 多语言切换 (配地球图标与国旗)
            var langLbl = new TextBlock { Text = "🌍 " + Lang.T("界面语言"), Foreground = Palette.Brush(Palette.TextDim), FontSize = 13, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 8, 0, 0) };
            setLang = new ModernDropdown
            {
                Width = 220,
                Height = 34,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 8, 0, 5)
            };
            setLang.SetItems(new string[] { "🌐 跟随系统 (Auto)", "🇨🇳 简体中文", "🇺🇸 English", "🇯🇵 日本語", "🇰🇷 한국어", "🇷🇺 Русский", "🇫🇷 Français", "🇩🇪 Deutsch", "🇪🇸 Español" },
                dsh.Cfg.Language == "zh" ? 1 : (dsh.Cfg.Language == "en" ? 2 : (dsh.Cfg.Language == "ja" ? 3 : (dsh.Cfg.Language == "ko" ? 4 : (dsh.Cfg.Language == "ru" ? 5 : (dsh.Cfg.Language == "fr" ? 6 : (dsh.Cfg.Language == "de" ? 7 : (dsh.Cfg.Language == "es" ? 8 : 0))))))));
            setLang.SelectionChanged += delegate
            {
                int si = setLang.SelectedIndex;
                string code = si == 1 ? "zh" : (si == 2 ? "en" : (si == 3 ? "ja" : (si == 4 ? "ko" : (si == 5 ? "ru" : (si == 6 ? "fr" : (si == 7 ? "de" : (si == 8 ? "es" : "")))))));
                if (dsh.Cfg.Language != code)
                {
                    dsh.Cfg.Language = code;
                    Lang.Set(code);
                    dsh.Cfg.Save();
                    RebuildAllPages();
                }
            };
            appGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetRow(langLbl, 1);
            Grid.SetRow(setLang, 1);
            appGrid.Children.Add(langLbl);
            appGrid.Children.Add(setLang);
            Grid.SetColumn(setLang, 1);

            appCardStack.Children.Add(appGrid);
            stack.Children.Add(Card(appCardStack));

            // 操作按钮组
            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0) };
            btnRow.Children.Add(Btn("💾 " + Lang.T("保存设置"), delegate { SaveSettings(); }, true));
            btnRow.Children.Add(Btn("↻ " + Lang.T("自动检测回填"), delegate { AutoFillSettings(); }, false));
            btnRow.Children.Add(Btn("⚡ " + Lang.T("检测代理"), delegate { DetectProxyFill(); }, false));
            btnRow.Children.Add(Btn("📌 " + Lang.T("桌面快捷方式"), delegate { MakeShortcut(); }, false));
            btnRow.Children.Add(Btn("📤 " + Lang.T("导出诊断包"), delegate { ExportDiag(); }, false));
            btnRow.Children.Add(Btn("📄 " + Lang.T("配置文件"), delegate { try { Process.Start("notepad.exe", "\"" + LauncherConfig.ConfigPath + "\""); } catch { } }, false));
            btnRow.Children.Add(Btn("ℹ️ " + Lang.T("关于"), delegate { ShowAbout(); }, false));
            stack.Children.Add(btnRow);

            scroll.Content = stack;
            return pg;
        }

        // ---------- 关于对话框 (中英双语介绍) ----------
        void ShowAbout()
        {
            var w = new Window
            {
                Title = Lang.T("关于"),
                Width = 560,
                MinWidth = 500,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                WindowStyle = WindowStyle.ToolWindow,
                ResizeMode = ResizeMode.NoResize,
                Background = Palette.Brush(Palette.Bg)
            };
            var g = new Grid { Margin = new Thickness(24) };
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 头部: Logo + 名称 + 版本
            var head = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            ImageSource whale = LoadEmbeddedPng(Palette.IsDark ? "DeepSeekHarness.whale-white.png" : "DeepSeekHarness.whale-blue.png");
            if (whale != null)
            {
                var logoBox = new Border
                {
                    Width = 44, Height = 44, CornerRadius = new CornerRadius(12),
                    Background = Palette.BlueGradient(), VerticalAlignment = VerticalAlignment.Center
                };
                logoBox.Child = new System.Windows.Controls.Image { Source = whale, Width = 28, Height = 28, Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                head.Children.Add(logoBox);
            }
            var headText = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 0, 0) };
            headText.Children.Add(new TextBlock { Text = "DeepSeek Harness Launcher", Foreground = Palette.Brush(Palette.Text), FontSize = 16, FontWeight = FontWeights.Bold });
            headText.Children.Add(new TextBlock { Text = "v" + Dsh.LauncherVersion + " · by loudMore", Foreground = Palette.Brush(Palette.TextFaint), FontSize = 12, Margin = new Thickness(0, 3, 0, 0) });
            head.Children.Add(headText);
            Grid.SetRow(head, 0);
            g.Children.Add(head);

            // 中英双语介绍
            var body = new StackPanel();
            string zh = "一个专门为 DeepSeek Harness (dsh) 打造的傻瓜式管理工具：\n" +
                "· 一键检测环境、安装 dsh，新手也能 5 分钟跑起来\n" +
                "· 图形化管理插件：安装、更新、修复依赖、启用禁用、一键维护\n" +
                "· 启动器 / dsh / 插件 三维更新看板，自动检查，一键升级\n" +
                "· 插件商城聚合 GitHub + npm + Awesome 数百插件\n" +
                "· 自动代理探测 + 国内镜像兜底，网络再差也能装\n" +
                "· 深色/浅色双主题，中英日韩俄法德西 8 种语言";
            string en = "A fool-proof manager built for DeepSeek Harness (dsh):\n" +
                "· One-click environment detection & dsh installation\n" +
                "· Visual plugin management: install, update, fix deps, toggle, maintain\n" +
                "· 3-way update board (launcher / dsh / plugins) with auto-check\n" +
                "· Plugin store aggregating GitHub + npm + awesome lists\n" +
                "· Auto proxy detection with China mirror fallbacks\n" +
                "· Dark/light themes, 8 languages";

            string show = (Lang.Code == "zh" || Lang.Code == "") ? zh : en;
            if (Lang.Code != "zh" && Lang.Code != "" && Lang.Code != "en")
            {
                // 其他语言: 显示英文为主 + 中文对照
                show = "🇨🇳 " + zh + "\n\n🇺🇸 " + en;
            }
            var bodyTb = new TextBlock
            {
                Text = show,
                Foreground = Palette.Brush(Palette.TextDim),
                FontSize = 13,
                LineHeight = 21,
                TextWrapping = TextWrapping.Wrap
            };
            body.Children.Add(bodyTb);
            Grid.SetRow(body, 1);
            g.Children.Add(body);

            var okBtn = Btn(Lang.T("我知道了"), delegate { w.DialogResult = true; }, true);
            okBtn.Width = 110;
            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
            row.Children.Add(okBtn);
            Grid.SetRow(row, 2);
            g.Children.Add(row);

            w.Content = g;
            w.ShowDialog();
        }

        ModernDropdown setLang;
        ModernDropdown setTheme;

        // 重建所有页面 (用于主题切换 / 语言切换时的实时全量响应)
        void RebuildAllPages()
        {
            pages.Clear();
            navs.Clear();
            host = null;
            BuildUi();
            for (int i = 0; i < 6; i++) { pageReady[i] = false; pageDirty[i] = true; }
            SwitchPage(curPage);
        }

        void AddSettingRow(Grid grid, int row, string label, string value, string key, string placeholder = "")
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var lbl = new TextBlock { Text = label, Foreground = Palette.Brush(Palette.TextDim), FontSize = 13, Margin = new Thickness(0, 8, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            var box = new System.Windows.Controls.TextBox
            {
                Text = value,
                FontSize = 13,
                Height = 32,
                Margin = new Thickness(0, 5, 0, 5),
                Background = Palette.Brush(Palette.BgInput),
                Foreground = Palette.Brush(Palette.Text),
                BorderThickness = new Thickness(1),
                BorderBrush = Palette.Brush(Palette.BorderSoft),
                Padding = new Thickness(10, 0, 10, 0),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            if (!string.IsNullOrEmpty(placeholder)) box.ToolTip = placeholder;
            Grid.SetRow(lbl, row);
            Grid.SetRow(box, row);
            Grid.SetColumn(box, 1);
            grid.Children.Add(lbl);
            grid.Children.Add(box);
            setBoxes[key] = box;
        }

        void SaveSettings()
        {
            int oldPort = dsh.Cfg.Port;
            int port = oldPort;
            if (int.TryParse(setBoxes["port"].Text.Trim(), out port))
            {
                if (port < 1 || port > 65535)
                {
                    ShowModernWarn(this, "设置", "端口无效：请输入 1~65535 之间的数字。");
                    return;
                }
                dsh.Cfg.Port = port;
            }
            dsh.Cfg.DshHome = setBoxes["home"].Text.Trim();
            dsh.Cfg.PluginsRoot = setBoxes["plugins"].Text.Trim();
            dsh.Cfg.LogDir = setBoxes["log"].Text.Trim();
            dsh.Cfg.NpmPackage = setBoxes["npm"].Text.Trim();
            dsh.Cfg.LauncherUpdateUrl = setBoxes["lup"].Text.Trim();
            dsh.Cfg.Proxy = setBoxes["proxy"].Text.Trim();
            dsh.Cfg.NpmRegistry = setBoxes["npmreg"].Text.Trim();
            int langSi = setLang != null ? setLang.SelectedIndex : 0;
            dsh.Cfg.Language = langSi == 1 ? "zh" : (langSi == 2 ? "en" : (langSi == 3 ? "ja" : (langSi == 4 ? "ko" : (langSi == 5 ? "ru" : (langSi == 6 ? "fr" : (langSi == 7 ? "de" : (langSi == 8 ? "es" : "")))))));
            dsh.Cfg.ApplyDefaults();
            if (!dsh.Cfg.Save())
            {
                ShowModernWarn(this, "设置", "设置保存失败（配置文件可能被占用或无权限）。");
                return;
            }
            dsh.AppendLog("[settings] 已保存: port=" + dsh.Cfg.Port + " home=" + dsh.Cfg.DshHome + " plugins=" + dsh.Cfg.PluginsRoot
                + " proxy=" + (string.IsNullOrEmpty(dsh.Cfg.Proxy) ? "(直连)" : dsh.Cfg.Proxy) + " language=" + dsh.Cfg.Language);
            MarkDirty(0); MarkDirty(1);
            sbText.Text = "设置已保存";

            // 端口修改 → 立即重启服务使生效 (新端口被占用时不启动, 不杀别人的进程)
            if (port != oldPort)
            {
                if (Dsh.IsPortOpen(port))
                {
                    ShowModernWarn(this, "设置", "端口 " + port + " 已被其他程序占用，无法重启服务。\n请换一个空闲端口（或先关闭占用该端口的程序）后再次保存。");
                    return;
                }
                if (ShowModernConfirm(this, "设置", "端口已从 " + oldPort + " 改为 " + port + "。\n\n是否立即重启服务使新端口生效？\n（服务会短暂中断几秒）"))
                {
                    ApplyPortRestart(oldPort, port);
                }
                else
                {
                    ShowModernInfo(this, "设置", "设置已保存。\n\n端口改动将在下次启动服务时生效。");
                }
            }
            else
            {
                ShowModernInfo(this, "设置", "设置已保存。\n\n端口/路径等改动在下次启动服务时生效。");
            }
        }

        // 停止旧端口服务 → 等旧端口释放 → 用新端口启动 → 校验监听
        void ApplyPortRestart(int oldPort, int newPort)
        {
            sbText.Text = "正在重启服务应用新端口…";
            SetBusy(true);
            var t = new Thread(delegate()
            {
                try
                {
                    dsh.StopServiceAsync();
                    int waited = 0;
                    while (waited < 40 && Dsh.IsPortOpen(oldPort)) { Thread.Sleep(300); waited++; }
                    if (Dsh.IsPortOpen(oldPort)) dsh.AppendLog("[port] 旧端口 " + oldPort + " 未能在 12 秒内释放");
                    dsh.StartServiceAsync();
                    waited = 0;
                    bool ok = false;
                    while (waited < 40)
                    {
                        Thread.Sleep(300);
                        if (Dsh.IsPortOpen(newPort)) { ok = true; break; }
                        waited++;
                    }
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        SetBusy(false);
                        RenderOverview();
                        if (ok)
                        {
                            sbText.Text = "服务已在新端口 " + newPort + " 运行";
                            dsh.AppendLog("[port] 服务已在新端口 " + newPort + " 就绪");
                            ShowModernInfo(this, "设置", "服务已在新端口 " + newPort + " 运行。\n打开浏览器访问 http://127.0.0.1:" + newPort);
                        }
                        else
                        {
                            sbText.Text = "新端口服务启动失败";
                            dsh.AppendLog("[port] 新端口 " + newPort + " 服务未就绪（可能 dsh 未安装或配置错误）");
                            ShowModernWarn(this, "设置", "新端口服务未能就绪。\n请到「日志」页查看 launcher.log 中的详细错误。");
                        }
                    }));
                }
                catch (Exception ex)
                {
                    Dispatcher.BeginInvoke(new Action(delegate { SetBusy(false); ShowModernWarn(this, "设置", "重启服务出错: " + ex.Message); }));
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        void AutoFillSettings()
        {
            sbText.Text = "正在检测…";
            var t = new Thread(delegate()
            {
                var env = dsh.DetectEnvironment();
                string proxy = null;
                try { proxy = dsh.ResolveProxy(); } catch { }
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    dsh.Env = env;
                    // 回填最新配置关键字段 (让用户看到软件当前生效的值)
                    setBoxes["home"].Text = dsh.Cfg.DshHome;
                    setBoxes["plugins"].Text = dsh.Cfg.PluginsRoot;
                    setBoxes["log"].Text = dsh.Cfg.LogDir;
                    setBoxes["proxy"].Text = string.IsNullOrEmpty(proxy) ? setBoxes["proxy"].Text.Trim() : proxy;
                    RenderEnv();
                    RenderOverview();
                    sbText.Text = "检测完成";
                }));
            });
            t.IsBackground = true;
            t.Start();
        }

        // 检测代理并回填输入框
        void DetectProxyFill()
        {
            sbText.Text = "正在检测代理…";
            var t = new Thread(delegate()
            {
                string p = null;
                try { p = dsh.ResolveProxy(); } catch { }
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    if (!string.IsNullOrEmpty(p)) setBoxes["proxy"].Text = p;
                    sbText.Text = string.IsNullOrEmpty(p) ? "未检测到代理" : "已检测到代理 " + p;
                }));
            });
            t.IsBackground = true;
            t.Start();
        }

        void MakeShortcut()
        {
            string err = dsh.CreateDesktopShortcut();
            if (err.Length == 0)
                ShowModernInfo(this, Lang.T("桌面快捷方式"), "已在桌面创建「DeepSeek Harness」快捷方式，双击即可启动。");
            else
                ShowModernWarn(this, Lang.T("桌面快捷方式"), err);
        }

        // ---------- 一键导出诊断包 (粉丝遇到问题 → 打包发给开发者 → 针对性修复) ----------
        void ExportDiag()
        {
            sbText.Text = "正在收集诊断信息…";
            SetBusy(true);
            var t = new Thread(delegate()
            {
                string err = "";
                string zipPath = "";
                try
                {
                    string ts = DateTime.Now.ToString("yyyyMMdd-HHmm");
                    string tmp = Path.Combine(Path.GetTempPath(), "dsh-diag-" + Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(tmp);
                    // 输出位置多级兜底 (防桌面被重定向/无权限/企业策略锁写导致整包失败):
                    // 桌面 → 我的文档 → 下载 → exe 目录 → 系统临时目录
                    zipPath = MakeDiagZipPath(ts);
                    if (string.IsNullOrEmpty(zipPath)) { err = "没有可写的输出位置"; return; }

                    // 1. 日志文件
                    string log1 = Path.Combine(dsh.Cfg.LogDir, "launcher.log");
                    if (File.Exists(log1)) File.Copy(log1, Path.Combine(tmp, "launcher.log"), true);
                    string log2 = Path.Combine(dsh.Cfg.LogDir, "dsh.log");
                    if (!File.Exists(log2)) log2 = Path.Combine(dsh.Cfg.DshHome, "dsh.log");
                    if (File.Exists(log2)) File.Copy(log2, Path.Combine(tmp, "dsh.log"), true);
                    string crash = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
                    if (File.Exists(crash)) File.Copy(crash, Path.Combine(tmp, "crash.log"), true);

                    // 2. 配置 (代理密码脱敏)
                    if (File.Exists(LauncherConfig.ConfigPath))
                    {
                        string cfgText = File.ReadAllText(LauncherConfig.ConfigPath);
                        cfgText = Regex.Replace(cfgText, "(\"proxy\"\\s*:\\s*\"[^\"]*://[^:\"]+):([^\"@]+)@", "$1:***@");
                        File.WriteAllText(Path.Combine(tmp, "launcher.json"), cfgText);
                    }

                    // 3. 环境报告
                    var sb = new StringBuilder();
                    sb.AppendLine("===== DeepSeek Harness 启动器诊断报告 =====");
                    sb.AppendLine("生成时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    sb.AppendLine("启动器版本: " + Dsh.LauncherVersion + " (WPF)");
                    sb.AppendLine("操作系统: " + Environment.OSVersion + (Environment.Is64BitOperatingSystem ? " (x64)" : " (x86)"));
                    sb.AppendLine(".NET: " + Environment.Version);
                    sb.AppendLine("CPU 核心数: " + Environment.ProcessorCount);
                    sb.AppendLine();
                    sb.AppendLine("===== 配置 =====");
                    sb.AppendLine("端口: " + dsh.Cfg.Port + " (监听: " + (Dsh.IsPortOpen(dsh.Cfg.Port) ? "是" : "否") + ")");
                    sb.AppendLine("dshHome: " + dsh.Cfg.DshHome + (Directory.Exists(dsh.Cfg.DshHome) ? "" : " (目录不存在)"));
                    sb.AppendLine("pluginsRoot: " + dsh.Cfg.PluginsRoot + (Directory.Exists(dsh.Cfg.PluginsRoot) ? "" : " (目录不存在)"));
                    sb.AppendLine("logDir: " + dsh.Cfg.LogDir);
                    sb.AppendLine("代理: " + (string.IsNullOrEmpty(dsh.Cfg.Proxy) ? "(未配置/直连)" : dsh.Cfg.Proxy));
                    sb.AppendLine("npmRegistry: " + (string.IsNullOrEmpty(dsh.Cfg.NpmRegistry) ? "(默认)" : dsh.Cfg.NpmRegistry));
                    sb.AppendLine("npmPackage: " + dsh.Cfg.NpmPackage);
                    sb.AppendLine();
                    sb.AppendLine("===== 环境检测 =====");
                    var env = dsh.Env;
                    sb.AppendLine("Node.js: " + (string.IsNullOrEmpty(env.NodePath) ? "未检测到" : env.NodeVersion + " @ " + env.NodePath));
                    sb.AppendLine("npm: " + (string.IsNullOrEmpty(env.NpmPath) ? "未检测到" : env.NpmVersion + " @ " + env.NpmPath));
                    sb.AppendLine("Git: " + (string.IsNullOrEmpty(env.GitPath) ? "未检测到" : CleanVer(env.GitVersion) + " @ " + env.GitPath));
                    sb.AppendLine("dsh: " + (string.IsNullOrEmpty(env.DshPath) ? "未检测到" : env.DshVersion + " @ " + env.DshPath));
                    sb.AppendLine();
                    sb.AppendLine("===== 插件清单 =====");
                    if (Directory.Exists(dsh.Cfg.PluginsRoot))
                    {
                        string[] dirs = Directory.GetDirectories(dsh.Cfg.PluginsRoot);
                        if (dirs.Length == 0) sb.AppendLine("(无插件)");
                        foreach (string d in dirs)
                        {
                            string name = Path.GetFileName(d);
                            bool dis = name.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);
                            bool git = Directory.Exists(Path.Combine(d, ".git"));
                            sb.AppendLine((dis ? "[已禁用] " : "[正常] ") + name + (git ? " (git)" : ""));
                        }
                    }
                    else sb.AppendLine("(插件目录不存在)");
                    sb.AppendLine();
                    sb.AppendLine("===== 最近错误摘要 (launcher.log 关键行) =====");
                    string tail = Dsh.ReadTail(Path.Combine(dsh.Cfg.LogDir, "launcher.log"), 800);
                    int errCount = 0;
                    foreach (string line in tail.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (line.IndexOf("error", StringComparison.OrdinalIgnoreCase) >= 0
                            || line.IndexOf("失败", StringComparison.OrdinalIgnoreCase) >= 0
                            || line.IndexOf("ERR", StringComparison.OrdinalIgnoreCase) >= 0
                            || line.IndexOf("[env]", StringComparison.OrdinalIgnoreCase) >= 0
                            || line.IndexOf("[install]", StringComparison.OrdinalIgnoreCase) >= 0
                            || line.IndexOf("[npm]", StringComparison.OrdinalIgnoreCase) >= 0
                            || line.IndexOf("[git]", StringComparison.OrdinalIgnoreCase) >= 0
                            || line.IndexOf("[port]", StringComparison.OrdinalIgnoreCase) >= 0
                            || line.IndexOf("[settings]", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            sb.AppendLine(line);
                            errCount++;
                            if (errCount >= 60) break;
                        }
                    }
                    if (errCount == 0) sb.AppendLine("(launcher.log 无错误记录)");
                    File.WriteAllText(Path.Combine(tmp, "env-report.txt"), sb.ToString());

                    // 4. 打包: 优先 .NET ZipArchive 直写 (不依赖子进程/中文路径/重定向桌面);
                    //    失败则逐级换输出目录重试; 最后用 tar.exe 兜底
                    string zipWrote = "";
                    foreach (string dir in DiagOutputDirs())
                    {
                        string cand = Path.Combine(dir, "dsh-诊断包-" + ts + ".zip");
                        try { zipWrote = WriteDiagZip(tmp, cand); } catch { zipWrote = ""; }
                        if (!string.IsNullOrEmpty(zipWrote)) { zipPath = zipWrote; break; }
                    }
                    if (string.IsNullOrEmpty(zipWrote))
                    {
                        // tar 兜底 (文件小, tar 孤儿也可解析)
                        foreach (string dir in DiagOutputDirs())
                        {
                            try
                            {
                                string cand = Path.Combine(dir, "dsh-诊断包-" + ts + ".zip");
                                if (File.Exists(cand)) File.Delete(cand);
                                string tarOk = Dsh.RunCapture("tar.exe", "-cf \"" + cand + "\" -C \"" + tmp + "\" .", 60000);
                                if (File.Exists(cand) && new FileInfo(cand).Length > 200) { zipPath = cand; zipWrote = cand; break; }
                                try { if (File.Exists(cand)) File.Delete(cand); } catch { }
                            }
                            catch { }
                        }
                        if (string.IsNullOrEmpty(zipWrote)) err = "打包失败（无可用输出目录）";
                    }
                    try { Directory.Delete(tmp, true); } catch { }
                }
                catch (Exception ex) { err = ex.Message; }
                string finalZip = zipPath;
                string finalErr = err;
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    SetBusy(false);
                    if (Dsh.DiagTestMode)
                    {
                        // 自动化测试: 结果写标记文件后退出
                        try
                        {
                            File.WriteAllText(Path.Combine(Path.GetTempPath(), "dsh-diag-test-result.txt"),
                                (finalErr.Length == 0 ? "OK:" + finalZip : "FAIL:" + finalErr));
                        }
                        catch { }
                        try { Environment.Exit(0); } catch { }
                        return;
                    }
                    if (finalErr.Length == 0)
                    {
                        sbText.Text = "诊断包已导出";
                        ShowModernInfo(this, "导出诊断包", "诊断包已导出到：\n" + finalZip + "\n\n把这个文件发给开发者，即可针对性排查问题。\n（已自动隐藏代理密码）");
                    }
                    else
                    {
                        sbText.Text = "导出失败";
                        ShowModernWarn(this, "导出诊断包", "导出失败：" + finalErr);
                    }
                }));
            });
            t.IsBackground = true;
            t.Start();
        }

        // 诊断包输出目录候选: 桌面 → 我的文档 → 下载 → exe 目录 → 系统临时目录
        static List<string> DiagOutputDirs()
        {
            var dirs = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Action<string> add = delegate(string p)
            {
                if (!string.IsNullOrEmpty(p))
                {
                    try { if (!Directory.Exists(p)) Directory.CreateDirectory(p); } catch { return; }
                    if (seen.Add(p)) dirs.Add(p);
                }
            };
            try { add(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)); } catch { }
            try { add(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)); } catch { }
            try { add(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")); } catch { }
            try { add(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)); } catch { }
            add(AppDomain.CurrentDomain.BaseDirectory);
            add(Path.GetTempPath());
            return dirs;
        }

        static string MakeDiagZipPath(string ts)
        {
            foreach (string dir in DiagOutputDirs())
            {
                string p = Path.Combine(dir, "dsh-诊断包-" + ts + ".zip");
                try { using (var fs = new FileStream(p, FileMode.Create)) { fs.WriteByte(0); } File.Delete(p); return p; }
                catch { }
            }
            return "";
        }

        // .NET ZipArchive 直写打包 (不依赖 PowerShell 子进程; 顺带验证输出目录可写)
        static string WriteDiagZip(string tmpDir, string zipPath)
        {
            try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { }
            using (var fs = new FileStream(zipPath, FileMode.CreateNew))
            using (var zip = new System.IO.Compression.ZipArchive(fs, System.IO.Compression.ZipArchiveMode.Create))
            {
                foreach (string f in Directory.GetFiles(tmpDir))
                {
                    var e = zip.CreateEntry(Path.GetFileName(f), System.IO.Compression.CompressionLevel.Optimal);
                    using (var es = e.Open())
                    using (var src = File.OpenRead(f))
                        src.CopyTo(es);
                }
            }
            return File.Exists(zipPath) ? zipPath : "";
        }

        // ---------- 现代化质感对话框 (深浅模式完全自适应，彻底淘汰原生 MessageBox) ----------
        public static void ShowModernInfo(Window owner, string title, string message)
        {
            ShowModernBox(owner, title, message, false);
        }

        public static void ShowModernWarn(Window owner, string title, string message)
        {
            ShowModernBox(owner, title, message, false, true);
        }

        public static bool ShowModernConfirm(Window owner, string title, string message)
        {
            return ShowModernBox(owner, title, message, true) == true;
        }

        static bool? ShowModernBox(Window owner, string title, string message, bool isConfirm, bool isWarn = false)
        {
            var w = new Window
            {
                Title = title,
                Width = 460,
                MinWidth = 400,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = owner,
                WindowStyle = WindowStyle.ToolWindow,
                ResizeMode = ResizeMode.NoResize,
                Background = Palette.Brush(Palette.Bg)
            };
            var g = new Grid { Margin = new Thickness(22) };
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var head = new TextBlock
            {
                Text = (isWarn ? "⚠️ " : (isConfirm ? "❓ " : "✨ ")) + title,
                Foreground = Palette.Brush(isWarn ? Palette.Warn : Palette.Text),
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(head, 0);

            var msg = new TextBlock
            {
                Text = message,
                Foreground = Palette.Brush(Palette.TextDim),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 20,
                Margin = new Thickness(0, 0, 0, 18)
            };
            Grid.SetRow(msg, 1);

            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            if (isConfirm)
            {
                var cancelBtn = new Button
                {
                    Content = "取消",
                    Height = 34,
                    Width = 84,
                    FontSize = 13,
                    Foreground = Palette.Brush(Palette.IsDark ? Palette.Text : Palette.TextDim),
                    Background = Palette.Brush(Palette.BgInput),
                    BorderThickness = new Thickness(1),
                    BorderBrush = Palette.Brush(Palette.BorderSoft),
                    Margin = new Thickness(0, 0, 10, 0),
                    Cursor = Cursors.Hand
                };
                cancelBtn.Click += delegate { w.DialogResult = false; };
                row.Children.Add(cancelBtn);

                var okBtn = new Button
                {
                    Content = "确定",
                    Height = 34,
                    Width = 84,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White,
                    Background = Palette.BlueGradient(),
                    BorderThickness = new Thickness(0),
                    Effect = Palette.GlowEffect(Palette.Blue, 0.35),
                    Cursor = Cursors.Hand
                };
                okBtn.Click += delegate { w.DialogResult = true; };
                row.Children.Add(okBtn);
            }
            else
            {
                var okBtn = new Button
                {
                    Content = "我知道了",
                    Height = 34,
                    Width = 100,
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = Brushes.White,
                    Background = Palette.BlueGradient(),
                    BorderThickness = new Thickness(0),
                    Effect = Palette.GlowEffect(Palette.Blue, 0.35),
                    Cursor = Cursors.Hand
                };
                okBtn.Click += delegate { w.DialogResult = true; };
                row.Children.Add(okBtn);
            }
            Grid.SetRow(row, 2);

            g.Children.Add(head);
            g.Children.Add(msg);
            g.Children.Add(row);
            w.Content = g;
            return w.ShowDialog();
        }

        // ---------- 自定义输入对话框 (深浅自适应 + 完整输入框与按钮, 彻底解决裁剪问题) ----------
        string Prompt(string title, string message, string def)
        {
            var w = new Window
            {
                Title = title,
                Width = 540,
                MinWidth = 500,
                SizeToContent = SizeToContent.Height,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                WindowStyle = WindowStyle.ToolWindow,
                ResizeMode = ResizeMode.NoResize,
                Background = Palette.Brush(Palette.Bg)
            };
            var g = new Grid { Margin = new Thickness(22) };
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var msg = new TextBlock
            {
                Text = message,
                Foreground = Palette.Brush(Palette.TextDim),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 14),
                LineHeight = 20
            };
            var box = new System.Windows.Controls.TextBox
            {
                Text = def,
                FontSize = 13,
                Height = 36,
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = Palette.Brush(Palette.BgInput),
                Foreground = Palette.Brush(Palette.Text),
                BorderThickness = new Thickness(1),
                BorderBrush = Palette.Brush(Palette.BorderSoft),
                Padding = new Thickness(10, 0, 10, 0),
                Margin = new Thickness(0, 0, 0, 18)
            };
            Grid.SetRow(msg, 0);
            Grid.SetRow(box, 1);

            var ok = Btn("确定安装", delegate { w.DialogResult = true; }, true);
            ok.Width = 100;
            var cancel = Btn("取消", delegate { w.DialogResult = false; }, false);
            cancel.Width = 90;

            var row = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            row.Children.Add(cancel);
            row.Children.Add(ok);
            Grid.SetRow(row, 2);

            g.Children.Add(msg);
            g.Children.Add(box);
            g.Children.Add(row);
            w.Content = g;
            w.Loaded += delegate { box.Focus(); box.SelectAll(); };
            return w.ShowDialog() == true ? box.Text : null;
        }

        // ---------- 现代极简悬浮托盘卡片 (WPF 自绘制, 告别 2008 年原生旧菜单) ----------
        class ModernTrayPopup : Window
        {
            public ModernTrayPopup(MainWindow main, Dsh dsh)
            {
                Width = 200;
                SizeToContent = SizeToContent.Height;
                WindowStyle = WindowStyle.None;
                AllowsTransparency = true;
                Background = Brushes.Transparent;
                ShowInTaskbar = false;
                Topmost = true;

                var card = new Border
                {
                    CornerRadius = new CornerRadius(12),
                    Background = Palette.CardGradient(),
                    BorderBrush = Palette.CardBorderBrush(),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(6, 6, 6, 6),
                    Effect = Palette.CardShadow()
                };

                var stack = new StackPanel();

                // 头部小标题
                var head = new Grid { Margin = new Thickness(10, 3, 10, 4) };
                var headTitle = new TextBlock { Text = "DeepSeek Harness", Foreground = Palette.Brush(Palette.Text), FontSize = 11, FontWeight = FontWeights.Bold };
                var headVer = new TextBlock { Text = "v" + Dsh.LauncherVersion, Foreground = Palette.Brush(Palette.IsDark ? Palette.Cyan : Palette.Blue), FontSize = 9, HorizontalAlignment = HorizontalAlignment.Right, FontWeight = FontWeights.SemiBold };
                head.Children.Add(headTitle);
                head.Children.Add(headVer);
                stack.Children.Add(head);
                stack.Children.Add(Separator());

                // 核心功能菜单项
                stack.Children.Add(Item("🏠 " + Lang.T("打开启动器"), delegate { main.ShowMain(); }));
                stack.Children.Add(Item("▶ " + Lang.T("一键启动服务"), delegate { dsh.StartServiceAsync(); main.PollServiceState(true); }));
                stack.Children.Add(Item("⏹ " + Lang.T("停止服务"), delegate { dsh.StopServiceAsync(); main.PollServiceState(false); }));
                stack.Children.Add(Item("🔄 " + Lang.T("重启服务"), delegate { dsh.RestartServiceAsync(); main.PollServiceState(true); }));
                stack.Children.Add(Item("🌐 " + Lang.T("打开浏览器"), delegate { dsh.OpenBrowser(); }));
                stack.Children.Add(Separator());

                stack.Children.Add(Item("🛍 " + Lang.T("插件商城"), delegate { main.OpenStore(); }));
                stack.Children.Add(Item("📂 " + Lang.T("打开插件目录"), delegate { main.OpenPluginsDir(); }));
                stack.Children.Add(Item("📄 " + Lang.T("查看日志"), delegate { main.SwitchPage(4); main.ShowMain(); }));
                stack.Children.Add(Separator());

                stack.Children.Add(Item(Palette.IsDark ? "☀️ " + Lang.T("浅色模式") : "🌙 " + Lang.T("深色模式"), delegate
                {
                    // 全局主题切换: 保存配置 → 重建整个主窗口 → 弹出展示新主题
                    Palette.IsDark = !Palette.IsDark;
                    dsh.Cfg.Theme = Palette.IsDark ? "dark" : "light";
                    try { dsh.Cfg.Save(); } catch { }
                    try { main.RebuildAllPages(); } catch { }
                    main.ShowMain();   // 弹出主窗口让用户立即看到新主题
                }));
                stack.Children.Add(Separator());

                stack.Children.Add(Item("❌ " + Lang.T("退出程序"), delegate
                {
                    main.quitting = true;
                    main.Close();
                    Application.Current.Shutdown();
                }, true));

                card.Child = stack;
                Content = card;

                Deactivated += delegate { Close(); };
            }

            Border Item(string label, Action onClick, bool danger = false)
            {
                var b = new Border
                {
                    CornerRadius = new CornerRadius(7),
                    Padding = new Thickness(10, 5, 10, 5),
                    Margin = new Thickness(2, 1, 2, 1),
                    Background = Brushes.Transparent,
                    Cursor = Cursors.Hand
                };
                var t = new TextBlock
                {
                    Text = label,
                    Foreground = Palette.Brush(danger ? Palette.Error : Palette.Text),
                    FontSize = 12,
                    VerticalAlignment = VerticalAlignment.Center
                };
                b.Child = t;

                b.MouseEnter += delegate
                {
                    b.Background = danger ? Palette.BrushA(Palette.Error, 35) : Palette.Brush(Palette.BgCardHover);
                    if (!danger) t.Foreground = Palette.Brush(Palette.IsDark ? Palette.Cyan : Palette.Blue);
                };
                b.MouseLeave += delegate
                {
                    b.Background = Brushes.Transparent;
                    t.Foreground = Palette.Brush(danger ? Palette.Error : Palette.Text);
                };
                b.MouseLeftButtonUp += delegate
                {
                    // 先执行动作再关闭: 避免 popup 关闭过程干扰主窗口操作 (主题切换/页面跳转等)
                    try { onClick(); }
                    catch { }
                    Close();
                };
                return b;
            }

            Border Separator()
            {
                return new Border
                {
                    Height = 1,
                    Background = Palette.Brush(Palette.BorderSoft),
                    Margin = new Thickness(8, 3, 8, 3)
                };
            }

            public void ShowAtCursor()
            {
                // 先显示完成布局, 再按真实尺寸定位 (避免 SizeToContent 未布局时 Height=0)
                Show();
                UpdateLayout();

                // Cursor.Position 是物理像素, WPF Left/Top 是 DIP — 需要 DPI 换算
                var pt = System.Windows.Forms.Cursor.Position;
                double scale = 1.0;
                var src = PresentationSource.FromVisual(this);
                if (src != null && src.CompositionTarget != null)
                    scale = src.CompositionTarget.TransformToDevice.M11;
                if (scale < 0.1) scale = 1.0;

                double wx = pt.X / scale;
                double wy = pt.Y / scale;
                double winW = ActualWidth;
                double winH = ActualHeight;
                if (winW <= 0) winW = 200;
                if (winH <= 0) winH = 300;

                double workH = SystemParameters.WorkArea.Height;
                double workW = SystemParameters.WorkArea.Width;

                // 优先弹出在鼠标上方 (托盘在屏幕底部, 菜单贴其上), 上方空间不足则翻转到下方
                double top = wy - winH - 6;
                if (top < 8) top = wy + 12;
                if (top + winH > workH - 4) top = workH - winH - 4;
                if (top < 8) top = 8;

                double left = wx - winW + 16;
                if (left < 8) left = 8;
                if (left + winW > workW - 8) left = workW - winW - 8;

                Left = left;
                Top = top;
                Activate();
            }
        }

        // ---------- 托盘 / 单实例 / 现代极简右键浮窗 ----------
        void InitTray()
        {
            try
            {
                tray = new System.Windows.Forms.NotifyIcon();
                tray.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);
                tray.Text = "DeepSeek Harness Launcher";
                tray.Visible = true;
                tray.DoubleClick += delegate { ShowMain(); };

                // 使用原生 MouseUp 事件拦截右键，唤起 WPF 现代化半透明圆角卡片
                tray.MouseUp += delegate(object s, System.Windows.Forms.MouseEventArgs e)
                {
                    if (e.Button == System.Windows.Forms.MouseButtons.Right)
                    {
                        Dispatcher.BeginInvoke(new Action(delegate
                        {
                            var popup = new ModernTrayPopup(this, dsh);
                            popup.ShowAtCursor();
                        }));
                    }
                };
            }
            catch { }
        }

        void ShowMain()
        {
            Show();
            Activate();
            if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
            // 强制重新合成: 修复最小化/托盘恢复后部分区域(侧边栏/标题栏)不重绘的问题
            // (Win11 22H2 等系统上 WPF 合成器缓存可能失效, 交互后才重绘)
            try
            {
                UpdateLayout();
                InvalidateVisual();
                var ui = Content as UIElement;
                if (ui != null)
                {
                    Dispatcher.BeginInvoke(new Action(delegate
                    {
                        try
                        {
                            // 微抖动 RenderTransform 强制合成器重绘整窗, 再还原
                            ui.RenderTransform = new TranslateTransform(0, 0.01);
                            ui.RenderTransform = null;
                            UpdateLayout();
                        }
                        catch { }
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
            catch { }
        }

        void StartReopenWatch()
        {
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            t.Tick += delegate
            {
                try
                {
                    if (File.Exists(Proc.ReopenFlagPath()))
                    {
                        File.Delete(Proc.ReopenFlagPath());
                        ShowMain();
                    }
                }
                catch { }
            };
            t.Start();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            if (!quitting)
            {
                e.Cancel = true;   // 关闭按钮 → 最小化到托盘
                Hide();
            }
        }

        // ---------- 切页: 淡入过渡 (WPF 合成器播放, GPU 平滑) ----------
        void SwitchPage(int idx)
        {
            for (int i = 0; i < navs.Count; i++) navs[i].Active = (i == idx);
            curPage = idx;
            if (host.Children.Count > 0 && host.Children[0] == pages[idx]) return;
            var page = pages[idx];
            host.Children.Clear();
            host.Children.Add(page);
            // 脏标记缓存: 仅首次进入或数据变化时重建, 其余切页直接复用已渲染视觉树 → 瞬时呈现
            if (!pageReady[idx] || pageDirty[idx])
            {
                if (idx == 0) RenderOverview();
                else if (idx == 1) RenderEnv();
                else if (idx == 2) RenderPlugins();
                else if (idx == 3) RenderUpdate();
                else if (idx == 4) RefreshLog();
                pageReady[idx] = true;
                pageDirty[idx] = false;
            }
            if (IsLoaded)
            {
                // 过渡动画: 阻尼淡入 + 物理平滑上滑 (WPF 合成器 GPU 播放, 丝滑 60fps)
                page.Opacity = 0.0;
                var tr = new TranslateTransform(0, 12);
                page.RenderTransform = tr;
                var ease = new QuarticEase { EasingMode = EasingMode.EaseOut };
                var fade = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(190)) { EasingFunction = ease };
                page.BeginAnimation(UIElement.OpacityProperty, fade);
                var slide = new DoubleAnimation(12, 0, TimeSpan.FromMilliseconds(190)) { EasingFunction = ease };
                tr.BeginAnimation(TranslateTransform.YProperty, slide);
            }
            else
            {
                page.Opacity = 1.0;
            }
        }

        // 标记页面数据已变化 (下次切到该页时重建)
        void MarkDirty(int idx)
        {
            if (idx >= 0 && idx < 6) pageDirty[idx] = true;
        }

        // 后台线程计算插件本地哈希 (避免 UI 线程跑 git 子进程卡顿)
        void RefreshPluginHashes()
        {
            var t = new Thread(delegate()
            {
                var plugins = dsh.ScanPlugins();
                var h = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var p in plugins)
                    if (p.IsGit)
                    {
                        string hash = dsh.LocalHash(p);
                        if (hash.Length > 0) h[p.Name] = hash;
                    }
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    pluginHashes = h;
                    if (curPage == 2) { pageDirty[2] = true; RenderPlugins(); }
                }));
            });
            t.IsBackground = true;
            t.Start();
        }
    }
}
