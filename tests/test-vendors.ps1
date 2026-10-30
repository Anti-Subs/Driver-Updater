Add-Type -Path ../src/Core.cs, ../src/Vendors.cs -ErrorAction Stop
. ./silent-switches.ps1
$fail = 0
function Check($name, $cond) { if ($cond) { "  ok   $name" } else { "  FAIL $name"; $script:fail++ } }
function Dev($key,$name,$maker,$cls,$ver,[string[]]$ids) { $d=[DriverUpdater.DeviceInfo]::new(); $d.Key=$key;$d.Name=$name;$d.Manufacturer=$maker;$d.Class=$cls;$d.Version=$ver;$d.HardwareIds=$ids;$d }
$devs = [System.Collections.Generic.List[DriverUpdater.DeviceInfo]]::new()
$devs.Add((Dev 'a' 'Realtek High Definition Audio' 'Realtek' 'MEDIA' '6.0.9977.1' @('HDAUDIO\FUNC_01&VEN_10EC&DEV_0285&SUBSYS_10431CA3&REV_1000','HDAUDIO\FUNC_01&VEN_10EC&DEV_0285&SUBSYS_10431CA3')))
$devs.Add((Dev 'w' 'Intel(R) Wi-Fi 6E AX211 160MHz' 'Intel Corporation' 'NET' '24.50.0.4' @('PCI\VEN_8086&DEV_7E40&SUBSYS_00948086&REV_20')))
$devs.Add((Dev 'b' 'Intel(R) Wireless Bluetooth(R)' 'Intel Corporation' 'BLUETOOTH' '24.70.0.4' @('USB\VID_8087&PID_0033&REV_0000')))
$devs.Add((Dev 'g' 'Intel(R) Arc(TM) Graphics' 'Intel Corporation' 'DISPLAY' '32.0.101.8860' @('PCI\VEN_8086&DEV_7D55&SUBSYS_1C631043&REV_08')))
$devs.Add((Dev 'n' 'NVIDIA GeForce RTX 4090 Laptop GPU' 'NVIDIA' 'DISPLAY' '32.0.15.6094' @('PCI\VEN_10DE&DEV_2757&SUBSYS_1C631043&REV_A1')))
$devs.Add((Dev 'p' 'Intel(R) AI Boost' 'Intel Corporation' 'COMPUTEACCELERATOR' '32.0.100.4841' @('PCI\VEN_8086&DEV_7D1D')))

"JSON reader"
$j = [DriverUpdater.MiniJson]::Parse('{"A":[1,2.5,"xA\"",true,null,{"b":{}}],"c":"y"}')
Check 'parse' ([DriverUpdater.MiniJson]::S($j,'c') -eq 'y' -and [DriverUpdater.MiniJson]::L($j,'a').Count -eq 6 -and [DriverUpdater.MiniJson]::L($j,'a')[2] -eq 'xA"')

"ASUS"
Check 'model from BIOS' ([DriverUpdater.Asus]::ModelFromBios('GU605MI.312') -eq 'GU605MI')
Check 'url' ([DriverUpdater.Asus]::DriversUrl('GU605MI', $true, $true) -eq 'https://rog.asus.com/support/webapi/product/GetPDDrivers?website=global&model=GU605MI&cpu=GU605MI&osid=52&systemCode=rog')
$asus = @'
{"Result":{"Count":2,"Obj":[
 {"Name":"Audio","Files":[
   {"Title":"Realtek Audio Driver","Version":"V6.0.10016.1","FileSize":"120.5 MBytes","ReleaseDate":"2026/09/01","DownloadUrl":{"Global":"https://dlcdnets.asus.com/pub/ASUS/nb/Image/Driver/Audio/A.zip"},"HardwareInfoList":[{"hardwareid":"HDAUDIO\\FUNC_01&VEN_10EC&DEV_0285&SUBSYS_10431CA3"}]},
   {"Title":"Realtek Audio Driver","Version":"V6.0.9900.1","DownloadUrl":{"Global":"https://dlcdnets.asus.com/old.zip"},"HardwareInfoList":[{"hardwareid":"HDAUDIO\\FUNC_01&VEN_10EC&DEV_0285&SUBSYS_10431CA3"}]}]},
 {"Name":"Wireless","Files":[
   {"Title":"Intel WLAN Driver","Version":"V23.60.0.1","DownloadUrl":{"Global":"https://dlcdnets.asus.com/w.zip"},"HardwareInfoList":[{"hardwareid":"PCI\\VEN_8086&DEV_7E40&SUBSYS_00948086&REV_20"}]},
   {"Title":"Dolby Atmos","Version":"V3.2","DownloadUrl":{"Global":"https://dlcdnets.asus.com/d.zip"},"HardwareInfoList":[]}]}]}}
