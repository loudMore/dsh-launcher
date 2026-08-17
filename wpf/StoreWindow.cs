// ============================================================
//  DeepSeek Harness 启动器 - WPF 重构版 · 插件商城窗口
//  分页拉取 GitHub topic:dsh-plugin (最多 500), 搜索/排序/语言筛选,
//  已安装自动标记, 懒渲染 + 加载更多, 暗色自绘下拉
// ============================================================
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shell;

namespace DeepSeekHarness
{
    // ---------- 自绘暗色/浅色自适应现代下拉 (Popup + ListBox) ----------
    class ModernDropdown : ContentControl
    {
        string[] items = new string[0];
        Popup popup;
        ListBox listBox;
        TextBlock faceText;
        Border face;
        bool hover;
        bool open;

        public int SelectedIndex = -1;

        public string SelectedItem
        {
            get { return (SelectedIndex >= 0 && SelectedIndex < items.Length) ? items[SelectedIndex] : ""; }
        }

        public event EventHandler SelectionChanged;

        public ModernDropdown()
        {
            face = new Border
            {
                CornerRadius = new CornerRadius(8),
                Background = Palette.Brush(Palette.BgInput),
                BorderBrush = Palette.Brush(Palette.BorderSoft),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 0, 10, 0),
                Cursor = Cursors.Hand,
                MinHeight = 34
            };
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
            faceText = new TextBlock
            {
                Foreground = Palette.Brush(Palette.Text),
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            var chev = new TextBlock
            {
                Text = "▾",
                Foreground = Palette.Brush(Palette.TextDim),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetColumn(chev, 1);
            g.Children.Add(faceText);
            g.Children.Add(chev);
            face.Child = g;
            Content = face;

            face.MouseEnter += delegate { hover = true; Paint(); };
            face.MouseLeave += delegate { hover = false; Paint(); };
            face.MouseLeftButtonUp += delegate { Toggle(); };

            popup = new Popup
            {
                PlacementTarget = this,
                Placement = PlacementMode.Bottom,
                StaysOpen = false,
                AllowsTransparency = true,
                PopupAnimation = PopupAnimation.Fade,
                Margin = new Thickness(0, 4, 0, 0)
            };
            listBox = new ListBox
            {
                Background = Palette.Brush(Palette.BgCard),
                Foreground = Palette.Brush(Palette.Text),
                BorderThickness = new Thickness(0),
                FontSize = 13,
                MaxHeight = 300,
                Padding = new Thickness(4)
            };
            var itemStyle = new Style(typeof(ListBoxItem));
            itemStyle.Setters.Add(new Setter(Control.BackgroundProperty, Brushes.Transparent));
            itemStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(12, 8, 12, 8)));
            itemStyle.Setters.Add(new Setter(Control.CursorProperty, Cursors.Hand));
            itemStyle.Setters.Add(new Setter(Control.ForegroundProperty, Palette.Brush(Palette.Text)));

            var hoverTrigger = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(Control.BackgroundProperty, Palette.Brush(Palette.BgCardHover)));
            hoverTrigger.Setters.Add(new Setter(Control.ForegroundProperty, Palette.Brush(Palette.IsDark ? Palette.Cyan : Palette.Blue)));
            itemStyle.Triggers.Add(hoverTrigger);

