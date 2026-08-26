using System.Runtime.InteropServices;

namespace DeskStealth;

internal static class NativeMethods
{
    internal const int WmDisplayChange = 0x007E;
    internal const int WmNcButtonDown = 0x00A1;
    internal const int WmKeyUp = 0x0101;
    internal const int WmSysKeyUp = 0x0105;
    internal const int HtCaption = 0x0002;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ReleaseCapture();

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    internal static extern nint SendMessage(nint windowHandle, int message, nint wParam, nint lParam);
}
