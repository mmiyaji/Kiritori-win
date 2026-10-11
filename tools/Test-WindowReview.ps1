param(
    [string]$AssemblyPath = "$PSScriptRoot\..\Kiritori\bin\x64\EffectsPreview\Kiritori.exe",
    [switch]$RequireHighDpi
)
# A separate EXE mirrors the app's DPI manifest/config. Settings.Save therefore
# uses the probe's own user.config, not the running Kiritori user's configuration.
$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$probeDirectory = Join-Path $workspace 'Kiritori.Tests\TestResults\WindowReview'
New-Item -ItemType Directory -Force -Path $probeDirectory | Out-Null
$probeExecutable = Join-Path $probeDirectory 'WindowDpiProbe.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$manifest = Join-Path $workspace 'Kiritori\Properties\app.manifest'
$source = Join-Path $PSScriptRoot 'WindowDpiProbe.cs'
& $compiler /nologo /target:exe /platform:x64 /r:System.Windows.Forms.dll /r:System.Drawing.dll "/win32manifest:$manifest" "/out:$probeExecutable" $source
if ($LASTEXITCODE -ne 0) { throw "Probe compilation failed: $LASTEXITCODE" }
Copy-Item -LiteralPath (Join-Path $workspace 'Kiritori\App.config') -Destination "$probeExecutable.config"
$probeArguments = @((Resolve-Path -LiteralPath $AssemblyPath).Path)
if ($RequireHighDpi) { $probeArguments += '--require-high-dpi' }
& $probeExecutable @probeArguments | Tee-Object -FilePath (Join-Path $probeDirectory 'results.txt')
if ($LASTEXITCODE -ne 0) { throw "Window regression probe failed: $LASTEXITCODE" }