            var selTrigger = new Trigger { Property = Selector.IsSelectedProperty, Value = true };
            selTrigger.Setters.Add(new Setter(Control.BackgroundProperty, Palette.BrushA(Palette.Blue, (byte)(Palette.IsDark ? 50 : 35))));
            selTrigger.Setters.Add(new Setter(Control.ForegroundProperty, Palette.Brush(Palette.IsDark ? Palette.Cyan : Palette.Blue)));
            selTrigger.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.SemiBold));
            itemStyle.Triggers.Add(selTrigger);

            listBox.ItemContainerStyle = itemStyle;
            listBox.SelectionChanged += delegate
            {
                int idx = listBox.SelectedIndex;
                if (idx >= 0 && idx < items.Length)
                {
                    SelectedIndex = idx;
                    open = false;
                    popup.IsOpen = false;
                    Paint();
                    InvalidateVisual();
                    if (SelectionChanged != null) SelectionChanged(this, EventArgs.Empty);
                }
            };
            var shell = new Border
            {
                Background = Palette.Brush(Palette.BgCard),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(4),
                BorderBrush = Palette.Brush(Palette.BorderSoft),
                BorderThickness = new Thickness(1),
                Effect = Palette.CardShadow(),
                Child = listBox,
                MinWidth = 160
            };
            popup.Child = shell;
        }

        void Toggle()
        {
            open = !open;
            if (open)
            {
                listBox.ItemsSource = items;
                listBox.SelectedIndex = SelectedIndex;
                popup.IsOpen = true;
            }
            else popup.IsOpen = false;
            Paint();
            InvalidateVisual();
        }

        void Paint()
        {
            if (face == null) return;
            face.Background = Palette.Brush(hover ? Palette.BgCardHover : Palette.BgInput);
            face.BorderBrush = Palette.Brush(open ? Palette.Blue : (hover ? Palette.Border : Palette.BorderSoft));
            if (faceText != null) faceText.Foreground = Palette.Brush(Palette.Text);
        }

        public void SetItems(string[] arr, int sel)
        {
            items = (arr == null) ? new string[0] : (string[])arr.Clone();
            SelectedIndex = (items.Length > 0) ? Math.Min(Math.Max(0, sel), items.Length - 1) : -1;
            Paint();
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            if (faceText != null)
            {
                faceText.Text = SelectedItem;
                faceText.Foreground = Palette.Brush(Palette.Text);
            }
        }
    }

    // ---------- 插件商城窗口 ----------
    class StoreWindow : Window
    {
        Dsh dsh;
        List<StoreItem> items = new List<StoreItem>();
        System.Windows.Controls.TextBox search;
        ModernDropdown sortDd, langDd;
        Button fetchBtn;
        TextBlock note;
        ScrollViewer sv;
        StackPanel listHost;
        int shownLimit = 80;
        bool loading;

        public StoreWindow(Dsh owner)
        {
            dsh = owner;
            Title = "插件商城";
            Width = 940;
            Height = 700;
            MinWidth = 620;
            MinHeight = 460;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResize;
            Background = Palette.Brush(Palette.Bg);
            var chrome = new WindowChrome();
            chrome.CaptionHeight = 44;
            chrome.ResizeBorderThickness = new Thickness(6);
            chrome.GlassFrameThickness = new Thickness(0, 0, 0, 1);
            chrome.CornerRadius = new CornerRadius(0);
            chrome.UseAeroCaptionButtons = false;
            WindowChrome.SetWindowChrome(this, chrome);
            BuildUi();
            Loaded += delegate { Seed(); Refresh(); };
        }

        void BuildUi()
        {
            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(44) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // 标题栏
            var bar = new Grid { Background = Palette.Brush(Palette.BgDeep) };
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            var title = new TextBlock { Text = "🛍  " + Lang.T("插件商城"), Foreground = Palette.Brush(Palette.Text), FontSize = 15, FontWeight = FontWeights.SemiBold, Margin = new Thickness(14, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            var btns = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            btns.Children.Add(TitleBtn("─", delegate { WindowState = WindowState.Minimized; }));
            btns.Children.Add(TitleBtn("▢", delegate { WindowState = (WindowState == WindowState.Maximized) ? WindowState.Normal : WindowState.Maximized; }));
            btns.Children.Add(TitleBtn("✕", delegate { Close(); }, true));
            Grid.SetColumn(btns, 1);
            bar.Children.Add(title);
            bar.Children.Add(btns);

            // 工具栏
            var toolbar = new WrapPanel { Margin = new Thickness(14, 10, 14, 0) };
            var searchBox = new Grid { Width = 220, Height = 34, Margin = new Thickness(0, 0, 8, 0) };
            search = new System.Windows.Controls.TextBox
            {
                FontSize = 13,
                Background = Palette.Brush(Palette.BgInput), Foreground = Palette.Brush(Palette.Text),
                BorderThickness = new Thickness(0), Padding = new Thickness(10, 0, 10, 0),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            var searchHint = new TextBlock
            {
                Text = Lang.T("搜索插件…"),
                Foreground = Palette.Brush(Palette.TextFaint),
                FontSize = 13,
                Margin = new Thickness(10, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            searchBox.Children.Add(search);
            searchBox.Children.Add(searchHint);
            Action updateHint = delegate { searchHint.Visibility = (search.Text.Length == 0 && !search.IsKeyboardFocused) ? Visibility.Visible : Visibility.Collapsed; };
            search.TextChanged += delegate { updateHint(); shownLimit = 80; Render(); };
            search.GotKeyboardFocus += delegate { updateHint(); };
            search.LostKeyboardFocus += delegate { updateHint(); };
            updateHint();
            toolbar.Children.Add(searchBox);
            sortDd = new ModernDropdown { Width = 150, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            sortDd.SetItems(new string[] { "★ " + Lang.T("按星标排序"), Lang.T("按名称排序"), Lang.T("默认顺序") }, 0);
            sortDd.SelectionChanged += delegate { shownLimit = 80; Render(); };
            langDd = new ModernDropdown { Width = 120, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            langDd.SetItems(new string[] { Lang.T("全部语言") }, 0);
            langDd.SelectionChanged += delegate { shownLimit = 80; Render(); };
            fetchBtn = Btn("↻ " + Lang.T("获取列表"), delegate { Refresh(); });
            fetchBtn.Margin = new Thickness(0, 0, 8, 0);
            var webBtn = Btn(Lang.T("打开网页"), delegate { try { Process.Start("https://github.com/topics/dsh-plugin"); } catch { } });
            webBtn.Margin = new Thickness(0, 0, 8, 0);
            note = new TextBlock { Text = "", Foreground = Palette.Brush(Palette.TextFaint), FontSize = 12, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
            toolbar.Children.Add(sortDd);
            toolbar.Children.Add(langDd);
            toolbar.Children.Add(fetchBtn);
            toolbar.Children.Add(webBtn);
            toolbar.Children.Add(note);

            // 列表
            listHost = new StackPanel { Margin = new Thickness(14, 10, 14, 14) };
            sv = new ScrollViewer { Content = listHost, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = Palette.Brush(Palette.Bg) };

            Grid.SetRow(bar, 0);
            Grid.SetRow(toolbar, 1);
            Grid.SetRow(sv, 2);
            root.Children.Add(bar);
            root.Children.Add(toolbar);
            root.Children.Add(sv);
            Content = root;
        }

        Button TitleBtn(string glyph, Action onClick, bool danger = false)
        {
            var b = new Button
            {
                Content = glyph, Width = 48, FontSize = 13,
                Foreground = Palette.Brush(Palette.TextDim), Background = Brushes.Transparent,
                BorderThickness = new Thickness(0), Cursor = Cursors.Hand
            };
            WindowChrome.SetIsHitTestVisibleInChrome(b, true);
            b.MouseEnter += delegate { b.Background = danger ? Palette.BrushA(Palette.Error, 70) : Palette.BrushA(Palette.Text, 24); };
            b.MouseLeave += delegate { b.Background = Brushes.Transparent; };
            b.Click += delegate { onClick(); };
            return b;
        }

        Button Btn(string text, Action onClick)
        {
            var b = new Button
            {
                Content = text, Height = 34, FontSize = 13, Foreground = Brushes.White,
                Background = Palette.Brush(Palette.BgInput), BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand, Padding = new Thickness(14, 0, 14, 0), VerticalAlignment = VerticalAlignment.Center
            };
            b.MouseEnter += delegate { b.Background = Palette.Brush(Palette.BlueLight); };
            b.MouseLeave += delegate { b.Background = Palette.Brush(Palette.BgInput); };
            b.Click += delegate { onClick(); };
            return b;
        }

        void Seed()
        {
            long age;
            var cached = StoreCache.LoadList(out age);
            if (cached != null && cached.Count > 0)
            {
                items = cached;
                shownLimit = 80;
                note.Text = string.Format("共 {0} 个插件 · 数据来自 GitHub · 缓存", cached.Count);
                BuildLangFilter();
                Render();
            }
        }

        void Refresh()
        {
            if (loading) return;
            loading = true;
            fetchBtn.IsEnabled = false;
            note.Text = Lang.T("正在获取插件列表…");
            note.Foreground = Palette.Brush(Palette.TextDim);
            var t = new Thread(delegate()
            {
                string proxy = null;
                try { proxy = dsh.ResolveProxy(); } catch { }
                var got = Dsh.FetchStore();
                if (got.Count > 0) StoreCache.SaveList(got);
                Dispatcher.BeginInvoke(new Action(delegate
                {
                    loading = false;
                    fetchBtn.IsEnabled = true;
                    if (got.Count > 0) { items = got; shownLimit = 80; }
                    note.Text = string.Format(Lang.T("共 {0} 个插件 · 数据来自 GitHub"), items.Count) + (got.Count == 0 && items.Count > 0 ? Lang.T(" · 缓存") : "");
                    note.Foreground = Palette.Brush(Palette.TextFaint);
                    BuildLangFilter();
                    Render();
                }));
            });
            t.IsBackground = true;
            t.Start();
        }

        void BuildLangFilter()
        {
            if (langDd == null) return;
            var set = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var it in items) if (!string.IsNullOrEmpty(it.Lang)) set.Add(it.Lang);
            string prev = langDd.SelectedItem;
            var arr = new List<string>();
            arr.Add("全部语言");
            foreach (string l in set) arr.Add(l);
            int sel = 0;
            for (int i = 0; i < arr.Count; i++)
                if (string.Equals(arr[i], prev, StringComparison.OrdinalIgnoreCase)) { sel = i; break; }
            langDd.SetItems(arr.ToArray(), sel);
        }

        List<StoreItem> Sorted()
        {
            var l = new List<StoreItem>(items);
            int mode = sortDd == null ? 0 : sortDd.SelectedIndex;
            if (mode == 0) l.Sort(delegate(StoreItem a, StoreItem b) { return b.Stars.CompareTo(a.Stars); });
            else if (mode == 1) l.Sort(delegate(StoreItem a, StoreItem b) { return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase); });
            return l;
        }

        void Render()
        {
            if (listHost == null) return;
            listHost.Children.Clear();
            if (items.Count == 0)
            {
                listHost.Children.Add(new TextBlock { Text = Lang.T("正在刷新…"), Foreground = Palette.Brush(Palette.TextDim), FontSize = 14, Margin = new Thickness(4, 10, 0, 0) });
                return;
            }
            string raw = (search == null ? "" : search.Text.Trim().ToLowerInvariant());
            string[] terms = raw.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string langSel = (langDd == null || langDd.SelectedIndex <= 0) ? "" : langDd.SelectedItem;
            var matches = new List<StoreItem>();
            foreach (var it in Sorted())
            {
                if (!string.IsNullOrEmpty(langSel) && !string.Equals(it.Lang, langSel, StringComparison.OrdinalIgnoreCase)) continue;
                bool ok = true;
                for (int t = 0; t < terms.Length; t++)
                {
                    string term = terms[t];
                    if (it.Name.ToLowerInvariant().IndexOf(term) < 0
                        && it.Desc.ToLowerInvariant().IndexOf(term) < 0
                        && it.FullName.ToLowerInvariant().IndexOf(term) < 0) { ok = false; break; }
                }
                if (ok) matches.Add(it);
            }
            if (matches.Count == 0)
            {
                listHost.Children.Add(new TextBlock { Text = Lang.T("没有匹配的插件，换个关键词试试"), Foreground = Palette.Brush(Palette.TextDim), FontSize = 14, Margin = new Thickness(4, 10, 0, 0) });
                return;
            }
            int shown = 0;
            for (int i = 0; i < matches.Count && shown < shownLimit; i++)
            {
                AddRow(matches[i]);
                shown++;
            }
            if (matches.Count > shown)
            {
                var more = Btn("↓ 加载更多 (" + (matches.Count - shown) + ")", delegate { shownLimit += 100; Render(); });
                more.Margin = new Thickness(0, 6, 0, 0);
                more.HorizontalAlignment = HorizontalAlignment.Left;
                listHost.Children.Add(more);
            }
        }

        void AddRow(StoreItem it)
        {
            bool installed = dsh.IsPluginInstalled(it.Name);
            var row = new Border
            {
                Background = Palette.CardGradient(),
                BorderBrush = Palette.CardBorderBrush(),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 0, 0, 10),
                Effect = Palette.CardShadow()
            };
            row.MouseEnter += delegate
            {
                row.Background = Palette.Brush(Palette.BgCardHover);
                row.BorderBrush = Palette.BrushA(Palette.BlueLight, 80);
            };
            row.MouseLeave += delegate
            {
                row.Background = Palette.CardGradient();
                row.BorderBrush = Palette.CardBorderBrush();
            };
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var left = new StackPanel();
            var meta = new System.Text.StringBuilder();
            if (it.Stars >= 0) meta.Append("★ ").Append(it.Stars);
            if (!string.IsNullOrEmpty(it.Lang)) { if (meta.Length > 0) meta.Append(" · "); meta.Append(it.Lang); }
            if (!string.IsNullOrEmpty(it.Pushed)) { if (meta.Length > 0) meta.Append(" · "); meta.Append(it.Pushed); }
            left.Children.Add(new TextBlock { Text = it.Name, Foreground = Palette.Brush(Palette.Text), FontSize = 15, FontWeight = FontWeights.SemiBold });
            left.Children.Add(new TextBlock { Text = meta.Length > 0 ? meta.ToString() : "GitHub", Foreground = Palette.Brush(Palette.Warn), FontSize = 12, Margin = new Thickness(0, 3, 0, 0) });
            left.Children.Add(new TextBlock
            {
                Text = string.IsNullOrEmpty(it.Desc) ? it.FullName : it.Desc,
                Foreground = Palette.Brush(Palette.TextDim), FontSize = 12, Margin = new Thickness(0, 5, 0, 0),
                TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 640
            });
            g.Children.Add(left);
            var right = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var browse = Btn("↗ 浏览", delegate { try { Process.Start(it.Url); } catch { } });
            browse.Margin = new Thickness(0, 0, 8, 0);
            var install = Btn(installed ? "✓ 已安装" : "↓ 安装", delegate
            {
                if (installed) return;
                var t = new Thread(delegate()
                {
                    string err = dsh.InstallPluginFromUrl(it.Url);
                    Dispatcher.BeginInvoke(new Action(delegate { Render(); if (err.Length > 0) MainWindow.ShowModernWarn(this, "安装失败", err); }));
                });
                t.IsBackground = true;
                t.Start();
            });
            install.Background = installed ? (Brush)Palette.Brush(Palette.BgInput) : (Brush)Palette.BlueGradient();
            if (!installed) install.Effect = Palette.GlowEffect(Palette.Blue, 0.35);
            install.MouseEnter += delegate
            {
                if (!installed)
                {
                    install.Background = Palette.Brush(Palette.BlueLight);
                    install.Effect = Palette.GlowEffect(Palette.Blue, 0.6);
                }
            };
            install.MouseLeave += delegate
            {
                install.Background = installed ? (Brush)Palette.Brush(Palette.BgInput) : (Brush)Palette.BlueGradient();
                if (!installed) install.Effect = Palette.GlowEffect(Palette.Blue, 0.35);
            };
            right.Children.Add(browse);
            right.Children.Add(install);
            Grid.SetColumn(right, 1);
            g.Children.Add(right);
            row.Child = g;
            listHost.Children.Add(row);
        }
    }
}

