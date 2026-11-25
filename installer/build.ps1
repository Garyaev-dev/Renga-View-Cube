$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$pluginDir = Join-Path $repoRoot 'bin\Release\ViewCubeOverlay'
$files = @('ViewCubeOverlay.dll','ViewCubeOverlay.deps.json','ViewCubeOverlay.rndesc','icon.png','Renga.NET8.PluginUtility.dll','Interop.Renga.dll')
$inputFiles = @($files | ForEach-Object { $path = Join-Path $pluginDir $_; if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing release file: $path" }; $path })
$inputFiles += Join-Path $repoRoot 'LICENSE.md'
[xml]$descriptor = Get-Content -LiteralPath (Join-Path $pluginDir 'ViewCubeOverlay.rndesc') -Raw -Encoding UTF8
$version = $descriptor.RengaPlugin.Version
if ($version -notmatch '^\d+\.\d+\.\d+\.\d+$') { throw 'Invalid plugin version.' }
$payload = Join-Path $repoRoot 'bin\ViewCubeInstaller-payload.zip'
$output = Join-Path $repoRoot "bin\ViewCubeInstaller-v$version-v2.exe"
Compress-Archive -LiteralPath $inputFiles -DestinationPath $payload -Force
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'C# compiler for .NET Framework 4 was not found.' }
& $compiler /nologo /target:winexe /platform:x64 /codepage:65001 "/out:$output" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "/resource:$payload,ViewCubeInstaller.payload.zip" (Join-Path $PSScriptRoot 'Program.cs') (Join-Path $PSScriptRoot 'RengaDiscovery.cs') (Join-Path $PSScriptRoot 'InstallerForm.cs')
if ($LASTEXITCODE -ne 0) { throw "Installer compilation failed ($LASTEXITCODE)." }
$test = Start-Process -FilePath $output -ArgumentList "--self-test" -WindowStyle Hidden -PassThru -Wait
if ($test.ExitCode -ne 0) { throw "Installer self-test failed ($($test.ExitCode))." }
Write-Host "Installer built and self-tested: $output"
