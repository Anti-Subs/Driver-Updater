$src = Get-Content ../src/VendorNet.cs -Raw
$i = $src.IndexOf('        public static void Unzip'); $j = $src.IndexOf('        public static InstallOutcome Install(', $i)
$code = "using System; using System.IO; using System.IO.Compression; namespace T { public static class Z {`n" + $src.Substring($i, $j - $i) + "}}"
Add-Type -TypeDefinition $code -ReferencedAssemblies System.IO.Compression, System.IO.Compression.ZipFile, System.Runtime -ErrorAction Stop
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$fail = 0
function Check($name, $cond) { if ($cond) { "  ok   $name" } else { "  FAIL $name"; $script:fail++ } }
$tmp = Join-Path ([IO.Path]::GetTempPath()) ("uz" + [guid]::NewGuid()); New-Item -ItemType Directory $tmp | Out-Null
$zipPath = Join-Path $tmp 'p.zip'
$fs = [IO.File]::Create($zipPath); $za = [IO.Compression.ZipArchive]::new($fs, [IO.Compression.ZipArchiveMode]::Create)
foreach ($n in 'pkg/Bin64/7z.dll','pkg/Bin64/7z.dll','pkg/Setup.exe','../evil.txt') { $e = $za.CreateEntry($n); $w = [IO.StreamWriter]::new($e.Open()); $w.Write($n); $w.Dispose() }
$za.Dispose(); $fs.Dispose()
$out = Join-Path $tmp 'extracted'
New-Item -ItemType Directory (Join-Path $out 'pkg/Bin64') -Force | Out-Null; Set-Content (Join-Path $out 'pkg/Bin64/7z.dll') 'old'
"Unzip"
try { [T.Z]::Unzip($zipPath, $out); $okRun = $true } catch { $okRun = $false; "  " + $_ }
Check 'leftovers and duplicate entries unpack fine' $okRun
Check 'files extracted' ((Test-Path (Join-Path $out 'pkg/Setup.exe')) -and (Get-Content (Join-Path $out 'pkg/Bin64/7z.dll')) -eq 'pkg/Bin64/7z.dll')
Check 'nothing written outside' (-not (Test-Path (Join-Path $tmp 'evil.txt')))
Remove-Item $tmp -Recurse -Force
if ($fail) { exit 1 }
