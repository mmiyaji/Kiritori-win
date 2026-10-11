param(
    [string]$AssemblyPath = "$PSScriptRoot\..\Kiritori\bin\x64\Release\Kiritori.exe",
    [string]$FfmpegPath = (Get-Command ffmpeg.exe -ErrorAction Stop).Source,
    [string]$OutputDirectory = "$PSScriptRoot\..\Kiritori.Tests\TestResults\LiveFrameEffects"
)

# Run with Windows PowerShell -STA after building Release/x64 (.NET Framework 4.8).
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $AssemblyPath).Path)
$flags = [Reflection.BindingFlags]'Instance,Public,NonPublic'
$windowType = $assembly.GetType('Kiritori.Views.LiveCapture.LivePreviewWindow', $true)
$eventType = $assembly.GetType('Kiritori.Views.LiveCapture.LiveCaptureFrameEventArgs', $true)
$optionsType = $assembly.GetType('Kiritori.Services.Recording.FfmpegPipeOptions', $true)
$recorderType = $assembly.GetType('Kiritori.Services.Recording.FfmpegPipeRecorder', $true)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$OutputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path

function Set-Field($instance, $name, $value) {
    $instance.GetType().GetField($name, $flags).SetValue($instance, $value)
}
function Get-Field($instance, $name) {
    return $instance.GetType().GetField($name, $flags).GetValue($instance)
}
function Assert-True($condition, $message) {
    if (-not $condition) { throw $message }
}
function Assert-Image($image, $effect, [bool]$encoded = $false) {
    $pixel = $image.GetPixel(24, 24)
    if ($effect -eq 'Grayscale') {
        $tolerance = if ($encoded) { 51 } else { 2 }
        Assert-True ([Math]::Abs([int]$pixel.R - $pixel.G) -le $tolerance -and
            [Math]::Abs([int]$pixel.G - $pixel.B) -le $tolerance) 'Grayscale channels differ'
    } elseif ($effect -eq 'Sepia') {
        Assert-True ($pixel.R -gt $pixel.G -and $pixel.G -gt $pixel.B) 'Sepia channel order failed'
    } else {
        Assert-True ($pixel.R -gt 30 -and $pixel.B -gt 30) 'Mosaic did not mix the checkerboard'
        $neighbor = $image.GetPixel(25, 24)
        # GDI+ GIF's palette dithers a flat color between adjacent 51-step colors.
        $tolerance = if ($encoded) { 51 } else { 0 }
        Assert-True ([Math]::Abs([int]$pixel.R - $neighbor.R) -le $tolerance -and
            [Math]::Abs([int]$pixel.G - $neighbor.G) -le $tolerance -and
            [Math]::Abs([int]$pixel.B - $neighbor.B) -le $tolerance) 'Mosaic block is not uniform'
    }
}

