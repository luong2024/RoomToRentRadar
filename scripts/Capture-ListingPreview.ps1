<#
.SYNOPSIS
Launches Room Radar and captures only its application window for visual QA.
.DESCRIPTION
Requires Windows and an existing Windows build. WidthPixels and HeightPixels,
when supplied together, resize the outer window in physical screen pixels.
The app stays open after capture. This script never captures the desktop.
.EXAMPLE
.\scripts\Capture-ListingPreview.ps1
.EXAMPLE
.\scripts\Capture-ListingPreview.ps1 -WidthPixels 480 -HeightPixels 900 -OutputPath "$env:TEMP\room-radar-narrow.png"
#>
[CmdletBinding()]
param(
    [string] $ExePath = (Join-Path $PSScriptRoot '..\RoomToRentRadar\bin\Debug\net10.0-windows10.0.19041.0\win-x64\RoomToRentRadar.exe'),
    [string] $OutputPath = (Join-Path ([System.IO.Path]::GetTempPath()) 'room-radar-preview.png'),
    [ValidateRange(320, 7680)]
    [int] $WidthPixels,
    [ValidateRange(320, 7680)]
    [int] $HeightPixels
)

$ErrorActionPreference = 'Stop'
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw 'This script requires Windows.'
}
if ($PSBoundParameters.ContainsKey('WidthPixels') -ne $PSBoundParameters.ContainsKey('HeightPixels')) {
    throw 'Supply WidthPixels and HeightPixels together, or omit both.'
}

$previewExecutable = (Resolve-Path -LiteralPath $ExePath).ProviderPath
if (-not (Test-Path -LiteralPath $previewExecutable -PathType Leaf) -or
    [System.IO.Path]::GetFileName($previewExecutable) -ne 'RoomToRentRadar.exe') {
    throw 'ExePath must identify a built RoomToRentRadar.exe file.'
}
$previewOutput = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputPath)
if ([System.IO.Path]::GetExtension($previewOutput) -ne '.png') {
    throw 'OutputPath must have a .png extension.'
}
$previewOutputDirectory = [System.IO.Path]::GetDirectoryName($previewOutput)
if (-not (Test-Path -LiteralPath $previewOutputDirectory -PathType Container)) {
    throw "The output directory does not exist: $previewOutputDirectory"
}

Add-Type -AssemblyName System.Drawing
if (-not ('RoomRadarPreview.NativeMethods' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

namespace RoomRadarPreview
{
    public static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct Rect { public int Left, Top, Right, Bottom; }

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(IntPtr window, out Rect rectangle);
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool MoveWindow(IntPtr window, int x, int y, int width, int height, bool repaint);
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool PrintWindow(IntPtr window, IntPtr deviceContext, uint flags);
    }
}
'@
}

# Per-monitor awareness ensures window bounds and bitmap size use physical pixels.
$previewPreviousDpiContext = [RoomRadarPreview.NativeMethods]::SetThreadDpiAwarenessContext([IntPtr]::new(-4))
if ($previewPreviousDpiContext -eq [IntPtr]::Zero) {
    throw 'Unable to enable DPI-aware capture on this Windows version.'
}

$previewBitmap = $null
$previewGraphics = $null
$previewDeviceContext = [IntPtr]::Zero
try {
    $previewProcess = Start-Process -FilePath $previewExecutable -WorkingDirectory ([System.IO.Path]::GetDirectoryName($previewExecutable)) -PassThru
    $previewStartupTimer = [Diagnostics.Stopwatch]::StartNew()
    do {
        $previewProcess.Refresh()
        if ($previewProcess.HasExited) {
            throw "Room Radar exited before creating a window (exit code $($previewProcess.ExitCode))."
        }
        $previewWindow = $previewProcess.MainWindowHandle
        if ($previewWindow -ne [IntPtr]::Zero) { break }
        Start-Sleep -Milliseconds 100
    } while ($previewStartupTimer.Elapsed.TotalSeconds -lt 15)
    if ($previewWindow -eq [IntPtr]::Zero) {
        throw 'Room Radar did not create a main window within 15 seconds.'
    }

    $previewRectangle = New-Object RoomRadarPreview.NativeMethods+Rect
    if (-not [RoomRadarPreview.NativeMethods]::GetWindowRect($previewWindow, [ref] $previewRectangle)) {
        throw 'Unable to read the Room Radar window bounds.'
    }
    if ($PSBoundParameters.ContainsKey('WidthPixels')) {
        if (-not [RoomRadarPreview.NativeMethods]::MoveWindow($previewWindow, $previewRectangle.Left, $previewRectangle.Top, $WidthPixels, $HeightPixels, $true)) {
            throw 'Unable to resize the Room Radar window.'
        }
    }

    # Allow native layout and the initial image resources to render after resize.
    Start-Sleep -Milliseconds 1500
    if (-not [RoomRadarPreview.NativeMethods]::GetWindowRect($previewWindow, [ref] $previewRectangle)) {
        throw 'The Room Radar window is no longer available.'
    }
    $previewActualWidth = $previewRectangle.Right - $previewRectangle.Left
    $previewActualHeight = $previewRectangle.Bottom - $previewRectangle.Top
    if ($previewActualWidth -le 0 -or $previewActualHeight -le 0) {
        throw 'The application window has invalid capture bounds.'
    }
    $previewBitmap = New-Object System.Drawing.Bitmap($previewActualWidth, $previewActualHeight)
    $previewGraphics = [System.Drawing.Graphics]::FromImage($previewBitmap)
    $previewDeviceContext = $previewGraphics.GetHdc()
    # PW_RENDERFULLCONTENT captures this window only, including WinUI content.
    if (-not [RoomRadarPreview.NativeMethods]::PrintWindow($previewWindow, $previewDeviceContext, 2)) {
        throw 'PrintWindow could not capture Room Radar. No desktop fallback was used.'
    }
    $previewGraphics.ReleaseHdc($previewDeviceContext)
    $previewDeviceContext = [IntPtr]::Zero
    $previewBitmap.Save($previewOutput, [System.Drawing.Imaging.ImageFormat]::Png)

    [PSCustomObject]@{
        ProcessId = $previewProcess.Id
        CapturePath = $previewOutput
        WidthPixels = $previewActualWidth
        HeightPixels = $previewActualHeight
    }
}
finally {
    if ($previewDeviceContext -ne [IntPtr]::Zero -and $null -ne $previewGraphics) {
        $previewGraphics.ReleaseHdc($previewDeviceContext)
    }
    if ($null -ne $previewGraphics) { $previewGraphics.Dispose() }
    if ($null -ne $previewBitmap) { $previewBitmap.Dispose() }
    [RoomRadarPreview.NativeMethods]::SetThreadDpiAwarenessContext($previewPreviousDpiContext) | Out-Null
}
