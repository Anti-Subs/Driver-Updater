Add-Type -Path ../src/Core.cs, ../src/Vendors.cs -ErrorAction Stop
$fail = 0
function Check($name, $cond) { if ($cond) { "  ok   $name" } else { "  FAIL $name"; $script:fail++ } }
$json = @'
{"url":"x","html_url":"https://github.com/Anti-Subs/Driver-Updater/releases/tag/v3.6.0","tag_name":"v3.6.0","name":"Driver Updater 3.6.0","draft":false,"prerelease":false,
 "body":"## What's new\r\n- **Update banner** when a new version is out\r\n- See [notes](https://example.com)\r\n",
 "assets":[{"name":"notes.txt","size":10,"browser_download_url":"https://github.com/Anti-Subs/Driver-Updater/releases/download/v3.6.0/notes.txt"},
           {"name":"DriverUpdaterSetup-3.6.0.exe","size":420000,"digest":"sha256:abc123","browser_download_url":"https://github.com/Anti-Subs/Driver-Updater/releases/download/v3.6.0/DriverUpdaterSetup-3.6.0.exe"}]}
'@
"App updates"
$r = [DriverUpdater.AppRelease]::Parse($json)
Check 'parsed release' ($r -ne $null -and $r.Version -eq '3.6.0' -and $r.AssetName -eq 'DriverUpdaterSetup-3.6.0.exe' -and $r.Size -eq 420000 -and $r.Digest -eq 'sha256:abc123')
Check 'download from github only' ($r.AssetUrl.StartsWith('https://github.com/'))
Check 'pre-release ignored' ($null -eq [DriverUpdater.AppRelease]::Parse($json.Replace('"prerelease":false','"prerelease":true')))
Check 'draft ignored' ($null -eq [DriverUpdater.AppRelease]::Parse($json.Replace('"draft":false','"draft":true')))
Check 'installer must match the tag' ($null -eq [DriverUpdater.AppRelease]::Parse($json.Replace('"tag_name":"v3.6.0"','"tag_name":"v3.7.0"')))
Check 'other hosts refused' ($null -eq [DriverUpdater.AppRelease]::Parse($json.Replace('https://github.com/Anti-Subs/Driver-Updater/releases/download/v3.6.0/DriverUpdaterSetup','https://evil.example/DriverUpdaterSetup')))
Check 'newer' ([DriverUpdater.AppRelease]::IsNewer('3.6.0', '3.5.3'))
Check 'same is not newer' (-not [DriverUpdater.AppRelease]::IsNewer('3.5.3', '3.5.3') -and -not [DriverUpdater.AppRelease]::IsNewer('3.5.3.0', '3.5.3'))
Check 'older is not newer' (-not [DriverUpdater.AppRelease]::IsNewer('3.4.2', '3.5.3'))
Check 'file version newer' ([DriverUpdater.AppRelease]::IsNewer('3.6.0.0', '3.5.3'))
$n = [DriverUpdater.AppRelease]::PlainNotes($r.Notes)
Check 'plain notes' ($n -like "What's new*" -and $n -like "*$([char]0x2022) Update banner when*" -and $n -notlike '*https*' -and $n -notlike '*`*`**')
if ($fail) { exit 1 }
