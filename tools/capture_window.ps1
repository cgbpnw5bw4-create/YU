param(
    [string]$ProcessName = 'DeskStealth',
    [Parameter(Mandatory)] [string]$OutputPath,
    [int]$WaitMilliseconds = 5000
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$source = @'
using System;
using System.Runtime.InteropServices;

public static class DeskStealthWindowCapture
{
    public delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr window, out RECT rectangle);

    [DllImport("user32.dll")]
    public static extern bool PrintWindow(IntPtr window, IntPtr deviceContext, uint flags);

    [DllImport("user32.dll")]
    public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr dpiContext);

    public static IntPtr FindVisibleWindow(uint targetProcessId)
    {
        var found = IntPtr.Zero;
        EnumWindows((window, _) =>
        {
            GetWindowThreadProcessId(window, out var processId);
            if (processId == targetProcessId && IsWindowVisible(window))
            {
                found = window;
                return false;
            }

            return true;
        }, IntPtr.Zero);
        return found;
    }
}
'@

if (-not ('DeskStealthWindowCapture' -as [type])) {
    Add-Type -TypeDefinition $source
}

# PowerShell itself is DPI-virtualized on many Windows installations. Switch only
# this capture thread to Per-Monitor-V2 so GetWindowRect returns physical pixels.
$previousDpiContext = [DeskStealthWindowCapture]::SetThreadDpiAwarenessContext([IntPtr]::new(-4))

$process = Get-Process -Name $ProcessName -ErrorAction Stop | Select-Object -First 1
$window = [IntPtr]::Zero
$deadline = [DateTime]::UtcNow.AddMilliseconds($WaitMilliseconds)
do {
    $window = [DeskStealthWindowCapture]::FindVisibleWindow([uint32]$process.Id)
    if ($window -ne [IntPtr]::Zero) { break }
    Start-Sleep -Milliseconds 50
} while ([DateTime]::UtcNow -lt $deadline)

if ($window -eq [IntPtr]::Zero) {
    throw "No visible window found for process $ProcessName ($($process.Id))."
}

$rectangle = New-Object DeskStealthWindowCapture+RECT
if (-not [DeskStealthWindowCapture]::GetWindowRect($window, [ref]$rectangle)) {
    throw "GetWindowRect failed for window $window."
}
$width = $rectangle.Right - $rectangle.Left
$height = $rectangle.Bottom - $rectangle.Top
if ($width -le 0 -or $height -le 0) {
    throw "Window $window returned invalid bounds ${width}x${height}."
}
$bitmap = New-Object System.Drawing.Bitmap($width, $height)
try {
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $deviceContext = $graphics.GetHdc()
        try {
            $captured = [DeskStealthWindowCapture]::PrintWindow($window, $deviceContext, 2)
            if (-not $captured) {
                throw "PrintWindow failed for window $window."
            }
        }
        finally {
            $graphics.ReleaseHdc($deviceContext)
        }
    }
    finally {
        $graphics.Dispose()
    }

    $resolvedOutput = [System.IO.Path]::GetFullPath($OutputPath)
    $outputDirectory = [System.IO.Path]::GetDirectoryName($resolvedOutput)
    [System.IO.Directory]::CreateDirectory($outputDirectory)
    $bitmap.Save($resolvedOutput, [System.Drawing.Imaging.ImageFormat]::Png)
}
finally {
    $bitmap.Dispose()
}

Write-Output "CAPTURED=$captured HANDLE=$window POSITION=$($rectangle.Left),$($rectangle.Top) SIZE=${width}x${height} PATH=$resolvedOutput"
