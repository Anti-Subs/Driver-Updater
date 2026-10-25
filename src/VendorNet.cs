// Driver Updater - fetching and installing drivers from ASUS, NVIDIA and Intel
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace DriverUpdater
{
    public class InstallOutcome
    {
        public bool Ok, Reboot, Opened, Kept, NotForDevice;
        public string Message;
        public static InstallOutcome Of(bool ok, string msg, bool reboot = false) { return new InstallOutcome { Ok = ok, Message = msg, Reboot = reboot }; }
    }

    public static class VendorNet
    {
        // Only these official sites are used for manufacturer downloads
        static readonly string[] AllowedHosts = { "asus.com", "nvidia.com", "geforce.com", "intel.com", "githubusercontent.com" };
        // Expected digital signature (publisher) for installers we open
        // (ASUS packages may contain installers signed by the chip maker, e.g. Realtek, so for ASUS any valid signature is accepted)
        static readonly Dictionary<string, string> Publishers = new Dictionary<string, string> { { "NVIDIA", "NVIDIA" }, { "Intel", "Intel" } };

        public static bool Allowed(string url)
        {
            Uri u;
            if (!Uri.TryCreate(url, UriKind.Absolute, out u) || u.Scheme != Uri.UriSchemeHttps) return false;
            return AllowedHosts.Any(h => u.Host.Equals(h, StringComparison.OrdinalIgnoreCase) || u.Host.EndsWith("." + h, StringComparison.OrdinalIgnoreCase));
        }

        static HttpWebRequest Request(string url)
        {
            if (!Allowed(url)) throw new InvalidOperationException("Not an allowed download site: " + url);
            var r = (HttpWebRequest)WebRequest.Create(url);
            r.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) DriverUpdater/" + App.Version;
            r.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            r.Timeout = 30000; r.ReadWriteTimeout = 60000;
            return r;
        }

        /// <summary>Downloads text; if the server sends a zip file (Intel's feed does), returns the first file inside it.</summary>
        public static string GetText(string url)
        {
            byte[] data;
            using (var resp = Request(url).GetResponse())
            using (var src = resp.GetResponseStream())
            using (var ms = new MemoryStream()) { src.CopyTo(ms); data = ms.ToArray(); }
            if (data.Length > 4 && data[0] == 'P' && data[1] == 'K')
            {
                using (var zip = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read))
                {
                    var entry = zip.Entries.Where(e => e.Length > 0).OrderByDescending(e => e.FullName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)).ThenByDescending(e => e.Length).FirstOrDefault();
                    if (entry == null) return "";
                    using (var sr = new StreamReader(entry.Open(), Encoding.UTF8)) return sr.ReadToEnd();
                }
            }
            return Encoding.UTF8.GetString(data).TrimStart('\uFEFF');
        }

        public static string GetString(string url)
        {
            using (var resp = Request(url).GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                return sr.ReadToEnd();
        }

        // ------------------------------------------------------------ checks during a scan
        public static Dictionary<string, ScanResult> CheckAsus(List<DeviceInfo> devices, bool win11)
        {
            string model = Asus.ModelFromBios(Sys.Wmi1("SELECT SMBIOSBIOSVersion FROM Win32_BIOS", "SMBIOSBIOSVersion"));
            bool rog = Sys.Wmi1("SELECT Model FROM Win32_ComputerSystem", "Model").IndexOf("ROG", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!System.Text.RegularExpressions.Regex.IsMatch(model, "^(?=.*[A-Za-z])(?=.*[0-9])[A-Za-z0-9_-]{4,}$"))
            {
                Log.Write("ASUS: no laptop model code in BIOS version '" + model + "' (desktop motherboard?) - skipped");
                return new Dictionary<string, ScanResult>();
            }
            string url = Asus.DriversUrl(model, rog, win11);
            Log.Write("ASUS: " + url);
            var pk = Asus.Parse(GetString(url)) ?? Asus.Parse(GetString(url + "&tag=" + new Random().Next(10, 99)));
            if (pk == null) { Log.Write("ASUS: no driver list for " + model); return new Dictionary<string, ScanResult>(); }
            var found = VendorMatch.ByHardwareId(pk, devices);
            ProbeAsusPackages(pk, devices, found);
            Log.Write("ASUS: " + pk.Count + " packages for " + model + ", " + found.Values.Count(r => r.Kind == "Update") + " newer than installed, " + found.Values.Count(r => r.Kind == "Ok") + " confirmed current");
            return found;
        }

        public static Dictionary<string, ScanResult> CheckNvidia(List<DeviceInfo> devices, bool win11)
        {
            var best = new Dictionary<string, ScanResult>(StringComparer.OrdinalIgnoreCase);
            var gpus = devices.Where(d => d.Class.Equals("DISPLAY", StringComparison.OrdinalIgnoreCase) && d.HardwareIds.Any(h => h.IndexOf("VEN_10DE", StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
            if (gpus.Count == 0) return best;
            string gpuData = null;
            try { gpuData = GetString("https://raw.githubusercontent.com/ZenitH-AT/nvidia-data/main/gpu-data.json"); } catch (Exception ex) { Log.Write("NVIDIA: using built-in GPU list (" + ex.Message + ")"); }
            foreach (var g in gpus)
            {
                bool notebook = g.Name.IndexOf("Laptop", StringComparison.OrdinalIgnoreCase) >= 0 || g.Name.IndexOf("Max-Q", StringComparison.OrdinalIgnoreCase) >= 0 || IsLaptop();
                string fam = (gpuData != null ? Nvidia.FamilyId(gpuData, g.Name, notebook) : null) ?? Nvidia.FamilyId(NvidiaData.GpuData, g.Name, notebook);
                if (fam == null) { Log.Write("NVIDIA: no product family for " + g.Name); continue; }
                var p = Nvidia.ParseLookup(GetString(Nvidia.LookupUrl(fam, win11)));
                if (p == null) { Log.Write("NVIDIA: no driver found for " + g.Name); continue; }
                string asWindows = Nvidia.ToWindows(p.Version, g.Version);
                Log.Write("NVIDIA: " + g.Name + " installed " + Nvidia.ToNvidia(g.Version) + ", latest " + p.Version);
                if (asWindows == null) continue;
                var pkg = new VendorPackage { Source = "NVIDIA", Title = p.Title, Version = asWindows, Url = p.Url, Size = p.Size };
                if (!VendorMatch.Offer(best, g, pkg)) VendorMatch.Confirm(best, g, pkg);
            }
            // NVIDIA's HD Audio (HDMI sound) and Platform Controllers ship inside the graphics driver package
            if (best.Count > 0)
            {
                var gpuResult = best.Values.First();
                foreach (var d in devices.Where(x => !x.Class.Equals("DISPLAY", StringComparison.OrdinalIgnoreCase) &&
                         x.HardwareIds.Any(h => h.IndexOf("VEN_10DE", StringComparison.OrdinalIgnoreCase) >= 0 || h.IndexOf("NVDA", StringComparison.OrdinalIgnoreCase) >= 0)))
                {
                    if (best.ContainsKey(d.Key)) continue;
                    best[d.Key] = new ScanResult
                    {
                        Key = d.Key, Name = d.Name, Manufacturer = d.Manufacturer, Class = d.Class, Installed = d.Version, Latest = "",
                        Size = "", Source = "NVIDIA", Kind = "Ok", Title = "Comes with the NVIDIA graphics driver", Ref = "bundled",
                        Hint = d.Hint, HardwareIds = string.Join("; ", d.HardwareIds)
                    };
                }
            }
            return best;
        }

        // ASUS packages often carry their own version number (e.g. V23.26.47.797) that differs from the driver
        // inside (e.g. 17.54.59.438). For those, read the real driver version from the package's INF files once
        // and remember it, so the comparison is exact.
        static void ProbeAsusPackages(List<VendorPackage> packages, List<DeviceInfo> devices, Dictionary<string, ScanResult> found)
        {
            string cacheFile = Path.Combine(Paths.StateDir, "asus-inf-cache.json");
            var cache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (File.Exists(cacheFile))
                {
                    var root = MiniJson.Parse(File.ReadAllText(cacheFile)) as IDictionary<string, object>;
                    if (root != null) foreach (var kv in root) cache[kv.Key] = Convert.ToString(kv.Value, CultureInfo.InvariantCulture);
                }
            }
            catch { }
            bool changed = false;
            foreach (var pair in VendorMatch.Unresolved(packages, devices, found).Take(6))
            {
                var d = pair.Key; var p = pair.Value;
                string key = p.Url + "|" + (d.HardwareIds.FirstOrDefault() ?? d.Key);
                string ver;
                if (!cache.TryGetValue(key, out ver))
                {
                    try { ver = InfVersionFromPackage(p, d) ?? ""; }
                    catch (Exception ex) { Log.Write("ASUS: couldn't read " + p.Title + ": " + ex.Message); continue; }
                    cache[key] = ver; changed = true;
                }
                if (ver.Length == 0) { Log.Write("ASUS: " + p.Title + " has no readable driver version for " + d.Name); continue; }
                Log.Write("ASUS: " + p.Title + " " + p.Version + " contains driver " + ver + " for " + d.Name + " (installed " + d.Version + ")");
                var real = new VendorPackage { Source = p.Source, Title = p.Title, Version = ver, Url = p.Url, Size = p.Size, HardwareIds = p.HardwareIds };
                if (!VendorMatch.Offer(found, d, real)) VendorMatch.Confirm(found, d, real);
            }
            if (changed)
                try { Directory.CreateDirectory(Paths.StateDir); File.WriteAllText(cacheFile, Json.Write(cache), Encoding.UTF8); } catch { }
        }

        static string InfVersionFromPackage(VendorPackage p, DeviceInfo d)
        {
            if (!p.Url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) return null;
            string file = Path.Combine(Paths.WorkDir, "probe-" + Math.Abs(p.Url.GetHashCode()).ToString(CultureInfo.InvariantCulture) + ".zip");
            Directory.CreateDirectory(Paths.WorkDir);
            try
            {
                Download(p.Url, file, s => { }, p.Title);
                Version best = null;
                using (var zip = ZipFile.OpenRead(file))
                    foreach (var e in zip.Entries.Where(x => x.FullName.EndsWith(".inf", StringComparison.OrdinalIgnoreCase) && x.Length < 4 * 1024 * 1024))
                    {
                        string text;
                        using (var sr = new StreamReader(e.Open(), Encoding.Default, true)) text = sr.ReadToEnd();
                        var v = Util.ParseVersion(VendorMatch.InfVersionFor(text, d.HardwareIds));
                        if (v != null && (best == null || v > best)) best = v;
                    }
                return best != null ? best.ToString() : null;
            }
            finally { try { File.Delete(file); } catch { } }
        }

        public static Dictionary<string, ScanResult> CheckIntel(List<DeviceInfo> devices)
        {
            if (!devices.Any(d => Intel.PatternFor(d) != null)) return new Dictionary<string, ScanResult>();
            string feed = GetText("https://dsadata.intel.com/data/en");
            var pk = Intel.ParseFeed(feed);
            if (pk.Count == 0) Log.Write("Intel: feed format not recognised; starts with: " + new string(feed.Take(300).Where(c => !char.IsControl(c)).ToArray()));
            var found = Intel.Match(pk, devices);
            Log.Write("Intel: " + pk.Count + " packages in feed; " + (found.Count > 0 ? string.Join(", ", found.Values.Select(r => r.Name + " " + r.Kind + " " + r.Latest)) : "no matches"));
            return found;
        }

        static bool? laptop;
        static bool IsLaptop()
        {
            if (laptop.HasValue) return laptop.Value;
            var types = new[] { 8, 9, 10, 11, 12, 14, 18, 21, 31, 32 };
            laptop = false;
            try
            {
                using (var s = new System.Management.ManagementObjectSearcher("SELECT ChassisTypes FROM Win32_SystemEnclosure"))
                    foreach (System.Management.ManagementObject o in s.Get())
                    {
                        var t = o["ChassisTypes"] as ushort[];
                        if (t != null && t.Any(x => types.Contains(x))) laptop = true;
                    }
            }
            catch { }
            return laptop.Value;
        }

        // ------------------------------------------------------------ installing
        /// <summary>Downloads a file; if the connection drops (e.g. a network driver was just reinstalled),
        /// waits for it to come back and tries again (up to 3 tries).</summary>
        public static void Download(string url, string file, Action<string> status, string label)
        {
            for (int attempt = 1; ; attempt++)
            {
                try { DownloadOnce(url, file, status, label); return; }
                catch (WebException ex)
                {
                    if (attempt >= 3) throw;
                    Log.Write("Download problem (" + ex.Status + "), waiting for the connection: " + ex.Message);
                    Net.WaitForInternet(60, status);
                }
            }
        }

        static void DownloadOnce(string url, string file, Action<string> status, string label)
        {
            var req = Request(url);
            req.Timeout = 60000; req.ReadWriteTimeout = 120000;
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var src = resp.GetResponseStream())
            using (var dst = File.Create(file))
            {
                long total = resp.ContentLength, done = 0;
                var buf = new byte[1 << 16];
                int n, lastPct = -1;
                var timer = Stopwatch.StartNew();
                while ((n = src.Read(buf, 0, buf.Length)) > 0)
                {
                    dst.Write(buf, 0, n);
                    done += n;
                    if (total > 0)
                    {
                        int pct = (int)(done * 100 / total);
                        if (pct != lastPct && timer.ElapsedMilliseconds > 250) { lastPct = pct; timer.Restart(); status("Downloading " + label + "... " + pct + "% of " + Util.FormatSize(total)); }
                    }
                }
            }
        }

        static string PackageFile(ScanResult job)
        {
            string dir = Path.Combine(Paths.WorkDir, job.Source + "-" + Math.Abs(job.Ref.GetHashCode()).ToString(CultureInfo.InvariantCulture));
            string fileName = Path.GetFileName(new Uri(job.Ref).AbsolutePath);
            if (string.IsNullOrEmpty(fileName)) fileName = "package.bin";
            return Path.Combine(dir, fileName);
        }

        /// <summary>Downloads a manufacturer package ahead of installing (all downloads happen before any install).</summary>
        public static string Prefetch(ScanResult job, Action<string> status)
        {
            if (!Allowed(job.Ref)) throw new InvalidOperationException("Not an official download address");
            string file = PackageFile(job), done = file + ".done";
            if (File.Exists(file) && File.Exists(done)) return file;
            string dir = Path.GetDirectoryName(file);
            if (Directory.Exists(dir)) try { Directory.Delete(dir, true); } catch { }
            Directory.CreateDirectory(dir);
            Download(job.Ref, file, status, job.Source + " driver for " + job.Name);
            File.WriteAllText(done, "ok");
            return file;
        }

        /// <summary>Unpacks a zip into a fresh folder. Leftovers from an earlier try are cleared, and a file listed
        /// twice inside the package simply overwrites the first copy (ZipFile.ExtractToDirectory fails on both).</summary>
        public static void Unzip(string file, string extract)
        {
            if (Directory.Exists(extract)) Directory.Delete(extract, true);
            Directory.CreateDirectory(extract);
            string root = Path.GetFullPath(extract).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            using (var zip = ZipFile.OpenRead(file))
                foreach (var e in zip.Entries)
                {
                    string target = Path.GetFullPath(Path.Combine(extract, e.FullName));
                    if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;   // never write outside the folder
                    if (e.FullName.EndsWith("/") || e.FullName.EndsWith("\\")) { Directory.CreateDirectory(target); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    e.ExtractToFile(target, true);
                }
        }

        public static InstallOutcome Install(ScanResult job, Action<string> status)
        {
            string url = job.Ref;
            if (!Allowed(url)) return InstallOutcome.Of(false, "Blocked: unofficial download address");
            string file = Prefetch(job, status);
            string dir = Path.GetDirectoryName(file);

            string launch = null;
            if (file.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                status("Unpacking " + job.Source + " package...");
                string extract = Path.Combine(dir, "extracted");
                Unzip(file, extract);
                if (Directory.GetFiles(extract, "*.inf", SearchOption.AllDirectories).Length > 0)
                {
                    status("Installing: " + job.Name);
                    int code = Sys.Run(Sys.SystemTool("pnputil.exe"), "/add-driver \"" + Path.Combine(extract, "*.inf") + "\" /subdirs /install");
                    switch (code)
                    {
                        case 0: return InstallOutcome.Of(true, "Installed");
                        case 3010: return InstallOutcome.Of(true, "Installed", true);
                        case 259: return InstallOutcome.Of(false, "Windows kept current driver");
                        default: return InstallOutcome.Of(false, "Failed (pnputil " + code + ")");
                    }
                }
                launch = new[] { "AsusSetup.exe", "Setup.exe", "Install.exe", "Installer.exe" }
                    .Select(n => Directory.GetFiles(extract, n, SearchOption.AllDirectories).FirstOrDefault()).FirstOrDefault(f => f != null)
                    ?? Directory.GetFiles(extract, "*.exe", SearchOption.AllDirectories).FirstOrDefault();
                if (launch == null) return InstallOutcome.Of(false, "Package has no installer");
            }
            else if (file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                if (job.Source == "NVIDIA")
                {
                    var outcome = InstallNvidiaSilently(file, job, status);
                    try { Directory.Delete(dir, true); } catch { }      // frees ~1 GB
                    return outcome;
                }
                launch = file;
            }
            else return InstallOutcome.Of(false, "Unknown package type");

            string why = CheckSignature(launch, job.Source);
            if (why != null) { Log.Write("Refused to open " + launch + ": " + why); return InstallOutcome.Of(false, "Not opened: " + why); }

            string silent = SilentSwitches(job.Source, Path.GetFileName(launch));
            var result = RunInstaller(launch, silent, job, status);
            if (result.Ok) try { Directory.Delete(dir, true); } catch { }
            return result;
        }

        /// <summary>Documented silent switches for known manufacturer installers (null = no silent mode known).</summary>
        public static string SilentSwitches(string source, string fileName)
        {
            string f = (fileName ?? "").ToLowerInvariant();
            if (source == "Intel")
            {
                if (f.StartsWith("gfx_win")) return "-s --overwrite --noExtras";      // graphics: silent, driver only (no Intel Graphics Software)
                if (f.StartsWith("bt")) return "/quiet /norestart";                      // Bluetooth 24.50+: /quiet (would auto-restart without /norestart)
                if (f.Contains("driver64") || f.StartsWith("wifi")) return "-q -s";     // Wi-Fi IT package
                if (f.StartsWith("npu_win")) return "-s";                              // NPU (same installer family as graphics)
            }
            return null;
        }

        /// <summary>Runs one installer and waits for it; silently when switches are known, otherwise in its own window.
        /// Then checks the device's driver version to report what really happened.</summary>
        static InstallOutcome RunInstaller(string file, string silentArgs, ScanResult job, Action<string> status)
        {
            bool silent = silentArgs != null;
            Log.Write(job.Source + " installer: " + file + (silent ? " " + silentArgs : " (interactive)"));
            var psi = silent
                ? new ProcessStartInfo(file, silentArgs) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(file) }
                : new ProcessStartInfo(file) { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(file) };
            int code;
            using (var p = Process.Start(psi))
            {
                var watch = Stopwatch.StartNew();
                while (!p.WaitForExit(1000))
                {
                    int sec = (int)watch.Elapsed.TotalSeconds;
                    if (sec % 3 == 0)
                        status(silent
                            ? "Installing " + job.Name + " (" + job.Source + ", in the background)... " + sec / 60 + ":" + (sec % 60).ToString("00")
                            : job.Source + "'s installer is open for " + job.Name + " - finish it there, then Driver Updater continues.");
                    if (watch.Elapsed.TotalMinutes > 30) { Log.Write(job.Source + " installer is taking too long; leaving it to finish."); return InstallOutcome.Of(false, "Still installing - scan again later"); }
                }
                code = p.ExitCode;
            }
            string now = Sys.DriverVersionOf(job.Key);
            Log.Write(job.Source + " installer exit code " + code + "; driver version now " + now + " (offered " + job.Latest + ")");
            var have = Util.ParseVersion(now); var want = Util.ParseVersion(job.Latest);
            if (have != null && want != null && have >= want) return InstallOutcome.Of(true, "Installed");
            if (job.Source == "Intel") return IntelOutcome(code);
            if (code == 0 || code == 3010 || code == 1641) return InstallOutcome.Of(true, "Installed", true);   // finishes after a restart
            return InstallOutcome.Of(false, (silent ? "Installer stopped" : "Not installed") + " (code " + code + ")");
        }

        /// <summary>Intel installer exit codes (Intel support article 000098195); the installer adds 1000 to them.</summary>
        public static InstallOutcome IntelOutcome(int exitCode)
        {
            int c = exitCode >= 1000 ? exitCode - 1000 : exitCode;
            switch (c)
            {
                case 0: case 2: case 14: return InstallOutcome.Of(true, "Installed", true);
                case 5: case 8: return new InstallOutcome { Ok = false, NotForDevice = true, Message = "This Intel package doesn't support this device" };
                case 6: return InstallOutcome.Of(false, "Intel's installer was closed");
                case 7: return InstallOutcome.Of(false, "Intel's installer didn't accept the silent switches");
                case 11: return InstallOutcome.Of(false, "Not enough free disk space");
                case 13: return InstallOutcome.Of(false, "Restart the PC, then try again");
                default: return InstallOutcome.Of(false, "Intel installer error " + exitCode);
            }
        }

        /// <summary>
        /// Installs only NVIDIA's display driver, silently (NVIDIA's documented switches: -s silent, -n no reboot prompt,
        /// Display.Driver = driver only, so the NVIDIA app and other extras are not installed).
        /// </summary>
        static InstallOutcome InstallNvidiaSilently(string file, ScanResult job, Action<string> status)
        {
            string why = CheckSignature(file, "NVIDIA");
            if (why != null) { Log.Write("Refused to run " + file + ": " + why); return InstallOutcome.Of(false, "Not installed: " + why); }

            // NVIDIA documents the silent, driver-only switches for the setup.exe inside the package,
            // so unpack it first with Windows' built-in tar; if that isn't possible, hand the switches to the package itself.
            string runFile = file;
            string extract = Path.Combine(Path.GetDirectoryName(file), "extracted");
            try
            {
                status("Unpacking the NVIDIA driver...");
                if (Directory.Exists(extract)) Directory.Delete(extract, true);
                Directory.CreateDirectory(extract);
                int tarCode = Sys.Run(Sys.SystemTool("tar.exe"), "-xf \"" + file + "\" -C \"" + extract + "\"", 10 * 60 * 1000);
                string setup = Path.Combine(extract, "setup.exe");
                if (tarCode == 0 && File.Exists(setup) && Directory.Exists(Path.Combine(extract, "Display.Driver")) && CheckSignature(setup, "NVIDIA") == null)
                    runFile = setup;
                else Log.Write("NVIDIA: couldn't unpack with tar (code " + tarCode + "); passing the switches to the package instead.");
            }
            catch (Exception ex) { Log.Write("NVIDIA: unpack failed (" + ex.Message + "); passing the switches to the package instead."); }

            status("Installing the NVIDIA driver (driver only, no extras). The screen may flicker or go black for a moment...");
            Log.Write("NVIDIA silent install: " + runFile + " -s -n Display.Driver");
            int code;
            var psi = new ProcessStartInfo(runFile, "-s -n Display.Driver") { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(runFile) };
            using (var p = Process.Start(psi))
            {
                var watch = Stopwatch.StartNew();
                while (!p.WaitForExit(1000))
                {
                    int sec = (int)watch.Elapsed.TotalSeconds;
                    if (sec % 5 == 0) status("Installing the NVIDIA driver (driver only)... " + sec / 60 + ":" + (sec % 60).ToString("00") + "  - the screen may flicker");
                    if (watch.Elapsed.TotalMinutes > 25) { Log.Write("NVIDIA installer is taking too long; leaving it to finish on its own."); return InstallOutcome.Of(false, "Still installing - check again later"); }
                }
                code = p.ExitCode;
            }
            string now = Sys.DriverVersionOf(job.Key);
            Log.Write("NVIDIA installer exit code " + code + "; driver version now " + now + " (wanted " + job.Latest + ")");
            var have = Util.ParseVersion(now); var want = Util.ParseVersion(job.Latest);
            if (have != null && want != null && have >= want) return InstallOutcome.Of(true, "Installed");
            if (code == 0 || code == 1 || code == 3010) return InstallOutcome.Of(true, "Installed", true);   // finishes after restart
            return InstallOutcome.Of(false, "NVIDIA installer stopped (code " + code + ")");
        }

        // ------------------------------------------------------------ signature check (WinVerifyTrust)
        [DllImport("wintrust.dll", CharSet = CharSet.Unicode)]
        static extern int WinVerifyTrust(IntPtr hwnd, [MarshalAs(UnmanagedType.LPStruct)] Guid action, ref WINTRUST_DATA data);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct WINTRUST_FILE_INFO { public uint cbStruct; public string pcwszFilePath; public IntPtr hFile; public IntPtr pgKnownSubject; }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct WINTRUST_DATA
        {
            public uint cbStruct; public IntPtr pPolicyCallbackData; public IntPtr pSIPClientData; public uint dwUIChoice; public uint fdwRevocationChecks;
            public uint dwUnionChoice; public IntPtr pFile; public uint dwStateAction; public IntPtr hWVTStateData; public string pwszURLReference;
            public uint dwProvFlags; public uint dwUIContext; public IntPtr pSignatureSettings;
        }

        /// <summary>Null when the file has a valid signature from the expected maker; otherwise the reason.</summary>
        public static string CheckSignature(string file, string source)
        {
            var info = new WINTRUST_FILE_INFO { cbStruct = (uint)Marshal.SizeOf(typeof(WINTRUST_FILE_INFO)), pcwszFilePath = file };
            IntPtr pInfo = Marshal.AllocHGlobal(Marshal.SizeOf(info));
            try
            {
                Marshal.StructureToPtr(info, pInfo, false);
                var data = new WINTRUST_DATA
                {
                    cbStruct = (uint)Marshal.SizeOf(typeof(WINTRUST_DATA)), dwUIChoice = 2 /* none */, fdwRevocationChecks = 0,
                    dwUnionChoice = 1 /* file */, pFile = pInfo, dwStateAction = 0, dwProvFlags = 0x10 /* safer */
                };
                var action = new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE"); // WINTRUST_ACTION_GENERIC_VERIFY_V2
                int rc = WinVerifyTrust(IntPtr.Zero, action, ref data);
                if (rc != 0) return "its digital signature isn't valid (0x" + rc.ToString("X8") + ")";
            }
            finally { Marshal.FreeHGlobal(pInfo); }
            string expected;
            if (!Publishers.TryGetValue(source, out expected)) return null;
            try
            {
                var cert = X509Certificate.CreateFromSignedFile(file);
                if (cert.Subject.IndexOf(expected, StringComparison.OrdinalIgnoreCase) < 0) return "it isn't signed by " + source + " (" + cert.Subject + ")";
            }
            catch (Exception ex) { return "its signature couldn't be read (" + ex.Message + ")"; }
            return null;
        }
    }
}
