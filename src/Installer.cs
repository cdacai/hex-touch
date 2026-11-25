using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class Installer
{
    private const string AppName = "HexTouch";
    private const string RunValue = "HexTouchTray";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\HexTouch";

    [STAThread]
    private static void Main(string[] args)
    {
        bool silent = false;
        foreach (string a in args)
        {
            if (string.Equals(a, "--silent", StringComparison.OrdinalIgnoreCase)) silent = true;
        }

        string defaultDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), AppName);
        string dir = defaultDir;

        // 命令行指定：--dir "D:\Tools\HexTouch"
        foreach (string a in args)
        {
            if (a.StartsWith("--dir=", StringComparison.OrdinalIgnoreCase))
            {
                dir = a.Substring(6).Trim('"');
            }
        }

        if (dir == defaultDir && !silent)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                dlg.Description = "选择安装位置（建议放在当前用户目录，避免权限问题）";
                dlg.SelectedPath = defaultDir;
                dlg.ShowNewFolderButton = true;
                if (dlg.ShowDialog() != DialogResult.OK)
                {
                    return;
                }
                dir = dlg.SelectedPath;
            }
        }

        // 目录里已经有别的文件时先提醒：卸载只删 HexTouch 自己的文件，不会连累原有内容。
        if (!silent && Directory.Exists(dir) && Directory.GetFileSystemEntries(dir).Length > 0)
        {
            if (MessageBox.Show(
                    "这个目录里已经有其他文件：\n" + dir +
                    "\n\nHexTouch 会把自己的文件放进去；卸载时只删除 HexTouch 自己的文件，" +
                    "这里的原有内容不会被动。\n\n继续安装吗？",
                    AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes)
            {
                return;
            }
        }

        string exe = Path.Combine(dir, AppName + ".exe");
        string uninstaller = Path.Combine(dir, AppName + "Uninstall.exe");

        try
        {
            Install(dir, exe, uninstaller, silent);
        }
        catch (Exception ex)
        {
            if (silent) Console.WriteLine(ex);
            else MessageBox.Show("安装失败：" + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void Install(string dir, string exe, string uninstaller, bool silent)
    {
        string hexCli = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"hex\bin\hex.com");

        if (!File.Exists(hexCli) && !silent)
        {
            if (MessageBox.Show(
                    "没有检测到 Hex 语音听写。\n\n建议先安装 Hex（https://anoma.ly/hex/）。\n仍然继续安装吗？",
                    AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.No)
            {
                return;
            }
        }

        Directory.CreateDirectory(dir);
        KillRunning();

        Extract(AppName + ".exe", exe);
        Extract(AppName + "Uninstall.exe", uninstaller);

        using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
        {
            if (k != null) k.SetValue(RunValue, "\"" + exe + "\"");
        }

        // 出现在「设置 → 应用 → 已安装的应用」里，卸载按钮指向独立卸载器
        using (RegistryKey k = Registry.CurrentUser.CreateSubKey(UninstallKey))
        {
            if (k != null)
            {
                k.SetValue("DisplayName", "HexTouch（Hex 语音听写按钮）");
                k.SetValue("DisplayIcon", exe + ",0");
                k.SetValue("UninstallString", "\"" + uninstaller + "\"");
                k.SetValue("InstallLocation", dir);
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }

        CreateShortcut(exe, dir);
        Process.Start(exe);

        if (!silent)
        {
            MessageBox.Show(
                "安装完成。\n\n程序目录：" + dir +
                "\n卸载程序：" + uninstaller +
                "\n开机自启：已开启（可在托盘右键菜单里关掉）" +
                "\n\n任务栏出现图标后，到「设置 → 个性化 → 任务栏 → 其他系统托盘图标」把 HexTouch 打开。",
                AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private static void CreateShortcut(string exe, string dir)
    {
        try
        {
            string link = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Programs), "HexTouch.lnk");

            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;

            object shell = Activator.CreateInstance(shellType);
            object shortcut = shellType.InvokeMember("CreateShortcut",
                BindingFlags.InvokeMethod, null, shell, new object[] { link });

            Type sc = shortcut.GetType();
            sc.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { exe });
            sc.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { dir });
            sc.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { exe + ",0" });
            sc.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { "Hex 语音听写按钮" });
            sc.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
        }
        catch { }
    }

    private static void Extract(string resourceName, string target)
    {
        using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
        {
            if (s == null)
            {
                throw new Exception("安装包不完整：缺少 " + resourceName);
            }
            using (FileStream f = File.Create(target))
            {
                s.CopyTo(f);
            }
        }
    }

    private static void KillRunning()
    {
        foreach (Process p in Process.GetProcessesByName(AppName))
        {
            try
            {
                p.Kill();
                p.WaitForExit(3000);
            }
            catch { }
        }
    }
}
