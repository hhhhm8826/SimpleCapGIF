using System.ComponentModel;
using System.Runtime.InteropServices;
using SimpleCapGIF.Core.Geometry;
using SimpleCapGIF.Windows.Interop;
using SimpleCapGIF.Localization;

namespace SimpleCapGIF.Windows.Display;

public static class OverlayWindowService
{
    private static readonly nint HwndTopmost = new(-1);

    public static void SetPhysicalBounds(nint windowHandle, PixelRect bounds)
    {
        if (!NativeMethods.SetWindowPos(
                windowHandle,
                HwndTopmost,
                bounds.X,
                bounds.Y,
                bounds.Width,
                bounds.Height,
                NativeMethods.SwpNoActivate))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), AppStrings.OverlayPlacementFailed);
        }
    }

    public static void SetClickThrough(nint windowHandle, bool enabled)
    {
        Marshal.SetLastPInvokeError(0);
        var currentStyle = NativeMethods.GetWindowLongPtr(windowHandle, NativeMethods.GwlExStyle);
        var error = Marshal.GetLastPInvokeError();
        if (currentStyle == 0 && error != 0) throw new Win32Exception(error);

        var currentValue = currentStyle.ToInt64();
        var updatedValue = enabled
            ? currentValue | NativeMethods.WsExTransparent
            : currentValue & ~NativeMethods.WsExTransparent;
        if (updatedValue == currentValue) return;

        Marshal.SetLastPInvokeError(0);
        var previousStyle = NativeMethods.SetWindowLongPtr(windowHandle, NativeMethods.GwlExStyle, new nint(updatedValue));
        error = Marshal.GetLastPInvokeError();
        if (previousStyle == 0 && error != 0) throw new Win32Exception(error);
    }
}
