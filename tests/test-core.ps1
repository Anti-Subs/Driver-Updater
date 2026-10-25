Add-Type -Path ../src/Core.cs -ErrorAction Stop
$fail = 0
function Check($name, $cond) { if ($cond) { "  ok   $name" } else { "  FAIL $name"; $script:fail++ } }

$row = '<tr id="60a58892-497b-4ca8-88fa-3a44d7e7a69f_R0" style="x"><td id="60a58892-497b-4ca8-88fa-3a44d7e7a69f_C1_R0"><a>Intel Corporation Display Driver Update (32.0.101.8860)</a></td><td id="60a58892-497b-4ca8-88fa-3a44d7e7a69f_C2_R0">Windows 11 Client, version 24H2 and later, Servicing Drivers</td><td id="60a58892-497b-4ca8-88fa-3a44d7e7a69f_C3_R0">Drivers (Video)</td><td id="60a58892-497b-4ca8-88fa-3a44d7e7a69f_C4_R0">6/24/2026</td><td id="60a58892-497b-4ca8-88fa-3a44d7e7a69f_C5_R0">32.0.101.8860</td><td id="60a58892-497b-4ca8-88fa-3a44d7e7a69f_C6_R0"><span>762.7 MB</span> <span>799750877</span></td></tr>'
$row2 = $row.Replace('60a58892','70a58892').Replace('32.0.101.8860','32.0.101.9034').Replace('Windows 11 Client','Windows 10')
"Catalog parsing"
$p = [DriverUpdater.Catalog]::Parse("<table>$row$row2</table>")
Check 'two rows' ($p.Count -eq 2)
Check 'fields' ($p[0].Version -eq '32.0.101.8860' -and $p[0].Size -eq '762.7 MB' -and $p[0].Classification -eq 'Drivers (Video)' -and $p[0].Title -like 'Intel Corporation Display*')
Check 'rate limit -> null' ($null -eq [DriverUpdater.Catalog]::Parse('Too many search requests. Please wait'))
Check 'no results -> empty' ([DriverUpdater.Catalog]::Parse('<span id="ctl00_catalogBody_noResultText">x</span>').Count -eq 0)
Check 'odd page -> null' ($null -eq [DriverUpdater.Catalog]::Parse('<html>maintenance</html>'))
$b = [DriverUpdater.Catalog]::Best($p, $false, 'Windows 11'); Check 'best prefers matching OS' ($b.Version -eq '32.0.101.8860')
$b = [DriverUpdater.Catalog]::Best($p, $false, 'Windows 12'); Check 'best = newest otherwise' ($b.Version -eq '32.0.101.9034')
Check 'arm filter' ($null -eq [DriverUpdater.Catalog]::Best($p, $true, 'Windows 11'))
$q = [DriverUpdater.Catalog]::QueriesFor(@('PCI\VEN_8086&DEV_7D55&SUBSYS_1C631043&REV_08','PCI\VEN_8086&DEV_7D55')); Check 'query VEN/DEV' ($q.Count -eq 1 -and $q[0] -eq 'VEN_8086&DEV_7D55')
$q = [DriverUpdater.Catalog]::QueriesFor(@('ACPI\CSC3556','*CSC3556','x')); Check 'query ACPI ids' ($q.Count -eq 2 -and $q[0] -eq 'ACPI\CSC3556')

