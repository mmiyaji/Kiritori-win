param(
    [string]$AssemblyPath = "$PSScriptRoot\..\Kiritori\bin\x64\EffectsPreview\Kiritori.exe",
    [switch]$Live,
    [int]$MonitorIndex = -1
)
# Shows the production form with a taskbar entry for Computer Use discovery.
# Settings are isolated under the probe executable's own user.config.
$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$directory = Join-Path $workspace 'Kiritori.Tests\TestResults\WindowReview'
New-Item -ItemType Directory -Force -Path $directory | Out-Null
$executable = Join-Path $directory 'WindowUiProbe.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$manifest = Join-Path $workspace 'Kiritori\Properties\app.manifest'
$source = Join-Path $PSScriptRoot 'WindowUiProbe.cs'
& $compiler /nologo /target:winexe /platform:x64 /r:System.Windows.Forms.dll /r:System.Drawing.dll "/win32manifest:$manifest" "/out:$executable" $source
if ($LASTEXITCODE -ne 0) { throw "UI probe compilation failed: $LASTEXITCODE" }
Copy-Item -LiteralPath (Join-Path $workspace 'Kiritori\App.config') -Destination "$executable.config"
$arguments = @((Resolve-Path -LiteralPath $AssemblyPath).Path)
if ($Live) { $arguments += '--live' }
if ($MonitorIndex -ge 0) { $arguments += "--monitor-index=$MonitorIndex" }
Start-Process -FilePath $executable -ArgumentList $arguments -PassThru
