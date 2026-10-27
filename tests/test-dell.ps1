Add-Type -Path ../src/Core.cs, ../src/Vendors.cs -ErrorAction Stop
$fail = 0
function Check($name, $cond) { if ($cond) { "  ok   $name" } else { "  FAIL $name"; $script:fail++ } }
function Dev($key,$name,$ver,[string[]]$ids) { $d=[DriverUpdater.DeviceInfo]::new(); $d.Key=$key;$d.Name=$name;$d.Manufacturer='M';$d.Class='SYSTEM';$d.Version=$ver;$d.HardwareIds=$ids;$d }
$xml = Get-Content -Raw (Join-Path $PSScriptRoot 'tests/fixtures/dell-catalog-sample.xml') -ErrorAction SilentlyContinue
if (-not $xml) { $xml = Get-Content -Raw (Join-Path $PSScriptRoot 'fixtures/dell-catalog-sample.xml') }
"Dell catalog"
$p11 = [DriverUpdater.Dell]::Parse($xml, $true, '0BDA')
Check 'drivers for Windows 11 with PCI IDs only' ($p11.Count -eq 2 -and ($p11 | ? Title -like 'Realtek Memory Card*') -and ($p11 | ? Title -like 'Intel Dynamic Tuning*'))
$rt = $p11 | ? Title -like 'Realtek Memory Card*'
Check 'https download from downloads.dell.com' ($rt.Url -like 'https://downloads.dell.com/FOLDER*/Realtek-Memory-Card-Reader-Driver_T63C5_*.EXE')
Check 'version and size' ($rt.Version -eq '10.0.22621.21366' -and $rt.Size -like '*MB')
Check 'hardware IDs built from PCI info' (($rt.HardwareIds -contains 'PCI\VEN_10EC&DEV_5227') -and ($rt.HardwareIds | ? { $_ -like 'PCI\VEN_10EC&DEV_525A&SUBSYS_*1028' }))
Check 'other models filtered out' ([DriverUpdater.Dell]::Parse($xml, $true, 'FFFF').Count -eq 0)
Check 'BIOS and Windows 7 packages ignored' (-not ($p11 | ? { $_.Title -match 'BIOS' }))
$d = Dev 'PCI\VEN_10EC&DEV_525A\1' 'Realtek PCIE CardReader' '10.0.22000.21355' @('PCI\VEN_10EC&DEV_525A&SUBSYS_0A9F1028&REV_01','PCI\VEN_10EC&DEV_525A&SUBSYS_0A9F1028')
$other = Dev 'PCI\VEN_10EC&DEV_525A\2' 'Realtek PCIE CardReader (HP)' '10.0.22000.21355' @('PCI\VEN_10EC&DEV_525A&SUBSYS_8A12103C&REV_01')
$r = [DriverUpdater.VendorMatch]::ByHardwareId($p11, [DriverUpdater.DeviceInfo[]]@($d))
Check 'newer Dell driver offered' ($r.ContainsKey($d.Key) -and $r[$d.Key].Kind -eq 'Update' -and $r[$d.Key].Latest -eq '10.0.22621.21366' -and $r[$d.Key].Source -eq 'Dell')
$cur = Dev 'PCI\VEN_10EC&DEV_525A\3' 'Realtek PCIE CardReader' '10.0.22621.21366' @('PCI\VEN_10EC&DEV_525A&SUBSYS_0A9F1028')
$r2 = [DriverUpdater.VendorMatch]::ByHardwareId($p11, [DriverUpdater.DeviceInfo[]]@($cur))
Check 'same version confirmed' ($r2[$cur.Key].Kind -eq 'Ok')
$idx = '<?xml version="1.0" encoding="utf-8"?><ManifestIndex><GroupManifest><SupportedSystems><Brand key="1" prefix="ALIENWARE"><Model systemID="0B0B"><Display lang="en">Alienware-m16-R1</Display></Model></Brand></SupportedSystems><ManifestInformation path="catalog/CatalogIndexModel_0B0B.cab" /></GroupManifest><GroupManifest><SupportedSystems><Brand key="2" prefix="LAT"><Model systemID="0CC1"/></Brand></SupportedSystems><ManifestInformation path="/catalog/CatalogIndexModel_0CC1.cab" /></GroupManifest></ManifestIndex>'
Check 'model catalog found by system ID' ([DriverUpdater.Dell]::ModelCatalogPath($idx, '0b0b') -eq 'catalog/CatalogIndexModel_0B0B.cab' -and [DriverUpdater.Dell]::ModelCatalogPath($idx, '0CC1') -eq 'catalog/CatalogIndexModel_0CC1.cab')
Check 'unknown system ID' ($null -eq [DriverUpdater.Dell]::ModelCatalogPath($idx, '1234'))
$audioXml = '<?xml version="1.0" encoding="utf-8"?><Manifest baseLocation="downloads.dell.com"><SoftwareComponent path="FOLDER1/1/Realtek-High-Definition-Audio-Driver_X_WIN64_6.0.9499.1_A69.EXE" vendorVersion="6.0.9499.1" size="1000"><Name><Display lang="en">Realtek High Definition Audio Driver</Display></Name><ComponentType value="DRVR"/><SupportedDevices><Device componentID="1"><PCIInfo deviceID="0289" vendorID="10EC" subDeviceID="09FF" subVendorID="1028"/></Device></SupportedDevices><SupportedOperatingSystems><OperatingSystem osCode="W21P4" osArch="x64"><Display lang="en">Windows 11</Display></OperatingSystem></SupportedOperatingSystems><SupportedSystems><Brand><Model systemID="09FF"/></Brand></SupportedSystems></SoftwareComponent></Manifest>'
$ap = [DriverUpdater.Dell]::Parse($audioXml, $true, '09FF')
$codec = Dev 'INTELAUDIO\FUNC_01&VEN_10EC&DEV_0289' 'Realtek Audio' '6.0.9000.1' @('INTELAUDIO\FUNC_01&VEN_10EC&DEV_0289&SUBSYS_102809FF&REV_1000','INTELAUDIO\FUNC_01&VEN_10EC&DEV_0289&SUBSYS_102809FF')
$ar = [DriverUpdater.VendorMatch]::ByHardwareId($ap, [DriverUpdater.DeviceInfo[]]@($codec))
Check 'Realtek audio codec matched (HD Audio IDs)' ($ar.ContainsKey($codec.Key) -and $ar[$codec.Key].Kind -eq 'Update' -and $ar[$codec.Key].Latest -eq '6.0.9499.1')
$otherPc = Dev 'HDAUDIO\FUNC_01&VEN_10EC&DEV_0289' 'Realtek Audio' '6.0.9000.1' @('HDAUDIO\FUNC_01&VEN_10EC&DEV_0289&SUBSYS_10431E12')
Check 'other PC maker codec not matched' (-not ([DriverUpdater.VendorMatch]::ByHardwareId($ap, [DriverUpdater.DeviceInfo[]]@($otherPc))).ContainsKey($otherPc.Key))
"Sources list"
$s = [DriverUpdater.SourceInfo]::All('Alienware', 'NVIDIA GeForce RTX 4090', 'Intel Core i9')
Check 'Dell/Alienware listed and used' (($s | ? Name -eq 'Dell and Alienware').BadgeVisibility -eq 'Visible' -and ($s | ? Name -eq 'ASUS').BadgeVisibility -eq 'Collapsed')
if ($fail) { exit 1 }
