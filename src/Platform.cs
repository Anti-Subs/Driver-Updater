// Driver Updater - Windows-specific services (WMI, Windows Update, Update Catalog, installs)
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace DriverUpdater
{
    public static class Paths
    {
        public static readonly string WorkDir = Path.Combine(Path.GetTempPath(), "DriverUpdater");
        public static readonly string StateDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "DriverUpdater");
        public static readonly string LogFile = Path.Combine(WorkDir, "log.txt");
        public static readonly string LastScanFile = Path.Combine(StateDir, "last-scan.json");
        public static readonly string CatalogCacheFile = Path.Combine(StateDir, "catalog-cache.json");
        public static readonly string OldLastScanFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DriverUpdater", "last-scan.json");
    }

    public static class Log
    {
        static readonly object Gate = new object();
        public static void Write(string message)
        {
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory(Paths.WorkDir);
                    File.AppendAllText(Paths.LogFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "  " + message + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch { }
        }
    }

    public static class Json
    {
        static JavaScriptSerializer S() { return new JavaScriptSerializer { MaxJsonLength = int.MaxValue }; }
        public static string Write(object o) { return S().Serialize(o); }
        public static object Read(string s) { return S().DeserializeObject(s); }
    }

    public static class Native
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
        [DllImport("user32.dll")] static extern IntPtr GetActiveWindow();
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern bool MoveFileEx(string existing, string newName, int flags);
        public const int MOVEFILE_DELAY_UNTIL_REBOOT = 4;

        public const uint OK = 0, YESNO = 4, YESNOCANCEL = 3, OKCANCEL = 1;
        public const uint ERROR = 0x10, QUESTION = 0x20, WARNING = 0x30, INFO = 0x40;
        public const int IDOK = 1, IDCANCEL = 2, IDYES = 6, IDNO = 7;

        public static int Ask(string text, string caption, uint buttons, uint icon)
        {
            return MessageBoxW(GetActiveWindow(), text, caption, buttons | icon);
        }
    }

    public static class Sys
    {
        public static string Wmi1(string query, string prop)
        {
            try
            {
                using (var s = new ManagementObjectSearcher(query))
                    foreach (ManagementObject o in s.Get()) { var v = o[prop]; if (v != null) return v.ToString(); }
            }
            catch { }
            return "";
        }

        public static string WmiAll(string query, string prop)
        {
            var parts = new List<string>();
            try
            {
                using (var s = new ManagementObjectSearcher(query))
                    foreach (ManagementObject o in s.Get()) { var v = o[prop]; if (v != null) parts.Add(v.ToString()); }
            }
            catch { }
            return string.Join(" ", parts);
        }

        /// <summary>Current driver version of one device (by device instance ID), or "".</summary>
        public static string DriverVersionOf(string deviceId)
        {
            return Wmi1("SELECT DriverVersion FROM Win32_PnPSignedDriver WHERE DeviceID = '" + deviceId.Replace("\\", "\\\\").Replace("'", "\\'") + "'", "DriverVersion");
        }

        public static string PcMaker() { return Wmi1("SELECT Manufacturer FROM Win32_ComputerSystem", "Manufacturer"); }

        public static string SystemTool(string name) { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), name); }

        /// <summary>Runs a program without a window and returns its exit code; output goes to the log.</summary>
        public static int Run(string file, string args, int timeoutMs = 30 * 60 * 1000)
        {
            var psi = new ProcessStartInfo(file, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using (var p = Process.Start(psi))
            {
                var outTask = p.StandardOutput.ReadToEndAsync();
                var errTask = p.StandardError.ReadToEndAsync();
                if (!p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch { } return -1; }
                p.WaitForExit();
                string output = (outTask.Result + errTask.Result).Trim();
                if (output.Length > 0) Log.Write(Path.GetFileName(file) + " " + args + Environment.NewLine + output);
                return p.ExitCode;
            }
        }

        public static string FindOnPath(string exe)
        {
            var dirs = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';').ToList();
            dirs.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps"));
            foreach (var d in dirs)
            {
                try { var f = Path.Combine(d.Trim(), exe); if (File.Exists(f)) return f; } catch { }
            }
            return null;
        }

        public static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch (Exception ex) { Log.Write("Could not open " + url + ": " + ex.Message); }
        }

        public static void Restart() { Run(SystemTool("shutdown.exe"), "/r /t 0", 10000); }
    }

    /// <summary>Lists the drivers installed on this PC.</summary>
    public static class Inventory
    {
        static readonly Regex Buses = new Regex(@"^(PCI|USB|HDAUDIO|INTELAUDIO|ACPI|HID|BTH|DISPLAY)\\", RegexOptions.IgnoreCase);

        public static List<DeviceInfo> Read(string pcMaker)
        {
            var hwIds = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            // The two WMI queries are the slow part of startup, so they run side by side
            var entityTask = Task.Run(() =>
            {
                using (var s = new ManagementObjectSearcher("SELECT PNPDeviceID, HardwareID FROM Win32_PnPEntity"))
                    foreach (ManagementObject o in s.Get())
                    {
                        var id = o["PNPDeviceID"] as string;
                        if (id != null) hwIds[id] = (o["HardwareID"] as string[]) ?? new string[0];
                    }
            });
            var signed = new List<ManagementObject>();
            using (var s = new ManagementObjectSearcher("SELECT DeviceID, DeviceName, DeviceClass, Manufacturer, DriverProviderName, DriverVersion, HardWareID, InfName FROM Win32_PnPSignedDriver"))
                foreach (ManagementObject o in s.Get()) signed.Add(o);
            try { entityTask.Wait(); } catch (AggregateException ex) { Log.Write("Hardware ID query failed: " + ex.InnerException.Message); }

            var list = new List<DeviceInfo>();
            foreach (var o in signed)
                {
                    var id = o["DeviceID"] as string;
                    var ver = o["DriverVersion"] as string;
                    if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(ver) || !Buses.IsMatch(id)) continue;
                    string[] ids;
                    if (!hwIds.TryGetValue(id, out ids) || ids.Length == 0)
                    {
                        var one = o["HardWareID"] as string;
                        ids = string.IsNullOrEmpty(one) ? new string[0] : new[] { one };
                    }
                    var d = new DeviceInfo
                    {
                        Key = id,
                        Name = (o["DeviceName"] as string) ?? id,
                        Class = (o["DeviceClass"] as string) ?? "",
                        Manufacturer = (o["Manufacturer"] as string) ?? "",
                        Provider = (o["DriverProviderName"] as string) ?? "",
                        Version = ver,
                        HardwareIds = ids
                    };
                    // Drivers that came with Windows (INF not named oemNN.inf) are updated by Windows itself
                    string inf = (o["InfName"] as string) ?? "";
                    d.BuiltIn = d.Provider.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase)
                             || (inf.Length > 0 && !inf.StartsWith("oem", StringComparison.OrdinalIgnoreCase));
                    d.Hint = Util.VendorHint(d.Manufacturer + " " + d.Provider + " " + d.Name, pcMaker);
                    list.Add(d);
                }
            return list;
        }
    }

    public class WuUpdate
    {
        public string Title, UpdateId, HardwareId, Manufacturer, DriverClass;
        public double Size;
    }

    /// <summary>Windows Update Agent (COM) for driver updates.</summary>
    public static class WindowsUpdate
    {
        static dynamic NewSession() { return Activator.CreateInstance(Type.GetTypeFromProgID("Microsoft.Update.Session", true)); }

        static dynamic Search(dynamic session)
        {
            dynamic searcher = session.CreateUpdateSearcher();
            return searcher.Search("IsInstalled=0 and Type='Driver'").Updates;
        }

        public static List<WuUpdate> FindDriverUpdates()
        {
            var list = new List<WuUpdate>();
            dynamic updates = Search(NewSession());
            int n = updates.Count;
            for (int i = 0; i < n; i++)
            {
                dynamic u = At((object)updates, i);
                var w = new WuUpdate { Title = Str(u.Title), UpdateId = Str(u.Identity.UpdateID) };
                try { w.HardwareId = Str(u.DriverHardwareID); } catch { w.HardwareId = ""; }
                try { w.Manufacturer = Str(u.DriverManufacturer); } catch { w.Manufacturer = ""; }
                try { w.DriverClass = Str(u.DriverClass); } catch { w.DriverClass = ""; }
                try { w.Size = Convert.ToDouble(u.MaxDownloadSize, CultureInfo.InvariantCulture); } catch { }
                list.Add(w);
            }
            return list;
        }

        /// <summary>Downloads and installs one update by ID. Returns (ok, message, rebootNeeded).</summary>
        public static Tuple<bool, string, bool> Install(string updateId)
        {
            dynamic session = NewSession();
            dynamic updates = Search(session);
            dynamic target = null;
            int n = updates.Count;
            for (int i = 0; i < n; i++)
            {
                dynamic u = At((object)updates, i);
                if (string.Equals(Str(u.Identity.UpdateID), updateId, StringComparison.OrdinalIgnoreCase)) { target = u; break; }
            }
            if (target == null) return Tuple.Create(false, "No longer offered", false);
            if (!(bool)target.EulaAccepted) target.AcceptEula();
            dynamic coll = Activator.CreateInstance(Type.GetTypeFromProgID("Microsoft.Update.UpdateColl", true));
            coll.Add(target);
            dynamic dl = session.CreateUpdateDownloader(); dl.Updates = coll; dl.Download();
            dynamic inst = session.CreateUpdateInstaller(); inst.Updates = coll;
            dynamic res = inst.Install();
            int code = (int)res.GetUpdateResult(0).ResultCode;
            bool reboot = false;
            try { reboot = (bool)res.RebootRequired || (bool)res.GetUpdateResult(0).RebootRequired; } catch { }
            bool ok = code == 2 || code == 3;
            return Tuple.Create(ok, ok ? "Installed" : "Failed (code " + code + ")", reboot);
        }

        static string Str(object o) { return o == null ? "" : Convert.ToString(o, CultureInfo.InvariantCulture); }

        /// <summary>Reads item i of a Windows Update COM collection.</summary>
        static dynamic At(object coll, int i)
        {
            try { return ((dynamic)coll)[i]; }
            catch { return coll.GetType().InvokeMember("Item", System.Reflection.BindingFlags.GetProperty, null, coll, new object[] { i }); }
        }
    }

    /// <summary>Microsoft Update Catalog searches, with pacing, retries on rate limits and a 12-hour cache.</summary>
    public class CatalogClient
    {
        const string Base = "https://www.catalog.update.microsoft.com";
        readonly Dictionary<string, List<CatalogEntry>> cache = new Dictionary<string, List<CatalogEntry>>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, DateTime> times = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        DateTime lastRequest = DateTime.MinValue;
        int pace = 1200;            // ms between searches; slows down automatically if Microsoft pushes back
        public int Requests;
        public bool Blocked;
        public Action<string> Status = s => { };
        public Func<bool> Cancelled = () => false;

        static WebClient NewClient()
        {
            var wc = new WebClient { Encoding = Encoding.UTF8 };
            wc.Headers[HttpRequestHeader.UserAgent] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) DriverUpdater/" + App.Version;
            return wc;
        }

        public void LoadCache()
        {
            try
            {
                if (!File.Exists(Paths.CatalogCacheFile)) return;
                var root = Json.Read(File.ReadAllText(Paths.CatalogCacheFile)) as Dictionary<string, object>;
                if (root == null) return;
                var limit = DateTime.Now.AddHours(-12);
                foreach (var kv in root)
                {
                    var e = kv.Value as Dictionary<string, object>;
                    if (e == null) continue;
                    DateTime t;
                    if (!DateTime.TryParse(Convert.ToString(e["t"], CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out t) || t < limit) continue;
                    var rows = new List<CatalogEntry>();
                    var arr = e["r"] as object[];
                    if (arr != null)
                        foreach (var o in arr.OfType<Dictionary<string, object>>())
                        {
                            Func<string, string> g = k => o.ContainsKey(k) ? Convert.ToString(o[k], CultureInfo.InvariantCulture) : "";
                            rows.Add(new CatalogEntry { Guid = g("Guid"), Title = g("Title"), Products = g("Products"), Classification = g("Classification"), Date = g("Date"), Version = g("Version"), Size = g("Size") });
                        }
                    cache[kv.Key] = rows; times[kv.Key] = t;
                }
            }
            catch (Exception ex) { Log.Write("Could not read catalog cache: " + ex.Message); }
        }

        public void SaveCache()
        {
            try
            {
                Directory.CreateDirectory(Paths.StateDir);
                var root = new Dictionary<string, object>();
                foreach (var kv in cache)
                    root[kv.Key] = new Dictionary<string, object> {
                        { "t", times[kv.Key].ToString("o", CultureInfo.InvariantCulture) },
                        { "r", kv.Value.Select(e => new Dictionary<string, object> { {"Guid",e.Guid},{"Title",e.Title},{"Products",e.Products},{"Classification",e.Classification},{"Date",e.Date},{"Version",e.Version},{"Size",e.Size} }).ToArray() }
                    };
                File.WriteAllText(Paths.CatalogCacheFile, Json.Write(root), Encoding.UTF8);
            }
            catch (Exception ex) { Log.Write("Could not save catalog cache: " + ex.Message); }
        }

        int refusedInARow;

        /// <summary>
        /// Results for a query, or null when Microsoft refused it. A refusal never stalls the scan:
        /// the search is skipped for now (Unknown) and the app slows down; RetryRefused() tries again once at the end.
        /// </summary>
        public List<CatalogEntry> Search(string query)
        {
            List<CatalogEntry> hit;
            if (cache.TryGetValue(query, out hit)) return hit;
            if (Blocked || Cancelled()) return null;
            double since = (DateTime.Now - lastRequest).TotalMilliseconds;
            if (since < pace) Thread.Sleep((int)(pace - since));
            lastRequest = DateTime.Now;
            Requests++;
            string html = null;
            try
            {
                using (var wc = NewClient()) html = wc.DownloadString(Base + "/Search.aspx?q=" + Uri.EscapeDataString(query));
            }
            catch (Exception ex) { Log.Write("Catalog request failed for '" + query + "': " + ex.Message); }
            var rows = html == null ? null : Catalog.Parse(html);
            if (rows == null)
            {
                Log.Write("Catalog refused '" + query + "' - will try again at the end.");
                pace = 3000;
                if (++refusedInARow >= 3) { Blocked = true; Log.Write("Catalog keeps refusing - pausing searches until the end of the scan."); }
                return null;
            }
            refusedInARow = 0;
            cache[query] = rows; times[query] = DateTime.Now;
            return rows;
        }

        /// <summary>One short pause, then searches are allowed again (for a final retry of refused drivers).</summary>
        public void PrepareRetry(int seconds)
        {
            for (int s = seconds; s > 0 && !Cancelled(); s--)
            {
                Status("Microsoft asked us to slow down - trying the remaining drivers in " + s + " s...");
                Thread.Sleep(1000);
            }
            Blocked = false; refusedInARow = 0; pace = 3000;
        }

        /// <summary>Best driver for a device. checkedOk=false means Microsoft refused the search.</summary>
        public CatalogEntry Find(DeviceInfo d, bool isArm, string osName, out bool checkedOk)
        {
            checkedOk = false;
            foreach (var q in Catalog.QueriesFor(d.HardwareIds))
            {
                var found = Search(q);
                if (found == null) continue;
                checkedOk = true;
                var best = Catalog.Best(found, isArm, osName);
                if (best != null) return best;
            }
            return null;
        }

        public static List<string> DownloadUrls(string guid)
        {
            string json = "[{\"size\":0,\"languages\":\"\",\"uidInfo\":\"" + guid + "\",\"updateID\":\"" + guid + "\"}]";
            using (var wc = NewClient())
            {
                var resp = Encoding.UTF8.GetString(wc.UploadValues(Base + "/DownloadDialog.aspx", "POST", new System.Collections.Specialized.NameValueCollection { { "updateIDs", json } }));
                return Regex.Matches(resp, @"files\[\d+\]\.url = '([^']+)'").Cast<Match>().Select(m => m.Groups[1].Value).Distinct().ToList();
            }
        }

        /// <summary>Downloads a catalog driver and installs it with pnputil. Returns (ok, message, rebootNeeded).</summary>
        /// <summary>Downloads and unpacks a catalog driver ahead of installing. Returns the folder with the INF files.</summary>
        public static string Prefetch(string guid, string name, Action<string> status)
        {
            string dir = Path.Combine(Paths.WorkDir, guid);
            string extract = Path.Combine(dir, "extracted"), done = Path.Combine(dir, ".done");
            if (File.Exists(done)) return extract;
            status("Downloading: " + name);
            List<string> urls = null;
            for (int attempt = 1; urls == null; attempt++)
            {
                try { urls = DownloadUrls(guid); }
                catch (WebException) { if (attempt >= 3) throw; Net.WaitForInternet(60, status); }
            }
            if (urls.Count == 0) throw new InvalidOperationException("No download link found");
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            Directory.CreateDirectory(extract);
            foreach (var u in urls)
            {
                string file = Path.Combine(dir, Path.GetFileName(new Uri(u).AbsolutePath));
                for (int attempt = 1; ; attempt++)
                {
                    try { using (var wc = NewClient()) wc.DownloadFile(u, file); break; }
                    catch (WebException ex) { if (attempt >= 3) throw; Log.Write("Download problem: " + ex.Message); Net.WaitForInternet(60, status); }
                }
                if (file.EndsWith(".cab", StringComparison.OrdinalIgnoreCase))
                    Sys.Run(Sys.SystemTool("expand.exe"), "-F:* \"" + file + "\" \"" + extract + "\"");
            }
            File.WriteAllText(done, "ok");
            return extract;
        }

        public static Tuple<bool, string, bool> Install(string guid, string name, Action<string> status)
        {
            string extract = Prefetch(guid, name, status);
            status("Installing: " + name);
            int code = Sys.Run(Sys.SystemTool("pnputil.exe"), "/add-driver \"" + Path.Combine(extract, "*.inf") + "\" /subdirs /install");
            switch (code)
            {
                case 0: return Tuple.Create(true, "Installed", false);
                case 3010: return Tuple.Create(true, "Installed", true);
                case 259: return Tuple.Create(false, "Windows kept current driver", false);
                default: return Tuple.Create(false, "Failed (pnputil " + code + ")", false);
            }
        }
    }

    public static class RestorePoint
    {
        /// <summary>Creates a System Restore point with a time limit. Returns null on success or a short reason.</summary>
        public static string Create(int timeoutSec, Action<int> tick)
        {
            const string keyPath = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore";
            object old = null;
            try
            {
                using (var k = Registry.LocalMachine.CreateSubKey(keyPath))
                {
                    old = k.GetValue("SystemRestorePointCreationFrequency");
                    k.SetValue("SystemRestorePointCreationFrequency", 0, RegistryValueKind.DWord);
                }
            }
            catch { }
            try
            {
                var task = Task.Run(() =>
                {
                    using (var mc = new ManagementClass(new ManagementScope(@"\\.\root\default"), new ManagementPath("SystemRestore"), null))
                    {
                        object r = mc.InvokeMethod("CreateRestorePoint", new object[] { "Before Driver Updater", 12u, 100u });
                        return Convert.ToInt32(r, CultureInfo.InvariantCulture);
                    }
                });
                for (int s = 0; s < timeoutSec; s++)
                {
                    if (task.Wait(1000)) break;
                    tick(s + 1);
                }
                if (!task.IsCompleted) return "Windows didn't finish creating it within " + (timeoutSec / 60) + " minutes.";
                if (task.IsFaulted) return "System Restore is not available on this PC.";
                int code = task.Result;
                if (code == 0) return null;
                if (code == 1058) return "System Restore is turned off on this PC.";
                return "Windows returned error code " + code + ".";
            }
            finally
            {
                try
                {
                    using (var k = Registry.LocalMachine.CreateSubKey(keyPath))
                    {
                        if (old == null) k.DeleteValue("SystemRestorePointCreationFrequency", false);
                        else k.SetValue("SystemRestorePointCreationFrequency", old, RegistryValueKind.DWord);
                    }
                }
                catch { }
            }
        }
    }

    public static class Net
    {
        /// <summary>Waits (up to maxSeconds) until the internet answers again - e.g. after a network driver reset.</summary>
        public static bool WaitForInternet(int maxSeconds, Action<string> status)
        {
            for (int s = 0; s < maxSeconds; s += 3)
            {
                status("Waiting for the internet connection to come back... (" + s + " s)");
                try
                {
                    Dns.GetHostEntry("www.catalog.update.microsoft.com");
                    Thread.Sleep(2000);          // give the adapter a moment to settle
                    return true;
                }
                catch { Thread.Sleep(3000); }
            }
            return false;
        }
    }

    public static class DriverStore
    {
        /// <summary>All driver versions present in Windows' driver store (from "pnputil /enum-drivers").</summary>
        public static HashSet<string> Versions()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var psi = new ProcessStartInfo(Sys.SystemTool("pnputil.exe"), "/enum-drivers") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, StandardOutputEncoding = Encoding.UTF8 };
                using (var p = Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit(30000);
                    // "Driver Version:  06/12/2025 20.43.12874.0" (label is translated on other languages, so match the date + version)
                    foreach (Match m in Regex.Matches(output, @"\d{1,4}[/.-]\d{1,2}[/.-]\d{1,4}\s+(\d+\.\d+\.\d+\.\d+)"))
                    {
                        var v = Util.ParseVersion(m.Groups[1].Value);
                        if (v != null) set.Add(v.ToString());
                    }
                }
            }
            catch (Exception ex) { Log.Write("Could not read the driver store: " + ex.Message); }
            return set;
        }

        public static DateTime LastBoot()
        {
            try
            {
                string s = Sys.Wmi1("SELECT LastBootUpTime FROM Win32_OperatingSystem", "LastBootUpTime");
                if (s.Length > 0) return ManagementDateTimeConverter.ToDateTime(s);
            }
            catch { }
            return DateTime.MinValue;
        }
    }

    /// <summary>Remembers updates that installed fine but Windows chose not to use (it ranked the current driver higher).</summary>
    public static class KeptStore
    {
        static string FileName { get { return Path.Combine(Paths.StateDir, "kept.json"); } }

        public static Dictionary<string, string> Load()
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!File.Exists(FileName)) return map;
                var root = MiniJson.Parse(File.ReadAllText(FileName)) as IDictionary<string, object>;
                if (root != null) foreach (var kv in root) map[kv.Key] = Convert.ToString(kv.Value, CultureInfo.InvariantCulture);
            }
            catch (Exception ex) { Log.Write("Could not read kept.json: " + ex.Message); }
            return map;
        }

        public static void Add(string key, string offeredVersion)
        {
            var map = Load();
            map[key] = offeredVersion;
            try { Directory.CreateDirectory(Paths.StateDir); File.WriteAllText(FileName, Json.Write(map), Encoding.UTF8); }
            catch (Exception ex) { Log.Write("Could not save kept.json: " + ex.Message); }
        }
    }

    /// <summary>Saves and loads the last scan (kept in ProgramData so it survives updates).</summary>
    public static class ScanStore
    {
        public static void Save(DateTime time, IEnumerable<ScanResult> results)
        {
            try
            {
                Directory.CreateDirectory(Paths.StateDir);
                var root = new Dictionary<string, object> {
                    { "Time", time.ToString("o", CultureInfo.InvariantCulture) },
                    { "Version", App.Version },
                    { "Results", results.Where(r => !(r.Key ?? "").StartsWith("WU:", StringComparison.Ordinal)).Select(r => r.ToDict()).ToArray() }
                };
                File.WriteAllText(Paths.LastScanFile, Json.Write(root), Encoding.UTF8);
            }
            catch (Exception ex) { Log.Write("Could not save scan results: " + ex.Message); }
        }

        public static bool Load(out DateTime time, out List<ScanResult> results)
        {
            time = DateTime.MinValue; results = new List<ScanResult>();
            try
            {
                if (!File.Exists(Paths.LastScanFile) && File.Exists(Paths.OldLastScanFile))
                {
                    Directory.CreateDirectory(Paths.StateDir);
                    File.Copy(Paths.OldLastScanFile, Paths.LastScanFile, true);
                    try { Directory.Delete(Path.GetDirectoryName(Paths.OldLastScanFile), true); } catch { }
                }
                if (!File.Exists(Paths.LastScanFile)) return false;
                var root = Json.Read(File.ReadAllText(Paths.LastScanFile).TrimStart('\uFEFF')) as Dictionary<string, object>;
                if (root == null || !root.ContainsKey("Time")) return false;
                if (!DateTime.TryParse(Convert.ToString(root["Time"], CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out time)) return false;
                time = time.ToLocalTime();
                object raw;
                if (root.TryGetValue("Results", out raw))
                {
                    var arr = raw as object[] ?? (raw is System.Collections.ArrayList ? ((System.Collections.ArrayList)raw).ToArray() : new object[] { raw });
                    foreach (var o in arr.OfType<Dictionary<string, object>>()) results.Add(ScanResult.FromDict(o));
                }
                return results.Count > 0;
            }
            catch (Exception ex) { Log.Write("Could not read saved scan: " + ex.Message); return false; }
        }
    }
}
