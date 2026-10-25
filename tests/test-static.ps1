# Source checks for mistakes that compile fine but break the app at start-up
$fail = 0
function Check($name, $cond) { if ($cond) { "  ok   $name" } else { "  FAIL $name"; $script:fail++ } }
$app = Get-Content ../src/App.cs -Raw
"Start-up"
Check 'Main runs on an STA thread (WPF needs it)' ($app -match '\[STAThread\]\s*public static int Main\(')
if ($fail) { exit 1 }
