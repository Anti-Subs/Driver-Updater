// Driver Updater - core logic (no Windows/WPF dependencies, unit-testable)
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace DriverUpdater
{
    /// <summary>One installed driver found on this PC.</summary>
    public class DeviceInfo
    {
        public string Key, Name, Manufacturer, Class, Version, Provider, Hint;
        public string[] HardwareIds = new string[0];
        public bool BuiltIn;
    }

    /// <summary>The outcome of checking one driver (also what gets saved between sessions).</summary>
    public class ScanResult
    {
        public string Key, Name, Manufacturer, Class, Installed, Latest, Size, Source, Kind, Title, Ref, Hint, HardwareIds;
        public bool Reboot;
        /// <summary>True for Microsoft sources (Windows Update / Update Catalog); false for manufacturer downloads.</summary>
        public bool IsMicrosoft { get { return Source == "Windows Update" || Source == "Microsoft Update Catalog"; } }
        /// <summary>BIOS / system firmware updates (applied by the PC at the next restart).</summary>
        public bool IsFirmware
        {
            get
            {
                return string.Equals(Class, "Firmware", StringComparison.OrdinalIgnoreCase)
                    || Regex.IsMatch((Name ?? "") + " " + (Title ?? ""), @"\s-\s*Firmware\s*-|System Firmware", RegexOptions.IgnoreCase);
            }
        }

        /// <summary>Marks an offered update "Kept" when it is already in Windows' driver store, or Windows declined it before.</summary>
        public static void ApplyKeptTo(ScanResult r, ICollection<string> storeVersions, IDictionary<string, string> kept)
        {
            if (r.Kind != "Update" || string.IsNullOrEmpty(r.Latest)) return;
            string k;
            bool declined = kept != null && kept.TryGetValue(r.Key, out k) && k == r.Latest;
            if (kept != null && kept.TryGetValue(r.Key, out k) && k == "x:" + r.Latest) { r.Kind = "NotFor"; return; }
            var lv = Util.ParseVersion(r.Latest);
            bool inStore = storeVersions != null && lv != null && storeVersions.Contains(lv.ToString());
            if (declined || inStore) r.Kind = "Kept";
        }

        public Dictionary<string, object> ToDict()
        {
            return new Dictionary<string, object> {
                {"Key",Key},{"Name",Name},{"Manufacturer",Manufacturer},{"Class",Class},{"Installed",Installed},
                {"Latest",Latest},{"Size",Size},{"Source",Source},{"Kind",Kind},{"Title",Title},{"Ref",Ref},
                {"Hint",Hint},{"HardwareIDs",HardwareIds},{"Reboot",Reboot}
            };
        }

        public static ScanResult FromDict(IDictionary<string, object> d)
        {
            Func<string, string> s = k => d.ContainsKey(k) && d[k] != null ? Convert.ToString(d[k], CultureInfo.InvariantCulture) : "";
            bool reboot = false;
            if (d.ContainsKey("Reboot") && d["Reboot"] is bool) reboot = (bool)d["Reboot"];
            return new ScanResult {
                Key = s("Key"), Name = s("Name"), Manufacturer = s("Manufacturer"), Class = s("Class"), Installed = s("Installed"),
                Latest = s("Latest"), Size = s("Size"), Source = s("Source"), Kind = s("Kind"), Title = s("Title"), Ref = s("Ref"),
                Hint = s("Hint"), HardwareIds = s("HardwareIDs"), Reboot = reboot
            };
        }
    }

    public class CatalogEntry
    {
        public string Guid, Title, Products, Classification, Date, Version, Size;
    }

    public static class Util
    {
        static readonly Regex VersionRx = new Regex(@"\d+(\.\d+){1,3}");

        public static Version ParseVersion(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            var m = VersionRx.Match(text);
            if (!m.Success) return null;
            Version v;
            return System.Version.TryParse(m.Value, out v) ? v : null;
        }

        public static string FormatSize(double bytes)
        {
            if (bytes <= 0) return "";
            if (bytes >= 1073741824) return (bytes / 1073741824).ToString("N1", CultureInfo.InvariantCulture) + " GB";
            if (bytes >= 1048576) return (bytes / 1048576).ToString("N1", CultureInfo.InvariantCulture) + " MB";
            return (bytes / 1024).ToString("N0", CultureInfo.InvariantCulture) + " KB";
        }

        public static string FormatWhen(DateTime t, DateTime now)
        {
            if (t.Date == now.Date) return "today at " + t.ToString("HH:mm", CultureInfo.InvariantCulture);
            if (t.Date == now.Date.AddDays(-1)) return "yesterday at " + t.ToString("HH:mm", CultureInfo.InvariantCulture);
            return t.ToString("d MMM, HH:mm", CultureInfo.InvariantCulture);
        }

        public static string Age(DateTime t, DateTime now)
        {
            int m = (int)(now - t).TotalMinutes;
            if (m < 1) return "just now";
            if (m < 60) return m + " min ago";
            int h = m / 60;
            if (h < 24) return h + (h == 1 ? " hour ago" : " hours ago");
            int d = h / 24;
            return d + (d == 1 ? " day ago" : " days ago");
        }

        /// <summary>NVIDIA's own version (e.g. 560.94) from the Windows driver version (e.g. 32.0.15.6094).</summary>
        public static string NvidiaVersion(string windowsVersion)
        {
            var parts = (windowsVersion ?? "").Split('.');
            if (parts.Length < 4) return null;
            string digits = parts[2] + parts[3];
            if (digits.Length < 5 || !digits.All(char.IsDigit)) return null;
            digits = digits.Substring(digits.Length - 5);
            return digits.Substring(0, 3) + "." + digits.Substring(3);
        }

        /// <summary>"V6.0.10016.1" -> "6.0.10016.1"</summary>
        public static string CleanVersion(string v)
        {
            v = (v ?? "").Trim();
            if (v.StartsWith("V", StringComparison.OrdinalIgnoreCase)) v = v.Substring(1);
            var p = ParseVersion(v);
            return p != null ? p.ToString() : v;
        }

        /// <summary>True when a vendor's version is a newer build of the same driver family (same major version).</summary>
        public static bool IsNewerSameFamily(string offered, string installed)
        {
            var a = ParseVersion(offered); var b = ParseVersion(installed);
            return a != null && b != null && a.Major == b.Major && a > b;
        }

        public static string Plural(int n, string word) { return n + " " + word + (n == 1 ? "" : "s"); }

        /// <summary>True for device IDs that belong to something plugged in over USB (the device itself,
        /// its storage, its keyboard/mouse part, or the phone/media entry Windows makes for it).</summary>
        public static bool IsUsbDeviceId(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            string u = id.ToUpperInvariant();
            return u.StartsWith(@"USB\") || u.StartsWith(@"USBSTOR\") || u.StartsWith(@"HID\VID_")
                || u.StartsWith(@"SWD\WPDBUSENUM\") || u.Contains("USBSTOR#");
        }

        /// <summary>Which official tool usually has newer builds for this driver.</summary>
        public static string VendorHint(string text, string pcMaker)
        {
            text = text ?? ""; pcMaker = pcMaker ?? "";
            if (Regex.IsMatch(text, @"\b(Monitor|Display)\b", RegexOptions.IgnoreCase) && !Regex.IsMatch(text, "NVIDIA|Intel|AMD|Radeon|Graphics|Adapter", RegexOptions.IgnoreCase)) return "";
            bool asus = Regex.IsMatch(pcMaker, "ASUS", RegexOptions.IgnoreCase);
            if (asus && Regex.IsMatch(text, "Realtek|Cirrus|ASUS|Innovation Platform|Platform Controllers|Dynamic Tuning", RegexOptions.IgnoreCase)) return "MyASUS";
            if (Regex.IsMatch(text, "NVIDIA", RegexOptions.IgnoreCase)) return "NVIDIA app";
            if (Regex.IsMatch(text, "Intel", RegexOptions.IgnoreCase)) return "Intel Driver & Support Assistant";
            if (Regex.IsMatch(text, "AMD|Radeon", RegexOptions.IgnoreCase)) return "AMD Software";
            if (Regex.IsMatch(pcMaker, "Dell", RegexOptions.IgnoreCase)) return "Dell SupportAssist";
            if (Regex.IsMatch(pcMaker, "HP|Hewlett", RegexOptions.IgnoreCase)) return "HP Support Assistant";
            if (Regex.IsMatch(pcMaker, "Lenovo", RegexOptions.IgnoreCase)) return "Lenovo Vantage";
            if (asus) return "MyASUS";
            return "";
        }

        /// <summary>Icon glyph (Segoe MDL2 Assets) and colours for a device class.</summary>
        public static string[] DeviceLook(string cls, string name)
        {
            string c = (cls ?? "").ToUpperInvariant();
            Func<int, string> g = code => ((char)code).ToString();
            switch (c)
            {
                case "DISPLAY": return new[] { g(0xE7F4), "#EEF2FF", "#4F46E5" };
                case "NET":
                    if (Regex.IsMatch(name ?? "", @"Wi-?Fi|Wireless|WLAN|802\.11", RegexOptions.IgnoreCase)) return new[] { g(0xE701), "#E0F2FE", "#0284C7" };
                    return new[] { g(0xE839), "#E0F2FE", "#0284C7" };
                case "MEDIA": case "AUDIOENDPOINT": return new[] { g(0xE767), "#FCE7F3", "#DB2777" };
                case "BLUETOOTH": return new[] { g(0xE702), "#E0E7FF", "#4338CA" };
                case "KEYBOARD": return new[] { g(0xE765), "#F3F4F6", "#374151" };
                case "MOUSE": return new[] { g(0xE962), "#F3F4F6", "#374151" };
                case "CAMERA": case "IMAGE": return new[] { g(0xE722), "#ECFDF5", "#059669" };
                case "USB": return new[] { g(0xE88E), "#FEF3C7", "#B45309" };
                case "BATTERY": return new[] { g(0xE83F), "#ECFDF5", "#059669" };
                case "SCSIADAPTER": case "HDCONTROLLER": case "DISKDRIVE": return new[] { g(0xEDA2), "#F1F5F9", "#475569" };
                case "COMPUTEACCELERATOR": case "PROCESSOR": return new[] { g(0xE950), "#F5F3FF", "#7C3AED" };
                case "HIDCLASS": return new[] { g(0xE772), "#F3F4F6", "#374151" };
                default: return new[] { g(0xE950), "#F1F5F9", "#475569" };
            }
        }
    }

    /// <summary>Reads Microsoft Update Catalog search pages.</summary>
    public static class Catalog
    {
        static readonly Regex RowRx = new Regex("<tr id=\"([0-9a-fA-F-]{36})_R\\d+\"[^>]*>(.*?)</tr>", RegexOptions.Singleline);
        static readonly Regex CellRx = new Regex("id=\"[0-9a-fA-F-]{36}_C(\\d+)_R\\d+\"[^>]*>(.*?)</td>", RegexOptions.Singleline);
        static readonly Regex TagRx = new Regex("<[^>]+>");
        static readonly Regex SpaceRx = new Regex("\\s+");
        static readonly Regex SizeRx = new Regex(@"^[\d.,]+ [KMG]?B");

        /// <summary>Returns null when the page is a refusal/error page (retry later), otherwise the rows (possibly empty).</summary>
        public static List<CatalogEntry> Parse(string html)
        {
            if (html == null || html.Contains("Too many search requests")) return null;
            var list = new List<CatalogEntry>();
            var rows = RowRx.Matches(html);
            foreach (Match r in rows)
            {
                var cells = new Dictionary<int, string>();
                foreach (Match c in CellRx.Matches(r.Groups[2].Value))
                {
                    string text = TagRx.Replace(c.Groups[2].Value, " ");
                    cells[int.Parse(c.Groups[1].Value, CultureInfo.InvariantCulture)] = WebUtility.HtmlDecode(SpaceRx.Replace(text, " ").Trim());
                }
                Func<int, string> cell = i => cells.ContainsKey(i) ? cells[i] : "";
                list.Add(new CatalogEntry {
                    Guid = r.Groups[1].Value, Title = cell(1), Products = cell(2), Classification = cell(3),
                    Date = cell(4), Version = cell(5), Size = SizeRx.Match(cell(6)).Value
                });
            }
            if (rows.Count == 0 && !html.Contains("noResultText")) return null; // unexpected page
            return list;
        }

        /// <summary>The query to search for a device: VEN/DEV (or VID/PID) when available, else its first hardware IDs.</summary>
        public static List<string> QueriesFor(string[] hardwareIds)
        {
            var q = new List<string>();
            var m = Regex.Match(string.Join(" ", hardwareIds ?? new string[0]), "(VEN|VID)_[0-9A-Fa-f]{4}&(DEV|PID)_[0-9A-Fa-f]{4}");
            if (m.Success) q.Add(m.Value);
            else foreach (var id in (hardwareIds ?? new string[0]).Take(2)) if (!string.IsNullOrEmpty(id) && !q.Contains(id)) q.Add(id);
            return q;
        }

        /// <summary>Newest matching driver for this PC's architecture and Windows version.</summary>
        public static CatalogEntry Best(IEnumerable<CatalogEntry> entries, bool isArm, string osName)
        {
            var hits = entries.Where(e => Regex.IsMatch(e.Classification ?? "", "Driver") && !string.IsNullOrEmpty(e.Version)).ToList();
            hits = hits.Where(e => Regex.IsMatch((e.Title ?? "") + " " + (e.Products ?? ""), "ARM64") == isArm).ToList();
            var os = hits.Where(e => (e.Products ?? "").Contains(osName)).ToList();
            if (os.Count > 0) hits = os;
            return hits.OrderByDescending(e => Util.ParseVersion(e.Version) ?? new Version(0, 0)).FirstOrDefault();
        }
    }

    /// <summary>A row in the driver list. Properties notify the UI when they change.</summary>
    public class DriverItem : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        void Set<T>(ref T field, T value, string name)
        {
            if (Equals(field, value)) return;
            field = value;
            var h = PropertyChanged; if (h != null) h(this, new PropertyChangedEventArgs(name));
        }

        public string Key { get; set; }
        public int Index { get; set; }
        public string Name { get; set; }
        public string Manufacturer { get; set; }
        public string Glyph { get; set; }
        public string IconBack { get; set; }
        public string IconFore { get; set; }
        public string Kind { get; set; }

        string _installed; public string Installed { get { return _installed; } set { Set(ref _installed, value, "Installed"); } }
        string _state; public string State { get { return _state; } set { Set(ref _state, value, "State"); } }
        string _source; public string Source { get { return _source; } set { Set(ref _source, value, "Source"); } }
        string _versionLine; public string VersionLine { get { return _versionLine; } set { Set(ref _versionLine, value, "VersionLine"); } }
        string _size; public string Size { get { return _size; } set { Set(ref _size, value, "Size"); } }
        string _pill = "Collapsed"; public string PillVisibility { get { return _pill; } set { Set(ref _pill, value, "PillVisibility"); } }
        string _check = "Hidden"; public string CheckVisibility { get { return _check; } set { Set(ref _check, value, "CheckVisibility"); } }
        bool _isChecked; public bool IsChecked { get { return _isChecked; } set { Set(ref _isChecked, value, "IsChecked"); } }
        string _tagText; public string TagText { get { return _tagText; } set { Set(ref _tagText, value, "TagText"); } }
        string _tagBack = "#F3F4F6"; public string TagBack { get { return _tagBack; } set { Set(ref _tagBack, value, "TagBack"); } }
        string _tagFore = "#6B7280"; public string TagFore { get { return _tagFore; } set { Set(ref _tagFore, value, "TagFore"); } }
        string _spin = "Collapsed"; public string SpinVisibility { get { return _spin; } set { Set(ref _spin, value, "SpinVisibility"); } }
        string _new = "Collapsed"; public string NewVisibility { get { return _new; } set { Set(ref _new, value, "NewVisibility"); } }
        string _accent = "Collapsed"; public string AccentVisibility { get { return _accent; } set { Set(ref _accent, value, "AccentVisibility"); } }
        string _border = "#E5E7EB"; public string CardBorder { get { return _border; } set { Set(ref _border, value, "CardBorder"); } }
        double _opacity = 1.0; public double RowOpacity { get { return _opacity; } set { Set(ref _opacity, value, "RowOpacity"); } }
        string _btnText; public string ButtonText { get { return _btnText; } set { Set(ref _btnText, value, "ButtonText"); } }
        string _btnTag; public string ButtonTag { get { return _btnTag; } set { Set(ref _btnTag, value, "ButtonTag"); } }
        string _btnVis = "Collapsed"; public string ButtonVisibility { get { return _btnVis; } set { Set(ref _btnVis, value, "ButtonVisibility"); } }
        string _reVis = "Collapsed"; public string ReinstallVisibility { get { return _reVis; } set { Set(ref _reVis, value, "ReinstallVisibility"); } }
        bool _btnEnabled = true; public bool ButtonEnabled { get { return _btnEnabled; } set { Set(ref _btnEnabled, value, "ButtonEnabled"); } }

        public void SetTag(string text, string back, string fore) { TagText = text; TagBack = back; TagFore = fore; }

        /// <summary>Applies one visual state to the row.</summary>
        public void ApplyState(string state, ScanResult r, bool busy)
        {
            State = state;
            PillVisibility = "Collapsed"; CheckVisibility = "Hidden"; ButtonVisibility = "Collapsed";
            SpinVisibility = "Collapsed"; AccentVisibility = "Collapsed"; CardBorder = "#E5E7EB";
            RowOpacity = 1.0; ButtonEnabled = !busy;
            VersionLine = Manufacturer + "  |  Version: " + Installed;
            Size = "";
            string hint = r != null ? (r.Hint ?? "") : "";
            ReinstallVisibility = !string.IsNullOrEmpty(Installed) && Kind != "Tool"
                && Array.IndexOf(new[] { "NotScanned", "Ok", "Vendor", "Unknown", "NotFor", "Kept", "Reinstalled" }, state) >= 0 ? "Visible" : "Collapsed";
            switch (state)
            {
                case "Reinstalled":
                    if (r != null && r.Reboot) SetTag("Reinstalled - restart to finish", "#FEF3C7", "#B45309");
                    else SetTag("Reinstalled", "#DCFCE7", "#15803D");
                    break;
                case "NotScanned": SetTag("Not scanned yet", "#F3F4F6", "#6B7280"); break;
                case "Waiting": SetTag("Waiting", "#F3F4F6", "#6B7280"); RowOpacity = 0.6; break;
                case "Checking": SetTag("Checking...", "#DBEAFE", "#1D4ED8"); SpinVisibility = "Visible"; CardBorder = "#93C5FD"; break;
                case "Update":
                    SetTag("Update available", "#FEE2E2", "#B91C1C");
                    Source = r.Source == "Windows Update" ? "Microsoft-signed  |  Windows Update"
                           : r.Source == "Microsoft Update Catalog" ? "Microsoft-signed  |  Update Catalog"
                           : "Official  |  " + r.Source + " support site";
                    PillVisibility = "Visible";
                    VersionLine = "Version: " + (string.IsNullOrEmpty(Installed) ? "not installed" : Installed) + "  >  " + r.Latest;
                    Size = r.Size ?? "";
                    CheckVisibility = "Visible"; IsChecked = !r.IsFirmware;
                    if (r.IsFirmware) Size = (Size.Length > 0 ? Size + "  |  " : "") + "BIOS update - not ticked by default. Keep the charger plugged in; it finishes during a restart";
                    ButtonText = "Update"; ButtonTag = "Update"; ButtonVisibility = "Visible";
                    AccentVisibility = "Visible"; CardBorder = "#FCA5A5";
                    break;
                case "Ok":
                    SetTag("Up to date", "#DCFCE7", "#15803D");
                    if (r != null && r.Ref == "bundled")
                    {
                        Source = "Comes with the " + r.Source + " graphics driver"; PillVisibility = "Visible";
                    }
                    else if (r != null && !string.IsNullOrEmpty(r.Source) && !r.IsMicrosoft)
                    {
                        Source = "Official  |  confirmed on " + r.Source + "'s site"; PillVisibility = "Visible";
                    }
                    if (hint.Length > 0) { Size = "Newer builds: " + hint; ButtonText = "Check newer"; ButtonTag = "Maker"; ButtonVisibility = "Visible"; }
                    break;
                case "Vendor":
                    SetTag("Check with maker", "#EFF6FF", "#1D4ED8");
                    if (hint.Length > 0) { Size = "Newer builds: " + hint; ButtonText = "Maker's tool"; ButtonTag = "Maker"; ButtonVisibility = "Visible"; }
                    break;
                case "Unknown": SetTag("Not checked - Microsoft busy", "#FEF3C7", "#B45309"); break;
                case "NotFor":
                    SetTag("Up to date", "#DCFCE7", "#15803D");
                    if (r != null) Size = r.Source + "'s " + r.Latest + " package doesn't support this device";
                    break;
                case "Kept":
                    SetTag("Up to date - Windows prefers this version", "#DCFCE7", "#15803D");
                    if (r != null && !string.IsNullOrEmpty(r.Latest)) Size = "Version " + r.Latest + " was offered, but Windows keeps the current one as the better match";
                    break;
                case "Queued":
                    ApplyState("Update", r, busy);
                    State = "Queued"; SetTag("Queued", "#F3F4F6", "#6B7280"); ButtonEnabled = false;
                    break;
                case "Installing":
                    ApplyState("Update", r, busy);
                    State = "Installing"; SetTag("Installing...", "#DBEAFE", "#1D4ED8"); SpinVisibility = "Visible"; ButtonEnabled = false;
                    break;
                case "Opened":
                    ApplyState("Update", r, busy);
                    State = "Opened"; SetTag(r.Source + " installer opened - finish there", "#FEF3C7", "#B45309");
                    break;
                case "Installed":
                    if (r != null && r.Reboot) SetTag("Installed - restart to finish", "#FEF3C7", "#B45309");
                    else SetTag("Installed", "#DCFCE7", "#15803D");
                    VersionLine = Manufacturer + "  |  Version: " + (r != null ? r.Latest : Installed);
                    IsChecked = false;
                    break;
            }
        }
    }

    /// <summary>The driver list: keeps rows, moves updates to the top, restores saved results.</summary>
    public class DriverList
    {
        public readonly ObservableCollection<object> Rows = new ObservableCollection<object>();
        public readonly Dictionary<string, DriverItem> ByKey = new Dictionary<string, DriverItem>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, ScanResult> Data = new Dictionary<string, ScanResult>(StringComparer.OrdinalIgnoreCase);
        public List<DeviceInfo> Inventory = new List<DeviceInfo>();
        public int TopCount;
        public bool Busy;

        public DriverItem NewRow(string key, string name, string maker, string cls, string version)
        {
            var look = Util.DeviceLook(cls, name);
            var it = new DriverItem { Key = key, Name = name, Manufacturer = maker, Installed = version, Glyph = look[0], IconBack = look[1], IconFore = look[2] };
            ByKey[key] = it;
            return it;
        }

        public void Reset(string state)
        {
            Rows.Clear(); ByKey.Clear(); Data.Clear(); TopCount = 0;
            foreach (var d in Inventory)
            {
                var it = NewRow(d.Key, d.Name, d.Manufacturer, d.Class, d.Version);
                it.ApplyState(state, null, Busy);
                Rows.Add(it);
            }
        }

        public void MoveToTop(DriverItem it)
        {
            int old = Rows.IndexOf(it);
            if (old >= 0 && old < TopCount) return;
            if (old < 0) Rows.Insert(Math.Min(TopCount, Rows.Count), it);
            else if (old != TopCount) Rows.Move(old, TopCount);
            TopCount++;
        }

        /// <summary>Applies a scan result; returns the row (moved to the top if it has an update).</summary>
        public DriverItem AddResult(ScanResult r, bool markNew)
        {
            Data[r.Key] = r;
            DriverItem it;
            if (!ByKey.TryGetValue(r.Key, out it)) it = NewRow(r.Key, r.Name, r.Manufacturer, r.Class, r.Installed);
            it.ApplyState(r.Kind, r, Busy);
            if (r.Kind == "Update" || r.Kind == "Installed")
            {
                MoveToTop(it);
                if (markNew && r.Kind == "Update") it.NewVisibility = "Visible";
            }
            return it;
        }

        static int Rank(string kind)
        {
            switch (kind) { case "Update": return 4; case "Ok": case "Kept": case "NotFor": return 3; case "Vendor": return 2; case "Unknown": return 1; default: return 0; }
        }

        /// <summary>True when result b should replace result a for the same driver (checks run in parallel).</summary>
        public static bool IsBetter(ScanResult b, ScanResult a)
        {
            if (a == null) return true;
            int rb = Rank(b.Kind), ra = Rank(a.Kind);
            if (rb != ra) return rb > ra;
            if (b.Kind != "Update") return false;
            var vb = Util.ParseVersion(b.Latest); var va = Util.ParseVersion(a.Latest);
            if (vb != null && va != null && vb != va) return vb > va;
            return b.IsMicrosoft && !a.IsMicrosoft;   // same version: prefer the silent Microsoft install
        }

        /// <summary>Applies a result only if it beats what this driver already has. Returns true if it was applied.</summary>
        public bool Merge(ScanResult r, bool markNew)
        {
            ScanResult cur;
            Data.TryGetValue(r.Key, out cur);
            if (!IsBetter(r, cur)) return false;
            AddResult(r, markNew);
            return true;
        }

        /// <summary>Shows a previous scan's results, adjusted for drivers that changed since. Returns how many were shown.</summary>
        /// <summary>
        /// An offered update that is already in Windows' driver store (or that Windows declined before) but isn't
        /// being used means Windows prefers the current driver: show it as current instead of offering it again.
        /// </summary>
        public static void ApplyKept(ScanResult r, ICollection<string> storeVersions, IDictionary<string, string> kept) { ScanResult.ApplyKeptTo(r, storeVersions, kept); }

        public int ImportSaved(IEnumerable<ScanResult> saved) { return ImportSaved(saved, false); }

        /// <param name="restartedSince">The PC has restarted since the scan was saved.</param>
        public int ImportSaved(IEnumerable<ScanResult> saved, bool restartedSince)
        {
            int shown = 0;
            foreach (var r in saved)
            {
                if (r.Key == null || r.Key.StartsWith("WU:", StringComparison.Ordinal)) continue;
                DriverItem it;
                if (!ByKey.TryGetValue(r.Key, out it)) continue;
                var cur = Util.ParseVersion(it.Installed);
                var latest = Util.ParseVersion(r.Latest);
                string kind = r.Kind;
                if (kind == "Installed")
                {
                    if (cur != null && latest != null && cur >= latest) kind = "Ok";
                    else if (restartedSince) kind = "Kept";      // restarted, still the old driver: Windows kept it
                    else r.Reboot = true;
                }
                else if ((r.Installed ?? "") != (it.Installed ?? ""))
                {
                    if (kind == "Update" && cur != null && latest != null && cur >= latest) kind = "Ok";
                    else if (kind == "Update") kind = "NotScanned";
                }
                r.Kind = kind;
                r.Installed = it.Installed;
                if (kind == "NotScanned") continue;
                AddResult(r, false);
                shown++;
            }
            return shown;
        }

        public int Count(params string[] states)
        {
            return Rows.OfType<DriverItem>().Count(i => states.Contains(i.State));
        }
    }
}