'@
$pk = [DriverUpdater.Asus]::Parse($asus)
Check 'parsed 4 packages' ($pk.Count -eq 4 -and $pk[0].Version -eq '6.0.10016.1' -and $pk[0].HardwareIds[0] -like 'HDAUDIO*')
$m = [DriverUpdater.VendorMatch]::ByHardwareId($pk, $devs)
Check 'audio newer from ASUS' ($m.ContainsKey('a') -and $m['a'].Latest -eq '6.0.10016.1' -and $m['a'].Source -eq 'ASUS' -and $m['a'].Ref -like 'https://dlcdnets.asus.com/*A.zip')
Check 'older ASUS wifi (different driver series) ignored' (-not $m.ContainsKey('w'))
$asus2 = $asus.Replace('V6.0.10016.1','V6.0.9977.1').Replace('V6.0.9900.1','V6.0.9977.1')
$m2 = [DriverUpdater.VendorMatch]::ByHardwareId([DriverUpdater.Asus]::Parse($asus2), $devs)
Check 'ASUS lists same version -> confirmed current' ($m2['a'].Kind -eq 'Ok' -and $m2['a'].Source -eq 'ASUS')
$it = [DriverUpdater.DriverItem]::new(); $it.Manufacturer='Realtek'; $it.Installed='6.0.9977.1'; $it.ApplyState('Ok', $m2['a'], $false)
Check 'row says confirmed on ASUS site' ($it.TagText -eq 'Up to date' -and $it.Source -eq "Official  |  confirmed on ASUS's site" -and $it.PillVisibility -eq 'Visible')
$m3 = [DriverUpdater.VendorMatch]::ByHardwareId($pk, $devs)
Check 'update still wins over confirmation' ($m3['a'].Kind -eq 'Update')
Check 'empty result -> null (retry)' ($null -eq [DriverUpdater.Asus]::Parse('{"Result":{"Obj":[]}}'))

"NVIDIA"
Check 'gpu key' ([DriverUpdater.Nvidia]::GpuKey('NVIDIA GeForce RTX 4090 Laptop GPU') -eq 'GeForce RTX 4090 Laptop GPU')
Check 'gpu key super' ([DriverUpdater.Nvidia]::GpuKey('NVIDIA GeForce RTX 4070 Super') -eq 'GeForce RTX 4070 SUPER')
$gd = Get-Content ../third_party/nvidia-gpu-data.json -Raw
Check 'family id' ([DriverUpdater.Nvidia]::FamilyId($gd, 'NVIDIA GeForce RTX 4090 Laptop GPU', $true) -eq '1004')
Check 'win version -> nvidia' ([DriverUpdater.Nvidia]::ToNvidia('32.0.15.6094') -eq '560.94')
Check 'nvidia -> win version' ([DriverUpdater.Nvidia]::ToWindows('576.02', '32.0.15.6094') -eq '32.0.15.7602')
Check 'nvidia -> win version (5xx->6xx)' ([DriverUpdater.Nvidia]::ToWindows('601.10', '32.0.15.6094') -eq '32.0.16.0110')
$nv = '{"Success":"1","IDS":[{"downloadInfo":{"Version":"577.10","IsCRD":"1","DownloadURL":"https://us.download.nvidia.com/Windows/577.10/577.10-notebook-win10-win11-64bit-international-nsd-dch-whql.exe","DownloadURLFileSize":"870.1 MB"}},{"downloadInfo":{"Version":"576.02","IsCRD":"0","DownloadURL":"https://us.download.nvidia.com/Windows/576.02/576.02-notebook-win10-win11-64bit-international-dch-whql.exe","DownloadURLFileSize":"863.4 MB","ReleaseDateTime":"Wed Apr 16, 2026"}}]}'
$p = [DriverUpdater.Nvidia]::ParseLookup($nv)
Check 'picks Game Ready not Studio' ($p.Version -eq '576.02' -and $p.Size -eq '863.4 MB')
Check 'lookup url' ([DriverUpdater.Nvidia]::LookupUrl('1004', $true) -like '*pfid=1004&osID=135&dch=1*')

