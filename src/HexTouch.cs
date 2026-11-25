// HexTouch — 托盘常驻 + 浮动小球，按住小球走 Hex 的本地听写。
// 目标框架：.NET Framework 4.x（Win10/11 自带），用系统 csc.exe 编译成单个 exe。
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace HexTouch
{
    internal static class Program
    {
        private const string MutexName = "Local\\HexTouchTray_9F31";
        private const string RunValueName = "HexTouchTray";

        [STAThread]
        private static void Main(string[] args)
        {
            bool created;
            using (var mutex = new Mutex(true, MutexName, out created))
            {
                if (!created)
                {
                    return;
                }

                Log.Init();
                try { Native.SetProcessDPIAware(); }
                catch { }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                TrayApp.CleanupLegacy();
                TrayApp.EnsureStartup();

                using (var app = new TrayApp(args))
                {
                    Application.Run(app);
                }

                GC.KeepAlive(mutex);
            }
        }
    }

    internal static class Log
    {
        private static readonly object Sync = new object();
        private static string path;

        public static string AppFolder
        {
            get { return Path.GetDirectoryName(Application.ExecutablePath); }
        }

        public static void Init()
        {
            path = Path.Combine(AppFolder, "hextouch.log");
        }

        public static void Write(string message)
        {
            try
            {
                lock (Sync)
                {
                    if (path == null) Init();
                    var fi = new FileInfo(path);
                    if (fi.Exists && fi.Length > 512 * 1024)
                    {
                        fi.Delete();
                    }
                    File.AppendAllText(path,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + message + Environment.NewLine);
                }
            }
            catch { }
        }
    }

    internal static class Native
    {
        public const uint GID_PRESSANDHOLD = 7;
        public const uint GC_PRESSANDHOLD = 0x00000001;

        [StructLayout(LayoutKind.Sequential)]
        public struct GESTURECONFIG
        {
            public uint dwID;
            public uint dwWant;
            public uint dwBlock;
        }

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct BLENDFUNCTION
        {
            public byte BlendOp;
            public byte BlendFlags;
            public byte SourceConstantAlpha;
            public byte AlphaFormat;
        }

        [DllImport("user32.dll")] public static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] public static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
        [DllImport("gdi32.dll")] public static extern IntPtr CreateCompatibleDC(IntPtr dc);
        [DllImport("gdi32.dll")] public static extern bool DeleteDC(IntPtr dc);
        [DllImport("gdi32.dll")] public static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
        [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr obj);

        [DllImport("user32.dll")]
        public static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr dstDc,
            ref Point dst, ref Size size, IntPtr srcDc, ref Point src,
            int colorKey, ref BLENDFUNCTION blend, int flags);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute,
            ref int value, int size);

        public static bool TryRoundWindow(IntPtr hwnd, int preference)
        {
            try
            {
                const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
                return DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE,
                    ref preference, sizeof(int)) == 0;
            }
            catch
            {
                return false;
            }
        }

        [DllImport("user32.dll")]
        public static extern bool SetGestureConfig(IntPtr hwnd, uint dwReserved, uint cIDs,
            [In] GESTURECONFIG[] pGestureConfig, uint cbSize);

        [DllImport("user32.dll")]
        public static extern bool DestroyIcon(IntPtr hIcon);

        public const int WM_TOUCH = 0x0240;
        public const uint TOUCHEVENTF_DOWN = 0x0002;
        public const uint TOUCHEVENTF_UP = 0x0004;

        [StructLayout(LayoutKind.Sequential)]
        public struct TOUCHINPUT
        {
            public int x;
            public int y;
            public IntPtr hSource;
            public uint dwID;
            public uint dwFlags;
            public uint dwMask;
            public uint dwTime;
            public IntPtr dwExtraInfo;
            public uint cxContact;
            public uint cyContact;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int x;
            public int y;
        }

        [DllImport("user32.dll")]
        public static extern bool RegisterTouchWindow(IntPtr hwnd, uint ulFlags);

        [DllImport("user32.dll")]
        public static extern bool GetTouchInputInfo(IntPtr hTouchInput, uint cInputs,
            [In, Out] TOUCHINPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        public static extern bool CloseTouchInputHandle(IntPtr hTouchInput);

        [DllImport("user32.dll")]
        public static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

        [DllImport("user32.dll")]
        public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        // 注册触摸窗口：触摸不再被系统转成鼠标，也就不会有「按住 = 右键」手势。
        public static bool RegisterTouch(IntPtr hwnd)
        {
            try
            {
                return RegisterTouchWindow(hwnd, 0);
            }
            catch { return false; }
        }

        // 关掉「触摸长按 = 右键」的系统手势，否则按住小球会被当成 press-and-hold。
        public static void DisablePressAndHold(IntPtr hwnd)
        {
            try
            {
                var cfg = new GESTURECONFIG[1];
                cfg[0].dwID = GID_PRESSANDHOLD;
                cfg[0].dwWant = 0;
                cfg[0].dwBlock = GC_PRESSANDHOLD;
                SetGestureConfig(hwnd, 0, 1, cfg, (uint)Marshal.SizeOf(typeof(GESTURECONFIG)));
            }
            catch { }
        }
    }

    internal static class Hex
    {
        private static readonly object Gate = new object();

        public static readonly string Cli;
        public static readonly string ExeForIcon;

        static Hex()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string bin = Path.Combine(appData, "hex", "bin");

            Cli = Path.Combine(bin, "hex.com");
            if (!File.Exists(Cli))
            {
                Cli = Path.Combine(bin, "hex.exe");
            }
            if (!File.Exists(Cli))
            {
                Cli = "hex";
            }

            ExeForIcon = null;
            try
            {
                string versionFile = Path.Combine(appData, "hex", "current-version");
                string version = File.Exists(versionFile) ? File.ReadAllText(versionFile).Trim() : "";
                string exe = Path.Combine(appData, "hex", "versions", version, "hex.exe");
                if (File.Exists(exe))
                {
                    ExeForIcon = exe;
                }
            }
            catch { }
        }

        public static int Run(string args, out string output)
        {
            return Run(args, out output, false);
        }

        public static int Run(string args, out string output, bool quiet)
        {
            output = "";
            int code = -1;
            lock (Gate)
            {
                try
                {
                    var psi = new ProcessStartInfo(Cli, args);
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;
                    psi.WindowStyle = ProcessWindowStyle.Hidden;
                    psi.RedirectStandardOutput = true;
                    psi.RedirectStandardError = true;
                    if (Cli != "hex")
                    {
                        psi.WorkingDirectory = Path.GetDirectoryName(Cli);
                    }

                    using (Process p = Process.Start(psi))
                    {
                        // 先等退出再读输出：ReadToEnd() 会一直阻塞到进程结束，
                        // 命令卡住时会让超时逻辑永远执行不到
                        if (!p.WaitForExit(3000))
                        {
                            try { p.Kill(); }
                            catch { }
                            output = "timeout";
                            if (!quiet) Log.Write("hex " + args + " -> timeout");
                            return -1;
                        }
                        string stdout = p.StandardOutput.ReadToEnd();
                        string stderr = p.StandardError.ReadToEnd();
                        output = (stdout + stderr).Trim();
                        code = p.ExitCode;
                    }
                }
                catch (Exception ex)
                {
                    output = ex.Message;
                }
            }

            if (!quiet)
            {
                Log.Write("hex " + args + " -> " + code +
                          (output.Length > 0 ? " | " + Shorten(output, 160) : ""));
            }
            return code;
        }

        public static void RunAsync(string args, Action<int, string> done)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                string output;
                int code = Run(args, out output);
                if (done != null)
                {
                    try { done(code, output); }
                    catch { }
                }
            });
        }

        // 直接拉起 Hex 进程。不用 `hex start`：那个命令在 Hex 未运行时会一直挂住不返回
        public static void StartHidden()
        {
            try
            {
                if (!string.IsNullOrEmpty(ExeForIcon) && File.Exists(ExeForIcon))
                {
                    var psi = new ProcessStartInfo(ExeForIcon);
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;
                    psi.WindowStyle = ProcessWindowStyle.Hidden;
                    psi.WorkingDirectory = Path.GetDirectoryName(ExeForIcon);
                    Process.Start(psi);
                    Log.Write("hex app started directly");
                }
            }
            catch (Exception ex)
            {
                Log.Write("start hex failed: " + ex.Message);
            }
        }

        // 打开 Hex 主窗口
        public static void StartVisible()
        {
            try
            {
                if (!string.IsNullOrEmpty(ExeForIcon) && File.Exists(ExeForIcon))
                {
                    Process.Start(new ProcessStartInfo(ExeForIcon) { UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                Log.Write("start visible failed: " + ex.Message);
            }
        }

        public static bool ReadStatus(out bool recording)
        {
            recording = false;
            string output;
            int code = Run("status", out output, true);
            if (code != 0)
            {
                return false;
            }
            recording = output.IndexOf("recording", StringComparison.OrdinalIgnoreCase) >= 0;
            return true;
        }

        public static string Shorten(string s, int limit)
        {
            if (string.IsNullOrEmpty(s))
            {
                return "";
            }
            s = s.Replace("\r", " ").Replace("\n", " ");
            return s.Length <= limit ? s : s.Substring(0, limit) + "...";
        }
    }

    internal sealed class TrayApp : ApplicationContext
    {
        private const string BallPosFile = "button.pos";

        private readonly NotifyIcon notify;
        private readonly ContextMenuStrip menu;
        private readonly ToolStripMenuItem autoStart;
        private readonly System.Threading.Timer menuWatch;
        private readonly BallForm ball;
        private readonly Icon iconIdle;
        private readonly Icon iconRec;
        private readonly Icon iconOff;
        private readonly System.Windows.Forms.Timer statusTimer;
        private DateTime lastToggle = DateTime.MinValue;
        private bool exiting;
        private bool hexRunning;
        private Point? testPos;

        public TrayApp(string[] args)
        {
            iconIdle = MakeStatusIcon(Color.FromArgb(252, 253, 255), false);
            iconRec = MakeStatusIcon(Color.FromArgb(226, 58, 58), true);
            iconOff = MakeStatusIcon(Color.FromArgb(205, 209, 216), false);

            ball = new BallForm(this);

            menu = new ContextMenuStrip();
            menu.AutoSize = false;
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = false; // 勾选和文字全部自绘，四项共享同一个文字起点
            menu.BackColor = Color.FromArgb(250, 250, 252);
            menu.ForeColor = Color.FromArgb(32, 36, 43);
            menu.Font = new Font("Microsoft YaHei UI", 12f);
            menu.Padding = new Padding(13, 8, 6, 8);
            menu.Renderer = new MenuRenderer();
            menu.Opened += delegate
            {
                PositionMenu();
                ApplyMenuCorners();
            };
            menu.Opening += delegate { autoStart.Checked = IsStartupEnabled(); };

            autoStart = new ToolStripMenuItem("开机自启动");
            ConfigureItem(autoStart);
            autoStart.Tag = "check";
            autoStart.Checked = IsStartupEnabled();
            autoStart.CheckOnClick = true; // 点一下自己切换勾选
            autoStart.Click += delegate { SetStartup(autoStart.Checked); };
            var openItem = MakeItem("唤起 Hex 窗口", delegate { Hex.StartVisible(); });
            openItem.Tag = "open";
            menu.Items.Add(openItem);
            menu.Items.Add(MakeSeparator());
            menu.Items.Add(autoStart);
            menu.Items.Add(MakeSeparator());
            menu.Items.Add(MakeItem("退出 Hex", delegate { QuitHex(); }));
            var exitItem = MakeItem("退出", delegate { ExitApp(); });
            exitItem.Tag = "danger";
            menu.Items.Add(exitItem);
            // 容器也不能自动缩回文字测量宽度：文字由 MenuRenderer 自绘，系统量不到真实布局。
            int rowWidth = 188;
            foreach (ToolStripItem item in menu.Items)
            {
                if (item is ToolStripMenuItem)
                {
                    var textSize = TextRenderer.MeasureText(item.Text, item.Font, Size.Empty,
                        TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
                    rowWidth = Math.Max(rowWidth,
                        textSize.Width + MenuRenderer.TextInset + MenuRenderer.RightInset(item));
                }
            }

            int height = menu.Padding.Vertical;
            foreach (ToolStripItem item in menu.Items)
            {
                item.Width = rowWidth;
                height += item.Height + item.Margin.Vertical;
            }
            menu.Size = new Size(rowWidth + menu.Padding.Horizontal, height);

            notify = new NotifyIcon();
            notify.Icon = iconIdle;
            UpdateTrayText();
            // 菜单弹出时 UI 线程会被它自己的消息循环占着，WinForms Timer 不一定跑得起来，
            // 所以用独立线程的 Timer 轮询：在菜单外按鼠标就关掉它
            menuWatch = new System.Threading.Timer(delegate
            {
                try
                {
                    if (!menu.Visible)
                    {
                        return;
                    }

                    bool pressed = (Native.GetAsyncKeyState(0x01) & 0x8000) != 0 ||
                                   (Native.GetAsyncKeyState(0x02) & 0x8000) != 0;
                    if (!pressed)
                    {
                        return;
                    }

                    if (menu.Bounds.Contains(Cursor.Position))
                    {
                        return;
                    }

                    menu.BeginInvoke((Action)delegate
                    {
                        try { menu.Close(); }
                        catch { }
                    });
                }
                catch { }
            }, null, Timeout.Infinite, Timeout.Infinite);

            menu.Closed += delegate
            {
                menuWatch.Change(Timeout.Infinite, Timeout.Infinite);
            };

            notify.ContextMenuStrip = menu;
            notify.Visible = true;
            notify.MouseClick += NotifyClicked;

            statusTimer = new System.Windows.Forms.Timer();
            statusTimer.Interval = 8000; // 状态轮询：每次都要起一个 hex 进程，8 秒一次足够
            statusTimer.Tick += StatusTick;
            statusTimer.Enabled = true;

            IntPtr unusedHandle = ball.Handle; // 提前建句柄，隐藏时状态轮询也能 BeginInvoke
            Log.Write("tray started; cli=" + Hex.Cli);

            bool wantBall = false;
            if (args != null)
            {
                foreach (string a in args)
                {
                    if (string.Equals(a, "--show-ball", StringComparison.OrdinalIgnoreCase))
                    {
                        wantBall = true;
                    }
                    else if (a != null && a.StartsWith("--pos=", StringComparison.OrdinalIgnoreCase))
                    {
                        string[] xy = a.Substring(6).Split(',');
                        int px, py;
                        if (xy.Length == 2 && int.TryParse(xy[0], out px) && int.TryParse(xy[1], out py))
                        {
                            testPos = new Point(px, py);
                        }
                    }
                }
            }
            if (wantBall)
            {
                ShowBall();
            }

            foreach (string a in args ?? new string[0])
            {
                if (string.Equals(a, "--selftest", StringComparison.OrdinalIgnoreCase))
                {
                    ShowBall();
                    StartSelfTest();
                }
            }
        }

        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "HexTouchTray";

        private static bool IsStartupEnabled()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKeyPath))
                {
                    if (k == null) return false;
                    object v = k.GetValue(RunValueName);
                    return v != null &&
                           string.Equals(v.ToString().Trim('"'), Application.ExecutablePath,
                               StringComparison.OrdinalIgnoreCase);
                }
            }
            catch
            {
                return false;
            }
        }

        private static void SetStartup(bool enable)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (k == null) return;
                    if (enable)
                    {
                        k.SetValue(RunValueName, "\"" + Application.ExecutablePath + "\"");
                    }
                    else
                    {
                        k.DeleteValue(RunValueName, false);
                    }
                }
                Log.Write("startup " + (enable ? "enabled" : "disabled"));
            }
            catch (Exception ex)
            {
                Log.Write("startup change failed: " + ex.Message);
            }
        }

        private void PositionMenu()
        {
            // 由系统继续负责弹出和触摸关闭；这里只在超出工作区时微调，不再用菜单底部留白躲任务栏。
            Rectangle wa = Screen.FromControl(menu).WorkingArea;
            int gap = 12;
            if (menu.Bottom > wa.Bottom - gap)
            {
                menu.Top = Math.Max(wa.Top + gap, wa.Bottom - gap - menu.Height);
            }
            if (menu.Right > wa.Right - gap)
            {
                menu.Left = Math.Max(wa.Left + gap, wa.Right - gap - menu.Width);
            }
        }

        private void ApplyMenuCorners()
        {
            // Win11 交给 DWM 画圆角和阴影；老系统回退到 Region。
            if (Native.TryRoundWindow(menu.Handle, 2))
            {
                menu.Region = null;
                return;
            }
            using (var path = MenuRenderer.RoundRect(
                new Rectangle(0, 0, menu.Width, menu.Height), 12))
            {
                menu.Region = new Region(path);
            }
        }

        private static void ConfigureItem(ToolStripMenuItem item)
        {
            // 显式给宽和高，不能再只固定高度，否则 AutoSize=false 会把宽度算成 0。
            item.AutoSize = false;
            item.Height = 64;
            item.Padding = Padding.Empty;
            item.Margin = Padding.Empty;
        }

        private static ToolStripSeparator MakeSeparator()
        {
            var sep = new ToolStripSeparator();
            sep.AutoSize = false;
            sep.Height = 1;
            sep.Margin = new Padding(0, 7, 0, 7);
            return sep;
        }

        private static ToolStripMenuItem MakeItem(string text, EventHandler handler)
        {
            var item = new ToolStripMenuItem(text);
            ConfigureItem(item);
            item.Click += handler;
            return item;
        }

        private static GraphicsPath RoundedRect(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // 64x64 抗锯齿绘制再交给系统缩放，边缘比 16x16 直接画干净得多
        private static Icon MakeStatusIcon(Color fill, bool recording)
        {
            const int S = 64;
            using (var bmp = new Bitmap(S, S))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);

                    using (var b = new SolidBrush(fill))
                    {
                        g.FillEllipse(b, 3, 3, S - 7, S - 7);
                    }
                    using (var edge = new Pen(recording ? Color.White : Color.FromArgb(240, 28, 30, 36), 3.5f))
                    {
                        g.DrawEllipse(edge, 3, 3, S - 7, S - 7);
                    }

                    if (recording)
                    {
                        using (var w = new SolidBrush(Color.White))
                        using (var path = RoundedRect(new RectangleF(S / 2f - 11, S / 2f - 11, 22, 22), 5f))
                        {
                            g.FillPath(w, path);
                        }
                    }
                    else
                    {
                        int[] heights = { 10, 18, 26, 18, 10 };
                        const int barW = 4;
                        const int gap = 4;
                        int total = heights.Length * barW + (heights.Length - 1) * gap;
                        float x = S / 2f - total / 2f;
                        using (var b = new SolidBrush(Color.FromArgb(240, 28, 30, 36)))
                        {
                            foreach (int h in heights)
                            {
                                using (var path = RoundedRect(new RectangleF(x, S / 2f - h / 2f, barW, h), barW / 2f))
                                {
                                    g.FillPath(b, path);
                                }
                                x += barW + gap;
                            }
                        }
                    }
                }

                IntPtr handle = bmp.GetHicon();
                try
                {
                    return (Icon)Icon.FromHandle(handle).Clone();
                }
                finally
                {
                    Native.DestroyIcon(handle);
                }
            }
        }

        private void StartSelfTest()
        {
            Log.Write("selftest begin");
            var t1 = new System.Windows.Forms.Timer { Interval = 1200 };
            t1.Tick += delegate { t1.Stop(); t1.Dispose(); BeginDictation(); };
            t1.Start();

            var t2 = new System.Windows.Forms.Timer { Interval = 5000 };
            t2.Tick += delegate { t2.Stop(); t2.Dispose(); EndDictation(); };
            t2.Start();

            var t3 = new System.Windows.Forms.Timer { Interval = 9500 };
            t3.Tick += delegate { t3.Stop(); t3.Dispose(); Log.Write("selftest end"); ExitApp(); };
            t3.Start();
        }

        private void NotifyMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || exiting)
            {
                return;
            }

            menu.Show(Cursor.Position);
            menuWatch.Change(0, 40);

            // 别让菜单底部压到任务栏：整体往上抬
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            int limit = wa.Bottom - 10;
            if (menu.Bottom > limit)
            {
                int top = limit - menu.Height;
                if (top < wa.Top)
                {
                    top = wa.Top;
                }
                menu.Top = top;
            }
        }

        private void UpdateTrayText()
        {
            string tip;
            if (ball.Recording)
            {
                tip = "录音中，点悬浮按钮停止";
            }
            else if (!hexRunning)
            {
                tip = "Hex 未运行，点悬浮按钮启动";
            }
            else
            {
                tip = ball.Visible ? "点击隐藏悬浮按钮" : "点击显示悬浮按钮";
            }
            notify.Text = "HexTouch · " + tip;
        }

        private void NotifyClicked(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || exiting)
            {
                return;
            }
            if ((DateTime.Now - lastToggle).TotalMilliseconds < 400)
            {
                return;
            }
            lastToggle = DateTime.Now;
            ToggleBall();
        }

        public void CloseMenu()
        {
            if (menu != null && menu.Visible)
            {
                menu.Close();
            }
        }

        public void ToggleBall()
        {
            if (exiting)
            {
                return;
            }
            if (ball.Visible)
            {
                HideBall();
                return;
            }
            ShowBall();
        }

        private void ShowBall()
        {
            EndDictation();
            // Hex 已经在跑就不要再拉一次，避免弹出主窗口
            if (Process.GetProcessesByName("hex").Length == 0)
            {
                Hex.StartHidden();
            }

            ball.Location = ClampToScreen(testPos.HasValue ? testPos.Value : LoadPosition());
            ball.Show();
            ball.BringToFront();
            hexRunning = Process.GetProcessesByName("hex").Length > 0;
            UpdateTrayText();
            Rectangle scr = Screen.PrimaryScreen.Bounds;
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            Log.Write("screen=" + scr + " work=" + wa + " ball=" + ball.Bounds + " visible=" + ball.Visible);
        }

        public void HideBall()
        {
            EndDictation();
            if (ball.Visible)
            {
                ball.Hide();
                Log.Write("ball hidden");
                UpdateTrayText();
            }
        }

        public void ToggleDictation()
        {
            if (ball.Recording)
            {
                EndDictation();
            }
            else
            {
                BeginDictation();
            }
        }

        public void BeginDictation()
        {
            if (!ball.Visible || ball.Recording)
            {
                return;
            }
            ball.Busy = true;
            ball.SetRecording(true);
            notify.Icon = iconRec;
            hexRunning = true;
            UpdateTrayText();
            Hex.RunAsync("dictation start", delegate(int code, string output)
            {
                if (code == 0)
                {
                    try
                    {
                        ball.BeginInvoke((Action)delegate { ball.Busy = false; });
                    }
                    catch { }
                    return;
                }

                Log.Write("dictation start failed: " + output);

                // Hex 可能被关掉了：先拉起来，等它就绪，再重试
                Hex.StartHidden();

                string retry = "";
                int again = -1;
                for (int i = 0; i < 5; i++)
                {
                    Thread.Sleep(2000);
                    again = Hex.Run("dictation start", out retry);
                    if (again == 0)
                    {
                        break;
                    }
                }
                Log.Write("dictation start retry -> " + again + " | " + Hex.Shorten(retry, 120));
                try
                {
                    ball.BeginInvoke((Action)delegate
                    {
                        ball.Busy = false;
                        if (again != 0)
                        {
                            ball.SetRecording(false);
                            RestoreStatusIcon();
                        }
                        else
                        {
                            notify.Icon = iconRec;
                            hexRunning = true;
                            UpdateTrayText();
                        }
                    });
                }
                catch { }
            });
        }

        public void EndDictation()
        {
            if (!ball.Recording)
            {
                return;
            }
            ball.SetRecording(false);
            RestoreStatusIcon();
            Hex.RunAsync("dictation stop", null);
            Log.Write("dictation stop requested");
        }

        private void RestoreStatusIcon()
        {
            bool recording;
            bool running = Hex.ReadStatus(out recording);
            hexRunning = running;
            notify.Icon = running ? (recording ? iconRec : iconIdle) : iconOff;
            UpdateTrayText();
        }

        private void StatusTick(object sender, EventArgs e)
        {
            // Hex 进程不在时不要反复调 CLI：命令会挂起并把串行闸门堵住，
            // 导致后面的 start/stop 全部排队，表现出来就是“点什么都没反应”
            bool hexAlive = Process.GetProcessesByName("hex").Length > 0;
            hexRunning = hexAlive;

            if (!hexAlive)
            {
                notify.Icon = iconOff;
                UpdateTrayText();
                if (ball.Recording && !ball.Busy)
                {
                    ball.SetRecording(false);
                }
                return;
            }

            // 悬浮按钮没显示时，用户也看不到录音状态，不必每轮都起一个 hex 进程查
            if (!ball.Visible)
            {
                notify.Icon = iconIdle;
                UpdateTrayText();
                return;
            }

            ThreadPool.QueueUserWorkItem(delegate
            {
                bool recording;
                bool running = Hex.ReadStatus(out recording);
                try
                {
                    ball.BeginInvoke((Action)delegate
                    {
                        if (exiting)
                        {
                            return;
                        }
                        hexRunning = running;
                        notify.Icon = running ? (recording ? iconRec : iconIdle) : iconOff;
                        UpdateTrayText();
                        if (recording != ball.Recording && !ball.Busy)
                        {
                            ball.SetRecording(recording);
                        }
                    });
                }
                catch { }
            });
        }

        private void QuitHex()
        {
            EndDictation();
            HideBall(); // 退出 Hex 时连小球一起收起
            Hex.RunAsync("quit --force", delegate(int code, string output)
            {
                Log.Write("quit hex -> " + code + " " + Hex.Shorten(output, 120));
                try
                {
                    ball.BeginInvoke((Action)delegate
                    {
                        notify.Icon = iconOff;
                        hexRunning = false;
                        UpdateTrayText();
                    });
                }
                catch { }
            });
        }

        private void ExitApp()
        {
            if (exiting)
            {
                return;
            }

            if (MessageBox.Show(
                    "退出后托盘图标会消失。\r\n\r\n重新打开：双击程序文件，或开始菜单里的 HexTouch。\r\n\r\n确定退出吗？",
                    "HexTouch", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            exiting = true;
            try
            {
                if (ball.Recording)
                {
                    string ignored;
                    Hex.Run("dictation stop", out ignored);
                }
            }
            catch { }

            statusTimer.Enabled = false;
            notify.Visible = false;
            notify.Dispose();
            ball.Close();
            ExitThread();
        }

        private Point LoadPosition()
        {
            try
            {
                string file = Path.Combine(Log.AppFolder, BallPosFile);
                if (File.Exists(file))
                {
                    string[] parts = File.ReadAllText(file).Split(',');
                    if (parts.Length == 2)
                    {
                        int x, y;
                        if (int.TryParse(parts[0], out x) && int.TryParse(parts[1], out y))
                        {
                            return new Point(x, y);
                        }
                    }
                }
            }
            catch { }

            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            int cx = wa.Left + wa.Width * 7 / 8;
            int px = cx - BallForm.BallSize / 2;
            int py = wa.Bottom - BallForm.TotalHeight - 335;
            return new Point(px, py);
        }

        public void SavePosition(Point p)
        {
            try
            {
                File.WriteAllText(Path.Combine(Log.AppFolder, BallPosFile), p.X + "," + p.Y);
            }
            catch { }
        }

        private static Point ClampToScreen(Point p)
        {
            Rectangle wa = Screen.PrimaryScreen.WorkingArea;
            int maxX = wa.Right - BallForm.TotalWidth;
            int maxY = wa.Bottom - BallForm.TotalHeight;
            int x = Math.Max(wa.Left, Math.Min(p.X, maxX));
            int y = Math.Max(wa.Top, Math.Min(p.Y, maxY));
            return new Point(x, y);
        }

        // 从旧名字（HexDictate）升级过来的机器，清掉残留的自启项和卸载项
        public static void CleanupLegacy()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (k != null) k.DeleteValue("HexDictateTray", false);
                }
            }
            catch { }

            try
            {
                Registry.CurrentUser.DeleteSubKeyTree(
                    @"Software\Microsoft\Windows\CurrentVersion\Uninstall\HexDictate", false);
            }
            catch { }
        }

        public static void EnsureStartup()
        {
            // 只初始化一次：之后由托盘菜单的「开机自启动」勾选控制，程序不再擅自写回
            try
            {
                using (RegistryKey flag = Registry.CurrentUser.CreateSubKey(@"Software\HexTouch"))
                {
                    if (flag == null) return;
                    if (flag.GetValue("StartupInitialized") != null) return;
                    flag.SetValue("StartupInitialized", 1);
                }
                SetStartup(true);
            }
            catch (Exception ex)
            {
                Log.Write("startup init failed: " + ex.Message);
            }
        }
    }

    internal sealed class MenuRenderer : ToolStripProfessionalRenderer
    {
        internal const int TextInset = 40;

        internal static int RightInset(ToolStripItem item)
        {
            return (item.Tag as string) == "check" ? 36 : 14;
        }

        public MenuRenderer() : base(new ProfessionalColorTable())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using (var b = new SolidBrush(Color.FromArgb(250, 250, 252)))
            {
                e.Graphics.FillRectangle(b, e.AffectedBounds);
            }
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            // Windows supplies the popup shadow.
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (!e.Item.Selected)
            {
                return;
            }
            var r = new Rectangle(0, 5, e.Item.Width, e.Item.Height - 10);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            bool danger = string.Equals(e.Item.Tag as string, "danger", StringComparison.Ordinal);
            using (var path = RoundRect(r, 6))
            using (var b = new SolidBrush(danger
                ? Color.FromArgb(252, 235, 234)
                : Color.FromArgb(236, 239, 244)))
            {
                e.Graphics.FillPath(b, path);
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            bool danger = string.Equals(e.Item.Tag as string, "danger", StringComparison.Ordinal);
            bool check = string.Equals(e.Item.Tag as string, "check", StringComparison.Ordinal);
            Color color = danger && e.Item.Selected
                ? Color.FromArgb(180, 35, 24)
                : (e.Item.Selected ? Color.FromArgb(18, 20, 26) : Color.FromArgb(32, 36, 43));

            // 所有项都从同一列开始，勾选项也不例外；垂直方向严格居中。
            var textRect = new Rectangle(TextInset, 0,
                e.Item.Width - TextInset - RightInset(e.Item), e.Item.Height);
            TextRenderer.DrawText(e.Graphics, e.Text, e.Item.Font, textRect, color,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);

            var iconState = e.Graphics.Save();
            e.Graphics.TranslateTransform(18, e.Item.Height / 2f);
            e.Graphics.ScaleTransform(1.35f, 1.35f);
            DrawMenuIcon(e.Graphics, 0, 0, e.Item.Tag as string, color);
            e.Graphics.Restore(iconState);
            var checkItem = e.Item as ToolStripMenuItem;
            if (check && checkItem != null && checkItem.Checked)
            {
                DrawCheck(e.Graphics, e.Item.Width - 19, e.Item.Height / 2f);
            }
        }

        private static void DrawMenuIcon(Graphics g, float x, float y, string kind, Color color)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(color, 1.5f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                if (kind == "open")
                {
                    g.DrawRectangle(pen, x - 7, y - 6, 14, 12);
                    g.DrawLine(pen, x - 7, y - 2, x + 7, y - 2);
                }
                else if (kind == "check")
                {
                    g.DrawEllipse(pen, x - 7, y - 7, 14, 14);
                    g.DrawLines(pen, new[] { new PointF(x, y - 4), new PointF(x, y), new PointF(x + 3, y + 2) });
                }
                else if (kind == "danger")
                {
                    g.DrawArc(pen, x - 6, y - 6, 12, 12, -52, 284);
                    g.DrawLine(pen, x, y - 7, x, y - 1);
                }
                else
                {
                    g.DrawLines(pen, new[] { new PointF(x - 1, y - 6), new PointF(x - 7, y - 6),
                        new PointF(x - 7, y + 6), new PointF(x - 1, y + 6) });
                    g.DrawLine(pen, x - 3, y, x + 7, y);
                    g.DrawLines(pen, new[] { new PointF(x + 3, y - 4), new PointF(x + 7, y),
                        new PointF(x + 3, y + 4) });
                }
            }
        }

        private static void DrawCheck(Graphics g, float cx, float cy)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(Color.FromArgb(30, 70, 112), 3f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                g.DrawLines(pen, new[]
                {
                    new PointF(cx - 8f, cy),
                    new PointF(cx - 2f, cy + 6f),
                    new PointF(cx + 8f, cy - 6f)
                });
            }
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            int y = e.Item.Height / 2;
            using (var p = new Pen(Color.FromArgb(228, 231, 236), 1f))
            {
                e.Graphics.DrawLine(p, 12, y, e.Item.Width - 12, y);
            }
        }

        internal static GraphicsPath RoundRect(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    internal sealed class BallForm : Form
    {
        public static readonly int BallSize = 112;
        public static readonly int SendSize = 60;
        public static readonly int TotalWidth = 190;
        public static readonly int TotalHeight = 146;

        // 三圆保持独立悬浮，不加底托。主球左侧留 2px，避免 Region 抗锯齿边缘被窗口边界切掉。
        private static readonly Rectangle BallRect = new Rectangle(2, 17, BallSize, BallSize);
        private static readonly Rectangle DeleteRectArea = new Rectangle(130, 0, SendSize, SendSize);
        private static readonly Rectangle SendRectArea = new Rectangle(130, 86, SendSize, SendSize);

        private const int WS_EX_TOPMOST = 0x00000008;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int WS_EX_LAYERED = 0x00080000;
        private const byte AC_SRC_OVER = 0x00;
        private const byte AC_SRC_ALPHA = 0x01;
        private const int ULW_ALPHA = 0x02;
        private static readonly int DragThreshold = 8;
        private const int WM_MOUSEACTIVATE = 0x0021;
        private const int MA_NOACTIVATE = 3;

        private readonly TrayApp owner;
        private readonly System.Windows.Forms.Timer pulse;
        private readonly System.Windows.Forms.Timer clickTimer;

        private bool recording;
        private bool busy;
        private bool dragging;
        private bool pressed;
        private bool clickPending;
        private int pressZone; // 0 = 主球, 1 = 回车, 2 = 删除
        private Point pressCursor;
        private Point pressWindow;
        private float phase;

        public BallForm(TrayApp owner)
        {
            this.owner = owner;

            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw, true);

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(TotalWidth, TotalHeight);
            BackColor = Color.FromArgb(228, 240, 255);
            Text = "HexTouchBall";

            pulse = new System.Windows.Forms.Timer();
            pulse.Interval = 40;
            pulse.Tick += delegate
            {
                phase += 0.22f;
                // 分层窗口的内容完全由 UpdateLayeredWindow 提供，Invalidate 不一定
                // 会走到 OnPaint，所以动画直接提交一帧。
                RenderLayered();
            };

            clickTimer = new System.Windows.Forms.Timer();
            clickTimer.Interval = 260;
            clickTimer.Tick += delegate
            {
                clickTimer.Stop();
                clickPending = false;
                SendEnter(false);
            };

            ApplyRegion();
        }

        public bool Recording
        {
            get { return recording; }
        }

        public bool Busy
        {
            get { return busy; }
            set { busy = value; }
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_LAYERED;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.DisablePressAndHold(Handle);
            bool ok = Native.RegisterTouch(Handle);
            Log.Write("ball handle created, RegisterTouchWindow=" + ok);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            bool ok = Native.RegisterTouch(Handle);
            Log.Write("ball shown, re-register touch=" + ok);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ApplyRegion();
            RenderLayered();
        }

        private void ApplyRegion()
        {
            // 分层窗口按逐像素 alpha 渲染，圆形边缘不再需要 1 bit 的 Region 裁剪，
            // 也就没有阶梯状锯齿了。
            Region = null;
        }

        public void SetRecording(bool value)
        {
            if (recording == value)
            {
                return;
            }
            recording = value;
            if (value)
            {
                phase = 0;
                pulse.Start();
            }
            else
            {
                pulse.Stop();
            }
            RenderLayered();
        }

        private static int ZoneOf(Point p)
        {
            if (BallRect.Contains(p))
            {
                return 0;
            }
            if (SendRectArea.Contains(p))
            {
                return 1;
            }
            if (DeleteRectArea.Contains(p))
            {
                return 2;
            }
            return -1;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            owner.CloseMenu();

            if (e.Button == MouseButtons.Right)
            {
                if (ZoneOf(e.Location) != 0)
                {
                    return; // 只有主球能拖动
                }
                pressed = false;
                dragging = true;
                pressCursor = Cursor.Position;
                pressWindow = Location;
                return;
            }

            if (e.Button == MouseButtons.Left)
            {
                pressZone = ZoneOf(e.Location);
                if (pressZone < 0)
                {
                    return;
                }

                pressed = true;
                dragging = false;
                RenderLayered();
                pressCursor = Cursor.Position;
                pressWindow = Location;

                Log.Write("down zone=" + pressZone + " at " + e.X + "," + e.Y);

                if (pressZone == 0)
                {
                    clickTimer.Stop();
                    clickPending = false;
                }
                else
                {
                    Capture = true;
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (dragging)
            {
                Point cur = Cursor.Position;
                Location = new Point(
                    pressWindow.X + (cur.X - pressCursor.X),
                    pressWindow.Y + (cur.Y - pressCursor.Y));
                return;
            }

            // 小按钮上不拖动窗口
            if (pressed && pressZone != 0)
            {
                return;
            }

            // 主球：按下后稍微一动就进入拖动，不用等长按
            if (pressed)
            {
                Point cur = Cursor.Position;
                if (Math.Abs(cur.X - pressCursor.X) > DragThreshold ||
                    Math.Abs(cur.Y - pressCursor.Y) > DragThreshold)
                {
                    dragging = true;
                    Location = new Point(
                        pressWindow.X + (cur.X - pressCursor.X),
                        pressWindow.Y + (cur.Y - pressCursor.Y));
                }
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (e.Button == MouseButtons.Right)
            {
                if (dragging)
                {
                    dragging = false;
                    owner.SavePosition(Location);
                }
                return;
            }

            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            Capture = false;
            Log.Write("up zone=" + (pressed ? pressZone : -1));
            bool wasDragging = dragging;
            dragging = false;

            if (!pressed)
            {
                return;
            }
            pressed = false;
            RenderLayered();

            if (wasDragging)
            {
                owner.SavePosition(Location);
                return;
            }

            if (pressZone == 0)
            {
                owner.ToggleDictation(); // 点一下开始，再点一下停止
            }
            else if (pressZone == 1)
            {
                if (clickPending)
                {
                    clickTimer.Stop();
                    clickPending = false;
                    SendEnter(true); // 双击：换行
                }
                else
                {
                    clickPending = true;
                    clickTimer.Start(); // 单击：延时确认不是双击后再发 Enter
                }
            }
            else if (pressZone == 2)
            {
                SendBackspace(); // 删除
            }
        }

        private static void SendEnter(bool withShift)
        {
            if (withShift)
            {
                PasteNewline();
                return;
            }

            Log.Write("send enter");
            Native.keybd_event(0x0D, 0, 0, UIntPtr.Zero); // VK_RETURN
            Thread.Sleep(15);
            Native.keybd_event(0x0D, 0, 0x0002, UIntPtr.Zero);
        }

        private static void SendBackspace()
        {
            Log.Write("send backspace");
            Native.keybd_event(0x08, 0, 0, UIntPtr.Zero); // VK_BACK
            Thread.Sleep(15);
            Native.keybd_event(0x08, 0, 0x0002, UIntPtr.Zero);
        }

        // 换行不用 Shift+Enter：中文输入法会把它吞掉。改成剪贴板里放一个换行符再粘贴，
        // 任何输入框都认，也不经过输入法。
        private static void PasteNewline()
        {
            Log.Write("paste newline");
            IDataObject backup = null;
            try
            {
                backup = Clipboard.GetDataObject();
            }
            catch { }

            try
            {
                Clipboard.SetText(Environment.NewLine);
                Thread.Sleep(50);

                Native.keybd_event(0x11, 0, 0, UIntPtr.Zero); // VK_CONTROL
                Thread.Sleep(15);
                Native.keybd_event(0x56, 0, 0, UIntPtr.Zero); // V
                Thread.Sleep(15);
                Native.keybd_event(0x56, 0, 0x0002, UIntPtr.Zero);
                Thread.Sleep(15);
                Native.keybd_event(0x11, 0, 0x0002, UIntPtr.Zero);
                Thread.Sleep(180);
            }
            catch (Exception ex)
            {
                Log.Write("paste newline failed: " + ex.Message);
            }
            finally
            {
                try
                {
                    if (backup != null)
                    {
                        Clipboard.SetDataObject(backup, true);
                    }
                    else
                    {
                        Clipboard.Clear();
                    }
                }
                catch { }
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_MOUSEACTIVATE)
            {
                m.Result = (IntPtr)MA_NOACTIVATE; // 点击绝不激活窗口，保住输入框焦点
                return;
            }
            if (m.Msg == Native.WM_TOUCH)
            {
                HandleTouch(m.WParam, m.LParam);
                m.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref m);
        }

        private void HandleTouch(IntPtr hTouch, IntPtr lParam)
        {
            int count = (int)(lParam.ToInt64() & 0xFFFF);
            if (count <= 0)
            {
                return;
            }

            var inputs = new Native.TOUCHINPUT[count];
            if (!Native.GetTouchInputInfo(hTouch, (uint)count, inputs,
                    Marshal.SizeOf(typeof(Native.TOUCHINPUT))))
            {
                return;
            }

            try
            {
                for (int i = 0; i < count; i++)
                {
                    Native.TOUCHINPUT ti = inputs[i];
                    bool down = (ti.dwFlags & Native.TOUCHEVENTF_DOWN) != 0;
                    bool up = (ti.dwFlags & Native.TOUCHEVENTF_UP) != 0;
                    if (!down && !up)
                    {
                        continue;
                    }

                    Log.Write("touch " + (down ? "down" : "up") + " at " + ti.x + "," + ti.y);

                    if (up)
                    {
                        owner.ToggleDictation();
                    }
                }
            }
            finally
            {
                Native.CloseTouchInputHandle(hTouch);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            pulse.Stop();
            clickTimer.Stop();
            base.OnFormClosing(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // 分层窗口不经过 GDI 双缓冲，每次重绘都整块推给 DWM。
            RenderLayered();
        }

        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);
            if (IsHandleCreated)
            {
                RenderLayered(); // UpdateLayeredWindow 用屏幕坐标，移动后要重新提交
            }
        }

        private void RenderLayered()
        {
            if (!IsHandleCreated || Width <= 0 || Height <= 0)
            {
                return;
            }

            IntPtr screenDc = Native.GetDC(IntPtr.Zero);
            IntPtr memDc = Native.CreateCompatibleDC(screenDc);
            IntPtr hBmp = IntPtr.Zero;
            IntPtr oldBmp = IntPtr.Zero;
            try
            {
                using (var bmp = new Bitmap(Width, Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.Clear(Color.Transparent);
                        PaintContent(g);
                    }

                    hBmp = bmp.GetHbitmap(Color.FromArgb(0));
                    oldBmp = Native.SelectObject(memDc, hBmp);

                    var size = new Size(Width, Height);
                    var src = new Point(0, 0);
                    var dst = new Point(Left, Top);
                    var blend = new Native.BLENDFUNCTION
                    {
                        BlendOp = AC_SRC_OVER,
                        BlendFlags = 0,
                        SourceConstantAlpha = 255,
                        AlphaFormat = AC_SRC_ALPHA
                    };
                    Native.UpdateLayeredWindow(Handle, screenDc, ref dst, ref size,
                        memDc, ref src, 0, ref blend, ULW_ALPHA);
                }
            }
            catch (Exception ex)
            {
                Log.Write("layered render failed: " + ex.Message);
            }
            finally
            {
                if (oldBmp != IntPtr.Zero) Native.SelectObject(memDc, oldBmp);
                if (hBmp != IntPtr.Zero) Native.DeleteObject(hBmp);
                if (memDc != IntPtr.Zero) Native.DeleteDC(memDc);
                if (screenDc != IntPtr.Zero) Native.ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        private void PaintContent(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var full = new RectangleF(BallRect.X, BallRect.Y, BallRect.Width, BallRect.Height);
            var edge = RectangleF.Inflate(full, -3, -3);

            if (recording)
            {
                Color red = Color.FromArgb(229, 72, 77);
                float k = (float)(0.5 + 0.5 * Math.Sin(phase));
                // 外扩层用同一颜色铺满 Region 边缘，遮住裁剪产生的背景渗色。
                using (var baseFill = new SolidBrush(red))
                {
                    g.FillEllipse(baseFill, RectangleF.Inflate(full, 1f, 1f));
                }
                using (var fill = new SolidBrush(red))
                {
                    g.FillEllipse(fill, full);
                }
                // 用颜色渐变模拟柔和脉冲，不启用分层窗口，触摸输入不受影响。
                using (var ring = new Pen(Mix(red, Color.White, 0.24f + 0.24f * k),
                    1.4f + 0.3f * k))
                {
                    g.DrawEllipse(ring, edge);
                }

                float side = 30;
                var stop = new RectangleF(
                    BallRect.X + (BallSize - side) / 2,
                    BallRect.Y + (BallSize - side) / 2,
                    side, side);
                using (var path = Rounded(stop, 5f))
                using (var white = new SolidBrush(Color.White))
                {
                    g.FillPath(white, path);
                }
            }
            else
            {
                bool down = pressed && pressZone == 0;
                Color body = down ? Color.FromArgb(62, 65, 61) : Color.FromArgb(42, 45, 41);
                using (var baseFill = new SolidBrush(body))
                {
                    g.FillEllipse(baseFill, RectangleF.Inflate(full, 1f, 1f));
                }
                using (var fill = new SolidBrush(body))
                {
                    g.FillEllipse(fill, full);
                }
                using (var ring = new Pen(Color.FromArgb(88, 92, 84), 1.4f))
                {
                    g.DrawEllipse(ring, edge);
                }
                DrawWave(g);
            }

            // 回车钮
            DrawSmallButton(g, SendRectArea, false);
            // 删除钮
            DrawSmallButton(g, DeleteRectArea, true);
        }

        private void DrawSmallButton(Graphics g, Rectangle area, bool backspace)
        {
            var body = RectangleF.Inflate(area, -1, -1);
            int zone = backspace ? 2 : 1;
            bool down = pressed && pressZone == zone;
            using (var outer = new SolidBrush(BackColor))
            {
                g.FillEllipse(outer, RectangleF.Inflate(area, 1f, 1f));
            }
            using (var fill = new SolidBrush(down
                ? Color.FromArgb(204, 225, 251)
                : BackColor))
            {
                g.FillEllipse(fill, area);
            }
            using (var ring = new Pen(Color.FromArgb(191, 212, 237), 1f))
            {
                g.DrawEllipse(ring, body);
            }

            float cx = area.X + area.Width / 2f;
            float cy = area.Y + area.Height / 2f;
            if (backspace)
            {
                DrawBackspace(g, cx, cy);
            }
            else
            {
                DrawEnter(g, cx, cy);
            }
        }

        private static Pen IconPen()
        {
            var pen = new Pen(Color.FromArgb(64, 82, 101), 2f);
            pen.StartCap = LineCap.Round;
            pen.EndCap = LineCap.Round;
            pen.LineJoin = LineJoin.Round;
            return pen;
        }

        // 标准回车符号：从右上折到底部，再向左。
        private static void DrawEnter(Graphics g, float cx, float cy)
        {
            using (var pen = IconPen())
            using (var path = new GraphicsPath())
            {
                path.AddLine(cx + 10, cy - 9, cx + 10, cy);
                path.AddArc(cx + 2, cy - 4, 8, 8, 0, 90);
                path.AddLine(cx + 6, cy + 4, cx - 10, cy + 4);
                g.DrawPath(pen, path);
                g.DrawLines(pen, new[]
                {
                    new PointF(cx - 4, cy - 2),
                    new PointF(cx - 10, cy + 4),
                    new PointF(cx - 4, cy + 10)
                });
            }
        }

        // 删除一个字符用退格图标，不再用容易被理解成“关闭”的叉。
        private static void DrawBackspace(Graphics g, float cx, float cy)
        {
            using (var pen = IconPen())
            {
                using (var path = new GraphicsPath())
                {
                    path.AddLine(cx - 12, cy, cx - 4, cy - 8);
                    path.AddLine(cx - 4, cy - 8, cx + 8, cy - 8);
                    path.AddArc(cx + 5, cy - 8, 6, 6, 270, 90);
                    path.AddArc(cx + 5, cy + 2, 6, 6, 0, 90);
                    path.AddLine(cx + 8, cy + 8, cx - 4, cy + 8);
                    path.CloseFigure();
                    g.DrawPath(pen, path);
                }
                g.DrawLine(pen, cx, cy - 3, cx + 6, cy + 3);
                g.DrawLine(pen, cx, cy + 3, cx + 6, cy - 3);
            }
        }

        private static Color Mix(Color from, Color to, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return Color.FromArgb(
                (int)(from.R + (to.R - from.R) * t),
                (int)(from.G + (to.G - from.G) * t),
                (int)(from.B + (to.B - from.B) * t));
        }

        private static GraphicsPath Rounded(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            float d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // 空闲态：深色音波
        private static void DrawWave(Graphics g)
        {
            int cx = BallRect.X + BallSize / 2;
            int cy = BallRect.Y + BallSize / 2;
            int[] heights = { 18, 34, 50, 34, 18 };
            int barW = 6;
            int gap = 8;
            int total = heights.Length * barW + (heights.Length - 1) * gap;
            float x = cx - total / 2f;

            using (var brush = new SolidBrush(Color.FromArgb(250, 247, 240)))
            {
                foreach (int h in heights)
                {
                    float height = h;
                    var r = new RectangleF(x, cy - height / 2f, barW, height);
                    using (var path = Rounded(r, barW / 2f))
                    {
                        g.FillPath(brush, path);
                    }
                    x += barW + gap;
                }
            }
        }
    }
}
