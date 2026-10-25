# Runs every logic test (works on Windows PowerShell 7 or pwsh on Linux/macOS). Run from the tests folder.
Set-Location $PSScriptRoot
$failed = 0
foreach ($t in 'test-core.ps1','test-vendors.ps1','test-merge.ps1','test-resolve.ps1','test-desktop.ps1','test-unzip.ps1','test-appupdate.ps1','test-static.ps1') {
    $out = pwsh -NoProfile -File $t 2>&1
    $bad = @($out | Where-Object { $_ -match 'FAIL' })
    "{0,-18} {1}" -f $t, $(if ($bad.Count) { "FAILED"; $failed++ } else { "passed" })
    $bad | ForEach-Object { "    $_" }
}
if ($failed) { exit 1 }
