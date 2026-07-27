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
}
