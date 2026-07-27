using System.ComponentModel;
using System.Runtime.InteropServices;
using SimpleCapGIF.Windows.Interop;
using SimpleCapGIF.Localization;

namespace SimpleCapGIF.Windows.Display;

public static class WindowCaptureExclusionService
{
    public static void Exclude(nint windowHandle)
    {
        if (!NativeMethods.SetWindowDisplayAffinity(windowHandle, NativeMethods.WdaExcludeFromCapture))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), AppStrings.WindowCaptureExclusionFailed);
        }
    }
}
