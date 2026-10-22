// Driver Updater - reading ASUS, NVIDIA and Intel driver data (pure parsing/matching, unit-testable)
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace DriverUpdater
{
    /// <summary>A driver package offered by a hardware maker.</summary>
    public class VendorPackage
    {
        public string Source, Title, Version, Url, Size, Date;
        public string[] HardwareIds = new string[0];
    }

    /// <summary>Minimal JSON reader: objects become case-insensitive dictionaries, arrays become lists.</summary>
    public static class MiniJson
    {
        public static object Parse(string s) { int i = 0; var v = Value(s, ref i); return v; }

        static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

        static object Value(string s, ref int i)
        {
            Ws(s, ref i);
            if (i >= s.Length) throw new FormatException("Unexpected end of JSON");
            char c = s[i];
            if (c == '{')
            {
                var d = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                i++; Ws(s, ref i);
                if (s[i] == '}') { i++; return d; }
                while (true)
                {
                    Ws(s, ref i); string k = Str(s, ref i); Ws(s, ref i);
                    if (s[i] != ':') throw new FormatException("Expected ':'");
                    i++; d[k] = Value(s, ref i); Ws(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == '}') { i++; return d; }
                    throw new FormatException("Expected ',' or '}'");
                }
            }
            if (c == '[')
            {
                var l = new List<object>();
                i++; Ws(s, ref i);
                if (s[i] == ']') { i++; return l; }
                while (true)
                {
                    l.Add(Value(s, ref i)); Ws(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == ']') { i++; return l; }
                    throw new FormatException("Expected ',' or ']'");
                }
            }
            if (c == '"') return Str(s, ref i);
            if (s.Substring(i).StartsWith("true", StringComparison.Ordinal)) { i += 4; return true; }
            if (s.Substring(i).StartsWith("false", StringComparison.Ordinal)) { i += 5; return false; }
            if (s.Substring(i).StartsWith("null", StringComparison.Ordinal)) { i += 4; return null; }
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            if (start == i) throw new FormatException("Unexpected character '" + c + "'");
            double num;
            double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out num);
            return num;
        }

        static string Str(string s, ref int i)
        {
            if (s[i] != '"') throw new FormatException("Expected string");
            i++;
            var sb = new StringBuilder();
            while (s[i] != '"')
            {
                char c = s[i++];
                if (c != '\\') { sb.Append(c); continue; }
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 't': sb.Append('\t'); break;
                    case 'r': sb.Append('\r'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u': sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber)); i += 4; break;
                    default: sb.Append(e); break;
                }
            }
            i++;
            return sb.ToString();
        }

        // Navigation helpers
        public static object Get(object o, string key)
        {
            var d = o as IDictionary<string, object>;
            object v;
            return d != null && d.TryGetValue(key, out v) ? v : null;
        }
        public static string S(object o, string key) { var v = Get(o, key); return v == null ? "" : Convert.ToString(v, CultureInfo.InvariantCulture); }
        public static List<object> L(object o, string key) { return Get(o, key) as List<object> ?? new List<object>(); }
    }

    public static class VendorMatch
    {
        /// <summary>True when the package lists one of the device's hardware IDs.</summary>
        public static bool Matches(DeviceInfo d, VendorPackage p)
        {
            var ids = p.HardwareIds.Select(Clean).Where(x => x.Length > 0).ToList();
            return ids.Count > 0 && d.HardwareIds.Any(h => ids.Any(id => h.IndexOf(id, StringComparison.OrdinalIgnoreCase) >= 0));
        }

        /// <summary>Package/device pairs that match by hardware ID but whose version numbers can't be compared
        /// (the package uses its own numbering) and that have no answer yet.</summary>
        public static List<KeyValuePair<DeviceInfo, VendorPackage>> Unresolved(IEnumerable<VendorPackage> packages, IEnumerable<DeviceInfo> devices, Dictionary<string, ScanResult> answered)
        {
            var list = new List<KeyValuePair<DeviceInfo, VendorPackage>>();
            var pk = packages.ToList();
            foreach (var d in devices)
            {
                if (answered.ContainsKey(d.Key)) continue;
                // newest package first, one probe per device
                var p = pk.Where(x => Matches(d, x)).OrderByDescending(x => Util.ParseVersion(x.Version) ?? new Version(0, 0)).FirstOrDefault();
                if (p != null) list.Add(new KeyValuePair<DeviceInfo, VendorPackage>(d, p));
            }
            return list;
        }

        /// <summary>Driver version from an INF file's DriverVer line, if the INF is for one of the device's hardware IDs.</summary>
        public static string InfVersionFor(string infText, string[] hardwareIds)
        {
            if (string.IsNullOrEmpty(infText)) return null;
            bool forDevice = (hardwareIds ?? new string[0]).Select(Clean).Where(x => x.Length > 3)
                .Any(id => infText.IndexOf(id, StringComparison.OrdinalIgnoreCase) >= 0);
            if (!forDevice) return null;
            var m = Regex.Match(infText, @"^\s*DriverVer\s*=\s*[^,\r\n]*,\s*([0-9][0-9.]*)", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            return m.Success && Util.ParseVersion(m.Groups[1].Value) != null ? Util.ParseVersion(m.Groups[1].Value).ToString() : null;
        }

        static string Clean(string hwid)
        {
            int i = hwid.IndexOf("&REV_", StringComparison.OrdinalIgnoreCase);
            return (i >= 0 ? hwid.Substring(0, i) : hwid).Trim();
        }

        /// <summary>Best newer package per device, matched by hardware ID. Only newer builds of the same driver family count.</summary>
        public static Dictionary<string, ScanResult> ByHardwareId(IEnumerable<VendorPackage> packages, IEnumerable<DeviceInfo> devices)
        {
            var best = new Dictionary<string, ScanResult>(StringComparer.OrdinalIgnoreCase);
            var devs = devices.ToList();
            foreach (var p in packages)
            {
                var ids = p.HardwareIds.Select(Clean).Where(x => x.Length > 0).ToList();
                if (ids.Count == 0) continue;
                foreach (var d in devs)
                {
                    if (!d.HardwareIds.Any(h => ids.Any(id => h.IndexOf(id, StringComparison.OrdinalIgnoreCase) >= 0))) continue;
                    if (!Offer(best, d, p)) Confirm(best, d, p);
                }
            }
            return best;
        }

        /// <summary>Records the package if it is a newer build for the device. Returns true when it is newer.</summary>
        public static bool Offer(Dictionary<string, ScanResult> best, DeviceInfo d, VendorPackage p)
        {
            if (!Util.IsNewerSameFamily(p.Version, d.Version)) return false;
            ScanResult cur;
            if (best.TryGetValue(d.Key, out cur) && cur.Kind == "Update" && Util.ParseVersion(cur.Latest) >= Util.ParseVersion(p.Version)) return true;
            best[d.Key] = new ScanResult
            {
                Key = d.Key, Name = d.Name, Manufacturer = d.Manufacturer, Class = d.Class, Installed = d.Version,
                Latest = p.Version, Size = p.Size ?? "", Source = p.Source, Kind = "Update", Title = p.Title, Ref = p.Url,
                Hint = d.Hint, HardwareIds = string.Join("; ", d.HardwareIds)
            };
            return true;
        }

        /// <summary>The maker lists this driver family at the same or an older version: the installed driver is current.</summary>
        public static void Confirm(Dictionary<string, ScanResult> best, DeviceInfo d, VendorPackage p)
        {
            var offered = Util.ParseVersion(p.Version); var installed = Util.ParseVersion(d.Version);
            if (offered == null || installed == null || offered.Major != installed.Major || offered > installed) return;
            ScanResult cur;
            if (best.TryGetValue(d.Key, out cur)) return;       // an update (or an earlier confirmation) wins
            best[d.Key] = new ScanResult
            {
                Key = d.Key, Name = d.Name, Manufacturer = d.Manufacturer, Class = d.Class, Installed = d.Version,
                Latest = p.Version, Size = "", Source = p.Source, Kind = "Ok", Title = p.Title, Ref = "",
                Hint = d.Hint, HardwareIds = string.Join("; ", d.HardwareIds)
            };
        }
    }

    // ---------------------------------------------------------------- ASUS
    public static class Asus
    {
        /// <summary>"GU605MI.312" -> "GU605MI"</summary>
        public static string ModelFromBios(string smbiosVersion)
        {
            var s = (smbiosVersion ?? "").Trim();
            int dot = s.IndexOf('.');
            return dot > 0 ? s.Substring(0, dot) : s;
        }

        public static string DriversUrl(string model, bool rog, bool win11)
        {
            string m = Uri.EscapeDataString(model);
            return "https://rog.asus.com/support/webapi/product/GetPDDrivers?website=global&model=" + m + "&cpu=" + m +
                   "&osid=" + (win11 ? "52" : "45") + (rog ? "&systemCode=rog" : "");
        }

        /// <summary>Returns null when the answer is empty (ASUS sometimes needs a second try).</summary>
        public static List<VendorPackage> Parse(string json)
        {
            var root = MiniJson.Parse(json);
            var groups = MiniJson.L(MiniJson.Get(root, "Result"), "Obj");
            if (groups.Count == 0) return null;
            var list = new List<VendorPackage>();
            foreach (var g in groups)
            {
                foreach (var f in MiniJson.L(g, "Files"))
                {
                    var p = new VendorPackage
                    {
                        Source = "ASUS",
                        Title = MiniJson.S(f, "Title"),
                        Version = Util.CleanVersion(MiniJson.S(f, "Version")),
                        Url = MiniJson.S(MiniJson.Get(f, "DownloadUrl"), "Global"),
                        Size = MiniJson.S(f, "FileSize"),
                        Date = MiniJson.S(f, "ReleaseDate"),
                        HardwareIds = MiniJson.L(f, "HardwareInfoList").Select(h => MiniJson.S(h, "hardwareid")).Where(x => x.Length > 0).ToArray()
                    };
                    if (p.Url.Length > 0 && Util.ParseVersion(p.Version) != null) list.Add(p);
                }
            }
            return list;
        }
    }

    // ---------------------------------------------------------------- NVIDIA
    public static class Nvidia
    {
        static readonly Regex NameRx = new Regex(@"(?<=NVIDIA )(.*(?= \([A-Z]+\))|.*(?= [0-9]+GB)|.*(?= with Max-Q Design)|.*(?= COLLECTORS EDITION)|.*)");

        /// <summary>"NVIDIA GeForce RTX 4090 Laptop GPU" -> "GeForce RTX 4090 Laptop GPU"</summary>
        public static string GpuKey(string name)
        {
            var m = NameRx.Match(name ?? "");
            return m.Success ? m.Value.Trim().Replace("Super", "SUPER") : null;
        }

        /// <summary>NVIDIA product-family ID for a GPU name, from the public-domain nvidia-data list.</summary>
        public static string FamilyId(string gpuDataJson, string gpuName, bool notebook)
        {
            string key = GpuKey(gpuName);
            if (key == null) return null;
            var root = MiniJson.Parse(gpuDataJson);
            foreach (var kind in notebook ? new[] { "notebook", "desktop" } : new[] { "desktop", "notebook" })
            {
                var v = MiniJson.S(MiniJson.Get(root, kind), key);
                if (v.Length > 0) return v;
            }
            return null;
        }

        public static string LookupUrl(string familyId, bool win11)
        {
            return "https://gfwsl.geforce.com/services_toolkit/services/com/nvidia/services/AjaxDriverService.php?func=DriverManualLookup&pfid=" +
                   familyId + "&osID=" + (win11 ? "135" : "57") + "&dch=1&numberOfResults=10&languageCode=1078";
        }

        /// <summary>Newest Game Ready (or notebook) driver from NVIDIA's lookup answer.</summary>
        public static VendorPackage ParseLookup(string json)
        {
            var root = MiniJson.Parse(json);
            foreach (var id in MiniJson.L(root, "IDS"))
            {
                var info = MiniJson.Get(id, "downloadInfo");
                if (info == null) continue;
                if (MiniJson.S(info, "IsCRD") != "0") continue;               // 0 = Game Ready / notebook, 1 = Studio
                string url = MiniJson.S(info, "DownloadURL");
                string ver = MiniJson.S(info, "Version");
                if (url.Length == 0 || Util.ParseVersion(ver) == null) continue;
                return new VendorPackage
                {
                    Source = "NVIDIA", Title = "NVIDIA " + ver + " driver", Version = ver, Url = url,
                    Size = MiniJson.S(info, "DownloadURLFileSize"), Date = MiniJson.S(info, "ReleaseDateTime")
                };
            }
            return null;
        }

        /// <summary>NVIDIA's version (e.g. 560.94) from the Windows driver version (e.g. 32.0.15.6094).</summary>
        public static string ToNvidia(string windowsVersion) { return Util.NvidiaVersion(windowsVersion); }

        /// <summary>Windows-format version for an NVIDIA version, using the installed version's prefix: 576.02 + 32.0.15.6094 -> 32.0.15.7602</summary>
        public static string ToWindows(string nvidiaVersion, string installedWindowsVersion)
        {
            var parts = (installedWindowsVersion ?? "").Split('.');
            string digits = (nvidiaVersion ?? "").Replace(".", "");
            if (parts.Length < 4 || parts[2].Length < 1 || digits.Length != 5 || !digits.All(char.IsDigit)) return null;
            string p2 = parts[2].Substring(0, parts[2].Length - 1) + digits[0];
            return parts[0] + "." + parts[1] + "." + p2 + "." + digits.Substring(1);
        }
    }

    // ---------------------------------------------------------------- Intel
    public static class Intel
    {
        /// <summary>Reads Intel's driver feed (the one Intel Driver &amp; Support Assistant uses).</summary>
        public static List<VendorPackage> ParseFeed(string json)
        {
            var root = MiniJson.Parse(json);
            var items = root as List<object>;
            if (items == null)
                foreach (var k in new[] { "data", "items", "drivers", "results" }) { items = MiniJson.Get(root, k) as List<object>; if (items != null) break; }
            var list = new List<VendorPackage>();
            if (items == null) return list;
            foreach (var it in items)
            {
                string name = MiniJson.S(it, "name");
                if (name.Length == 0) name = MiniJson.S(it, "title");
                string ver = MiniJson.S(it, "version");
                var urls = MiniJson.L(it, "files").Select(f => f is string ? (string)f : MiniJson.S(f, "url")).Where(u => u.Length > 0).ToList();
                string url = PickFile(urls);
                if (name.Length == 0 || url == null || Util.ParseVersion(ver) == null) continue;
                list.Add(new VendorPackage { Source = "Intel", Title = name, Version = Util.CleanVersion(ver), Url = url, Size = MiniJson.S(it, "size") });
            }
            return list;
        }

        static string PickFile(List<string> urls)
        {
            var ok = urls.Where(u => u.StartsWith("https://", StringComparison.OrdinalIgnoreCase)).ToList();
            return ok.FirstOrDefault(u => Regex.IsMatch(u, @"Driver64.*\.zip$|\.zip$", RegexOptions.IgnoreCase) && Regex.IsMatch(u, "Driver64|x64|win64", RegexOptions.IgnoreCase))
                ?? ok.FirstOrDefault(u => u.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && !Regex.IsMatch(u, "win32|x86", RegexOptions.IgnoreCase))
                ?? ok.FirstOrDefault(u => u.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && !Regex.IsMatch(u, "win32|x86", RegexOptions.IgnoreCase));
        }

        /// <summary>Which Intel package name pattern fits a device (by device class and name).</summary>
        public static Regex PatternFor(DeviceInfo d)
        {
            if ((d.Manufacturer ?? "").IndexOf("Intel", StringComparison.OrdinalIgnoreCase) < 0) return null;
            string c = (d.Class ?? "").ToUpperInvariant(), n = d.Name ?? "";
            if (c == "DISPLAY" && Regex.IsMatch(n, "Graphics|Arc|Iris|UHD", RegexOptions.IgnoreCase)) return new Regex(@"Graphics", RegexOptions.IgnoreCase);
            if (c == "NET" && Regex.IsMatch(n, @"Wi-?Fi|Wireless", RegexOptions.IgnoreCase)) return new Regex(@"^(?!.*Bluetooth).*(Wi-?Fi|Wireless)", RegexOptions.IgnoreCase);
            if (c == "BLUETOOTH" && Regex.IsMatch(n, "Bluetooth|Wireless", RegexOptions.IgnoreCase)) return new Regex(@"Bluetooth", RegexOptions.IgnoreCase);
            if (c == "COMPUTEACCELERATOR" && Regex.IsMatch(n, "AI Boost|NPU", RegexOptions.IgnoreCase)) return new Regex(@"\bNPU\b", RegexOptions.IgnoreCase);
            return null;
        }

        public static Dictionary<string, ScanResult> Match(IEnumerable<VendorPackage> packages, IEnumerable<DeviceInfo> devices)
        {
            var best = new Dictionary<string, ScanResult>(StringComparer.OrdinalIgnoreCase);
            var pk = packages.ToList();
            foreach (var d in devices)
            {
                var rx = PatternFor(d);
                if (rx == null) continue;
                var candidates = pk.Where(p => rx.IsMatch(p.Title)).ToList();
                // Intel ships several graphics series with the same major version (e.g. 101.7xxx for older chips,
                // 101.9xxx for Arc / Core Ultra), so offer the closest newer build, not simply the highest one.
                var newer = candidates.Where(p => Util.IsNewerSameFamily(p.Version, d.Version))
                                      .OrderBy(p => Util.ParseVersion(p.Version)).FirstOrDefault();
                if (newer != null) VendorMatch.Offer(best, d, newer);
                else foreach (var p in candidates) VendorMatch.Confirm(best, d, p);
            }
            return best;
        }
    }
}
