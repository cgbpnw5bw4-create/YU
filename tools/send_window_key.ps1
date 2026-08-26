param(
    [string]$ProcessName = 'DeskStealth',
    [Parameter(Mandatory)]
    [ValidateSet('Escape', 'W', 'A', 'S', 'D', 'Enter')]
    [string]$Key,
    [int]$HoldMilliseconds = 50,
    [int]$RepeatCount = 1,
    [int]$RepeatIntervalMilliseconds = 40,
    [switch]$WithShift,
    [switch]$ActivateWindow
)

$ErrorActionPreference = 'Stop'

$source = @'
using System;
using System.Runtime.InteropServices;

public static class DeskStealthWindowInput
{
    public delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll")]
    public static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr window, int command);

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

if (-not ('DeskStealthWindowInput' -as [type])) {
    Add-Type -TypeDefinition $source
}

$virtualKeys = @{
    Escape = 0x1B
    W = 0x57
    A = 0x41
    S = 0x53
    D = 0x44
    Enter = 0x0D
}

$process = Get-Process -Name $ProcessName -ErrorAction Stop | Select-Object -First 1
$window = [DeskStealthWindowInput]::FindVisibleWindow([uint32]$process.Id)
if ($window -eq [IntPtr]::Zero) {
    throw "No visible window found for process $ProcessName ($($process.Id))."
}

$virtualKey = $virtualKeys[$Key]
if ($ActivateWindow) {
    [DeskStealthWindowInput]::ShowWindow($window, 5) | Out-Null
    [DeskStealthWindowInput]::SetForegroundWindow($window) | Out-Null
    Start-Sleep -Milliseconds 100
}
if ($WithShift) {
    [DeskStealthWindowInput]::PostMessage($window, 0x0100, [IntPtr]0x10, [IntPtr]::Zero) | Out-Null
}
[DeskStealthWindowInput]::PostMessage($window, 0x0100, [IntPtr]$virtualKey, [IntPtr]::Zero) | Out-Null
for ($repeat = 1; $repeat -lt [Math]::Max(1, $RepeatCount); $repeat++) {
    Start-Sleep -Milliseconds ([Math]::Max(0, $RepeatIntervalMilliseconds))
    [DeskStealthWindowInput]::PostMessage($window, 0x0100, [IntPtr]$virtualKey, [IntPtr]::Zero) | Out-Null
}
Start-Sleep -Milliseconds ([Math]::Max(0, $HoldMilliseconds))
[DeskStealthWindowInput]::PostMessage($window, 0x0101, [IntPtr]$virtualKey, [IntPtr]::Zero) | Out-Null
if ($WithShift) {
    [DeskStealthWindowInput]::PostMessage($window, 0x0101, [IntPtr]0x10, [IntPtr]::Zero) | Out-Null
}
Write-Output "SENT=$Key HANDLE=$window HOLD_MS=$HoldMilliseconds REPEATS=$RepeatCount WITH_SHIFT=$WithShift ACTIVATED=$ActivateWindow"
