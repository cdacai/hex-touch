using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

internal static class Uninstaller
{
    private const string AppName = "HexTouch";
    private const string RunValue = "HexTouchTray";
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\HexTouch";

    // 只删自己放进去的文件。安装目录允许用户自选，不能整目录递归删除，
    // 否则用户选了一个已有内容的文件夹，卸载会连累里面的其他文件。
    private static readonly string[] OwnedFiles =
    {
        AppName + ".exe",
        AppName + "Uninstall.exe",
        "hextouch.log",
        "button.pos"
    };

    [STAThread]
    private static void Main(string[] args)
    {
        string self = Application.ExecutablePath;
        string dir = Path.GetDirectoryName(self);

        bool silent = false;
        bool fromTemp = false;
        foreach (string a in args)
        {
            if (string.Equals(a, "--silent", StringComparison.OrdinalIgnoreCase)) silent = true;
            if (string.Equals(a, "--from-temp", StringComparison.OrdinalIgnoreCase)) fromTemp = true;
        }

        // 临时副本：真正要删的目录是参数传进来的那个
        if (fromTemp && args.Length >= 2)
        {
            dir = args[1];
        }

        if (!fromTemp)
        {
            if (!silent &&
                MessageBox.Show("确定要卸载 HexTouch 吗？\n\n会关闭程序、清除开机自启，并删除 HexTouch 自己的文件。\n目录里其他文件不会被动。",
                    "HexTouch 卸载", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            {
                return;
            }

            // 自己不能删自己：复制到临时目录，让副本去删
            try
            {
                string temp = Path.Combine(Path.GetTempPath(), AppName + "Uninstall.exe");
                File.Copy(self, temp, true);
                string arguments = "--from-temp \"" + dir + "\"" + (silent ? " --silent" : "");
                Process.Start(temp, arguments);
            }
            catch (Exception ex)
            {
                if (silent) Console.WriteLine(ex);
                else MessageBox.Show("无法启动卸载程序：" + ex.Message, AppName);
            }
            return;
        }

        // 临时副本：执行清理
        KillRunning();

        using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey, true))
        {
            if (k != null) k.DeleteValue(RunValue, false);
        }
        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(UninstallKey, false);
        }
        catch { }

        try
        {
            string link = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Programs), "HexTouch.lnk");
            if (File.Exists(link))
            {
                File.Delete(link);
            }
        }
        catch { }

        Exception lastError = null;
        for (int i = 0; i < 20; i++)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    foreach (string name in OwnedFiles)
                    {
                        string file = Path.Combine(dir, name);
                        if (File.Exists(file)) File.Delete(file);
                    }

                    // 只有目录已经空了才移除；还有用户自己的文件就留着。
                    if (Directory.GetFileSystemEntries(dir).Length == 0)
                    {
                        Directory.Delete(dir, false);
                    }
                }
                lastError = null;
                break;
            }
            catch (Exception ex)
            {
                lastError = ex;
                Thread.Sleep(300);
            }
        }

        if (lastError != null)
        {
            if (!silent)
            {
                MessageBox.Show("删除目录失败：" + lastError.Message + "\n请手动删除：" + dir,
                    AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            return;
        }

        if (!silent)
        {
            MessageBox.Show("已卸载，HexTouch 的文件和开机自启都已清除。", AppName,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
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
