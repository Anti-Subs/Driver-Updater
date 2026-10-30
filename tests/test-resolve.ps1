Add-Type -Path ../src/Core.cs, ../src/Vendors.cs -ErrorAction Stop
$fail=0; function Check($n,$c){ if($c){"  ok   $n"}else{"  FAIL $n";$script:fail++} }
function Dev($key,$name,$ver,[string[]]$ids){ $d=[DriverUpdater.DeviceInfo]::new(); $d.Key=$key;$d.Name=$name;$d.Manufacturer='M';$d.Class='MEDIA';$d.Version=$ver;$d.HardwareIds=$ids;$d }
$amp = Dev 'amp' 'Cirrus Logic CS35L56 Speaker Amps' '17.54.59.438' @('ACPI\VEN_CSC&DEV_3556&SUBSYS_10431C63','ACPI\CSC3556','*CSC3556')
$pkg = [DriverUpdater.VendorPackage]::new(); $pkg.Source='ASUS'; $pkg.Title='Cirrus SmartAMP Driver'; $pkg.Version='23.26.47.797'; $pkg.Url='https://dlcdnets.asus.com/c.zip'; $pkg.HardwareIds=@('ACPI\VEN_CSC&DEV_3551&SUBSYS_1043123F','ACPI\VEN_CSC&DEV_3556&SUBSYS_10431C63')
"ASUS package with its own version numbering"
Check 'matches by hardware ID' ([DriverUpdater.VendorMatch]::Matches($amp, $pkg))
$best = [System.Collections.Generic.Dictionary[string,DriverUpdater.ScanResult]]::new([StringComparer]::OrdinalIgnoreCase)
$found = [DriverUpdater.VendorMatch]::ByHardwareId([DriverUpdater.VendorPackage[]]@($pkg), [DriverUpdater.DeviceInfo[]]@($amp))
Check 'numbers differ -> no answer yet' ($found.Count -eq 0)
$u = [DriverUpdater.VendorMatch]::Unresolved([DriverUpdater.VendorPackage[]]@($pkg), [DriverUpdater.DeviceInfo[]]@($amp), $found)
Check 'needs a look inside the package' ($u.Count -eq 1 -and $u[0].Value.Title -eq 'Cirrus SmartAMP Driver')
$inf = "[Version]`r`nSignature=`"`$WINDOWS NT`$`"`r`nClass=Media`r`nDriverVer = 09/30/2026,17.60.12.101`r`n[Models]`r`n%Amp% = Install, ACPI\VEN_CSC&DEV_3556&SUBSYS_10431C63`r`n"
Check 'reads DriverVer from INF' ([DriverUpdater.VendorMatch]::InfVersionFor($inf, $amp.HardwareIds) -eq '17.60.12.101')
Check 'ignores INF for other devices' ($null -eq [DriverUpdater.VendorMatch]::InfVersionFor($inf.Replace('DEV_3556','DEV_3551').Replace('SUBSYS_10431C63','SUBSYS_1043123F'), @('ACPI\VEN_XYZ&DEV_0001')))
$real = [DriverUpdater.VendorPackage]::new(); $real.Source='ASUS'; $real.Title=$pkg.Title; $real.Version='17.60.12.101'; $real.Url=$pkg.Url; $real.HardwareIds=$pkg.HardwareIds
$null = [DriverUpdater.VendorMatch]::Offer($found, $amp, $real)
Check 'real version newer -> update' ($found['amp'].Kind -eq 'Update' -and $found['amp'].Latest -eq '17.60.12.101')
$found2 = [System.Collections.Generic.Dictionary[string,DriverUpdater.ScanResult]]::new([StringComparer]::OrdinalIgnoreCase)
$real.Version='17.54.59.438'; if (-not [DriverUpdater.VendorMatch]::Offer($found2, $amp, $real)) { [DriverUpdater.VendorMatch]::Confirm($found2, $amp, $real) }
Check 'same real version -> confirmed current' ($found2['amp'].Kind -eq 'Ok')

"Updates Windows won't use"
function SR($k,$kind,$latest,$src){ $r=[DriverUpdater.ScanResult]::new(); $r.Key=$k;$r.Name=$k;$r.Installed='20.40.12849.0';$r.Latest=$latest;$r.Kind=$kind;$r.Source=$src;$r }
$store = [System.Collections.Generic.HashSet[string]]::new([string[]]@('20.43.12874.0','1.2.3.4'))
$r = SR 'sst' 'Update' '20.43.12874.0' 'Microsoft Update Catalog'; [DriverUpdater.ScanResult]::ApplyKeptTo($r, $store, $null)
Check 'already in driver store -> Kept' ($r.Kind -eq 'Kept')
$r = SR 'x' 'Update' '20.50.0.0' 'Microsoft Update Catalog'; [DriverUpdater.ScanResult]::ApplyKeptTo($r, $store, $null)
Check 'genuinely new -> still Update' ($r.Kind -eq 'Update')
$kept = [System.Collections.Generic.Dictionary[string,string]]::new(); $kept['y']='5.0'
$r = SR 'y' 'Update' '5.0' 'Intel'; [DriverUpdater.ScanResult]::ApplyKeptTo($r, $null, $kept)
Check 'declined before -> Kept' ($r.Kind -eq 'Kept')
$it=[DriverUpdater.DriverItem]::new(); $it.Manufacturer='Intel'; $it.Installed='20.40.12849.0'; $r.Latest='20.43.12874.0'; $it.ApplyState('Kept',$r,$false)
Check 'row text' ($it.TagText -eq 'Up to date – Windows prefers this version' -and $it.Size -like 'Version 20.43.12874.0 was offered*' -and $it.ButtonVisibility -eq 'Collapsed')

"Restart pending that never took effect"
$L=[DriverUpdater.DriverList]::new(); $d=[DriverUpdater.DeviceInfo]::new(); $d.Key='k';$d.Name='k';$d.Version='1.0'; $L.Inventory.Add($d); $L.Reset('NotScanned')
$s = SR 'k' 'Installed' '2.0' 'Microsoft Update Catalog'; $s.Installed='1.0'
$null=$L.ImportSaved([DriverUpdater.ScanResult[]]@($s), $false); Check 'no restart yet -> restart to finish' ($L.ByKey['k'].TagText -eq 'Installed – restart to finish')
$s = SR 'k' 'Installed' '2.0' 'Microsoft Update Catalog'; $s.Installed='1.0'; $L.Reset('NotScanned')
$null=$L.ImportSaved([DriverUpdater.ScanResult[]]@($s), $true); Check 'restarted, still old -> Windows kept it' ($L.ByKey['k'].State -eq 'Kept')

"NVIDIA companions"
$b = SR 'hd' 'Ok' '' 'NVIDIA'; $b.Ref='bundled'; $it=[DriverUpdater.DriverItem]::new(); $it.Manufacturer='NVIDIA'; $it.ApplyState('Ok',$b,$false)
Check 'says it comes with the graphics driver' ($it.Source -eq 'Comes with the NVIDIA graphics driver' -and $it.TagText -eq 'Up to date')
""; if($fail){"$fail FAILED"}else{'ALL PASSED'}