"Intel"
$intel = '[{"name":"Intel Wireless Wi-Fi Drivers for Windows 10 and Windows 11","Version":"24.70.0","Files":[{"Url":"https://downloadmirror.intel.com/1/WiFi-24.70.0-Driver64-Win10-Win11.zip"},{"Url":"https://downloadmirror.intel.com/1/WiFi-24.70.0-Driver32.zip"}]},
 {"name":"Intel Wireless Bluetooth for Windows 10 and Windows 11","Version":"24.70.0","Files":[{"Url":"https://downloadmirror.intel.com/2/BT-24.70.0-64UWD-Win10-Win11.zip"}]},
 {"name":"Intel Arc & Iris Xe Graphics - Windows","Version":"32.0.101.9034","Files":[{"Url":"https://downloadmirror.intel.com/3/gfx_win_101.9034.exe"}]},
 {"name":"Intel NPU Driver - Windows","Version":"32.0.100.5540","Files":[{"Url":"https://downloadmirror.intel.com/4/npu_win_32.0.100.5540.zip"}]}]'
$ip = [DriverUpdater.Intel]::ParseFeed($intel)
Check 'feed parsed' ($ip.Count -eq 4 -and $ip[0].Url -like '*Driver64*')
$im = [DriverUpdater.Intel]::Match($ip, $devs)
Check 'wifi newer' ($im['w'].Latest -eq '24.70.0' -and $im['w'].Ref -like '*Driver64*')
Check 'bluetooth same version -> confirmed current' ($im['b'].Kind -eq 'Ok' -and $im['b'].Source -eq 'Intel')
Check 'graphics newer' ($im['g'].Latest -eq '32.0.101.9034' -and $im['g'].Ref -like '*.exe')
Check 'npu newer' ($im['p'].Latest -eq '32.0.100.5540')
Check 'nvidia not matched by intel' (-not $im.ContainsKey('n'))
Check 'odd feed -> empty' ([DriverUpdater.Intel]::ParseFeed('{"x":1}').Count -eq 0)

"List behaviour"
$L = [DriverUpdater.DriverList]::new(); foreach ($d in $devs) { $L.Inventory.Add($d) }; $L.Reset('Waiting')
function SR($k,$v,$src) { $r=[DriverUpdater.ScanResult]::new(); $r.Key=$k;$r.Name=$k;$r.Installed='1.0';$r.Latest=$v;$r.Kind='Update';$r.Source=$src;$r }
[void]$L.AddResult((SR 'g' '32.0.101.8900' 'Windows Update'), $true)
[void]$L.AddResult((SR 'g' '32.0.101.9034' 'Intel'), $true)
Check 'better result replaces, row not moved twice' ($L.TopCount -eq 1 -and $L.Rows[0].Key -eq 'g' -and $L.Rows[0].Source -like 'Official  |  Intel*')
$L.Rows[0].ApplyState('Opened', (SR 'g' '32.0.101.9034' 'NVIDIA'), $false)
Check 'opened state' ($L.Rows[0].TagText -eq 'NVIDIA installer opened – finish there')
""; if ($fail) { "$fail FAILED" } else { 'ALL PASSED' }
"Silent switches"
Check 'intel graphics' ([DriverUpdater.VendorNet]::SilentSwitches('Intel','gfx_win_101.9150.exe') -eq '-s --overwrite --noExtras')
Check 'intel wifi' ([DriverUpdater.VendorNet]::SilentSwitches('Intel','WiFi-24.70.0-Driver64-Win10-Win11.exe') -eq '-q -s')
Check 'intel npu' ([DriverUpdater.VendorNet]::SilentSwitches('Intel','npu_win_32.0.100.5540.exe') -eq '-s')
Check 'unknown -> interactive' ($null -eq [DriverUpdater.VendorNet]::SilentSwitches('ASUS','AsusSetup.exe'))
""; if ($fail) { "$fail FAILED" } else { 'ALL PASSED (with switches)' }
