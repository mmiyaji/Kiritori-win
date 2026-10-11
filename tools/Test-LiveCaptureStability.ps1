param([string]$AssemblyPath = "$PSScriptRoot\..\Kiritori\bin\x64\EffectsPreview\Kiritori.exe")

# Run with Windows PowerShell -STA. No user settings or running app are changed.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -TypeDefinition @'
using System;
using System.Collections;
using System.Reflection;
using System.Threading;
public static class LiveRecordingRaceProbe
{
    public static Exception ConsumerFailure;
    public static Thread StartDelayedConsumer(object queue)
    {
        ConsumerFailure = null;
        var worker = new Thread(() => {
            Thread.Sleep(3100);
            try {
                // Even after close times out, the collection must stay valid until its consumer exits.
                var items = (IEnumerable)queue.GetType().GetMethod("GetConsumingEnumerable", Type.EmptyTypes)
                    .Invoke(queue, null);
                foreach (var item in items) {
                    ((IDisposable)item.GetType().GetField("Item1").GetValue(item)).Dispose();
                }
            } catch (Exception ex) { ConsumerFailure = ex; }
        });
        worker.IsBackground = true;
        worker.Start();
        return worker;
    }
    public static void Run(object window, object recorder, object frame)
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = window.GetType();
        var field = type.GetField("_rec", flags);
        var receive = type.GetMethod("OnFrameArrived", flags);
        int done = 0;
        var toggler = new Thread(() => {
            while (Volatile.Read(ref done) == 0) {
                field.SetValue(window, recorder);
                field.SetValue(window, null);
            }
        });
        toggler.IsBackground = true;
        toggler.Start();
        try {
            for (int i = 0; i < 10000; i++) receive.Invoke(window, new[] { frame });
        } finally {
            Volatile.Write(ref done, 1);
            toggler.Join();
            field.SetValue(window, null);
        }
    }
}
'@
$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $AssemblyPath).Path)
$windowType = $assembly.GetType('Kiritori.Views.LiveCapture.LivePreviewWindow', $true)
$eventType = $assembly.GetType('Kiritori.Views.LiveCapture.LiveCaptureFrameEventArgs', $true)
$optionsType = $assembly.GetType('Kiritori.Services.Recording.FfmpegPipeOptions', $true)
$recorderType = $assembly.GetType('Kiritori.Services.Recording.FfmpegPipeRecorder', $true)
$flags = [Reflection.BindingFlags]'Instance,NonPublic'
function Set-Field($instance, $name, $value) { $instance.GetType().GetField($name, $flags).SetValue($instance, $value) }
function Get-Field($instance, $name) { $instance.GetType().GetField($name, $flags).GetValue($instance) }
function Assert-True($condition, $message) { if (-not $condition) { throw $message } }
function Assert-Disposed($bitmap, $message) {
    $disposed = $false
    try { $bitmap.GetPixel(0, 0) | Out-Null } catch { $disposed = $true }
    Assert-True $disposed $message
}

