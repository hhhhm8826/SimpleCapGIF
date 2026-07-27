using System.Runtime.InteropServices;

namespace SimpleCapGIF.Windows.Interop;

internal static partial class NativeMethods
{
    internal const uint MonitorDefaultToNearest = 2;
    internal const uint MonitorDefaultToNull = 0;
    internal const uint WdaExcludeFromCapture = 0x00000011;
    internal const uint SwpNoActivate = 0x0010;
    internal const int MdtEffectiveDpi = 0;
    internal const uint CursorShowing = 0x00000001;

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool RegisterHotKey(nint window, int id, uint modifiers, uint virtualKey);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool UnregisterHotKey(nint window, int id);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetCursorPos(out NativePoint point);

    [LibraryImport("user32.dll")]
    internal static partial nint MonitorFromPoint(NativePoint point, uint flags);

    [LibraryImport("user32.dll")]
    internal static partial nint MonitorFromRect(in NativeRect rect, uint flags);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);

    #pragma warning disable SYSLIB1054
    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    #pragma warning restore SYSLIB1054

    [LibraryImport("shcore.dll")]
    internal static partial int GetDpiForMonitor(nint monitor, int dpiType, out uint dpiX, out uint dpiY);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool SetWindowDisplayAffinity(nint window, uint affinity);

    [LibraryImport("dwmapi.dll")]
    internal static partial int DwmFlush();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetCursorInfo(ref CursorInfo info);

    #pragma warning disable SYSLIB1054
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetIconInfo(nint icon, out IconInfo info);
    #pragma warning restore SYSLIB1054

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool DeleteObject(nint value);

    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct NativePoint(int X, int Y);

    [StructLayout(LayoutKind.Sequential)]
    internal readonly record struct NativeRect(int Left, int Top, int Right, int Bottom);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct MonitorInfo
    {
        internal uint Size;
        internal NativeRect Monitor;
        internal NativeRect Work;
        internal uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        internal string DeviceName;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CursorInfo
    {
        internal uint Size;
        internal uint Flags;
        internal nint Cursor;
        internal NativePoint ScreenPosition;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct IconInfo
    {
        [MarshalAs(UnmanagedType.Bool)]
        internal bool IsIcon;
        internal uint HotspotX;
        internal uint HotspotY;
        internal nint MaskBitmap;
        internal nint ColorBitmap;
    }
}
