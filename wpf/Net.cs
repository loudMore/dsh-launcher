// ============================================================
//  DeepSeek Harness 启动器 - 统一网络层
//  全程序唯一 HTTP 出口: TLS 1.2+ / 重定向 / 超时 / 进度下载
//  代理策略: 浏览器同源四级探测, TTL 缓存 + 用前复验, 断代理自动回直连
//  下载策略: 官方源优先, 失败逐级回退可靠镜像 (全部链在此集中定义)
//  编译: build.bat (与 WpfApp/Logic/StoreWindow 同一 csc 命令行)
// ============================================================
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;

namespace DeepSeekHarness
{
    static class Net
    {
        // ---------- 0. TLS 现代化: .NET4 默认 TLS1.0, GitHub/npm 全要求 1.2+ ----------
        // 必须在任何请求前调用 (App.Main 第一行); 静态构造兜底, 幂等。
        static bool tlsDone;

        public static void Init()
        {
            if (tlsDone) return;
            tlsDone = true;
            try
            {
                // 3072=Tls12 (所有 .NET4 都有); 12288=Tls13 (仅 4.8 有, 反射探测防抛异常)
                var proto = SecurityProtocolType.Tls | SecurityProtocolType.Tls12;
                try
                {
                    object tls13 = Enum.Parse(typeof(SecurityProtocolType), "12288", false);
                    proto |= (SecurityProtocolType)tls13;
                }
                catch { }
                ServicePointManager.SecurityProtocol = proto;
            }
            catch { try { ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12; } catch { } }
            try { ServicePointManager.DefaultConnectionLimit = 16; } catch { }
            try { ServicePointManager.Expect100Continue = false; } catch { }
        }

        static Net() { Init(); }

        const string UserAgent = "dsh-launcher/" + Dsh.LauncherVersion + " (Windows)";
        const int ConnectTimeoutMs = 15000;
        const int ReadTimeoutMs = 30000;

        // ============================================================
        //  1. 代理 (浏览器同源策略): 显式配置 -> 注册表 -> 环境变量 -> 兜底扫描
        //  探测结果只做 TTL 缓存, 绝不写进程环境变量 (防污染所有子进程);
        //  每次取用时轻量复验, 代理断开立即回直连, 对用户无感。
        // ============================================================
        public class ProxyState
        {
            public string Url;          // "http://host:port" / null=直连
            public string Source;       // config / system / env / scan / none
        }

        // 兜底扫描的常见本地代理端口 (最后手段, 带总时限)
        public static readonly string[] FallbackProxyPorts = {
            "7890", "7897", "10809", "1080", "8118", "2080"
        };

        static readonly object proxyLock = new object();
        static ProxyState cached;               // null = 未探测
        static long cacheAt;                    // Environment.TickCount
        const long CacheTtlMs = 60000;

        // ForceProxy != null: 忽略探测结果, 强制用指定代理 (net-probe 诊断用)
        public static string ForceProxy = null;

        /// <summary>当前应使用的代理 (null = 直连)。带 TTL 缓存与活性复验。</summary>
        public static ProxyState CurrentProxy()
        {
            lock (proxyLock)
            {
                if (ForceProxy != null) return new ProxyState { Url = ForceProxy, Source = "forced" };
                long now = Environment.TickCount;
                if (cached != null && Math.Abs(now - cacheAt) < CacheTtlMs)
                {
                    // TTL 内: 代理需复验活性 (死代理立即失效); 直连结果无需复验
                    if (cached.Url == null) return cached;
                    if (ProxyAlive(cached.Url)) return cached;
                    cached = null;   // 死了 -> 重探测
                }
                cached = DetectProxy();
                cacheAt = now;
                return cached;
            }
        }

        /// <summary>代理 URL 字符串 (无则 null), 给 git -c http.proxy / npm_config_proxy 用</summary>
        public static string ProxyUrl() { return CurrentProxy().Url; }