foreach ($effect in @('Mosaic', 'Sepia', 'Grayscale')) {
    $window = [Activator]::CreateInstance($windowType)
    $recorder = $null
    $queue = $null
    $queuedBitmap = $null
    try {
        Set-Field $window '_maxFps' 0
        $menu = @($window.ContextMenuStrip.Items | Where-Object {
            $_ -is [Windows.Forms.ToolStripMenuItem] -and $_.DropDownItems.Count -ge 4 -and
            $null -ne $_.DropDownItems[0].Tag -and $_.DropDownItems[0].Tag.GetType().Name -eq 'LiveFrameEffect'
        })[0]
        Assert-True ($null -ne $menu) 'Effect menu missing'
        $selected = @($menu.DropDownItems | Where-Object { $null -ne $_.Tag -and $_.Tag.ToString() -eq $effect })[0]
        $selected.PerformClick()
        $queueField = $windowType.GetField('_gifQueue', $flags)
        $queue = [Activator]::CreateInstance($queueField.FieldType)
        $queueField.SetValue($window, $queue)
        $clock = $windowType.GetField('_gifClock', $flags).GetValue($window)
        $clock.Restart()
        $options = [Activator]::CreateInstance($optionsType)
        $videoPath = Join-Path $OutputDirectory "$effect.mp4"
        Set-Field $options 'OutputPath' $videoPath
        Set-Field $options 'Width' 48
        Set-Field $options 'Height' 48
        Set-Field $options 'Fps' 10
        Set-Field $options 'FfmpegPath' $FfmpegPath
        $recorder = [Activator]::CreateInstance($recorderType, [object[]]@($options))
        $recorder.Start($null, $null)
        Set-Field $window '_rec' $recorder
        Start-Sleep -Milliseconds 250
        $source = New-Object Drawing.Bitmap 48,48
        for ($y = 0; $y -lt 48; $y++) {
            for ($x = 0; $x -lt 48; $x++) {
                $color = if (($x + $y) % 2 -eq 0) { [Drawing.Color]::Red } else { [Drawing.Color]::Blue }
                $source.SetPixel($x, $y, $color)
            }
        }
        $event = $eventType.GetConstructor([type[]]@([Drawing.Bitmap], [bool])).Invoke(
            [object[]]@($source.PSObject.BaseObject, $true))
        $windowType.GetMethod('OnFrameArrived', $flags).Invoke($window, [object[]]@($event)) | Out-Null
        Assert-True ($queue.Count -eq 1) 'Filtered GIF frame was not enqueued'
        $queuedBitmap = $queue.Take().Item1
        Assert-Image $queuedBitmap $effect
        $gifPath = Join-Path $OutputDirectory "$effect.gif"
        $gifFrames = New-Object 'Collections.Generic.List[Drawing.Bitmap]'
        $gifDelays = New-Object 'Collections.Generic.List[int]'
        $gifFrames.Add($queuedBitmap)
        $gifFrames.Add($queuedBitmap)
        $gifDelays.Add(10)
        $gifDelays.Add(10)
        $assembly.GetType('Kiritori.Services.Recording.GifWriter').GetMethod('SaveAnimatedGif').Invoke(
            $null, [object[]]@($gifFrames.PSObject.BaseObject, $gifDelays.PSObject.BaseObject, $gifPath.PSObject.BaseObject, $true)) | Out-Null
        $gif = [Drawing.Bitmap]::FromFile($gifPath)
        try { Assert-Image $gif $effect $true } finally { $gif.Dispose() }
        $snapshot = $windowType.GetMethod('CloneCurrentFrame', $flags).Invoke($window, @())
        try {
            Assert-Image $snapshot $effect
            Assert-True ($snapshot.GetPixel(24,24).ToArgb() -eq $queuedBitmap.GetPixel(24,24).ToArgb()) 'GIF differs from displayed snapshot'
            $snapshot.Save((Join-Path $OutputDirectory "$effect.png"))
        } finally { $snapshot.Dispose() }
        Start-Sleep -Milliseconds 500
        $recorder.StopAsync(5000).GetAwaiter().GetResult() | Out-Null
        Set-Field $window '_rec' $null
        Assert-True ((Get-Item -LiteralPath $videoPath).Length -gt 0) 'Video was not saved'
        $decodedPath = Join-Path $OutputDirectory "$effect-decoded.png"
        & $FfmpegPath -hide_banner -loglevel error -y -i $videoPath -frames:v 1 $decodedPath
        Assert-True ($LASTEXITCODE -eq 0) 'Recorded video could not be decoded'
        $decoded = [Drawing.Bitmap]::FromFile($decodedPath)
        try { Assert-Image $decoded $effect $true } finally { $decoded.Dispose() }
        Set-Field $window '_paused' $true
        if ($effect -eq 'Mosaic') {
            $strength = @($menu.DropDownItems | Where-Object { $_ -is [Windows.Forms.ToolStripMenuItem] -and $_.Text -match 'モザイクの強度|Mosaic strength' })[0]
            Assert-True ($null -ne $strength -and $strength.Enabled) 'Mosaic strength menu is unavailable'
            $rawFrame = Get-Field $window '_latest'
            $gradient = New-Object Drawing.Bitmap 48,48
            for ($y = 0; $y -lt 48; $y++) {
                for ($x = 0; $x -lt 48; $x++) {
                    $gradient.SetPixel($x, $y, $(if ($x -lt 18) { [Drawing.Color]::Red } else { [Drawing.Color]::Blue }))
                }
            }
            Set-Field $window '_latest' $gradient
            $mediumSnapshot = $windowType.GetMethod('CloneCurrentFrame', $flags).Invoke($window, @())
            $strong = @($strength.DropDownItems | Where-Object { $_.Tag -eq 24 })[0]
            $strong.PerformClick()
            Assert-True ((Get-Field $window '_mosaicBlockSize') -eq 24) 'Strong mosaic preset was not selected'
            Assert-True (@($strength.DropDownItems | Where-Object Checked).Count -eq 1) 'Mosaic strength check state is not exclusive'
            $strengthSnapshot = $windowType.GetMethod('CloneCurrentFrame', $flags).Invoke($window, @())
            try { Assert-True ($strengthSnapshot.GetPixel(20,24).ToArgb() -ne $mediumSnapshot.GetPixel(20,24).ToArgb()) 'Strength change failed while paused' }
            finally { $strengthSnapshot.Dispose(); $mediumSnapshot.Dispose(); Set-Field $window '_latest' $rawFrame; $gradient.Dispose() }
        }
        $menu.DropDownItems[0].PerformClick()
        Assert-True (@($menu.DropDownItems | Where-Object { $null -ne $_.Tag -and $_.Tag.GetType().Name -eq 'LiveFrameEffect' -and $_.Checked }).Count -eq 1) 'Effect check state is not exclusive'
        $strength = @($menu.DropDownItems | Where-Object { $_ -is [Windows.Forms.ToolStripMenuItem] -and $_.Text -match 'モザイクの強度|Mosaic strength' })[0]
        if ($strength) { Assert-True (-not $strength.Enabled) 'Mosaic strength remained enabled after selecting another effect' }
        $raw = $windowType.GetMethod('CloneCurrentFrame', $flags).Invoke($window, @())
        try { Assert-True ($raw.GetPixel(24,24).ToArgb() -eq [Drawing.Color]::Red.ToArgb()) 'None did not restore raw pixels while paused' }
        finally { $raw.Dispose() }
        Write-Output "PASS: $effect display, GIF queue/encoding, encoded MP4, paused effect update/original restore"
    } finally {
        if ($recorder) { $recorder.Dispose() }
        if ($queue) { Set-Field $window '_gifQueue' $null; $queue.Dispose() }
        if ($queuedBitmap) { $queuedBitmap.Dispose() }
        $window.Close()
        $window.Dispose()
    }
}
