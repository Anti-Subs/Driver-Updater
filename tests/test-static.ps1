# Source checks for mistakes that compile fine but break the app at start-up
$fail = 0
function Check($name, $cond) { if ($cond) { "  ok   $name" } else { "  FAIL $name"; $script:fail++ } }
$app = Get-Content ../src/App.cs -Raw
"Start-up"
Check 'Main runs on an STA thread (WPF needs it)' ($app -match '\[STAThread\]\s*public static int Main\(')
"Dark mode"
Add-Type -Path ../src/Core.cs, ../src/Xaml.cs -ErrorAction Stop
[DriverUpdater.Theme]::Dark = $true
foreach ($n in 'Main','Setup') {
    $x = [DriverUpdater.Theme]::Apply([DriverUpdater.Xaml]::$n)
    $ok = $true; try { [xml]$x | Out-Null } catch { $ok = $false }
    Check "$n window markup still valid in dark mode" $ok
    Check "$n window has no white or light-grey surfaces left" ($x -notmatch '(Background|BorderBrush|Fill)="White"' -and $x -notmatch 'Property="(Background|BorderBrush)"\s+Value="White"' -and $x -notmatch '#F4F6FB|#111827')
}
$x = [DriverUpdater.Theme]::Apply([DriverUpdater.Xaml]::Main)
Check 'white text on coloured buttons and badges kept' ($x -match 'Foreground="White"')
Check 'every colour has a dark partner' (-not ([regex]::Matches([DriverUpdater.Xaml]::Main + [DriverUpdater.Xaml]::Setup, '(?<=")#[0-9A-Fa-f]{6}(?=")') | % Value | Sort-Object -Unique | ? { $_ -notin '#1D4ED7','#EF4444','#F59E0B' -and [DriverUpdater.Theme]::C($_) -eq $_ }))
[DriverUpdater.Theme]::Dark = $false
Check 'light mode unchanged' ([DriverUpdater.Theme]::Apply([DriverUpdater.Xaml]::Main) -eq [DriverUpdater.Xaml]::Main)
Check 'auto follows Windows' ([DriverUpdater.Theme]::ShouldBeDark('auto', $true) -and -not [DriverUpdater.Theme]::ShouldBeDark('auto', $false) -and [DriverUpdater.Theme]::ShouldBeDark('dark', $false) -and -not [DriverUpdater.Theme]::ShouldBeDark('light', $true))
if ($fail) { exit 1 }
