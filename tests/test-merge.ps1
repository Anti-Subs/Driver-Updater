Add-Type -Path ../src/Core.cs -ErrorAction Stop
$fail=0; function Check($n,$c){ if($c){"  ok   $n"}else{"  FAIL $n";$script:fail++} }
function SR($k,$kind,$v,$src){ $r=[DriverUpdater.ScanResult]::new(); $r.Key=$k;$r.Name=$k;$r.Installed='1.0';$r.Latest=$v;$r.Kind=$kind;$r.Source=$src;$r }
$L=[DriverUpdater.DriverList]::new()
foreach($n in 'a','b','c'){ $d=[DriverUpdater.DeviceInfo]::new(); $d.Key=$n;$d.Name=$n;$d.Version='1.0'; $L.Inventory.Add($d) }
$L.Reset('Waiting')
"Merging results that arrive in any order"
Check 'first result applied' ($L.Merge((SR a Ok 1.0 'Microsoft Update Catalog'),$true))
Check 'update beats up-to-date' ($L.Merge((SR a Update 2.0 'Windows Update'),$true) -and $L.ByKey['a'].State -eq 'Update' -and $L.Rows[0].Key -eq 'a')
Check 'up-to-date does not undo update' (-not $L.Merge((SR a Ok 1.0 'Microsoft Update Catalog'),$true) -and $L.ByKey['a'].State -eq 'Update')
Check 'newer update wins' ($L.Merge((SR a Update 3.0 'NVIDIA'),$true) -and $L.Data['a'].Latest -eq '3.0' -and $L.TopCount -eq 1)
Check 'older update loses' (-not $L.Merge((SR a Update 2.5 'Windows Update'),$true))
Check 'same version prefers Microsoft' ($L.Merge((SR a Update 3.0 'Microsoft Update Catalog'),$true) -and $L.Data['a'].Source -eq 'Microsoft Update Catalog')
Check 'unknown replaced by ok' ($L.Merge((SR b Unknown '' ''),$true) -and $L.Merge((SR b Ok 1.0 'x'),$true))
Check 'vendor-hint does not undo ok' (-not $L.Merge((SR b Vendor '' ''),$true))
Check 'second update goes under first' ($L.Merge((SR c Update 5.0 'Intel'),$true) -and $L.Rows[1].Key -eq 'c' -and $L.TopCount -eq 2)
""; if($fail){"$fail FAILED"}else{'ALL PASSED'}
