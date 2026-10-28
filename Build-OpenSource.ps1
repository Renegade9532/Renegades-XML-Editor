$ErrorActionPreference = 'Stop'
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'The .NET Framework C# compiler was not found. Install the .NET Framework 4.x Developer Pack.' }

$outDir = Join-Path $PSScriptRoot 'Release-OpenSource'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$output = Join-Path $outDir 'Renegades XML Editor.exe'
$icon = Join-Path $PSScriptRoot 'RenegadeWolfLauncher.ico'
$sources = @('Program.cs', 'PremiumMdt.cs', 'NativeRpfArchive.cs', 'NativeResourceReader.cs', 'NativeVehiclePreview.cs', 'NativeTextureDecoder.cs') | ForEach-Object { Join-Path $PSScriptRoot $_ }

& $compiler /nologo /target:winexe "/win32icon:$icon" /main:RedefinedVehicleImporter.PremiumMdtProgram "/out:$output" /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Xml.Linq.dll /r:'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\System.Xaml.dll' /r:'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationCore.dll' /r:'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationFramework.dll' /r:'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll' $sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Write-Host "Built: $output"
