using System.ComponentModel;
using System.Runtime.InteropServices;
using SimpleCapGIF.Core.Geometry;
using SimpleCapGIF.Windows.Interop;
using Vortice.DXGI;
using static Vortice.DXGI.DXGI;

namespace SimpleCapGIF.Windows.Display;

public static class MonitorService
{
    public static MonitorDescriptor GetMonitorAtCursor()
    {
        var cursor = GetCursorPosition();
        return GetMonitor(NativeMethods.MonitorFromPoint(new NativeMethods.NativePoint(cursor.X, cursor.Y), NativeMethods.MonitorDefaultToNearest));
    }

    public static PixelPoint GetCursorPosition()
    {
        if (!NativeMethods.GetCursorPos(out var cursor))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return new PixelPoint(cursor.X, cursor.Y);
    }

    public static bool TryGetMonitorContaining(PixelRect region, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out MonitorDescriptor? monitor)
    {
        var rectangle = new NativeMethods.NativeRect(region.Left, region.Top, region.Right, region.Bottom);
        var handle = NativeMethods.MonitorFromRect(in rectangle, NativeMethods.MonitorDefaultToNull);
        if (handle == 0)
        {
            monitor = null;
            return false;
        }

        var candidate = GetMonitor(handle);
        if (!candidate.Bounds.Contains(region))
        {
            monitor = null;
            return false;
        }

        monitor = candidate;
        return true;
    }

    public static MonitorDescriptor GetMonitor(nint handle)
    {
        var info = new NativeMethods.MonitorInfo
        {
            Size = (uint)Marshal.SizeOf<NativeMethods.MonitorInfo>(),
            DeviceName = string.Empty,
        };
        if (!NativeMethods.GetMonitorInfo(handle, ref info))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        var dpiResult = NativeMethods.GetDpiForMonitor(handle, NativeMethods.MdtEffectiveDpi, out var dpiX, out var dpiY);
        if (dpiResult != 0)
        {
            dpiX = 96;
            dpiY = 96;
        }

        return new MonitorDescriptor(
            handle,
            info.DeviceName,
            ToPixelRect(info.Monitor),
            ToPixelRect(info.Work),
            dpiX,
            dpiY);
    }

    public static bool IsHdrEnabled(MonitorDescriptor monitor)
    {
        using var factory = CreateDXGIFactory1<IDXGIFactory1>();
        for (uint adapterIndex = 0; ; adapterIndex++)
        {
            var adapterResult = factory.EnumAdapters1(adapterIndex, out var adapter);
            if (adapterResult.Failure) break;
            using (adapter)
            {
                for (uint outputIndex = 0; ; outputIndex++)
                {
                    var outputResult = adapter.EnumOutputs(outputIndex, out var output);
                    if (outputResult.Failure) break;
                    using (output)
                    {
                        if (!string.Equals(output.Description.DeviceName, monitor.DeviceName, StringComparison.OrdinalIgnoreCase)) continue;
                        try
                        {
                            using var output6 = output.QueryInterface<IDXGIOutput6>();
                            var description = output6.Description1;
                            return description.BitsPerColor > 8 &&
                                description.ColorSpace is ColorSpaceType.RgbFullG2084NoneP2020 or ColorSpaceType.RgbStudioG2084NoneP2020;
                        }
                        catch
                        {
                            return false;
                        }
                    }
                }
            }
        }

        return false;
    }

    private static PixelRect ToPixelRect(NativeMethods.NativeRect rect) =>
        new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
}