        static ProxyState DetectProxy()
        {
            // ① 用户显式配置 (最高优先, 只信活的)
            string cfgProxy = "";
            try
            {
                if (LauncherConfig.Loaded != null) cfgProxy = LauncherConfig.Loaded.Proxy;
            }
            catch { }
            if (!string.IsNullOrEmpty(cfgProxy) && ProxyAlive(cfgProxy))
                return new ProxyState { Url = NormalizeProxy(cfgProxy), Source = "config" };

            // ② 系统注册表代理 (与 Chrome/Edge 同源的事实源, ~0ms)
            string sys = ReadSystemProxy();
            if (!string.IsNullOrEmpty(sys) && ProxyAlive(sys))
                return new ProxyState { Url = sys, Source = "system" };

            // ③ 环境变量 (只在活着时采纳, 防残留死代理)
            string env = Environment.GetEnvironmentVariable("HTTPS_PROXY");
            if (string.IsNullOrEmpty(env)) env = Environment.GetEnvironmentVariable("HTTP_PROXY");
            if (string.IsNullOrEmpty(env)) env = Environment.GetEnvironmentVariable("ALL_PROXY");
            if (!string.IsNullOrEmpty(env) && ProxyAlive(env))
                return new ProxyState { Url = NormalizeProxy(env), Source = "env" };

            // ④ 本地常见代理端口扫描兜底 (先 TCP 过滤再真验, 总预算 2.5s)
            long budget = Environment.TickCount + 2500;
            foreach (string port in FallbackProxyPorts)
            {
                if (Environment.TickCount > budget) break;
                int pn;
                if (!int.TryParse(port, out pn)) continue;
                if (!LocalPortListening(pn)) continue;
                string cand = "http://127.0.0.1:" + port;
                if (ProxyAlive(cand))
                    return new ProxyState { Url = cand, Source = "scan" };
            }

            return new ProxyState { Url = null, Source = "none" };
        }

        /// <summary>探测代理活性: TCP 连本地端口即可 (127.0.0.1 上能连上即代理进程活着)。
        /// 远端代理只做格式校验 (无法本地核验, 交给上层 HTTP 失败重试兜底)。</summary>
        public static bool ProxyAlive(string p)
        {
            try
            {
                string url = NormalizeProxy(p);
                var u = new Uri(url);
                string host = u.Host;
                if (host == "127.0.0.1" || host == "localhost" || host == "::1")
                    return LocalPortListening(u.Port);
                return true;    // 远程代理: 交 HTTP 层兜底
            }
            catch { return false; }
        }

        public static string NormalizeProxy(string p)
        {
            if (string.IsNullOrEmpty(p)) return p;
            string t = p.Trim();
            if (t.IndexOf("://") < 0) t = "http://" + t;
            return t;
        }

        /// <summary>本地回环端口是否有进程监听 (~120ms)</summary>
        public static bool LocalPortListening(int port)
        {
            try
            {
                using (var c = new TcpClient())
                {
                    var ar = c.BeginConnect("127.0.0.1", port, null, null);
                    bool ok = ar.AsyncWaitHandle.WaitOne(120);
                    if (ok) { try { c.EndConnect(ar); } catch { } return true; }
                    return false;
                }
            }
            catch { return false; }
        }

