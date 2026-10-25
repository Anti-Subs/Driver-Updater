# Changelog

## 3.5.1 - 25 Oct 2026
- Fixed 'file already exists' when unpacking some Intel and ASUS packages (leftovers from an earlier try, or a file listed twice in the package)
- A failed update keeps the driver's name visible; the reason is shown underneath instead of covering the row

## 3.5.0 - 25 Oct 2026
- New 'Reinstall' button on up-to-date drivers: puts the same version back from the copy Windows keeps (no download) - handy when a device misbehaves

## 3.4.3 - 25 Oct 2026
- New in Tools: 'Remove old USB devices' clears USB devices Windows remembers but that aren't plugged in (they stop pulling in driver offers like Apple's USB driver)
- Monitors no longer point you at your PC maker's tool - their drivers are just colour profiles

## 3.4.2 - 23 Oct 2026
- All updates download first, then install - a network driver reset can no longer break the other downloads
- Network adapter drivers install last; dropped downloads wait for the connection and retry
- Intel Bluetooth installs silently with Intel's documented switches (no automatic restart)
- Intel graphics: offers the closest newer driver in your chip's own series (fixes code 1008 on older Intel graphics)
- Intel installer errors explained in plain words; packages that don't fit a device aren't offered again

## 3.4.1 - 22 Oct 2026
- About page now shows this change log

## 3.4.0 - 22 Oct 2026
- Drivers that came with Windows (like the built-in keyboard) are no longer listed - Windows keeps them updated
- NVIDIA HD Audio and Platform Controllers are confirmed together with the NVIDIA graphics driver
- ASUS packages with their own version numbers are checked by reading the real driver version inside
- Updates that Windows declines to use show as 'Windows prefers this version' instead of popping up every scan
- Drivers still waiting after a restart are recognised as kept by Windows

## 3.3.1 - 22 Oct 2026
- Intel graphics, Wi-Fi and NPU drivers install silently, without Intel's extra apps
- Manufacturer installers run one at a time, and each result is checked against the installed driver version
- Header no longer says 'all up to date' while updates are still pending

## 3.3.0 - 22 Oct 2026
- ASUS, NVIDIA and Intel sites now confirm drivers are up to date, not just find updates
- Fixed the Intel check (Intel's feed is a zip file)
- Much faster scans: no long waits when Microsoft's catalog pushes back
- The log lists each driver's result and where it came from

## 3.2.1 - 22 Oct 2026
- Every scan re-reads installed drivers, so updates made elsewhere are seen
- NVIDIA updates install the driver only, silently - no NVIDIA app

## 3.2.0 - 22 Oct 2026
- Windows Update, manufacturer and catalog checks run at the same time
- Faster start-up; window fits small and high-scaling screens
- Removed the blue arrow from the sidebar; better support for other PCs (desktops, ARM, ASUS desktop boards)

## 3.1.0 - 22 Oct 2026
- New sources: ASUS (MyASUS service), NVIDIA and Intel official driver lookups
- Downloads only from official sites; installers must pass a Windows signature check

## 3.0.1 - 22 Oct 2026
- Fixed a start-up error in the new native version

## 3.0.0 - 22 Oct 2026
- Rebuilt as a native Windows app (no PowerShell), about 250 KB
- The same file installs, runs and uninstalls the app

## 2.2.1 - 22 Oct 2026
- Last scan kept through app updates and for every account on the PC

## 2.2.0 - 22 Oct 2026
- Remembers your last scan and shows when it ran
- Shows which drivers need a restart to finish

## 2.1.1 - 22 Oct 2026
- Setup explains when Windows blocks the app (Smart App Control)

## 2.1.0 - 22 Oct 2026
- Single page: all drivers always visible; updates move to the top while scanning
- Version number in the installer's file name and file properties

## 2.0.0 - 22 Oct 2026
- Modern design with sidebar, driver cards and per-driver Update buttons
- Proper installer, Start-menu entry and uninstaller
- Handles Microsoft catalog rate limits; restore points can no longer hang

## 1.0 - 22 Oct 2026
- First version: scans drivers, checks Windows Update and the Microsoft Update Catalog, installs with a restore point
