// Driver Updater - change log shown on the About page (newest first)
using System.Collections.Generic;
using System.Linq;

namespace DriverUpdater
{
    public class ChangeEntry
    {
        public string Version { get; set; }
        public string Date { get; set; }
        public string Notes { get; set; }       // one change per line, shown as bullets
        public string Badge { get; set; }       // "Installed" on the running version
        public string BadgeVisibility { get; set; }
    }

    public static class Changelog
    {
        static readonly string[][] Raw =
        {
            new[] { "3.7.5", "30 Oct 2026",
                "Fixed: 3.7.4 closed with an error as soon as it opened" },
            new[] { "3.7.4", "30 Oct 2026",
                "New: Click the counters above the list (updates, up to date, check with maker, not checked) to show only those drivers; click again or on the total to show all",
                "Fixed: Bluetooth mice and keyboards made Microsoft's catalog stop answering, leaving the drivers after them unchecked",
                "Fixed: Dell's catalog couldn't be downloaded or unpacked on some PCs",
                "Fixed: Devices with nothing to search for (like Logitech Bluetooth helpers) showed 'Not checked – Microsoft busy' and were retried over and over",
                "Improved: Consistent wording, capitalisation and punctuation throughout the app" },
            new[] { "3.7.3", "27 Oct 2026",
                "Dell: built-in audio (Realtek and other sound chips) is now matched to Dell's drivers too",
                "Dell: more time for the first catalog download, and clearer notes in the log",
                "Spots drivers made for a different PC maker (e.g. a Lenovo keyboard driver on an ASUS, often left by other driver tools) and offers to switch back to Windows' own driver",
                "Never offers another PC maker's driver builds from Microsoft's catalog, and never searches for standard devices every PC has" },
            new[] { "3.7.2", "26 Oct 2026",
                "Slim scrollbar and clearer tick boxes that match light and dark mode",
                "AMD chipset devices (GPIO, 3D V-Cache, PCI...) point to AMD's chipset drivers instead of the Radeon graphics app",
                "Update check finds the official repository by its permanent GitHub number, so renaming the account can't break or redirect it",
                "When Microsoft's catalog limits searches, the drivers it skipped are checked again automatically a few minutes later – no need to scan again",
                "Tools: 'Save hardware report' – your PC's maker, model and devices (no personal data), to share so more makers' catalogs can be added" },
            new[] { "3.7.1", "25 Oct 2026",
                "Includes everything from 3.5.3, 3.6.1 and 3.7.0",
                "New: Dell and Alienware drivers for your exact model, from Dell's official catalog, installed silently",
                "New: Dark mode that follows Windows, or pick Light / Dark on the About page",
                "New: Update notifications – a banner when a new version is out, with one-click Update now",
                "New: Reinstall button on up-to-date drivers (no download needed)",
                "New: Remove old USB devices in Tools – clears devices Windows remembers but aren't plugged in",
                "New: About page lists every driver source and marks the ones used on this PC",
                "Improved: Colour-coded device icons by kind (graphics, network, audio, storage, input...)",
                "Improved: BIOS / firmware updates aren't ticked by default and need the charger plugged in",
                "Improved: Windows Update errors explained in plain words",
                "Improved: 'Update all' becomes 'Update selected' when you untick some",
                "Improved: Plug-in devices and monitors no longer point to your PC maker's tool",
                "Improved: Tools page – Utilities moved to the top",
                "Fixed: 'File already exists' when unpacking some Intel and ASUS packages",
                "Fixed: A failed update no longer hides the driver's name",
                "Fixed: Old USB devices not being found on some PCs" },
            new[] { "3.4.2", "23 Oct 2026",
                "All updates download first, then install – a network driver reset can no longer break the other downloads",
                "Network adapter drivers install last; dropped downloads wait for the connection and retry",
                "Intel Bluetooth installs silently with Intel's documented switches (no automatic restart)",
                "Intel graphics: offers the closest newer driver in your chip's own series (fixes code 1008 on older Intel graphics)",
                "Intel installer errors explained in plain words; packages that don't fit a device aren't offered again" },
            new[] { "3.4.1", "22 Oct 2026", "About page now shows this change log" },
            new[] { "3.4.0", "22 Oct 2026",
                "Drivers that came with Windows (like the built-in keyboard) are no longer listed – Windows keeps them updated",
                "NVIDIA HD Audio and Platform Controllers are confirmed together with the NVIDIA graphics driver",
                "ASUS packages with their own version numbers are checked by reading the real driver version inside",
                "Updates that Windows declines to use show as 'Windows prefers this version' instead of popping up every scan",
                "Drivers still waiting after a restart are recognised as kept by Windows" },
            new[] { "3.3.1", "22 Oct 2026",
                "Intel graphics, Wi-Fi and NPU drivers install silently, without Intel's extra apps",
                "Manufacturer installers run one at a time, and each result is checked against the installed driver version",
                "Header no longer says 'all up to date' while updates are still pending" },
            new[] { "3.3.0", "22 Oct 2026",
                "ASUS, NVIDIA and Intel sites now confirm drivers are up to date, not just find updates",
                "Fixed the Intel check (Intel's feed is a zip file)",
                "Much faster scans: no long waits when Microsoft's catalog pushes back",
                "The log lists each driver's result and where it came from" },
            new[] { "3.2.1", "22 Oct 2026",
                "Every scan re-reads installed drivers, so updates made elsewhere are seen",
                "NVIDIA updates install the driver only, silently – no NVIDIA app" },
            new[] { "3.2.0", "22 Oct 2026",
                "Windows Update, manufacturer and catalog checks run at the same time",
                "Faster start-up; window fits small and high-scaling screens",
                "Removed the blue arrow from the sidebar; better support for other PCs (desktops, ARM, ASUS desktop boards)" },
            new[] { "3.1.0", "22 Oct 2026",
                "New sources: ASUS (MyASUS service), NVIDIA and Intel official driver lookups",
                "Downloads only from official sites; installers must pass a Windows signature check" },
            new[] { "3.0.1", "22 Oct 2026", "Fixed a start-up error in the new native version" },
            new[] { "3.0.0", "22 Oct 2026",
                "Rebuilt as a native Windows app (no PowerShell), about 250 KB",
                "The same file installs, runs and uninstalls the app" },
            new[] { "2.2.1", "22 Oct 2026", "Last scan kept through app updates and for every account on the PC" },
            new[] { "2.2.0", "22 Oct 2026",
                "Remembers your last scan and shows when it ran",
                "Shows which drivers need a restart to finish" },
            new[] { "2.1.1", "22 Oct 2026", "Setup explains when Windows blocks the app (Smart App Control)" },
            new[] { "2.1.0", "22 Oct 2026",
                "Single page: all drivers always visible; updates move to the top while scanning",
                "Version number in the installer's file name and file properties" },
            new[] { "2.0.0", "22 Oct 2026",
                "Modern design with sidebar, driver cards and per-driver Update buttons",
                "Proper installer, Start-menu entry and uninstaller",
                "Handles Microsoft catalog rate limits; restore points can no longer hang" },
            new[] { "1.0", "22 Oct 2026",
                "First version: scans drivers, checks Windows Update and the Microsoft Update Catalog, installs with a restore point" },
        };

        public static List<ChangeEntry> Entries(string runningVersion)
        {
            return Raw.Select(r => new ChangeEntry
            {
                Version = "Version " + r[0],
                Date = r[1],
                Notes = string.Join("\n", r.Skip(2).Select(n => "•  " + n)),
                Badge = "This version",
                BadgeVisibility = r[0] == runningVersion ? "Visible" : "Collapsed"
            }).ToList();
        }
    }
}
