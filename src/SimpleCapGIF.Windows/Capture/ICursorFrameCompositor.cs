using SimpleCapGIF.Core.Geometry;

namespace SimpleCapGIF.Windows.Capture;

internal interface ICursorFrameCompositor
{
    void Composite(byte[] target, PixelSize targetSize, PixelRect sourceRegion);
}

internal sealed class WindowsCursorFrameCompositor : ICursorFrameCompositor
{
    public void Composite(byte[] target, PixelSize targetSize, PixelRect sourceRegion) =>
        CursorCompositor.Composite(target, targetSize, sourceRegion);
}