        // 注册表系统代理 (IE/Chrome/Edge 同源): ProxyEnable=1 时读 ProxyServer
        static string ReadSystemProxy()
        {
            try
            {
                using (var k = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings"))
                {
                    if (k != null && Convert.ToInt32(k.GetValue("ProxyEnable", 0)) == 1)
                    {
                        string ps = k.GetValue("ProxyServer") as string;
                        if (!string.IsNullOrEmpty(ps))
                        {
                            Match m = Regex.Match(ps, "(https?|socks)=([^;]+)");
                            string host = m.Success ? m.Groups[2].Value.Trim() : ps.Trim();
                            string scheme = m.Success && m.Groups[1].Value == "socks" ? "socks://" : "http://";
                            if (host.IndexOf("://") < 0) host = scheme + host;
                            // WebProxy 不支持 socks -> socks 时当没有系统代理 (极少数用户, 交兜底)
                            if (host.StartsWith("socks://", StringComparison.OrdinalIgnoreCase)) return null;
                            return host;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        // ============================================================
        //  2. 统一 HTTP: 全部走 HttpWebRequest 一条栈
        //  每 URL 双通道: 有活代理先代理, 失败自动直连重试 (代理开/关对用户无感)
        // ============================================================

        class HttpResult
        {
            public bool Ok;
            public byte[] Body;
            public int Status;
            public Exception Error;     // 代理/直连各自的异常
            public bool ViaProxy;
        }

        static HttpResult GetOnce(string url, string proxy, int timeoutMs, long deadline)
        {
            var r = new HttpResult { ViaProxy = proxy != null };
            try
            {
                var req = (HttpWebRequest)WebRequest.Create(url);
                req.UserAgent = UserAgent;
                req.Accept = "*/*";
                req.Timeout = Math.Max(1000, Math.Min(timeoutMs, MsLeft(deadline)));
                req.ReadWriteTimeout = ReadTimeoutMs;
                req.AllowAutoRedirect = true;
                req.MaximumAutomaticRedirections = 5;
                req.Proxy = string.IsNullOrEmpty(proxy) ? null : new WebProxy(proxy);
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var s = resp.GetResponseStream())
                using (var ms = new MemoryStream())
                {
                    byte[] buf = new byte[16384];
                    int n;
                    while ((n = s.Read(buf, 0, buf.Length)) > 0) ms.Write(buf, 0, n);
                    r.Body = ms.ToArray();
                    r.Status = (int)resp.StatusCode;
                    r.Ok = r.Status >= 200 && r.Status < 300;
                    if (!r.Ok) r.Error = new WebException("HTTP " + r.Status);
                }
            }
            catch (Exception ex) { r.Error = ex; }
            return r;
        }

        static int MsLeft(long deadline)
        {
            long left = deadline - Environment.TickCount;
            return left <= 0 ? 1 : (left > int.MaxValue ? int.MaxValue : (int)left);
        }

        /// <summary>双通道 GET: 代理(如可用) -> 直连。返回最好的一次结果。</summary>
        static HttpResult GetDual(string url, int timeoutMs)
        {
            Init();
            long deadline = Environment.TickCount + timeoutMs * 2 + 2000;
            string proxy = null;
            try { proxy = CurrentProxy().Url; } catch { }
            HttpResult best = null;
            if (!string.IsNullOrEmpty(proxy))
            {
                HttpResult via = GetOnce(url, proxy, timeoutMs, deadline);
                if (via.Ok) return via;
                best = via;
            }
            HttpResult direct = GetOnce(url, null, timeoutMs, deadline);
            if (direct.Ok) return direct;
            // 都失败: 优先报代理错误 (信息更多), 但 404 类响应直连结果更可信
            if (best != null && best.Status >= 400 && direct.Status == 0) return best;
            return direct.Status != 0 ? direct : (best ?? direct);
        }

        /// <summary>取文本 (自动 UTF-8/GBK 解码)。失败返回 null。</summary>
        public static string FetchText(string url, int timeoutMs)
        {
            HttpResult r = GetDual(url, timeoutMs);
            return r.Ok ? Decode(r.Body) : null;
        }

        /// <summary>下载文件 (双通道 + 进度回调)。失败返回 false 并 out 错误说明。</summary>
        public static bool DownloadFile(string url, string destPath, int timeoutMs, Action<int> progress)
        {
            Init();
            long deadline = Environment.TickCount + timeoutMs * 2 + 2000;
            string proxy = null;
            try { proxy = CurrentProxy().Url; } catch { }
            // 双通道: 代理失败 -> 直连重试
            string[] channels = string.IsNullOrEmpty(proxy) ? new string[] { null } : new string[] { proxy, null };
            Exception lastErr = null;
            foreach (string ch in channels)
            {
                try
                {
                    if (progress != null) { try { progress(0); } catch { } }
                    var req = (HttpWebRequest)WebRequest.Create(url);
                    req.UserAgent = UserAgent;
                    req.Accept = "*/*";
                    req.Timeout = ConnectTimeoutMs;
                    req.ReadWriteTimeout = ReadTimeoutMs;
                    req.AllowAutoRedirect = true;
                    req.Proxy = string.IsNullOrEmpty(ch) ? null : new WebProxy(ch);
                    using (var resp = (HttpWebResponse)req.GetResponse())
                    using (var s = resp.GetResponseStream())
                    {
                        int status = (int)resp.StatusCode;
                        if (status < 200 || status >= 300) throw new WebException("HTTP " + status);
                        long total = resp.ContentLength;
                        string tmp = destPath + ".part";
                        using (var fs = new FileStream(tmp, FileMode.Create))
                        {
                            byte[] buf = new byte[32768];
                            int n; long got = 0; int lastPct = -1;
                            while ((n = s.Read(buf, 0, buf.Length)) > 0)
                            {
                                fs.Write(buf, 0, n);
                                got += n;
                                if (progress != null && total > 0)
                                {
                                    int pct = (int)(got * 100 / total);
                                    if (pct != lastPct) { lastPct = pct; try { progress(pct); } catch { } }
                                }
                            }
                        }
                        if (File.Exists(destPath)) try { File.Delete(destPath); } catch { }
                        File.Move(tmp, destPath);
                        if (progress != null) { try { progress(100); } catch { } }
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    lastErr = ex;
                    try { if (File.Exists(destPath + ".part")) File.Delete(destPath + ".part"); } catch { }
                    try { if (File.Exists(destPath)) File.Delete(destPath); } catch { }
                }
            }
            Log("download fail " + url + " : " + (lastErr == null ? "?" : lastErr.Message));
            return false;
        }

        public static string Sha256File(string path)
        {
            try
            {
                using (var sha = SHA256.Create())
                using (var fs = File.OpenRead(path))
                {
                    byte[] h = sha.ComputeHash(fs);
                    var sb = new StringBuilder(h.Length * 2);
                    for (int i = 0; i < h.Length; i++) sb.Append(h[i].ToString("X2"));
                    return sb.ToString();
                }
            }
            catch { return ""; }
        }

        // ============================================================
        //  3. 镜像链 (集中定义, 全部官方优先, 失败逐级回退)
        // ============================================================
        public const string NpmRegistryMirror = "https://registry.npmmirror.com";
        public const string NpmRegistryHuawei = "https://mirrors.huaweicloud.com/repository/npm/";

        public static readonly string[] NodeIndexChains = {
            "https://nodejs.org/dist/index.json",
            "https://npmmirror.com/mirrors/node/index.json",
            "https://mirrors.huaweicloud.com/nodejs/index.json"
        };
        public static readonly string[] NodeDistBases = {           // 与 NodeIndexChains 同序
            "https://nodejs.org/dist/",
            "https://npmmirror.com/mirrors/node/",
            "https://mirrors.huaweicloud.com/nodejs/"
        };

        // GitHub 资产加速前缀 (拼在完整 GitHub URL 前)
        public static readonly string[] GhAccelerators = {
            "",                        // 官方直连 (最优先)
            "https://ghfast.top/",
            "https://gh-proxy.com/",
            "https://ghproxy.net/"
        };

        public static readonly string[] VersionTxtChains = {
            "https://raw.githubusercontent.com/loudMore/dsh-launcher/main/version.txt",
            "https://cdn.jsdelivr.net/gh/loudMore/dsh-launcher@main/version.txt",
            "https://ghfast.top/https://raw.githubusercontent.com/loudMore/dsh-launcher/main/version.txt"
        };

        /// <summary>同一路径的官方 + 加速链 (版本化文件名已自带 CDN 缓存绕过)</summary>
        public static List<string> GhAssetChain(string githubPath)
        {
            var list = new List<string>();
            foreach (string acc in GhAccelerators)
                list.Add(acc.Length == 0 ? "https://github.com/" + githubPath : acc + "https://github.com/" + githubPath);
            return list;
        }

        // git 智能协议加速 (url.insteadOf 映射; 官方直连优先, 镜像兜底)
        public static readonly string[] GitMirrorPrefixes = {
            "https://ghfast.top/",
            "https://gh-proxy.com/",
            "https://ghproxy.net/"
        };

        // ============================================================
        //  4. 日志 (独立小文件, 供诊断; UI 日志走 Dsh.AppendLog)
        // ============================================================
        public static void Log(string msg)
        {
            try
            {
                File.AppendAllText(Path.Combine(LauncherConfig.DataDir, "launcher-debug.log"),
                    DateTime.Now.ToString("HH:mm:ss.fff") + " [net] " + msg + "\r\n");
            }
            catch { }
        }

        // ============================================================
        //  5. 诊断探测 (--net-probe 参数入口): 逐链逐 URL 实测并报告
        // ============================================================
        public static string RunNetProbe()
        {
            Init();
            var sb = new StringBuilder();
            sb.AppendLine("dsh-launcher net-probe  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            sb.AppendLine("os=" + Environment.OSVersion.VersionString + "  clr=" + Environment.Version);
            sb.AppendLine("tls=" + ServicePointManager.SecurityProtocol);
            sb.AppendLine();

            // -- 代理四级探测 --
            sb.AppendLine("[proxy] 四级探测");
            string cfgProxy = "";
            try { if (LauncherConfig.Loaded != null) cfgProxy = LauncherConfig.Loaded.Proxy; } catch { }
            sb.AppendLine("  1. config: " + (string.IsNullOrEmpty(cfgProxy) ? "(未配置)" : cfgProxy + (ProxyAlive(cfgProxy) ? " [活]" : " [死->跳过]")));
            string sys = ReadSystemProxy();
            sb.AppendLine("  2. system(registry): " + (string.IsNullOrEmpty(sys) ? "(未启用)" : sys));
            string env = Environment.GetEnvironmentVariable("HTTPS_PROXY");
            if (string.IsNullOrEmpty(env)) env = Environment.GetEnvironmentVariable("HTTP_PROXY");
            sb.AppendLine("  3. env: " + (string.IsNullOrEmpty(env) ? "(未设置)" : env));
            var swScan = Stopwatch.StartNew();
            var ps = CurrentProxy();
            sb.AppendLine("  4. result: proxy=" + (ps.Url ?? "(直连)") + "  source=" + ps.Source + "  (" + swScan.ElapsedMilliseconds + "ms)");
            sb.AppendLine();

            // -- 逐链逐 URL 实测 (与真实路径同策略: 活代理失败自动直连, 报告两个通道) --
            Action<string, string> probe = delegate(string chainName, string url)
            {
                long t0 = Environment.TickCount;
                string desc;
                string via = "direct";
                try
                {
                    // 先按探测结果走代理, 失败自动直连 (真实用户路径)
                    HttpResult r = null;
                    if (!string.IsNullOrEmpty(ps.Url))
                    {
                        var rp = GetOnce(url, ps.Url, 12000, Environment.TickCount + 30000);
                        if (rp.Ok) { r = rp; via = "proxy"; }
                    }
                    if (r == null)
                    {
                        r = GetOnce(url, null, 12000, Environment.TickCount + 30000);
                        if (r != null && r.Ok && !string.IsNullOrEmpty(ps.Url)) via = "proxy-fail->direct";
                        else if (r != null && r.Ok) via = "direct";
                    }
                    if (r != null && r.Ok) desc = "HTTP " + r.Status;
                    else if (r != null && r.Error != null) desc = r.Error is WebException ? ((WebException)r.Error).Status.ToString() : r.Error.Message;
                    else desc = "fail";
                }
                catch (Exception ex) { desc = ex.GetType().Name + ": " + ex.Message; via = "direct"; }
                sb.AppendLine("  " + chainName.PadRight(14) + " " + desc.PadRight(24) + via.PadRight(22) + (Environment.TickCount - t0) + "ms  " + url);
            };

            sb.AppendLine("[chain] npm registry");
            sb.AppendLine("  official      (npm 自身命令, 由 install 流程实测)");
            probe("npmmirror", NpmRegistryMirror);
            probe("huawei", NpmRegistryHuawei);

            sb.AppendLine("[chain] node dist");
            for (int i = 0; i < NodeIndexChains.Length; i++) probe("node-" + i, NodeIndexChains[i]);

            sb.AppendLine("[chain] github");
            probe("github", "https://api.github.com/zen");
            for (int i = 0; i < GhAccelerators.Length; i++)
            {
                string acc = GhAccelerators[i];
                probe("gh-" + i, acc.Length == 0 ? "https://github.com/loudMore/dsh-launcher/raw/main/version.txt" : acc + "https://github.com/loudMore/dsh-launcher/raw/main/version.txt");
            }

            sb.AppendLine("[chain] version.txt");
            for (int i = 0; i < VersionTxtChains.Length; i++) probe("ver-" + i, VersionTxtChains[i]);

            sb.AppendLine("[chain] git smart-http (git-for-windows release)");
            for (int i = 0; i < GhAccelerators.Length; i++)
            {
                string acc = GhAccelerators[i];
                string u = acc.Length == 0
                    ? "https://github.com/git-for-windows/git/releases/download/v2.47.1.windows.1/MinGit-2.47.1-64-bit.zip"
                    : acc + "https://github.com/git-for-windows/git/releases/download/v2.47.1.windows.1/MinGit-2.47.1-64-bit.zip";
                // 只探测可达性 (HEAD), 不下载 45MB; 同样走双通道
                try
                {
                    long t0 = Environment.TickCount;
                    var req = (HttpWebRequest)WebRequest.Create(u);
                    req.UserAgent = UserAgent;
                    req.Method = "HEAD";
                    req.Timeout = 12000;
                    string via = "direct";
                    string status;
                    if (!string.IsNullOrEmpty(ps.Url)) req.Proxy = new WebProxy(ps.Url);
                    try
                    {
                        using (var resp = (HttpWebResponse)req.GetResponse()) { status = "HEAD HTTP " + (int)resp.StatusCode; if (!string.IsNullOrEmpty(ps.Url)) via = "proxy"; }
                    }
                    catch (WebException wex1)
                    {
                        // 代理通道失败 -> 直连重试
                        var hr = wex1.Response as HttpWebResponse;
                        if (hr != null) { status = "HEAD HTTP " + (int)hr.StatusCode; }
                        else if (!string.IsNullOrEmpty(ps.Url))
                        {
                            try
                            {
                                req = (HttpWebRequest)WebRequest.Create(u);
                                req.UserAgent = UserAgent;
                                req.Method = "HEAD";
                                req.Timeout = 12000;
                                req.Proxy = null;
                                using (var resp2 = (HttpWebResponse)req.GetResponse()) { status = "HEAD HTTP " + (int)resp2.StatusCode; via = "proxy-fail->direct"; }
                            }
                            catch (WebException wex2)
                            {
                                var h2 = wex2.Response as HttpWebResponse;
                                status = "HEAD " + (h2 != null ? "HTTP " + (int)h2.StatusCode : wex2.Status.ToString());
                            }
                            catch (Exception ex2) { status = "HEAD " + ex2.Message; }
                        }
                        else status = "HEAD " + wex1.Status;
                    }
                    sb.AppendLine("  gitzip-" + i + "        " + status.PadRight(24) + via.PadRight(22) + (Environment.TickCount - t0) + "ms");
                }
                catch (Exception ex) { sb.AppendLine("  gitzip-" + i + "        HEAD " + ex.Message); }
            }

            sb.AppendLine();
            sb.AppendLine("NET-PROBE DONE");
            string outPath = Path.Combine(LauncherConfig.DataDir, "net-probe.txt");
            try { File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8); } catch { }
            return sb.ToString();
        }

        // ============================================================
        //  6. 文本解码 (UTF-8 BOM / UTF-8 / GBK)
        // ============================================================
        public static string Decode(byte[] b)
        {
            if (b == null || b.Length == 0) return "";
            if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF)
            {
                byte[] t = new byte[b.Length - 3];
                Array.Copy(b, 3, t, 0, t.Length);
                b = t;
            }
            if (IsUtf8(b)) return Encoding.UTF8.GetString(b);
            try { return Encoding.GetEncoding(936).GetString(b); } catch { }
            return Encoding.UTF8.GetString(b);
        }

        static bool IsUtf8(byte[] b)
        {
            int i = 0;
            while (i < b.Length)
            {
                byte c = b[i];
                if (c < 0x80) { i++; continue; }
                if (c >= 0xC2 && c <= 0xDF) { if (i + 1 >= b.Length || (b[i + 1] & 0xC0) != 0x80) return false; i += 2; continue; }
                if (c >= 0xE0 && c <= 0xEF) { if (i + 2 >= b.Length || (b[i + 1] & 0xC0) != 0x80 || (b[i + 2] & 0xC0) != 0x80) return false; i += 3; continue; }
                if (c >= 0xF0 && c <= 0xF4) { if (i + 3 >= b.Length || (b[i + 1] & 0xC0) != 0x80 || (b[i + 2] & 0xC0) != 0x80 || (b[i + 3] & 0xC0) != 0x80) return false; i += 4; continue; }
                return false;
            }
            return true;
        }
    }

    // ============================================================
    //  无 UI 自动化入口 (--headless <cmdfile>): 逐行执行命令, 全程无窗口无模态框
    //  结果写 <DataDir>\headless-result.txt 并返回 stdout (winexe 无控制台时靠结果文件)
    //  命令: detect / svc-start / svc-status / svc-stop / svc-wait / plugins /
    //        update-check / selfupdate-check / store / proxy / quit
    // ============================================================
    static class HeadlessRun
    {
        public static string Run(string cmdFile, bool sandbox)
        {
            var sb = new StringBuilder();
            sb.AppendLine("dsh-launcher headless  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  sandbox=" + sandbox);
            var dsh = new Dsh();
            // 隔离配置: 命令文件首行 "#!config <path>" 指定 launcher.json (测试绝不碰生产端口)
            string cfgPath = null;
            try
            {
                string[] pre = File.ReadAllLines(cmdFile);
                foreach (string ln in pre)
                {
                    string t = ln.Trim();
                    if (t.StartsWith("#!config") && t.Length > 8)
                    {
                        string v = t.Substring(8).Trim().Trim('"', '\'');
                        if (v.Length > 0) cfgPath = v;
                    }
                }
            }
            catch { }
            if (cfgPath != null)
            {
                var cfg = LauncherConfig.Load();
                try
                {
                    var ser = new JavaScriptSerializer();
                    var d = ser.DeserializeObject(File.ReadAllText(cfgPath)) as Dictionary<string, object>;
                    if (d != null)
                    {
                        object v;
                        if (d.TryGetValue("port", out v)) cfg.Port = Convert.ToInt32(v);
                        if (d.TryGetValue("dshCommand", out v)) cfg.DshCommand = Convert.ToString(v);
                        if (d.TryGetValue("dshHome", out v)) cfg.DshHome = Convert.ToString(v);
                        if (d.TryGetValue("pluginsRoot", out v)) cfg.PluginsRoot = Convert.ToString(v);
                        if (d.TryGetValue("logDir", out v)) cfg.LogDir = Convert.ToString(v);
                        if (d.TryGetValue("npmPackage", out v)) cfg.NpmPackage = Convert.ToString(v);
                    }
                    sb.AppendLine("using isolated config: " + cfgPath);
                }
                catch (Exception ex) { sb.AppendLine("isolated config read FAIL: " + ex.Message + " (fall back to default)"); }
                dsh.Cfg = cfg;
            }
            else dsh.Cfg = LauncherConfig.Load();
            LauncherConfig.Loaded = dsh.Cfg;
            dsh.OnStatus = delegate(string s) { sb.AppendLine("  [status] " + s); };
            sb.AppendLine("cfg: port=" + dsh.Cfg.Port + " dshCmd=" + dsh.Cfg.DshCommand + " home=" + dsh.Cfg.DshHome + " plugins=" + dsh.Cfg.PluginsRoot);
            try
            {
                foreach (string raw in File.ReadAllLines(cmdFile))
                {
                    string cmd = raw.Trim();
                    if (cmd.Length == 0 || cmd.StartsWith("#")) continue;
                    sb.AppendLine(">> " + cmd);
                    try
                    {
                        if (cmd == "detect")
                        {
                            var env = dsh.DetectEnvironment();
                            sb.AppendLine(string.Format("   node={0} v{1} | npm v{2} | git={3} v{4} | dsh={5} v{6}",
                                env.NodePath == "" ? "MISSING" : "ok", env.NodeVersion, env.NpmVersion,
                                env.GitPath == "" ? "MISSING" : "ok", env.GitVersion,
                                env.DshPath == "" ? "MISSING" : env.DshPath, env.DshVersion));
                        }
                        else if (cmd == "svc-start")
                        {
                            dsh.StartServiceAsync();
                            sb.AppendLine("   start issued (async)");
                        }
                        else if (cmd == "svc-status")
                        {
                            sb.AppendLine("   port " + dsh.Cfg.Port + " open=" + Dsh.IsPortOpen(dsh.Cfg.Port)
                                + " ownProc=" + (dsh.ServerProc != null && !dsh.ServerProc.HasExited ? "alive pid=" + dsh.ServerProc.Id : "none"));
                        }
                        else if (cmd == "svc-stop")
                        {
                            dsh.StopServiceAsync();
                            sb.AppendLine("   stop issued (async)");
                        }
                        else if (cmd.StartsWith("svc-wait"))
                        {
                            int secs = 20;
                            string[] parts = cmd.Split(' ');
                            if (parts.Length > 1) int.TryParse(parts[1], out secs);
                            int waited = 0;
                            while (waited < secs && !Dsh.IsPortOpen(dsh.Cfg.Port)) { Thread.Sleep(1000); waited++; }
                            sb.AppendLine("   waited " + waited + "s, port open=" + Dsh.IsPortOpen(dsh.Cfg.Port));
                        }
                        else if (cmd == "plugins")
                        {
                            var list = dsh.ScanPlugins();
                            sb.AppendLine("   count=" + list.Count);
                            foreach (var p in list) sb.AppendLine("   - " + p.Name + (p.IsGit ? " (git " + p.Branch + ")" : "") + (p.Disabled ? " [disabled]" : ""));
                        }
                        else if (cmd == "update-check")
                        {
                            var info = dsh.CheckUpdates(dsh.DetectEnvironment());
                            sb.AppendLine("   dsh " + info.DshCurrent + " -> " + (info.DshLatest ?? "?") + " update=" + info.DshUpdate + " | plugins=" + info.PluginCount);
                        }
                        else if (cmd == "selfupdate-check")
                        {
                            string latest = dsh.CheckLauncherUpdate();
                            sb.AppendLine("   local=" + Dsh.LauncherVersion + " remote=" + (latest ?? "unreachable"));
                        }
                        else if (cmd == "store")
                        {
                            var items = Dsh.FetchStore();
                            sb.AppendLine("   items=" + items.Count);
                        }
                        else if (cmd == "proxy")
                        {
                            var ps = Net.CurrentProxy();
                            sb.AppendLine("   proxy=" + (ps.Url ?? "(direct)") + " source=" + ps.Source);
                        }
                        else if (cmd == "sleep")
                        {
                            Thread.Sleep(2000);
                            sb.AppendLine("   slept 2s");
                        }
                        else if (cmd == "install")
                        {
                            // 一键全环境安装 (Node+Git+dsh); 仅在 --install-test 下真正执行, 普通沙盒只演练
                            string err;
                            bool ok = dsh.InstallDshNow(out err, null);
                            sb.AppendLine(ok ? "   INSTALL OK" : "   INSTALL FAIL: " + err);
                        }
                        else if (cmd == "install-git")
                        {
                            string gerr;
                            string gver = dsh.InstallGitNow(out gerr);
                            sb.AppendLine(gver != null ? "   GIT OK: " + gver : "   GIT FAIL: " + gerr);
                        }
                        else if (cmd == "quit") break;
                        else sb.AppendLine("   (unknown command, skipped)");
                    }
                    catch (Exception ex) { sb.AppendLine("   CMD ERROR: " + ex.Message); }
                }
                // 收尾: 若服务是我们起的且没显式 stop, 等 3s 后停掉 (测试不留进程)
                Thread.Sleep(3000);
                if (dsh.ServerProc != null && !dsh.ServerProc.HasExited)
                {
                    var p = dsh.ServerProc;
                    try
                    {
                        Process.Start(new ProcessStartInfo("taskkill", "/pid " + p.Id + " /T /F")
                        { UseShellExecute = false, CreateNoWindow = true }).WaitForExit(10000);
                        sb.AppendLine("cleanup: own server tree killed pid=" + p.Id);
                    }
                    catch { }
                }
                sb.AppendLine("HEADLESS DONE");
            }
            catch (Exception ex) { sb.AppendLine("HEADLESS FATAL: " + ex); }
            string result = sb.ToString();
            try { File.WriteAllText(Path.Combine(LauncherConfig.DataDir, "headless-result.txt"), result, Encoding.UTF8); } catch { }
            return result;
        }
    }
}
