using SimpleCapGIF.Core.Geometry;

namespace SimpleCapGIF.Windows.Display;

public sealed record MonitorDescriptor(nint Handle, string DeviceName, PixelRect Bounds, PixelRect WorkArea, uint DpiX, uint DpiY)
{
    public double ScaleX => DpiX / 96d;
    public double ScaleY => DpiY / 96d;
}
