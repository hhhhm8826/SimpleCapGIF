using SimpleCapGIF.Core.Geometry;

namespace SimpleCapGIF.Windows.Capture;

internal interface ICaptureFrameSource : IDisposable
{
    bool TryAcquireLatest(uint timeoutMilliseconds);
    void CopyCurrentFrame(Span<byte> destination);
}

internal interface ICaptureFrameSourceFactory
{
    ICaptureFrameSource Create(PixelRect region, PixelSize outputSize);
}

internal sealed class DxgiFrameSourceFactory : ICaptureFrameSourceFactory
{
    public ICaptureFrameSource Create(PixelRect region, PixelSize outputSize) => new DxgiFrameSource(region, outputSize);
}
