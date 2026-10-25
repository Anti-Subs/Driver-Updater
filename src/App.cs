// Driver Updater - main program and window logic
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Windows.Threading;

[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8", FrameworkDisplayName = ".NET Framework 4.8")]
[assembly: AssemblyTitle("Driver Updater")]
[assembly: AssemblyDescription("Scan and update device drivers")]
[assembly: AssemblyProduct("Driver Updater")]
[assembly: AssemblyCompany("Driver Updater")]
[assembly: AssemblyCopyright("")]

namespace DriverUpdater
{
    /// <summary>Small helpers for working with WPF objects loaded at run time.</summary>
    public static class Wpf
    {
        const string PF = ", PresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35";
        const string PC = ", PresentationCore, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35";
        static readonly Type VisibilityType = Type.GetType("System.Windows.Visibility" + PC, true);
        static readonly object BrushConv = Activator.CreateInstance(Type.GetType("System.Windows.Media.BrushConverter" + PC, true));
        static readonly Dictionary<string, object> Brushes = new Dictionary<string, object>();

        public static dynamic Load(string xaml)
        {
            var reader = Type.GetType("System.Windows.Markup.XamlReader" + PF, true);
            dynamic w = reader.GetMethod("Parse", new[] { typeof(string) }).Invoke(null, new object[] { Theme.Apply(xaml) });
            if (Theme.Dark) DarkTitleBar((object)w);
            return w;
        }

        [System.Runtime.InteropServices.DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        /// <summary>Dark window title bar (Windows 10 20H1+ and Windows 11).</summary>
        static void DarkTitleBar(object window)
        {
            try
            {
                var helperType = Type.GetType("System.Windows.Interop.WindowInteropHelper" + PF, true);
                dynamic helper = Activator.CreateInstance(helperType, window);
                IntPtr h = helper.EnsureHandle();
                int on = 1;
                if (DwmSetWindowAttribute(h, 20, ref on, 4) != 0) DwmSetWindowAttribute(h, 19, ref on, 4);
            }
            catch (Exception ex) { Log.Write("Dark title bar not available: " + ex.Message); }
        }

        public static dynamic Vis(bool visible) { return Enum.Parse(VisibilityType, visible ? "Visible" : "Collapsed"); }
        public static dynamic VisName(string name) { return Enum.Parse(VisibilityType, name); }

        public static dynamic Brush(string hex)
        {
            hex = Theme.C(hex);
            object b;
            if (!Brushes.TryGetValue(hex, out b)) { b = ((dynamic)BrushConv).ConvertFromString(hex); Brushes[hex] = b; }
            return b;
        }

        sealed class Relay
        {
            readonly Action<object, EventArgs> action;
            public Relay(Action<object, EventArgs> a) { action = a; }
            public void Invoke(object sender, EventArgs e) { action(sender, e); }
        }

        static Delegate Make(Type delegateType, Action<object, EventArgs> a)
        {
            return Delegate.CreateDelegate(delegateType, new Relay(a), typeof(Relay).GetMethod("Invoke"));
        }

        /// <summary>Subscribes to a CLR event (Click, Closing, ContentRendered...) by name.</summary>
        public static void On(object target, string evt, Action<object, EventArgs> a)
        {
            var ei = target.GetType().GetEvent(evt);
            ei.AddEventHandler(target, Make(ei.EventHandlerType, a));
        }

        /// <summary>Listens for clicks of any button inside a container (routed ButtonBase.Click).</summary>
        public static void OnButtonClick(object container, Action<object, EventArgs> a)
        {
            var bb = Type.GetType("System.Windows.Controls.Primitives.ButtonBase" + PF, true);
            object clickEvent = bb.GetField("ClickEvent").GetValue(null);
            var handlerType = Type.GetType("System.Windows.RoutedEventHandler" + PC, true);
            ((dynamic)container).AddHandler((dynamic)clickEvent, (dynamic)Make(handlerType, a));
        }

        public static bool IsButton(object o)
        {
            if (o == null) return false;
            for (var t = o.GetType(); t != null; t = t.BaseType) if (t.FullName == "System.Windows.Controls.Button") return true;
            return false;
        }
    }

    public static class App
    {
        public static readonly string Version = typeof(App).Assembly.GetName().Version.ToString(3);
        /// <summary>Set when the main window closes only to reopen (e.g. to switch light/dark).</summary>
        public static bool Reopen;

        /// <summary>Run by a newly downloaded installer: wait for the old app to close, install over it, reopen it.</summary>
        static int FinishSelfUpdate(string[] args)
        {
            int pid = 0;
            if (args.Length > 1) int.TryParse(args[1], out pid);
            Log.Write("Self-update to " + Version + " starting");
            try
            {
                if (pid > 0) try { Process.GetProcessById(pid).WaitForExit(30000); } catch (ArgumentException) { }
                string dir = Installer.InstalledDir() ?? Installer.DefaultDir;
                string exe = Installer.Install(dir, Installer.HasDesktopShortcut, step => Log.Write("Update: " + step));
                Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = dir });
                return 0;
            }
            catch (Exception ex)
            {
                Log.Write("Self-update failed: " + ex);
                string self = Process.GetCurrentProcess().MainModule.FileName;
                Native.Ask("Driver Updater couldn't finish updating to " + Version + ":\n\n" + ex.Message +
                           "\n\nYou can run the installer yourself:\n" + self, "Driver Updater", Native.OK, Native.WARNING);
                return 1;
            }
        }

        [STAThread]
        public static int Main(string[] args)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                Log.Write("FATAL: " + ex);
                Native.Ask("Driver Updater ran into a problem:\n\n" + (ex != null ? ex.Message : "") + "\n\nDetails were saved to:\n" + Paths.LogFile, "Driver Updater", Native.OK, Native.ERROR);
            };
            try
            {
                string self = Process.GetCurrentProcess().MainModule.FileName;
                if (args.Length > 0 && args[0].Equals("/uninstall", StringComparison.OrdinalIgnoreCase)) { Installer.BeginUninstall(); return 0; }
                if (args.Length > 0 && args[0].Equals("/uninstall-finish", StringComparison.OrdinalIgnoreCase))
                {
                    int pid = 0;
                    if (args.Length > 2) int.TryParse(args[2], out pid);
                    Installer.FinishUninstall(args.Length > 1 ? args[1] : null, pid);
                    return 0;
                }
                if (args.Length > 0 && args[0].Equals("/update", StringComparison.OrdinalIgnoreCase)) return FinishSelfUpdate(args);
                if (Path.GetFileName(self).StartsWith("DriverUpdaterSetup", StringComparison.OrdinalIgnoreCase)) { Settings.ApplyTheme(); new SetupWindow().Show(); return 0; }
                do
                {
                    Reopen = false;
                    Settings.ApplyTheme();
                    new MainWindow().Show();
                } while (Reopen);
                return 0;
            }
            catch (Exception ex)
            {
                Log.Write("FATAL: " + ex);
                Native.Ask("Driver Updater ran into a problem:\n\n" + ex.Message + "\n\nDetails were saved to:\n" + Paths.LogFile, "Driver Updater", Native.OK, Native.ERROR);
                return 1;
            }
        }
    }

    // ------------------------------------------------------------------ Setup window
    public class SetupWindow
    {
        dynamic win;
        dynamic E(string n) { return win.FindName(n); }
        object O(string n) { return (object)win.FindName(n); }
        string state = "options";
        string installedExe;

        public void Show()
        {
            win = Wpf.Load(Xaml.Setup);
            string existingDir = Installer.InstalledDir();
            string existingVer = Installer.InstalledVersion();
            E("TxtDir").Text = existingDir ?? Installer.DefaultDir;
            if (existingDir != null)
            {
                E("HeaderSub").Text = "Version " + App.Version + "  -  update the installed copy" + (existingVer != null ? " (" + existingVer + ")" : "");
                E("BtnInstall").Content = "Update";
            }
            else E("HeaderSub").Text = "Version " + App.Version + "  -  Setup";

            Wpf.On(O("BtnBrowse"), "Click", (s, e) =>
            {
                using (var dlg = new System.Windows.Forms.FolderBrowserDialog { Description = "Choose where to install Driver Updater", SelectedPath = (string)E("TxtDir").Text })
                {
                    if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        string p = dlg.SelectedPath;
                        if (!string.Equals(Path.GetFileName(p), Installer.AppName, StringComparison.OrdinalIgnoreCase)) p = Path.Combine(p, Installer.AppName);
                        E("TxtDir").Text = p;
                    }
                }
            });
            Wpf.On(O("BtnCancel"), "Click", (s, e) => win.Close());
            Wpf.On(O("BtnInstall"), "Click", (s, e) => OnInstallClick());
            Wpf.On((object)win, "Closing", (s, e) => { if (state == "installing") ((CancelEventArgs)e).Cancel = true; });
            Wpf.On((object)win, "ContentRendered", (s, e) => win.Activate());
            win.ShowDialog();
        }

        void OnInstallClick()
        {
            if (state == "done")
            {
                if ((bool)E("ChkLaunch").IsChecked && installedExe != null)
                {
                    try { Process.Start(new ProcessStartInfo(installedExe) { UseShellExecute = true }); }
                    catch (Exception ex)
                    {
                        Log.Write("Launch failed: " + ex.Message);
                        Native.Ask("Driver Updater is installed, but Windows blocked it from starting:\n" + ex.Message +
                            "\n\nIf this PC only allows apps with a trusted digital signature (Smart App Control, or a policy from your IT team), Driver Updater can't run here until it is signed.",
                            "Driver Updater Setup", Native.OK, Native.WARNING);
                    }
                }
                win.Close();
                return;
            }
            if (state != "options") return;
            string dir = ((string)E("TxtDir").Text ?? "").Trim();
            if (dir.Length == 0) { Native.Ask("Choose an install folder.", "Driver Updater Setup", Native.OK, Native.INFO); return; }
            if (Installer.AnotherCopyRunning() &&
                Native.Ask("Driver Updater is open. Close it and continue?", "Driver Updater Setup", Native.OKCANCEL, Native.WARNING) != Native.IDOK) return;

            bool desktop = (bool)E("ChkDesktop").IsChecked;
            state = "installing";
            E("PageOptions").Visibility = Wpf.Vis(false);
            E("PageProgress").Visibility = Wpf.Vis(true);
            E("BtnInstall").IsEnabled = false;
            E("BtnCancel").IsEnabled = false;
            Dispatcher disp = (Dispatcher)win.Dispatcher;
            var t = new Thread(() =>
            {
                string error = null, exe = null;
                try
                {
                    exe = Installer.Install(dir, desktop, msg => disp.BeginInvoke(new Action(() =>
                    {
                        E("ProgressText").Text = msg;
                        E("Progress").Value = (double)E("Progress").Value + 1;
                    })));
                }
                catch (Exception ex) { error = ex.Message; Log.Write("Install failed: " + ex); }
                disp.BeginInvoke(new Action(() => FinishInstall(exe, error)));
            }) { IsBackground = true };
            t.Start();
        }

        void FinishInstall(string exe, string error)
        {
            if (error != null)
            {
                state = "options";
                Native.Ask("Installation failed:\n" + error, "Driver Updater Setup", Native.OK, Native.ERROR);
                win.Close();
                return;
            }
            installedExe = exe;
            state = "done";
            E("Progress").Value = (double)E("Progress").Maximum;
            E("PageProgress").Visibility = Wpf.Vis(false);
            E("PageDone").Visibility = Wpf.Vis(true);
            E("BtnCancel").Visibility = Wpf.Vis(false);
            E("BtnInstall").Content = "Finish";
            E("BtnInstall").IsEnabled = true;
        }
    }

    // ------------------------------------------------------------------ Main window
    public class MainWindow
    {
        dynamic win;
        Dispatcher disp;
        dynamic E(string n) { return win.FindName(n); }
        object O(string n) { return (object)win.FindName(n); }

        readonly DriverList list = new DriverList();
        readonly ObservableCollection<object> tools = new ObservableCollection<object>();
        string mode = "loading";           // loading | ready | scanning | scanned | installing | tool
        bool busy;
        volatile bool cancel;
        DateTime? lastScan;
        string pcMaker = "";
        bool isArm = string.Equals(Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE"), "ARM64", StringComparison.OrdinalIgnoreCase)
                  || string.Equals(Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432"), "ARM64", StringComparison.OrdinalIgnoreCase);
        string osName = Environment.OSVersion.Version.Build >= 22000 ? "Windows 11" : "Windows 10";
        List<ToolDef> toolDefs;
        bool hasWinget;

        class ToolDef { public string Name, Desc, WingetId, Url; public bool Match; public string[] Look; }

        void Post(Action a) { disp.BeginInvoke(a); }
        void Footer(string text) { E("FooterStatus").Text = text ?? " "; }

        public void Show()
        {
            Log.Write("===== Driver Updater " + App.Version + " started =====");
            win = Wpf.Load(Xaml.Main);
            disp = (Dispatcher)win.Dispatcher;
            disp.UnhandledException += (s, e) =>
            {
                Log.Write("UI error: " + e.Exception);
                Native.Ask("Something went wrong:\n" + e.Exception.Message + "\n\nDetails were saved to the log.", "Driver Updater", Native.OK, Native.WARNING);
                e.Handled = true;
                SetBusy(false);
            };

            E("AboutVersion").Text = "Version " + App.Version;
            E("ListChanges").ItemsSource = Changelog.Entries(App.Version);
            E("ListDrivers").ItemsSource = list.Rows;
            list.Rows.CollectionChanged += (s, e) =>
            {
                if (e.NewItems == null) return;
                foreach (var o in e.NewItems)
                {
                    var row = o as DriverItem;
                    if (row != null) row.PropertyChanged += (o2, a) => { if (a.PropertyName == "IsChecked" && mode != "loading") Post(UpdateHeader); };
                }
            };
            E("ListTools").ItemsSource = tools;

            Wpf.On(O("NavDrivers"), "Click", (s, e) => ShowPage("Drivers"));
            Wpf.On(O("NavTools"), "Click", (s, e) => ShowPage("Tools"));
            Wpf.On(O("NavAbout"), "Click", (s, e) => ShowPage("About"));
            Wpf.On(O("BtnScan"), "Click", (s, e) =>
            {
                if (mode == "scanning") { cancel = true; SetScanButton(); }
                else if (!busy) StartScan();
            });
            Wpf.On(O("BtnUpdateAll"), "Click", (s, e) => { if (!busy) StartInstall(list.Rows.OfType<DriverItem>().Where(i => i.State == "Update" && i.IsChecked).ToList(), false); });
            Wpf.On(O("BtnRestore"), "Click", (s, e) => Sys.OpenUrl(Sys.SystemTool("rstrui.exe")));
            Wpf.On(O("BtnDevMgr"), "Click", (s, e) => Sys.OpenUrl("devmgmt.msc"));
            Wpf.On(O("BtnLog"), "Click", (s, e) => { Directory.CreateDirectory(Paths.WorkDir); Sys.OpenUrl(Paths.WorkDir); });
            Wpf.On(O("BtnExport"), "Click", (s, e) => Export());
            Wpf.On(O("BtnOldUsb"), "Click", (s, e) => { if (!busy) CleanOldUsb(); });
            Wpf.On(O("BtnUpdNow"), "Click", (s, e) => StartAppUpdate());
            Wpf.On(O("BtnUpdLater"), "Click", (s, e) => { if (appRelease != null) SelfUpdate.Dismiss(appRelease.Version); E("UpdateBanner").Visibility = Wpf.Vis(false); });
            Wpf.On(O("BtnUpdNotes"), "Click", (s, e) => ShowAppNotes());
            Wpf.On(O("BtnCheckApp"), "Click", (s, e) => CheckAppUpdate(true));
            Wpf.On(O("BtnThemeAuto"), "Click", (s, e) => SetAppearance("auto"));
            Wpf.On(O("BtnThemeLight"), "Click", (s, e) => SetAppearance("light"));
            Wpf.On(O("BtnThemeDark"), "Click", (s, e) => SetAppearance("dark"));
            ShowAppearance();
            Microsoft.Win32.UserPreferenceChangedEventHandler follow = (s, e) =>
            {
                if (Settings.Get("appearance", "auto") != "auto") return;
                if (Settings.SystemDark() == Theme.Dark) return;
                Post(() => { if (!busy) ReopenWindow(); });
            };
            Microsoft.Win32.SystemEvents.UserPreferenceChanged += follow;
            Wpf.On((object)win, "Closed", (s, e) => Microsoft.Win32.SystemEvents.UserPreferenceChanged -= follow);

            Action<object, EventArgs> cardClick = (s, e) =>
            {
                object src = ((dynamic)e).OriginalSource;
                if (!Wpf.IsButton(src) || busy) return;
                var item = ((dynamic)src).DataContext as DriverItem;
                if (item == null) return;
                string tag = ((dynamic)src).Tag as string;
                if (tag == "Update") StartInstall(new List<DriverItem> { item }, false);
                else if (tag == "Tool") InvokeTool(item.Index);
                else if (tag == "Reinstall") StartReinstall(item);
                else ShowPage("Tools");
            };
            Wpf.OnButtonClick(O("ListDrivers"), cardClick);
            Wpf.OnButtonClick(O("ListTools"), cardClick);

            Wpf.On((object)win, "Closing", (s, e) =>
            {
                if (busy && mode != "loading" &&
                    Native.Ask("A scan or install is still running. Close anyway?", "Driver Updater", Native.YESNO, Native.WARNING) != Native.IDYES)
                    ((CancelEventArgs)e).Cancel = true;
            });
            Wpf.On((object)win, "ContentRendered", (s, e) => win.Activate());

            ShowPage(reopenPage ?? "Drivers");
            reopenPage = null;
            SetScanButton();
            FitToScreen();
            StartInventory();          // starts reading drivers while the window is still opening
            CheckAppUpdate(false);
            win.ShowDialog();
            Log.Write("Driver Updater closed.");
        }

        /// <summary>Keeps the window inside the screen on small or high-scaling displays.</summary>
        void FitToScreen()
        {
            try
            {
                var sp = Type.GetType("System.Windows.SystemParameters, PresentationFramework, Version=4.0.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35", true);
                var area = (System.Windows.Rect)sp.GetProperty("WorkArea").GetValue(null, null);
                double w = Math.Min(1200, area.Width - 24), h = Math.Min(780, area.Height - 24);
                win.MinWidth = Math.Min(900.0, w); win.MinHeight = Math.Min(560.0, h);
                win.Width = w; win.Height = h;
            }
            catch (Exception ex) { Log.Write("FitToScreen: " + ex.Message); }
        }

        // ---------------------------------------------------------------- page + header
        static string reopenPage;
        string currentPage = "Drivers";

        void ShowPage(string name)
        {
            currentPage = name;
            foreach (var p in new[] { "Drivers", "Tools", "About" })
            {
                bool on = p == name;
                E("Page" + p).Visibility = Wpf.Vis(on);
                E("Nav" + p).Background = Wpf.Brush(on ? "#EFF6FF" : "#00FFFFFF");
                E("Nav" + p).Foreground = Wpf.Brush(on ? "#2563EB" : "#6B7280");
            }
        }

        void SetBusy(bool b)
        {
            busy = b; list.Busy = b;
            E("BtnUpdateAll").IsEnabled = !b;
            foreach (DriverItem t in tools) t.ButtonEnabled = !b;
            foreach (DriverItem i in list.Rows) if (i.State != "Queued" && i.State != "Installing") i.ButtonEnabled = !b;
            SetScanButton();
        }

        void SetScanButton()
        {
            dynamic btn = E("BtnScan");
            if (mode == "scanning")
            {
                btn.Content = cancel ? "Stopping..." : "Stop";
                btn.Style = win.FindResource("OutlineButton");
                btn.IsEnabled = !cancel;
            }
            else if (mode == "loading")
            {
                btn.Content = "Scan now"; btn.Style = win.FindResource("PrimaryButton"); btn.IsEnabled = false;
            }
            else
            {
                btn.Content = lastScan.HasValue ? "Scan again" : "Scan now";
                btn.Style = win.FindResource(lastScan.HasValue ? "OutlineButton" : "PrimaryButton");
                btn.IsEnabled = !busy;
            }
        }

        void UpdateHeader()
        {
            int nUpd = list.Count("Update", "Queued", "Installing", "Opened");
            int nOk = list.Count("Ok", "Installed", "Kept", "NotFor", "Reinstalled");
            int nMk = list.Count("Vendor");
            int nUnk = list.Count("Unknown");
            int total = list.Rows.Count;
            string suffix;
            if (mode == "loading" || mode == "ready") suffix = "";
            else if (mode == "scanning") suffix = nUpd > 0 ? ": " + Util.Plural(nUpd, "update") + " found so far" : ": checking...";
            else if (!lastScan.HasValue) suffix = "";
            else if (nUpd > 0) suffix = ": " + Util.Plural(nUpd, "update") + " available";
            else if (nUnk > 0) suffix = ": partly checked";
            else suffix = ": all up to date";
            E("CountRun").Text = suffix;
            E("CountRun").Foreground = Wpf.Brush(nUpd > 0 || mode == "scanning" ? "#DC2626" : nUnk > 0 ? "#B45309" : "#16A34A");

            bool scanned = lastScan.HasValue || mode == "scanning";
            E("ChipUpd").Visibility = Wpf.Vis(scanned);
            E("ChipOk").Visibility = Wpf.Vis(scanned);
            E("ChipMk").Visibility = Wpf.Vis(scanned && nMk > 0);
            E("ChipUnk").Visibility = Wpf.Vis(nUnk > 0);
            E("ChipUpdText").Text = Util.Plural(nUpd, "update");
            E("ChipOkText").Text = nOk + " up to date";
            E("ChipMkText").Text = nMk + " check with maker";
            E("ChipUnkText").Text = nUnk + " not checked";
            if (mode != "loading") E("ChipTotalText").Text = scanned ? total + " drivers in total" : total + " drivers  -  not scanned yet";

            E("LastScanText").Text = mode == "scanning" ? "Scanning now..."
                : lastScan.HasValue ? "Last scan: " + Util.FormatWhen(lastScan.Value, DateTime.Now) + "  (" + Util.Age(lastScan.Value, DateTime.Now) + ")"
                : "Never scanned";
            E("UpdateBadgeText").Text = nUpd.ToString(CultureInfo.InvariantCulture);
            E("UpdateBadge").Visibility = Wpf.Vis(nUpd > 0);
            int nUpdRows = list.Count("Update");
            int nSel = list.Rows.OfType<DriverItem>().Count(i => i.State == "Update" && i.IsChecked);
            E("BtnUpdateAll").Content = nSel == nUpdRows ? "Update All (" + nUpd + ")" : "Update selected (" + nSel + ")";
            E("BtnUpdateAll").Visibility = Wpf.Vis(nUpd > 0 && mode != "scanning");
        }

        void ScanBar(bool show, string title = null, string sub = null, string eta = "")
        {
            E("ScanBar").Visibility = Wpf.Vis(show);
            if (title != null) E("ScanBarTitle").Text = title;
            if (sub != null) E("ScanBarSub").Text = sub;
            if (eta != null) E("ScanEta").Text = eta;
        }

        void Progress(dynamic bar, int value, int max, bool indeterminate)
        {
            bar.IsIndeterminate = indeterminate;
            bar.Maximum = (double)Math.Max(1, max);
            bar.Value = (double)value;
        }

        void Run(Action work)
        {
            SetBusy(true);
            new Thread(() =>
            {
                try { work(); }
                catch (Exception ex) { Log.Write("Background task error: " + ex); Post(() => { Footer("Something went wrong: " + ex.Message); SetBusy(false); }); }
            }) { IsBackground = true }.Start();
        }

        // ---------------------------------------------------------------- startup: list drivers, show last scan
        void StartInventory()
        {
            mode = "loading";
            ScanBar(true, "Reading your drivers", "This takes a few seconds...", "");
            Progress(E("ScanProgress"), 0, 1, true);
            Run(() =>
            {
                pcMaker = Sys.PcMaker();
                List<DeviceInfo> all = null; string error = null;
                try { all = Inventory.Read(pcMaker); } catch (Exception ex) { error = ex.Message; Log.Write("Could not read drivers: " + ex); }
                string gpus = Sys.WmiAll("SELECT Name FROM Win32_VideoController", "Name");
                string cpu = Sys.Wmi1("SELECT Name FROM Win32_Processor", "Name");
                hasWinget = Sys.FindOnPath("winget.exe") != null;
                DateTime saveTime; List<ScanResult> saved;
                bool haveSaved = ScanStore.Load(out saveTime, out saved);
                Post(() =>
                {
                    BuildTools(gpus, cpu);
                    ScanBar(false);
                    SetBusy(false);
                    if (error != null) { E("ChipTotalText").Text = "Could not read drivers"; Footer("Could not read the driver list: " + error); }
                    var third = (all ?? new List<DeviceInfo>()).Where(d => !d.BuiltIn).OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
                    list.Inventory = third;
                    list.Reset("NotScanned");
                    bool loaded = haveSaved && list.ImportSaved(saved, DriverStore.LastBoot() > saveTime) > 0;
                    if (loaded) lastScan = saveTime;
                    mode = loaded ? "scanned" : "ready";
                    SetScanButton();
                    UpdateHeader();
                    if (error != null) return;
                    Log.Write("Found " + (all ?? new List<DeviceInfo>()).Count + " drivers, " + third.Count + " from hardware makers.");
                    if (loaded)
                    {
                        int pending = list.Count("Installed");
                        Footer(pending > 0
                            ? "Showing your last scan from " + Util.FormatWhen(saveTime, DateTime.Now) + ". " + pending + " driver(s) will finish installing when you restart your PC."
                            : "Showing your last scan from " + Util.FormatWhen(saveTime, DateTime.Now) + ". Click 'Scan again' to check for anything new.");
                    }
                    else Footer("Found " + third.Count + " drivers from hardware makers. Click 'Scan now' to check them for updates.");
                });
            });
        }

        // ---------------------------------------------------------------- scan
        void StartScan()
        {
            if (list.Inventory.Count == 0) return;
            list.Reset("Waiting");
            cancel = false;
            mode = "scanning";
            ScanBar(true, "Reading your drivers", "Checking what's installed right now...", "");
            Progress(E("ScanProgress"), 0, 1, true);
            E("ListScroll").ScrollToTop();
            UpdateHeader();
            Run(() =>
            {
                // Re-read installed drivers so updates made since the app opened (e.g. in the NVIDIA app) are seen
                var devices = list.Inventory.ToList();
                try
                {
                    var fresh = Inventory.Read(pcMaker).Where(d => !d.BuiltIn).OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
                    if (fresh.Count > 0) devices = fresh;
                }
                catch (Exception ex) { Log.Write("Could not re-read drivers, using the list from startup: " + ex.Message); }
                Post(() => { list.Inventory = devices; list.Reset("Waiting"); UpdateHeader(); });
                ScanWork(devices);
            });
        }

        void ScanWork(List<DeviceInfo> devices)
        {
            Log.Write("Scan started.");
            var started = DateTime.Now;
            bool win11 = osName == "Windows 11";
            var storeTask = System.Threading.Tasks.Task.Run(() => DriverStore.Versions());
            var kept = KeptStore.Load();
            Action<ScanResult> report = r =>
            {
                if (r.Kind == "Update")
                {
                    try { storeTask.Wait(15000); } catch { }
                    ScanResult.ApplyKeptTo(r, storeTask.IsCompleted && !storeTask.IsFaulted ? storeTask.Result : null, kept);
                }
                Log.Write("  " + r.Name + ": " + r.Kind + (string.IsNullOrEmpty(r.Source) ? "" : " (" + r.Source + (string.IsNullOrEmpty(r.Latest) ? "" : " " + r.Latest) + ")"));
                Post(() =>
            {
                if (list.Merge(r, true) && r.Kind == "Update" && list.TopCount == 1) E("ListScroll").ScrollToTop();
                UpdateHeader();
                });
            };

            // 1) Windows Update runs in the background the whole time (it is the slowest check)
            var wuTask = System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    foreach (var u in WindowsUpdate.FindDriverUpdates())
                    {
                        var dev = devices.FirstOrDefault(d => d.HardwareIds.Any(h => string.Equals(h, u.HardwareId, StringComparison.OrdinalIgnoreCase)));
                        report(new ScanResult
                        {
                            Key = dev != null ? dev.Key : "WU:" + u.UpdateId,
                            Name = dev != null ? dev.Name : u.Title,
                            Manufacturer = dev != null ? dev.Manufacturer : u.Manufacturer,
                            Class = dev != null ? dev.Class : u.DriverClass,
                            Installed = dev != null ? dev.Version : "",
                            Latest = Convert.ToString(Util.ParseVersion(u.Title), CultureInfo.InvariantCulture) ?? "",
                            Size = Util.FormatSize(u.Size), Source = "Windows Update", Kind = "Update", Title = u.Title, Ref = u.UpdateId,
                            Hint = dev != null ? dev.Hint : "", HardwareIds = dev != null ? string.Join("; ", dev.HardwareIds) : u.HardwareId
                        });
                    }
                    Log.Write("Windows Update check finished after " + (int)(DateTime.Now - started).TotalSeconds + " s.");
                }
                catch (Exception ex) { Log.Write("Windows Update check failed: " + ex.Message); }
            });

            // 2) Manufacturer sites, all at once (a few seconds)
            var checks = new List<KeyValuePair<string, Func<Dictionary<string, ScanResult>>>>();
            if (System.Text.RegularExpressions.Regex.IsMatch(pcMaker ?? "", "ASUS", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                checks.Add(new KeyValuePair<string, Func<Dictionary<string, ScanResult>>>("ASUS", () => VendorNet.CheckAsus(devices, win11)));
            if (System.Text.RegularExpressions.Regex.IsMatch(pcMaker ?? "", "Dell|Alienware", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                checks.Add(new KeyValuePair<string, Func<Dictionary<string, ScanResult>>>("Dell", () => VendorNet.CheckDell(devices, win11)));
            checks.Add(new KeyValuePair<string, Func<Dictionary<string, ScanResult>>>("NVIDIA", () => VendorNet.CheckNvidia(devices, win11)));
            checks.Add(new KeyValuePair<string, Func<Dictionary<string, ScanResult>>>("Intel", () => VendorNet.CheckIntel(devices)));
            string makers = string.Join(", ", checks.Select(c => c.Key));
            int lastComma = makers.LastIndexOf(", ", StringComparison.Ordinal);
            if (lastComma > 0) makers = makers.Substring(0, lastComma) + " and " + makers.Substring(lastComma + 2);
            Post(() => { ScanBar(true, "Checking manufacturer websites", makers + "...", ""); Progress(E("ScanProgress"), 0, 1, true); });
            var vendorTasks = checks.Select(c => System.Threading.Tasks.Task.Run(() =>
            {
                try { return c.Value(); }
                catch (Exception ex) { Log.Write(c.Key + " check failed: " + ex.Message); return new Dictionary<string, ScanResult>(); }
            })).ToArray();
            System.Threading.Tasks.Task.WaitAll(vendorTasks, TimeSpan.FromSeconds(45));
            var vendorUpdated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);   // a maker answered for these drivers
            foreach (var t in vendorTasks.Where(t => t.Status == System.Threading.Tasks.TaskStatus.RanToCompletion))
                foreach (var r in t.Result.Values) { vendorUpdated.Add(r.Key); report(r); }
            Log.Write("Manufacturer checks finished after " + (int)(DateTime.Now - started).TotalSeconds + " s.");

            // 3) Microsoft Update Catalog, one driver at a time (paced so Microsoft doesn't refuse)
            var catalog = new CatalogClient();
            catalog.Cancelled = () => cancel;
            catalog.Status = msg => Post(() => E("ScanBarSub").Text = msg);
            catalog.LoadCache();
            var todo = devices.Where(d => !vendorUpdated.Contains(d.Key)).ToList();
            var refused = new List<DeviceInfo>();
            DateTime start = DateTime.Now;
            for (int i = 0; i < todo.Count; i++)
            {
                if (cancel) break;
                var dev = todo[i];
                int n = i + 1;
                string eta = "";
                if (n >= 3)
                {
                    double per = (DateTime.Now - start).TotalSeconds / (n - 1);
                    int left = (int)(per * (todo.Count - n + 1));
                    eta = left >= 90 ? "about " + Math.Round(left / 60.0) + " min left" : left >= 20 ? "about 1 min left" : "almost done";
                }
                Post(() =>
                {
                    ScanBar(true, "Checking " + n + " of " + todo.Count, dev.Name, eta);
                    Progress(E("ScanProgress"), n, todo.Count, false);
                    DriverItem it;
                    if (list.ByKey.TryGetValue(dev.Key, out it) && it.State == "Waiting") it.ApplyState("Checking", null, busy);
                });
                var res = CatalogResult(catalog, dev);
                if (cancel && res.Kind == "Unknown") break;
                if (res.Kind == "Unknown") refused.Add(dev);
                report(res);
            }
            // One quick retry for drivers Microsoft refused, after a short pause
            if (refused.Count > 0 && !cancel)
            {
                catalog.PrepareRetry(20);
                for (int i = 0; i < refused.Count && !cancel; i++)
                {
                    var dev = refused[i]; int n = i + 1;
                    Post(() => ScanBar(true, "Retrying " + n + " of " + refused.Count, dev.Name, ""));
                    report(CatalogResult(catalog, dev));
                }
            }
            catalog.SaveCache();
            Log.Write("Catalog checks finished after " + (int)(DateTime.Now - started).TotalSeconds + " s (" + catalog.Requests + " web requests, rest from cache).");

            // 4) Wait for Windows Update if it is still going
            if (!wuTask.IsCompleted && !cancel)
            {
                Post(() => { ScanBar(true, "Finishing the Windows Update check", "Windows is still answering - almost done...", ""); Progress(E("ScanProgress"), 0, 1, true); });
                while (!wuTask.Wait(500) && !cancel) { }
            }
            bool wasCancelled = cancel;
            Log.Write("Scan took " + (int)(DateTime.Now - started).TotalSeconds + " s.");
            Post(() => CompleteScan(wasCancelled));
        }

        ScanResult CatalogResult(CatalogClient catalog, DeviceInfo dev)
        {
            bool checkedOk = true;
            CatalogEntry hit = null;
            if (dev.HardwareIds.Length > 0) hit = catalog.Find(dev, isArm, osName, out checkedOk);
            var cur = Util.ParseVersion(dev.Version);
            var res = new ScanResult
            {
                Key = dev.Key, Name = dev.Name, Manufacturer = dev.Manufacturer, Class = dev.Class, Installed = dev.Version,
                Latest = "", Size = "", Source = "", Kind = checkedOk ? "Vendor" : "Unknown", Title = "", Ref = "",
                Hint = dev.Hint, HardwareIds = string.Join("; ", dev.HardwareIds)
            };
            if (hit != null)
            {
                var nv = Util.ParseVersion(hit.Version);
                res.Latest = nv != null ? nv.ToString() : hit.Version;
                res.Source = "Microsoft Update Catalog";
                if (nv != null && cur != null && nv > cur) { res.Kind = "Update"; res.Title = hit.Title; res.Ref = hit.Guid; res.Size = hit.Size; }
                else res.Kind = "Ok";
            }
            return res;
        }

        void CompleteScan(bool wasCancelled)
        {
            foreach (DriverItem it in list.Rows)
            {
                it.NewVisibility = "Collapsed";
                if (it.State == "Waiting" || it.State == "Checking") it.ApplyState("NotScanned", null, false);
            }
            ScanBar(false);
            lastScan = DateTime.Now;
            mode = "scanned";
            SetBusy(false);
            UpdateHeader();
            int nUpd = list.Count("Update"), nUnk = list.Count("Unknown");
            string when = lastScan.Value.ToString("HH:mm", CultureInfo.InvariantCulture);
            if (wasCancelled) Footer("Scan stopped at " + when + ". Drivers that weren't checked show 'Not scanned yet'.");
            else if (nUnk > 0) Footer("Scanned at " + when + ". Microsoft's catalog is limiting searches, so " + nUnk + " driver(s) weren't checked - scan again in about 15 minutes (checked drivers are remembered).");
            else if (nUpd > 0) Footer("Scanned at " + when + ". Updates are at the top. Every install creates a System Restore point first.");
            else Footer("Scanned at " + when + ". No updates from Microsoft - see Tools for drivers your hardware makers publish first.");
            Log.Write("Scan finished: " + nUpd + " updates, " + nUnk + " not checked, cancelled=" + wasCancelled + ".");
            ScanStore.Save(lastScan.Value, list.Data.Values);
        }

        // ---------------------------------------------------------------- install
        void StartInstall(List<DriverItem> targets, bool skipRestore)
        {
            targets = targets.Where(t => t.State == "Update").ToList();
            if (targets.Count == 0) { Native.Ask("Tick at least one driver to update.", "Driver Updater", Native.OK, Native.INFO); return; }
            var firmware = targets.Where(t => list.Data[t.Key].IsFirmware).ToList();
            if (firmware.Count > 0)
            {
                bool onBattery = false;
                try { onBattery = System.Windows.Forms.SystemInformation.PowerStatus.PowerLineStatus == System.Windows.Forms.PowerLineStatus.Offline; } catch { }
                if (onBattery)
                {
                    Native.Ask("Plug in the charger before installing a BIOS / firmware update.\n\nThe PC refuses to update its firmware on battery, and a power cut during the update can leave it unable to start." +
                               (targets.Count > firmware.Count ? "\n\nThe other drivers will be installed now." : ""), "Driver Updater", Native.OK, Native.WARNING);
                    targets = targets.Except(firmware).ToList();
                    if (targets.Count == 0) return;
                    firmware.Clear();
                }
            }
            if (!skipRestore)
            {
                string names = string.Join("\n", targets.Take(8).Select(t => "  - " + t.Name));
                if (firmware.Count > 0) names += "\n\nIncludes a BIOS / firmware update: Windows prepares it now and the PC finishes it during the next restart " +
                                                 "(the screen may stay dark for a few minutes - don't turn it off). If BitLocker is on, keep your recovery key handy.";
                if (targets.Count > 8) names += "\n  - and " + (targets.Count - 8) + " more";
                if (Native.Ask("Install " + targets.Count + " driver update(s)?\n\n" + names + "\n\nA System Restore point will be created first.", "Driver Updater", Native.YESNO, Native.QUESTION) != Native.IDYES) return;
            }
            mode = "installing";
            foreach (var t in targets) t.ApplyState("Queued", list.Data[t.Key], true);
            dynamic bar = E("InstallProgress");
            bar.Visibility = Wpf.Vis(true);
            Progress(bar, 0, targets.Count + 1, false);
            ShowPage("Drivers");
            UpdateHeader();
            var jobs = targets.Select(t => list.Data[t.Key]).ToList();
            var results = new Dictionary<string, InstallOutcome>(StringComparer.OrdinalIgnoreCase);
            Run(() =>
            {
                if (!skipRestore)
                {
                    Post(() => Footer("Creating a System Restore point..."));
                    Log.Write("Creating a System Restore point...");
                    string why = RestorePoint.Create(240, s => Post(() => Footer("Creating a System Restore point... (" + s + " s)")));
                    if (why != null)
                    {
                        Log.Write("Restore point failed: " + why);
                        Post(() => RestoreFailed(targets, why));
                        return;
                    }
                    Log.Write("Restore point created.");
                }
                int step = 1;
                Post(() => Progress(E("InstallProgress"), 1, targets.Count + 1, false));

                // Network adapters last: reinstalling one briefly drops the connection
                jobs = jobs.OrderBy(j => string.Equals(j.Class, "NET", StringComparison.OrdinalIgnoreCase) ? 1 : 0).ToList();
                // Download everything first, so a connection drop during an install can't break the others
                var prefetchErrors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var j in jobs.Where(x => x.Source != "Windows Update"))
                {
                    var pj = j;
                    try
                    {
                        Action<string> say = msg => Post(() => Footer(msg));
                        if (pj.Source == "Microsoft Update Catalog") CatalogClient.Prefetch(pj.Ref, pj.Name, say);
                        else VendorNet.Prefetch(pj, say);
                    }
                    catch (Exception ex) { prefetchErrors[pj.Key] = ex.Message; Log.Write("Download failed for " + pj.Name + ": " + ex.Message); }
                }
                foreach (var j in jobs)
                {
                    var job = j;
                    Post(() => { DriverItem it; if (list.ByKey.TryGetValue(job.Key, out it)) it.ApplyState("Installing", job, true); });
                    InstallOutcome res;
                    string preErr;
                    if (prefetchErrors.TryGetValue(job.Key, out preErr))
                    {
                        results[job.Key] = InstallOutcome.Of(false, "Download failed: " + preErr);
                        step++; int sx = step;
                        Post(() => Progress(E("InstallProgress"), sx, targets.Count + 1, false));
                        continue;
                    }
                    try
                    {
                        Action<string> say = msg => Post(() => Footer(msg));
                        if (job.Source == "Windows Update")
                        {
                            say("Downloading and installing: " + job.Name);
                            var t = WindowsUpdate.Install(job.Ref); res = InstallOutcome.Of(t.Item1, t.Item2, t.Item3);
                        }
                        else if (job.Source == "Microsoft Update Catalog")
                        {
                            var t = CatalogClient.Install(job.Ref, job.Name, say); res = InstallOutcome.Of(t.Item1, t.Item2, t.Item3);
                        }
                        else res = VendorNet.Install(job, say);
                    }
                    catch (Exception ex) { Log.Write("Install failed for " + job.Name + ": " + ex); res = InstallOutcome.Of(false, "Failed: " + ex.Message); }
                    // Did Windows actually start using it? If not (and no restart is pending), Windows prefers the current driver.
                    if (res.Ok && !res.Reboot && !res.Opened)
                    {
                        var now = Util.ParseVersion(Sys.DriverVersionOf(job.Key));
                        var want = Util.ParseVersion(job.Latest);
                        if (now != null && want != null && now < want)
                        {
                            KeptStore.Add(job.Key, job.Latest);
                            res = InstallOutcome.Of(false, "Windows kept the current driver");
                            res.Kept = true;
                        }
                    }
                    Log.Write(job.Name + " (" + job.Source + "): " + res.Message + (res.Reboot ? " (restart needed)" : ""));
                    results[job.Key] = res;
                    step++;
                    int s2 = step;
                    Post(() => Progress(E("InstallProgress"), s2, targets.Count + 1, false));
                }
                Post(() => CompleteInstall(targets, results));
            });
        }

        void RestoreFailed(List<DriverItem> targets, string why)
        {
            E("InstallProgress").Visibility = Wpf.VisName("Hidden");
            mode = "scanned";
            SetBusy(false);
            foreach (var t in targets) t.ApplyState("Update", list.Data[t.Key], false);
            UpdateHeader();
            Footer("No restore point: " + why);
            if (Native.Ask("A System Restore point could not be created:\n" + why + "\n\nInstall anyway, without a restore point?", "Driver Updater", Native.YESNO, Native.WARNING) == Native.IDYES)
                StartInstall(targets, true);
        }

        void CompleteInstall(List<DriverItem> targets, Dictionary<string, InstallOutcome> results)
        {
            E("InstallProgress").Visibility = Wpf.VisName("Hidden");
            mode = "scanned";
            SetBusy(false);
            int ok = 0, fail = 0, opened = 0;
            var needRestart = new List<DriverItem>();
            foreach (var t in targets)
            {
                var d = list.Data[t.Key];
                InstallOutcome r;
                if (results.TryGetValue(t.Key, out r) && r.Opened)
                {
                    opened++;
                    t.ApplyState("Opened", d, false);
                }
                else if (r != null && r.Ok)
                {
                    ok++;
                    d.Reboot = r.Reboot;
                    if (r.Reboot) { d.Kind = "Installed"; needRestart.Add(t); }
                    else { d.Kind = "Ok"; d.Installed = d.Latest; t.Installed = d.Latest; }
                    t.ApplyState("Installed", d, false);
                }
                else if (r != null && r.NotForDevice)
                {
                    KeptStore.Add(t.Key, "x:" + d.Latest);
                    d.Kind = "NotFor";
                    t.ApplyState("NotFor", d, false);
                }
                else if (r != null && r.Kept)
                {
                    d.Kind = "Kept";
                    t.ApplyState("Kept", d, false);
                }
                else
                {
                    fail++;
                    t.ApplyState("Update", d, false);
                    t.SetTag("Not installed", "#FEE2E2", "#B91C1C");
                    if (r != null && !string.IsNullOrEmpty(r.Message)) t.Size = r.Message.Length > 160 ? r.Message.Substring(0, 157) + "..." : r.Message;
                }
            }
            UpdateHeader();
            if (lastScan.HasValue) ScanStore.Save(lastScan.Value, list.Data.Values);
            string msg = ok + " installed, " + fail + " not installed" + (opened > 0 ? ", " + opened + " manufacturer installer(s) opened" : "") + ". Details are in the log (Tools > Open log folder).";
            if (needRestart.Count > 0) msg += " " + needRestart.Count + " need a restart to finish.";
            Footer(msg);
            if (needRestart.Count > 0)
            {
                string names = string.Join("\n", needRestart.Take(6).Select(t => "  - " + t.Name));
                if (Native.Ask(ok + " driver(s) installed.\n\nWindows needs a restart to finish installing:\n" + names +
                    "\n\nThe old driver keeps working until then. Restart now?\n(Choose No to restart later yourself.)", "Driver Updater", Native.YESNO, Native.QUESTION) == Native.IDYES)
                    Sys.Restart();
            }
            else Native.Ask(ok + " driver(s) installed, " + fail + " not installed." +
                (opened > 0 ? "\n\n" + opened + " manufacturer installer(s) opened in their own window. Follow the steps there to finish, then click 'Scan again'." : ""),
                "Driver Updater", Native.OK, Native.INFO);
        }

        // ---------------------------------------------------------------- manufacturer tools
        void BuildTools(string gpus, string cpu)
        {
            Func<int, string> g = c => ((char)c).ToString();
            Func<string, string, bool> m = (text, pattern) => System.Text.RegularExpressions.Regex.IsMatch(text ?? "", pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            toolDefs = new List<ToolDef>
            {
                new ToolDef { Name = "Intel Driver & Support Assistant", Desc = "Newest Intel graphics, Wi-Fi, Bluetooth, NPU (AI Boost) and chipset drivers, straight from Intel.", WingetId = "Intel.IntelDriverAndSupportAssistant", Url = "https://www.intel.com/content/www/us/en/support/detect.html", Match = m(gpus + " " + cpu, "Intel"), Look = new[] { g(0xE950), "#E0F2FE", "#0369A1" } },
                new ToolDef { Name = "NVIDIA app", Desc = "Game Ready and Studio drivers for NVIDIA GeForce graphics.", Url = "https://www.nvidia.com/en-us/software/nvidia-app/", Match = m(gpus, "NVIDIA"), Look = new[] { g(0xE7F4), "#ECFDF5", "#15803D" } },
                new ToolDef { Name = "MyASUS / Armoury Crate", Desc = "ASUS-tuned audio, power, fan and platform drivers made for your exact laptop model.", Url = "https://www.asus.com/support/download-center/", Match = m(pcMaker, "ASUS"), Look = new[] { g(0xE7F8), "#F1F5F9", "#0F172A" } },
                new ToolDef { Name = "AMD Software: Adrenalin Edition", Desc = "Drivers for AMD Radeon graphics and Ryzen chipsets.", Url = "https://www.amd.com/en/support/download/drivers.html", Match = m(gpus + " " + cpu, "AMD|Radeon"), Look = new[] { g(0xE7F4), "#FEF2F2", "#B91C1C" } },
                new ToolDef { Name = "Dell SupportAssist", Desc = "Drivers and BIOS updates for Dell and Alienware PCs.", Url = "https://www.dell.com/support/home/drivers", Match = m(pcMaker, "Dell|Alienware"), Look = new[] { g(0xE7F8), "#EFF6FF", "#1D4ED8" } },
                new ToolDef { Name = "HP Support Assistant", Desc = "Drivers and BIOS updates for HP PCs.", Url = "https://support.hp.com/drivers", Match = m(pcMaker, "HP|Hewlett"), Look = new[] { g(0xE7F8), "#EFF6FF", "#0369A1" } },
                new ToolDef { Name = "Lenovo Vantage", Desc = "Drivers and BIOS updates for Lenovo PCs.", Url = "https://pcsupport.lenovo.com", Match = m(pcMaker, "Lenovo"), Look = new[] { g(0xE7F8), "#FEF2F2", "#B91C1C" } },
            }.OrderBy(t => t.Match ? 0 : 1).ToList();
            E("ListSources").ItemsSource = SourceInfo.All(pcMaker, gpus, cpu);
            tools.Clear();
            for (int i = 0; i < toolDefs.Count; i++)
            {
                var d = toolDefs[i];
                tools.Add(new DriverItem
                {
                    Index = i, Name = d.Name, Source = d.Desc, Kind = "Tool", Glyph = d.Look[0], IconBack = d.Look[1], IconFore = d.Look[2],
                    PillVisibility = d.Match ? "Visible" : "Collapsed",
                    ButtonText = d.WingetId != null && hasWinget ? "Install" : "Download"
                });
            }
        }

        void InvokeTool(int index)
        {
            var d = toolDefs[index];
            if (d.WingetId != null && hasWinget)
            {
                int ans = Native.Ask("Install " + d.Name + " now?\n\nIt comes from the official Windows Package Manager (winget) source. Choose No to open its download page instead.", "Driver Updater", Native.YESNOCANCEL, Native.QUESTION);
                if (ans == Native.IDCANCEL) return;
                if (ans == Native.IDYES)
                {
                    string prev = mode;
                    mode = "tool";
                    dynamic bar = E("InstallProgress");
                    bar.Visibility = Wpf.Vis(true);
                    Progress(bar, 0, 1, true);
                    Footer("Installing " + d.Name + "...");
                    Run(() =>
                    {
                        int code = -1;
                        try { code = Sys.Run(Sys.FindOnPath("winget.exe") ?? "winget.exe", "install --id " + d.WingetId + " --exact --source winget --silent --accept-package-agreements --accept-source-agreements"); }
                        catch (Exception ex) { Log.Write("winget failed: " + ex.Message); }
                        Post(() =>
                        {
                            mode = prev;
                            E("InstallProgress").Visibility = Wpf.VisName("Hidden");
                            SetBusy(false);
                            if (code == 0)
                            {
                                ((DriverItem)tools[index]).ButtonText = "Installed";
                                Footer(d.Name + " installed.");
                                Native.Ask(d.Name + " is installed.\n\nOpen it from the Start menu and run its scan to get the newest drivers.", "Driver Updater", Native.OK, Native.INFO);
                            }
                            else if (Native.Ask("The automatic install didn't finish (code " + code + "). Open the download page instead?", "Driver Updater", Native.YESNO, Native.WARNING) == Native.IDYES)
                                Sys.OpenUrl(d.Url);
                        });
                    });
                    return;
                }
            }
            Sys.OpenUrl(d.Url);
        }

        // ---------------------------------------------------------------- appearance
        void ShowAppearance()
        {
            string pref = Settings.Get("appearance", "auto");
            foreach (var kv in new[] { new[] { "auto", "BtnThemeAuto" }, new[] { "light", "BtnThemeLight" }, new[] { "dark", "BtnThemeDark" } })
                E(kv[1]).Style = win.FindResource(kv[0] == pref ? "PrimaryButton" : "OutlineButton");
        }

        void SetAppearance(string pref)
        {
            Settings.Set("appearance", pref);
            ShowAppearance();
            if (Theme.ShouldBeDark(pref, Settings.SystemDark()) == Theme.Dark) return;
            if (busy) { Native.Ask("The new look is applied when the current scan or install finishes and you reopen Driver Updater.", "Driver Updater", Native.OK, Native.INFO); return; }
            ReopenWindow();
        }

        /// <summary>Rebuilds the window (same app, no restart): used to switch between light and dark.</summary>
        void ReopenWindow()
        {
            if (lastScan.HasValue) ScanStore.Save(lastScan.Value, list.Data.Values);
            App.Reopen = true;
            reopenPage = currentPage;
            win.Close();
        }

        // ---------------------------------------------------------------- app updates
        AppRelease appRelease;
        bool appUpdating;

        void CheckAppUpdate(bool manual)
        {
            if (manual) E("BtnCheckApp").IsEnabled = false;
            new Thread(() =>
            {
                AppRelease r = null;
                try { r = SelfUpdate.Check(manual); } catch (Exception ex) { Log.Write("App update check error: " + ex.Message); }
                Post(() =>
                {
                    if (manual) E("BtnCheckApp").IsEnabled = true;
                    bool newer = r != null && AppRelease.IsNewer(r.Version, App.Version);
                    if (newer && (manual || r.Version != SelfUpdate.Dismissed))
                    {
                        appRelease = r;
                        E("UpdateBannerText").Text = "Driver Updater " + r.Version + " is available (you have " + App.Version + ").";
                        E("UpdateBannerButtons").Visibility = Wpf.Vis(true);
                        E("UpdateBanner").Visibility = Wpf.Vis(true);
                        if (manual) { ShowPage("Drivers"); }
                    }
                    else if (manual)
                    {
                        if (r == null) Native.Ask("Couldn't check for updates right now. Check the internet connection, or see the releases page:\n" + SelfUpdate.ReleasesPage, "Driver Updater", Native.OK, Native.INFO);
                        else Native.Ask("You have the latest version (" + App.Version + ").", "Driver Updater", Native.OK, Native.INFO);
                    }
                });
            }) { IsBackground = true }.Start();
        }

        void ShowAppNotes()
        {
            if (appRelease == null) return;
            string notes = AppRelease.PlainNotes(appRelease.Notes);
            if (Native.Ask("What's new in Driver Updater " + appRelease.Version + ":\n\n" + (notes.Length > 0 ? notes : "(No notes were published for this version.)") +
                           "\n\nUpdate now?", "Driver Updater " + appRelease.Version, Native.YESNO, Native.INFO) == Native.IDYES)
                StartAppUpdate();
        }

        void StartAppUpdate()
        {
            if (appRelease == null || appUpdating) return;
            if (busy) { Native.Ask("Please wait until the current scan or install finishes, then click Update now.", "Driver Updater", Native.OK, Native.INFO); return; }
            var r = appRelease;
            appUpdating = true;
            E("UpdateBannerButtons").Visibility = Wpf.Vis(false);
            E("UpdateBannerText").Text = "Downloading Driver Updater " + r.Version + "...";
            Run(() =>
            {
                string file = null, error = null;
                try { file = SelfUpdate.Download(r, pct => Post(() => E("UpdateBannerText").Text = "Downloading Driver Updater " + r.Version + "...  " + pct + "%")); }
                catch (Exception ex) { error = ex.Message; Log.Write("App update download failed: " + ex); }
                Post(() =>
                {
                    SetBusy(false);
                    appUpdating = false;
                    if (file == null)
                    {
                        E("UpdateBannerText").Text = "Driver Updater " + r.Version + " is available (you have " + App.Version + ").";
                        E("UpdateBannerButtons").Visibility = Wpf.Vis(true);
                        if (Native.Ask("The update couldn't be downloaded:\n" + error + "\n\nOpen the releases page to download it yourself?", "Driver Updater", Native.YESNO, Native.WARNING) == Native.IDYES)
                            Sys.OpenUrl(r.PageUrl.Length > 0 ? r.PageUrl : SelfUpdate.ReleasesPage);
                        return;
                    }
                    E("UpdateBannerText").Text = "Installing Driver Updater " + r.Version + "...";
                    try
                    {
                        if (lastScan.HasValue) ScanStore.Save(lastScan.Value, list.Data.Values);
                        SelfUpdate.Apply(file);
                        Log.Write("Handing over to the " + r.Version + " installer");
                        Environment.Exit(0);
                    }
                    catch (Exception ex)
                    {
                        Log.Write("Couldn't start the update: " + ex);
                        Native.Ask("Couldn't start the update:\n" + ex.Message + "\n\nYou can run it yourself:\n" + file, "Driver Updater", Native.OK, Native.WARNING);
                        E("UpdateBannerButtons").Visibility = Wpf.Vis(true);
                    }
                });
            });
        }

        // ---------------------------------------------------------------- reinstall
        void StartReinstall(DriverItem item)
        {
            var dev = list.Inventory.FirstOrDefault(d => string.Equals(d.Key, item.Key, StringComparison.OrdinalIgnoreCase));
            string cls = dev != null ? dev.Class : "";
            string note = string.Equals(cls, "DISPLAY", StringComparison.OrdinalIgnoreCase) ? "\n\nYour screen may go black or flicker for a few seconds."
                        : string.Equals(cls, "NET", StringComparison.OrdinalIgnoreCase) ? "\n\nThe connection on this adapter will drop for a few seconds."
                        : string.Equals(cls, "MEDIA", StringComparison.OrdinalIgnoreCase) || string.Equals(cls, "AUDIOENDPOINT", StringComparison.OrdinalIgnoreCase) ? "\n\nSound from this device will stop for a moment."
                        : "";
            if (Native.Ask("Reinstall the driver for " + item.Name + "?\n\nThe same version (" + item.Installed + ") is installed again from the copy Windows already keeps - nothing is downloaded. " +
                           "This often fixes a device that stopped working properly." + note, "Reinstall driver", Native.YESNO, Native.QUESTION) != Native.IDYES) return;

            ScanResult saved; list.Data.TryGetValue(item.Key, out saved);
            string prevState = item.State, prev = mode;
            mode = "installing";
            item.ApplyState("Checking", saved, true);
            item.SetTag("Reinstalling...", "#DBEAFE", "#1D4ED8");
            dynamic bar = E("InstallProgress");
            bar.Visibility = Wpf.Vis(true);
            Progress(bar, 0, 1, true);
            Footer("Reinstalling " + item.Name + "...");
            Run(() =>
            {
                Reinstaller.Result res;
                try { res = Reinstaller.Run(item.Key, cls); }
                catch (Exception ex) { Log.Write("Reinstall failed: " + ex); res = new Reinstaller.Result { Message = ex.Message }; }
                string ver = Sys.DriverVersionOf(item.Key);
                Post(() =>
                {
                    mode = prev;
                    E("InstallProgress").Visibility = Wpf.VisName("Hidden");
                    SetBusy(false);
                    if (res.Ok)
                    {
                        if (ver.Length > 0) item.Installed = ver;
                        item.ApplyState("Reinstalled", new ScanResult { Key = item.Key, Name = item.Name, Reboot = res.Reboot }, false);
                        UpdateHeader();
                        Footer(item.Name + " reinstalled." + (res.Reboot ? " Restart to finish." : ""));
                        if (res.Reboot && Native.Ask(item.Name + " was reinstalled. Windows needs a restart to finish.\n\nRestart now?", "Reinstall driver", Native.YESNO, Native.QUESTION) == Native.IDYES)
                            Sys.Restart();
                    }
                    else
                    {
                        item.ApplyState(prevState, saved, false);
                        Footer("Couldn't reinstall " + item.Name + ".");
                        Native.Ask("Couldn't reinstall " + item.Name + ".\n\n" + res.Message + "\n\nNothing was changed. Details are in the log.", "Reinstall driver", Native.OK, Native.WARNING);
                    }
                });
            });
        }

        // ---------------------------------------------------------------- old USB devices
        void CleanOldUsb()
        {
            string prev = mode;
            mode = "tool";
            dynamic bar = E("InstallProgress");
            bar.Visibility = Wpf.Vis(true);
            Progress(bar, 0, 1, true);
            Footer("Looking for USB devices that aren't plugged in...");
            Run(() =>
            {
                var found = OldDevices.Find();
                Log.Write("Old USB devices found: " + found.Count);
                Post(() =>
                {
                    Action done = () => { mode = prev; E("InstallProgress").Visibility = Wpf.VisName("Hidden"); SetBusy(false); };
                    if (found.Count == 0)
                    {
                        done(); Footer("No old USB devices found.");
                        Native.Ask("Windows isn't remembering any USB devices that are unplugged right now. Nothing to clear.", "Driver Updater", Native.OK, Native.INFO);
                        return;
                    }
                    var names = found.Select(f => f.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                    string shown = string.Join("\n", names.Take(15).Select(n => "  \u2022 " + n)) + (names.Count > 15 ? "\n  ...and " + (names.Count - 15) + " more" : "");
                    int ans = Native.Ask("Windows remembers " + Util.Plural(found.Count, "USB device") + " that aren't plugged in now:\n\n" + shown +
                        "\n\nRemove them? Devices that are plugged in are never touched. Anything you plug in again is set up automatically, " +
                        "though it may get a new drive letter or COM port number.", "Remove old USB devices", Native.YESNO, Native.QUESTION);
                    if (ans != Native.IDYES) { done(); Footer(" "); return; }
                    Progress(bar, 0, found.Count, false);
                    Footer("Removing old USB devices...");
                    Run(() =>
                    {
                        int ok = 0, n = 0;
                        foreach (var f in found)
                        {
                            if (OldDevices.Remove(f.Id)) ok++;
                            int k = ++n;
                            Post(() => Progress(E("InstallProgress"), k, found.Count, false));
                        }
                        Log.Write("Old USB devices removed: " + ok + " of " + found.Count);
                        Post(() =>
                        {
                            done();
                            Footer("Removed " + Util.Plural(ok, "old USB device") + ".");
                            string msg = ok == found.Count ? "Done - removed " + Util.Plural(ok, "old USB device") + "."
                                       : "Removed " + ok + " of " + found.Count + ". Windows kept the rest (details are in the log).";
                            if (Native.Ask(msg + "\n\nScan again now? Updates offered only for those old devices should disappear.", "Remove old USB devices", Native.YESNO, Native.INFO) == Native.IDYES)
                            { ShowPage("Drivers"); StartScan(); }
                        });
                    });
                });
            });
        }

        // ---------------------------------------------------------------- export
        void Export()
        {
            if (list.Rows.Count == 0) { Native.Ask("The driver list is still loading.", "Driver Updater", Native.OK, Native.INFO); return; }
            using (var dlg = new System.Windows.Forms.SaveFileDialog { Filter = "CSV file (*.csv)|*.csv", FileName = "Drivers_" + DateTime.Now.ToString("yyyy-MM-dd") + ".csv" })
            {
                if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                var inv = list.Inventory.ToDictionary(d => d.Key, StringComparer.OrdinalIgnoreCase);
                Func<string, string> q = s => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
                var lines = new List<string> { "Device,Manufacturer,InstalledVersion,LatestFromMicrosoft,Status,Source,ManufacturerTool,HardwareIDs" };
                foreach (DriverItem it in list.Rows)
                {
                    ScanResult r; list.Data.TryGetValue(it.Key, out r);
                    DeviceInfo d; inv.TryGetValue(it.Key, out d);
                    lines.Add(string.Join(",", new[] {
                        q(it.Name), q(it.Manufacturer), q(it.Installed), q(r != null ? r.Latest : ""), q(it.TagText), q(r != null ? r.Source : ""),
                        q(r != null ? r.Hint : d != null ? d.Hint : ""), q(r != null ? r.HardwareIds : d != null ? string.Join("; ", d.HardwareIds) : "") }));
                }
                File.WriteAllLines(dlg.FileName, lines, new System.Text.UTF8Encoding(true));
                Footer("Saved driver list to " + dlg.FileName);
            }
        }
    }
}