"Utilities"
Check 'version' ([DriverUpdater.Util]::ParseVersion('Intel - Display - 32.0.101.9034') -eq [version]'32.0.101.9034')
Check 'version null' ($null -eq [DriverUpdater.Util]::ParseVersion('none'))
Check 'size' ([DriverUpdater.Util]::FormatSize(33554432) -eq '32.0 MB')
$now = [datetime]'2026-10-22 21:50'
Check 'when today' ([DriverUpdater.Util]::FormatWhen([datetime]'2026-10-22 21:40', $now) -eq 'today at 21:40')
Check 'when yesterday' ([DriverUpdater.Util]::FormatWhen([datetime]'2026-10-21 08:05', $now) -eq 'yesterday at 08:05')
Check 'when older' ([DriverUpdater.Util]::FormatWhen([datetime]'2026-10-19 20:25', $now) -eq '19 Oct, 20:25')
Check 'age' ([DriverUpdater.Util]::Age([datetime]'2026-10-22 19:35', $now) -eq '2 hours ago')
Check 'hint asus audio' ([DriverUpdater.Util]::VendorHint('Realtek High Definition Audio', 'ASUSTeK COMPUTER INC.') -eq 'MyASUS')
Check 'hint monitor none' ([DriverUpdater.Util]::VendorHint('Alienware Alienware Alienware 34 Curved QD-OLED Gaming Monitor', 'ASUSTeK COMPUTER INC.') -eq '')
Check 'hint generic display none' ([DriverUpdater.Util]::VendorHint('(Standard monitor types) Microsoft Generic PnP Monitor', 'ASUSTeK COMPUTER INC.') -eq '')
Check 'usb ids' ([DriverUpdater.Util]::IsUsbDeviceId('USB\VID_05AC&PID_12A8\abc') -and [DriverUpdater.Util]::IsUsbDeviceId('USBSTOR\Disk&Ven_SanDisk\1') -and [DriverUpdater.Util]::IsUsbDeviceId('HID\VID_046D&PID_C52B&MI_00\7') -and [DriverUpdater.Util]::IsUsbDeviceId('SWD\WPDBUSENUM\_??_USBSTOR#DISK') -and [DriverUpdater.Util]::IsUsbDeviceId('STORAGE\VOLUME\_??_USBSTOR#DISK&VEN_X'))
Check 'non-usb ids' (-not [DriverUpdater.Util]::IsUsbDeviceId('PCI\VEN_8086&DEV_7D55\3') -and -not [DriverUpdater.Util]::IsUsbDeviceId('HID\{00001124-0000-1000-8000-00805F9B34FB}_VID&0002046D') -and -not [DriverUpdater.Util]::IsUsbDeviceId('DISPLAY\DELA1B2\5') -and -not [DriverUpdater.Util]::IsUsbDeviceId(''))
Check 'hint intel' ([DriverUpdater.Util]::VendorHint('Intel Corporation Intel(R) Arc', 'ASUSTeK COMPUTER INC.') -eq 'Intel Driver & Support Assistant')

"Reinstall button"
$it = [DriverUpdater.DriverItem]::new(); $it.Name='Wifi'; $it.Manufacturer='Intel'; $it.Installed='24.5'
$it.ApplyState('Ok', $null, $false); Check 'reinstall shown when up to date' ($it.ReinstallVisibility -eq 'Visible')
$it.ApplyState('Kept', $null, $false); Check 'reinstall shown when kept' ($it.ReinstallVisibility -eq 'Visible')
$ru=[DriverUpdater.ScanResult]::new(); $ru.Source='Windows Update'; $ru.Latest='24.7'
$it.ApplyState('Update', $ru, $false); Check 'reinstall hidden when update offered' ($it.ReinstallVisibility -eq 'Collapsed')
$it.ApplyState('Checking', $null, $false); Check 'reinstall hidden while checking' ($it.ReinstallVisibility -eq 'Collapsed')
$rr=[DriverUpdater.ScanResult]::new(); $rr.Reboot=$true
$it.ApplyState('Reinstalled', $rr, $false); Check 'reinstalled tag' ($it.TagText -eq 'Reinstalled - restart to finish' -and $it.ReinstallVisibility -eq 'Visible')
$it.Installed=''; $it.ApplyState('Ok', $null, $false); Check 'no reinstall without a driver' ($it.ReinstallVisibility -eq 'Collapsed')

"Firmware"
$fw=[DriverUpdater.ScanResult]::new(); $fw.Name='ASUSTeK COMPUTER INC. - Firmware - 10.1.2.309'; $fw.Source='Windows Update'; $fw.Latest='10.1.2.309'; $fw.Class=''
Check 'firmware detected by title' $fw.IsFirmware
$fw2=[DriverUpdater.ScanResult]::new(); $fw2.Name='X'; $fw2.Class='Firmware'; Check 'firmware detected by class' $fw2.IsFirmware
$nf=[DriverUpdater.ScanResult]::new(); $nf.Name='Intel(R) Wi-Fi 6 AX200 160MHz'; $nf.Class='NET'; Check 'wifi not firmware' (-not $nf.IsFirmware)
$it2 = [DriverUpdater.DriverItem]::new(); $it2.Name=$fw.Name; $it2.Installed=''
$it2.ApplyState('Update', $fw, $false); Check 'firmware not ticked by default' (-not $it2.IsChecked -and $it2.Size -like '*BIOS update*')
$it2.ApplyState('Update', $nf, $false); Check 'normal update ticked' ($it2.IsChecked)

