# Compiles C# sources into a .NET Framework 4.8 Windows executable using the Roslyn compiler bundled with PowerShell.
param(
  [string[]]$Sources, [string]$Out, [string]$Icon, [string]$Manifest,
  [string]$Kind = "WindowsApplication", [switch]$Optimize, [string]$RefDir = "/tmp/refasm/v4.8"
)
$ErrorActionPreference = "Stop"
$Sources = @($Sources | ForEach-Object { $_ -split "," } | Where-Object { $_ })
Add-Type -Path (Join-Path $PSHOME 'Microsoft.CodeAnalysis.dll')
Add-Type -Path (Join-Path $PSHOME 'Microsoft.CodeAnalysis.CSharp.dll')
$ref = $RefDir
$names = 'mscorlib','System','System.Core','System.IO.Compression','System.IO.Compression.FileSystem','System.Management','System.Net.Http','System.Windows.Forms','System.Drawing','WindowsBase','System.Xaml','Microsoft.CSharp','System.Web.Extensions','System.Xml','System.Runtime.Serialization'
$refs = foreach ($n in $names) { [Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile("$ref/$n.dll") }
$refs += foreach ($f in Get-ChildItem "$ref/Facades/*.dll") { [Microsoft.CodeAnalysis.MetadataReference]::CreateFromFile($f.FullName) }
$parse = [Microsoft.CodeAnalysis.CSharp.CSharpParseOptions]::new([Microsoft.CodeAnalysis.CSharp.LanguageVersion]::CSharp9)
$trees = foreach ($s in $Sources) { [Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree]::ParseText([IO.File]::ReadAllText($s), $parse, $s, [Text.Encoding]::UTF8) }
$opt = [Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions]::new([Microsoft.CodeAnalysis.OutputKind]$Kind)
$opt = $opt.WithPlatform([Microsoft.CodeAnalysis.Platform]::AnyCpu).WithOptimizationLevel($(if ($Optimize) { [Microsoft.CodeAnalysis.OptimizationLevel]::Release } else { [Microsoft.CodeAnalysis.OptimizationLevel]::Debug }))
$comp = [Microsoft.CodeAnalysis.CSharp.CSharpCompilation]::Create([IO.Path]::GetFileNameWithoutExtension($Out), [Microsoft.CodeAnalysis.SyntaxTree[]]@($trees), [Microsoft.CodeAnalysis.MetadataReference[]]@($refs), $opt)
$res = $null
$man = if ($Manifest) { [IO.File]::OpenRead($Manifest) } else { $null }
$ico = if ($Icon) { [IO.File]::OpenRead($Icon) } else { $null }
$res = $comp.CreateDefaultWin32Resources($true, $false, $man, $ico)
$fs = [IO.File]::Create($Out)
try { $r = $comp.Emit($fs, $null, $null, $res) } finally { $fs.Dispose(); if ($man) { $man.Dispose() }; if ($ico) { $ico.Dispose() } }
$diag = $r.Diagnostics | Where-Object { $_.Severity -ge [Microsoft.CodeAnalysis.DiagnosticSeverity]::Warning -and $_.Id -notin 'CS1701','CS1702' }
$diag | ForEach-Object { $_.ToString() }
if (-not $r.Success) { Remove-Item $Out -ErrorAction SilentlyContinue; Write-Host 'BUILD FAILED'; exit 1 }
Write-Host "BUILD OK -> $Out ($((Get-Item $Out).Length) bytes)"
