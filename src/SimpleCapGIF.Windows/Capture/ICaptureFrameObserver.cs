using SimpleCapGIF.Core.Geometry;

namespace SimpleCapGIF.Windows.Capture;

public interface ICaptureFrameObserver
{
    void OnFrame(ReadOnlySpan<byte> frame, PixelSize size, TimeSpan elapsed);
}