"Driver list"
$L = [DriverUpdater.DriverList]::new()
foreach ($n in 'Audio','Camera','Touch','Wifi') { $d = [DriverUpdater.DeviceInfo]::new(); $d.Key="K:$n"; $d.Name=$n; $d.Manufacturer='M'; $d.Class='MEDIA'; $d.Version=@{Audio='6.0.1';Camera='10.0';Touch='16.0';Wifi='24.5'}[$n]; $L.Inventory.Add($d) }
$L.Reset('Waiting')
Check 'reset rows' ($L.Rows.Count -eq 4 -and $L.Rows[0].State -eq 'Waiting' -and $L.Rows[0].RowOpacity -eq 0.6)
function MkR($n,$k,$inst,$lat) { $r=[DriverUpdater.ScanResult]::new(); $r.Key="K:$n"; $r.Name=$n; $r.Manufacturer='M'; $r.Class='MEDIA'; $r.Installed=$inst; $r.Latest=$lat; $r.Kind=$k; $r.Source='Microsoft Update Catalog'; $r.Size='1 MB'; $r.Hint='Intel Driver & Support Assistant'; $r }
[void]$L.AddResult((MkR Touch Ok 16.0 16.0), $true)
[void]$L.AddResult((MkR Wifi Update 24.5 24.7), $true)
Check 'update moved to top' ($L.Rows[0].Name -eq 'Wifi' -and $L.Rows[0].NewVisibility -eq 'Visible' -and $L.Rows[0].IsChecked)
[void]$L.AddResult((MkR Camera Update 10.0 10.1), $true)
Check 'second update below first' ($L.Rows[1].Name -eq 'Camera' -and $L.TopCount -eq 2)
Check 'ok row has maker button' ($L.ByKey['K:Touch'].ButtonText -eq 'Check newer' -and $L.ByKey['K:Touch'].Size -like 'Newer builds:*')
Check 'counts' ($L.Count('Update') -eq 2 -and $L.Count('Ok') -eq 1)
$saved = @($L.Data.Values | % { [DriverUpdater.ScanResult]::FromDict($_.ToDict()) })
Check 'dict roundtrip' ($saved.Count -eq 3 -and ($saved | ? Key -eq 'K:Wifi').Latest -eq '24.7')
# Simulate: Wifi installed (no reboot), Camera installed (needs reboot)
($saved | ? Key -eq 'K:Camera').Kind = 'Installed'; ($saved | ? Key -eq 'K:Camera').Reboot = $true
($saved | ? Key -eq 'K:Wifi').Kind = 'Ok'; ($saved | ? Key -eq 'K:Wifi').Installed = '24.7'
"Reopen before restart"
$L.Inventory[3].Version = '24.7'   # wifi now new
$L.Reset('NotScanned'); $n = $L.ImportSaved([DriverUpdater.ScanResult[]]$saved)
Check 'shown 3' ($n -eq 3)
Check 'camera pending restart at top' ($L.Rows[0].Name -eq 'Camera' -and $L.Rows[0].TagText -eq 'Installed - restart to finish')
Check 'wifi up to date' ($L.ByKey['K:Wifi'].State -eq 'Ok')
Check 'audio never scanned' ($L.ByKey['K:Audio'].State -eq 'NotScanned')
"Reopen after restart"
$L.Inventory[1].Version = '10.1'
$saved2 = @($L.Data.Values | % { [DriverUpdater.ScanResult]::FromDict($_.ToDict()) })
$L.Reset('NotScanned'); [void]$L.ImportSaved([DriverUpdater.ScanResult[]]$saved2)
Check 'camera now up to date' ($L.ByKey['K:Camera'].State -eq 'Ok')
"Driver updated elsewhere"
$s3 = MkR Audio Update 6.0.1 6.0.2; $L.Inventory[0].Version = '6.0.3'; $L.Reset('NotScanned'); [void]$L.ImportSaved([DriverUpdater.ScanResult[]]@($s3))
Check 'update no longer offered' ($L.ByKey['K:Audio'].State -eq 'Ok')
$s4 = MkR Audio Update 6.0.1 6.0.9; $L.Reset('NotScanned'); [void]$L.ImportSaved([DriverUpdater.ScanResult[]]@($s4))
Check 'changed but still older -> rescan' ($L.ByKey['K:Audio'].State -eq 'NotScanned')
"States"
$it = $L.ByKey['K:Audio']; $it.ApplyState('Installing', (MkR Audio Update 6.0.3 6.0.9), $false)
Check 'installing' ($it.State -eq 'Installing' -and $it.SpinVisibility -eq 'Visible' -and -not $it.ButtonEnabled -and $it.TagText -eq 'Installing...')
""; if ($fail) { "$fail FAILED" } else { 'ALL PASSED' }
