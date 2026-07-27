using System.Runtime.InteropServices;
using SimpleCapGIF.Windows.Interop;

namespace SimpleCapGIF.Windows.Display;

public static class DesktopCompositionService
{
    public static void Flush()
    {
        var result = NativeMethods.DwmFlush();
        if (result < 0) Marshal.ThrowExceptionForHR(result);
    }
}
