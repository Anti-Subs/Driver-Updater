Add-Type -Path ../src/Core.cs, ../src/Vendors.cs -ErrorAction Stop
$src = Get-Content ../src/VendorNet.cs -Raw
$a = $src.IndexOf('    public class InstallOutcome'); $b = $src.IndexOf('    public static class VendorNet')
$i = $src.IndexOf('        public static string SilentSwitches'); $j = $src.IndexOf('        /// <summary>Runs one installer', $i)
$k = $src.IndexOf('        public static InstallOutcome IntelOutcome'); $l = $src.IndexOf("`n        }", $src.IndexOf('Intel installer error', $k)) + 10
Add-Type -TypeDefinition ("using System; namespace DriverUpdater {`n" + $src.Substring($a, $b - $a) + "public static class VendorNetT {`n" + $src.Substring($i, $j - $i) + $src.Substring($k, $l - $k) + "`n}}") -ErrorAction Stop
$fail=0; function Check($n,$c){ if($c){"  ok   $n"}else{"  FAIL $n";$script:fail++} }
"Switches"
Check 'bluetooth /quiet /norestart' ([DriverUpdater.VendorNetT]::SilentSwitches('Intel','BT-24.70.0-64UWD-Win10-Win11.exe') -eq '/quiet /norestart')
Check 'wifi unchanged' ([DriverUpdater.VendorNetT]::SilentSwitches('Intel','WiFi-24.70.0-Driver64-Win10-Win11.exe') -eq '-q -s')
"Intel exit codes (installer adds 1000)"
$o = [DriverUpdater.VendorNetT]::IntelOutcome(1008); Check '1008 -> package not for this device' ($o.NotForDevice -and -not $o.Ok)
$o = [DriverUpdater.VendorNetT]::IntelOutcome(1000); Check '1000 -> installed' ($o.Ok)
$o = [DriverUpdater.VendorNetT]::IntelOutcome(1014); Check '1014 -> installed, restart' ($o.Ok -and $o.Reboot)
$o = [DriverUpdater.VendorNetT]::IntelOutcome(7); Check '7 -> switches refused' ($o.Message -like '*silent switches*')
"Intel graphics series"
function Pk($t,$v){ $p=[DriverUpdater.VendorPackage]::new(); $p.Source='Intel'; $p.Title=$t; $p.Version=$v; $p.Url='https://downloadmirror.intel.com/x.exe'; $p }
$d=[DriverUpdater.DeviceInfo]::new(); $d.Key='g'; $d.Name='Intel(R) UHD Graphics 770'; $d.Manufacturer='Intel Corporation'; $d.Class='DISPLAY'; $d.Version='32.0.101.7088'; $d.HardwareIds=@('PCI\VEN_8086&DEV_A780')
$pk = [DriverUpdater.VendorPackage[]]@((Pk 'Intel Arc & Iris Xe Graphics - Windows' '32.0.101.9150'), (Pk 'Intel 11th-14th Gen Processor Graphics - Windows' '32.0.101.7101'))
$m = [DriverUpdater.Intel]::Match($pk, [DriverUpdater.DeviceInfo[]]@($d))
Check 'picks the closest newer (own series)' ($m['g'].Latest -eq '32.0.101.7101')
$m = [DriverUpdater.Intel]::Match([DriverUpdater.VendorPackage[]]@((Pk 'Intel Arc Graphics' '32.0.101.9150')), [DriverUpdater.DeviceInfo[]]@($d))
Check 'only another series available -> still offered once' ($m['g'].Latest -eq '32.0.101.9150')
"Remembered as not for this device"
$r=[DriverUpdater.ScanResult]::new(); $r.Key='g'; $r.Kind='Update'; $r.Latest='32.0.101.9150'; $r.Source='Intel'
$kept=[System.Collections.Generic.Dictionary[string,string]]::new(); $kept['g']='x:32.0.101.9150'
[DriverUpdater.ScanResult]::ApplyKeptTo($r, $null, $kept); Check 'not offered again' ($r.Kind -eq 'NotFor')
$it=[DriverUpdater.DriverItem]::new(); $it.Manufacturer='Intel'; $it.Installed='32.0.101.7088'; $it.ApplyState('NotFor',$r,$false)
Check 'row explains why' ($it.TagText -eq 'Up to date' -and $it.Size -eq "Intel's 32.0.101.9150 package doesn't support this device")
$r.Latest='32.0.101.9200'; $r.Kind='Update'; [DriverUpdater.ScanResult]::ApplyKeptTo($r, $null, $kept); Check 'a newer package is offered again' ($r.Kind -eq 'Update')
""; if($fail){"$fail FAILED"}else{'ALL PASSED'}
