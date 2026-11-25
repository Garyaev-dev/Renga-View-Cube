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
$output = Join-Path $repoRoot "bin\ViewCubeInstaller-v$version.exe"
Compress-Archive -LiteralPath $inputFiles -DestinationPath $payload -Force
# Generate a multi-resolution Windows icon from the plugin image.
Add-Type -AssemblyName System.Drawing
$iconPath = Join-Path $repoRoot 'bin\ViewCubeInstaller.ico'
$imagePath = Join-Path $pluginDir 'icon.png'
$image = [System.Drawing.Image]::FromFile($imagePath)
$frames = @()
try {
  foreach ($size in @(16, 24, 32, 48, 64, 128, 256)) {
    $bitmap = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $stream = [System.IO.MemoryStream]::new()
    try {
      $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
      $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
      $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
      $graphics.DrawImage($image, 0, 0, $size, $size)
      $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
      $frames += ,@{ Size = $size; Bytes = $stream.ToArray() }
    } finally { $stream.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
  }
} finally { $image.Dispose() }
$file = [System.IO.File]::Create($iconPath)
$writer = [System.IO.BinaryWriter]::new($file)
try {
  $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$frames.Count)
  $offset = 6 + 16 * $frames.Count
  foreach ($frame in $frames) {
    $dimension = if ($frame.Size -eq 256) { 0 } else { $frame.Size }
    $writer.Write([byte]$dimension); $writer.Write([byte]$dimension)
    $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32)
    $writer.Write([uint32]$frame.Bytes.Length); $writer.Write([uint32]$offset)
    $offset += $frame.Bytes.Length
  }
  foreach ($frame in $frames) { $writer.Write([byte[]]$frame.Bytes) }
} finally { $writer.Dispose(); $file.Dispose() }

$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'C# compiler for .NET Framework 4 was not found.' }
& $compiler /nologo /target:winexe /platform:x64 /codepage:65001 "/win32icon:$iconPath" "/out:$output" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "/resource:$payload,ViewCubeInstaller.payload.zip" (Join-Path $PSScriptRoot 'Program.cs') (Join-Path $PSScriptRoot 'RengaDiscovery.cs') (Join-Path $PSScriptRoot 'InstallerForm.cs')
if ($LASTEXITCODE -ne 0) { throw "Installer compilation failed ($LASTEXITCODE)." }
$test = Start-Process -FilePath $output -ArgumentList "--self-test" -WindowStyle Hidden -PassThru -Wait
if ($test.ExitCode -ne 0) { throw "Installer self-test failed ($($test.ExitCode))." }
Write-Host "Installer built and self-tested: $output"
