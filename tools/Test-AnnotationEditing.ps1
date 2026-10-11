param(
    [string]$AssemblyPath = "$PSScriptRoot\..\Kiritori\bin\x64\AnnotationToolsPreview\Kiritori.exe"
)
$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$directory = Join-Path $workspace 'Kiritori.Tests\TestResults\AnnotationEditing'
New-Item -ItemType Directory -Force -Path $directory | Out-Null
$executable = Join-Path $directory 'AnnotationEditingProbe.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$manifest = Join-Path $workspace 'Kiritori\Properties\app.manifest'
$source = Join-Path $PSScriptRoot 'AnnotationEditingProbe.cs'
& $compiler /nologo /target:exe /platform:x64 /r:System.Windows.Forms.dll /r:System.Drawing.dll "/win32manifest:$manifest" "/out:$executable" $source
if ($LASTEXITCODE -ne 0) { throw "Annotation editing probe compilation failed: $LASTEXITCODE" }
Copy-Item -LiteralPath (Join-Path $workspace 'Kiritori\App.config') -Destination "$executable.config"
& $executable (Resolve-Path -LiteralPath $AssemblyPath).Path | Tee-Object -FilePath (Join-Path $directory 'results.txt')
if ($LASTEXITCODE -ne 0) { throw "Annotation editing probe failed: $LASTEXITCODE" }
