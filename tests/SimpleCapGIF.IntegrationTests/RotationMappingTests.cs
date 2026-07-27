using SimpleCapGIF.Core.Geometry;
using SimpleCapGIF.Windows.Capture;
using Vortice.DXGI;

namespace SimpleCapGIF.IntegrationTests;

public sealed class RotationMappingTests
{
    [Theory]
    [InlineData(1, 10, 20, 30, 40, 10, 20, 40, 60)]
    [InlineData(2, 10, 20, 30, 40, 964, 10, 1004, 40)]
    [InlineData(3, 10, 20, 30, 40, 984, 708, 1014, 748)]
    [InlineData(4, 10, 20, 30, 40, 20, 728, 60, 758)]
    public void MapsLogicalSelectionIntoUnrotatedDuplicationSurface(
        int rotationValue,
        int x,
        int y,
        int width,
        int height,
        int expectedLeft,
        int expectedTop,
        int expectedRight,
        int expectedBottom)
    {
        var actual = DxgiFrameSource.MapSourceRect(
            new PixelRect(x, y, width, height),
            new PixelSize(1024, 768),
            (ModeRotation)rotationValue);

        Assert.Equal(expectedLeft, actual.Left);
        Assert.Equal(expectedTop, actual.Top);
        Assert.Equal(expectedRight, actual.Right);
        Assert.Equal(expectedBottom, actual.Bottom);
    }
}