$window = [Activator]::CreateInstance($windowType)
$source = New-Object Drawing.Bitmap 24,24
$queued = $null
$buffered = $null
$recorder = $null
try {
    Set-Field $window '_maxFps' 0
    $options = [Activator]::CreateInstance($optionsType)
    $options.Width = 24; $options.Height = 24
    $recorder = [Activator]::CreateInstance($recorderType, [object[]]@($options))
    $event = $eventType.GetConstructor([type[]]@([Drawing.Bitmap], [bool])).Invoke(
        [object[]]@($source.PSObject.BaseObject, $false))
    [LiveRecordingRaceProbe]::Run($window, $recorder, $event)
    Write-Output 'PASS: 10000 frames concurrent with recorder reference changes'

    $queueField = $windowType.GetField('_gifQueue', $flags)
    $queue = [Activator]::CreateInstance($queueField.FieldType)
    $queueField.SetValue($window, $queue)
    $clock = Get-Field $window '_gifClock'
    $clock.Restart()
    $bytesPerFrame = [long](24 * 24 * 4)
    $limit = [long](512 * 1024 * 1024)
    $receive = $windowType.GetMethod('OnFrameArrived', $flags)
    # Reject before allocation when either retained output or queued raw images fill the budget.
    foreach ($field in @('_gifBufferedBytes', '_gifQueuedBytes')) {
        Set-Field $window '_gifBufferedBytes' ([long]0)
        Set-Field $window '_gifQueuedBytes' ([long]0)
        Set-Field $window $field ($limit - $bytesPerFrame + 1)
        Set-Field $window '_gifLastMs' (-1000)
        $receive.Invoke($window, [object[]]@($event)) | Out-Null
        Assert-True ($queue.Count -eq 0) 'GIF frame exceeded the shared memory budget'
    }
    Set-Field $window '_gifBufferedBytes' ([long]0)
    Set-Field $window '_gifQueuedBytes' ([long]0)
    Set-Field $window '_gifLastMs' (-1000)
    $receive.Invoke($window, [object[]]@($event)) | Out-Null
    Assert-True ($queue.Count -eq 1) 'GIF frame below the memory budget was rejected'
    Assert-True ((Get-Field $window '_gifQueuedBytes') -eq $bytesPerFrame) 'Queued GIF memory was not reserved'
    # Peek without taking ownership away from shutdown.
    $queued = $queue.ToArray()[0].Item1
    Write-Output 'PASS: GIF budget includes raw queue and processed buffers before cloning'

    $frames = New-Object 'Collections.Generic.List[Drawing.Bitmap]'
    $buffered = New-Object Drawing.Bitmap 24,24
    $frames.Add($buffered)
    Set-Field $window '_gifFrames' $frames.PSObject.BaseObject
    Set-Field $window '_gifBufferedBytes' $bytesPerFrame
    $closedArgs = New-Object Windows.Forms.FormClosedEventArgs ([Windows.Forms.CloseReason]::UserClosing)
    $windowType.GetMethod('OnFormClosed', $flags).Invoke($window,
        [object[]]@($closedArgs.PSObject.BaseObject)) | Out-Null
    Assert-Disposed $queued 'Closing left a queued GIF bitmap alive'
    Assert-Disposed $buffered 'Closing left a processed GIF bitmap alive'
    Assert-True ($null -eq (Get-Field $window '_gifFrames')) 'Closing retained the GIF frame list'
    Assert-True ((Get-Field $window '_gifQueuedBytes') -eq 0) 'Closing retained queued memory accounting'
    Assert-True ((Get-Field $window '_gifBufferedBytes') -eq 0) 'Closing retained buffered memory accounting'
    # A callback already dispatched before close must not publish new pixels afterwards.
    $receive.Invoke($window, [object[]]@($event)) | Out-Null
    Assert-True ($null -eq (Get-Field $window '_latest')) 'Frame callback published after close'
    Write-Output 'PASS: closing disposes queued/processed GIF images and rejects late frame callbacks'
} finally {
    if ($recorder) { $recorder.Dispose() }
    if ($queued) { $queued.Dispose() }
    if ($buffered) { $buffered.Dispose() }
    $window.Close()
    $window.Dispose()
    $source.Dispose()
}

