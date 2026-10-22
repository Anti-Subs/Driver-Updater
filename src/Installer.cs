// Driver Updater - install / update / uninstall (built into the same program)
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Win32;

namespace DriverUpdater
{
    public static class Installer
    {
        public const string AppName = "Driver Updater";
        const string RegPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\DriverUpdater";

        public static string DefaultDir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), AppName); } }
        static string StartMenuLink { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), AppName + ".lnk"); } }
        static string DesktopLink { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), AppName + ".lnk"); } }

        /// <summary>Folder of the current installation, or null.</summary>
        public static string InstalledDir()
        {
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(RegPath))
                    return k == null ? null : k.GetValue("InstallLocation") as string;
            }
            catch { return null; }
        }

        public static string InstalledVersion()
        {
            try
            {
                using (var k = Registry.LocalMachine.OpenSubKey(RegPath))
                    return k == null ? null : k.GetValue("DisplayVersion") as string;
            }
            catch { return null; }
        }

        static void CloseRunningCopies()
        {
            int me = Process.GetCurrentProcess().Id;
            foreach (var p in Process.GetProcessesByName("DriverUpdater"))
            {
                if (p.Id == me) continue;
                try { p.CloseMainWindow(); if (!p.WaitForExit(3000)) p.Kill(); p.WaitForExit(5000); } catch { }
            }
        }

        public static bool AnotherCopyRunning()
        {
            int me = Process.GetCurrentProcess().Id;
            return Process.GetProcessesByName("DriverUpdater").Any(p => p.Id != me);
        }

        static void MakeShortcut(string link, string target, string dir)
        {
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell", true));
            dynamic s = shell.CreateShortcut(link);
            s.TargetPath = target;
            s.WorkingDirectory = dir;
            s.IconLocation = target + ",0";
            s.Description = "Scan and update device drivers";
            s.Save();
        }

        /// <summary>Copies this program into place and registers it. Returns the installed exe path.</summary>
        public static string Install(string dir, bool desktopShortcut, Action<string> step)
        {
            step("Closing Driver Updater if it's open...");
            CloseRunningCopies();

            step("Copying files...");
            Directory.CreateDirectory(dir);
            string exe = Path.Combine(dir, "DriverUpdater.exe");
            string self = Process.GetCurrentProcess().MainModule.FileName;
            if (!string.Equals(Path.GetFullPath(self), Path.GetFullPath(exe), StringComparison.OrdinalIgnoreCase))
            {
                for (int i = 0; ; i++)
                {
                    try { File.Copy(self, exe, true); break; }
                    catch (IOException) { if (i >= 5) throw; Thread.Sleep(1000); }
                }
            }
            // Leftovers from the earlier PowerShell-based versions
            foreach (var old in new[] { "app.ico", "DriverUpdater.ps1" })
            {
                try { File.Delete(Path.Combine(dir, old)); } catch { }
                try { File.Delete(Path.Combine(Paths.WorkDir, old)); } catch { }
            }

            step("Creating shortcuts...");
            MakeShortcut(StartMenuLink, exe, dir);
            if (desktopShortcut) MakeShortcut(DesktopLink, exe, dir);
            else if (File.Exists(DesktopLink)) try { File.Delete(DesktopLink); } catch { }

            step("Registering with Windows...");
            using (var k = Registry.LocalMachine.CreateSubKey(RegPath))
            {
                k.SetValue("DisplayName", AppName);
                k.SetValue("DisplayVersion", App.Version);
                k.SetValue("Publisher", AppName);
                k.SetValue("DisplayIcon", exe + ",0");
                k.SetValue("InstallLocation", dir);
                k.SetValue("UninstallString", "\"" + exe + "\" /uninstall");
                k.SetValue("QuietUninstallString", "\"" + exe + "\" /uninstall");
                k.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                k.SetValue("EstimatedSize", (int)(new FileInfo(exe).Length / 1024), RegistryValueKind.DWord);
            }
            Log.Write("Installed " + App.Version + " to " + dir);
            return exe;
        }

        /// <summary>Step 1 (from the installed copy): confirm, then hand over to a temporary copy so the program file can be deleted.</summary>
        public static void BeginUninstall()
        {
            if (Native.Ask("Remove " + AppName + " from this PC?\n\nDrivers you installed with it will stay installed.", "Uninstall " + AppName, Native.YESNO, Native.QUESTION) != Native.IDYES) return;
            string dir = InstalledDir() ?? Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
            string temp = Path.Combine(Path.GetTempPath(), "DriverUpdater-uninstall-" + Process.GetCurrentProcess().Id + ".exe");
            File.Copy(Process.GetCurrentProcess().MainModule.FileName, temp, true);
            Process.Start(new ProcessStartInfo(temp, "/uninstall-finish \"" + dir + "\" " + Process.GetCurrentProcess().Id) { UseShellExecute = false });
        }

        /// <summary>Step 2 (from the temporary copy): remove everything.</summary>
        public static void FinishUninstall(string dir, int waitPid)
        {
            try { if (waitPid > 0) Process.GetProcessById(waitPid).WaitForExit(15000); } catch { }
            CloseRunningCopies();
            Thread.Sleep(500);
            var problems = new System.Collections.Generic.List<string>();
            foreach (var l in new[] { StartMenuLink, DesktopLink })
                if (File.Exists(l)) try { File.Delete(l); } catch { problems.Add(l); }
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir) && File.Exists(Path.Combine(dir, "DriverUpdater.exe")))
            {
                for (int i = 0; i < 5 && Directory.Exists(dir); i++)
                    try { Directory.Delete(dir, true); } catch { Thread.Sleep(1000); }
                if (Directory.Exists(dir)) problems.Add(dir);
            }
            try { Registry.LocalMachine.DeleteSubKeyTree(RegPath, false); } catch { }
            foreach (var d in new[] { Paths.WorkDir, Paths.StateDir, Path.GetDirectoryName(Paths.OldLastScanFile) })
                try { if (Directory.Exists(d)) Directory.Delete(d, true); } catch { }
            // This temporary copy deletes itself at the next restart
            try { Native.MoveFileEx(Process.GetCurrentProcess().MainModule.FileName, null, Native.MOVEFILE_DELAY_UNTIL_REBOOT); } catch { }

            if (problems.Count > 0)
                Native.Ask(AppName + " was removed, but these items couldn't be deleted (you can delete them yourself):\n\n" + string.Join("\n", problems), "Uninstall " + AppName, Native.OK, Native.WARNING);
            else
                Native.Ask(AppName + " has been removed.", "Uninstall " + AppName, Native.OK, Native.INFO);
        }
    }
}