# Run the production GIF consumer and ensure raw reservations are released after preprocessing.
$window = [Activator]::CreateInstance($windowType)
$source = New-Object Drawing.Bitmap 24,24
try {
    Set-Field $window '_maxFps' 0
    $queueField = $windowType.GetField('_gifQueue', $flags)
    $queue = [Activator]::CreateInstance($queueField.FieldType)
    $queueField.SetValue($window, $queue)
    $clock = Get-Field $window '_gifClock'
    $clock.Restart()
    $frames = New-Object 'Collections.Generic.List[Drawing.Bitmap]'
    $delays = New-Object 'Collections.Generic.List[int]'
    Set-Field $window '_gifFrames' $frames.PSObject.BaseObject
    Set-Field $window '_gifDelaysCs' $delays.PSObject.BaseObject
    $event = $eventType.GetConstructor([type[]]@([Drawing.Bitmap], [bool])).Invoke(
        [object[]]@($source.PSObject.BaseObject, $false))
    $rawImages = @()
    for ($index = 0; $index -lt 3; $index++) {
        Set-Field $window '_gifLastMs' (-1000)
        $windowType.GetMethod('OnFrameArrived', $flags).Invoke($window, [object[]]@($event)) | Out-Null
    }
    Assert-True ($queue.Count -eq 3) 'Production worker fixture did not enqueue three frames'
    $rawImages = @($queue.ToArray() | ForEach-Object { $_.Item1 })
    $queue.CompleteAdding()
    $options = [Activator]::CreateInstance($assembly.GetType('Kiritori.Views.LiveCapture.GifRecordOptions'))
    $windowType.GetMethod('GifWorkerLoop', $flags).Invoke($window,
        [object[]]@($queue, $options, 10)) | Out-Null
    Assert-True ((Get-Field $window '_gifQueuedBytes') -eq 0) 'Production GIF worker leaked raw-byte reservations'
    Assert-True ($frames.Count -gt 0) 'Production GIF worker did not retain an output frame'
    foreach ($rawImage in $rawImages) { Assert-Disposed $rawImage 'Production GIF worker retained a consumed raw bitmap' }
    $closedArgs = New-Object Windows.Forms.FormClosedEventArgs ([Windows.Forms.CloseReason]::UserClosing)
    $windowType.GetMethod('OnFormClosed', $flags).Invoke($window,
        [object[]]@($closedArgs.PSObject.BaseObject)) | Out-Null
    Write-Output 'PASS: production GIF consumer releases raw images and their byte reservations'
} finally {
    $source.Dispose()
    $window.Close()
    $window.Dispose()
}

# Exercise the timeout path: window resources are disposed while its GIF worker retires later.
$window = [Activator]::CreateInstance($windowType)
$source = New-Object Drawing.Bitmap 24,24
$buffered = New-Object Drawing.Bitmap 24,24
try {
    Set-Field $window '_maxFps' 0
    $queueField = $windowType.GetField('_gifQueue', $flags)
    $queue = [Activator]::CreateInstance($queueField.FieldType)
    $queueField.SetValue($window, $queue)
    $clock = Get-Field $window '_gifClock'
    $clock.Restart()
    Set-Field $window '_gifLastMs' (-1000)
    $event = $eventType.GetConstructor([type[]]@([Drawing.Bitmap], [bool])).Invoke(
        [object[]]@($source.PSObject.BaseObject, $false))
    $windowType.GetMethod('OnFrameArrived', $flags).Invoke($window, [object[]]@($event)) | Out-Null
    $queued = $queue.ToArray()[0].Item1
    $frames = New-Object 'Collections.Generic.List[Drawing.Bitmap]'
    $frames.Add($buffered)
    Set-Field $window '_gifFrames' $frames.PSObject.BaseObject
    Set-Field $window '_gifBufferedBytes' ([long](24 * 24 * 4))
    $worker = [LiveRecordingRaceProbe]::StartDelayedConsumer($queue)
    Set-Field $window '_gifWorkerThread' $worker
    $closedArgs = New-Object Windows.Forms.FormClosedEventArgs ([Windows.Forms.CloseReason]::UserClosing)
    $windowType.GetMethod('OnFormClosed', $flags).Invoke($window,
        [object[]]@($closedArgs.PSObject.BaseObject)) | Out-Null
    Assert-Disposed $queued 'Closing did not promptly drain pending GIF images'
    Assert-True ($worker.Join(5000)) 'Delayed GIF consumer did not exit'
    Assert-True ($null -eq [LiveRecordingRaceProbe]::ConsumerFailure) 'GIF collection was destroyed before the consumer exited'
    for ($attempt = 0; $attempt -lt 100 -and $null -ne (Get-Field $window '_gifFrames'); $attempt++) {
        Start-Sleep -Milliseconds 50
    }
    Assert-Disposed $buffered 'Deferred cleanup left a buffered bitmap alive'
    Assert-True ($null -eq (Get-Field $window '_gifFrames')) 'Deferred cleanup did not clear buffered frames'
    Write-Output 'PASS: timed-out GIF consumer keeps its collection valid and completes deferred cleanup'
} finally {
    $buffered.Dispose()
    $source.Dispose()
    $window.Close()
    $window.Dispose()
}
