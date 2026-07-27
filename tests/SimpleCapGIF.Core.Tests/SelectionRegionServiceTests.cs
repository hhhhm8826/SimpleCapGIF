using SimpleCapGIF.Core.Geometry;
using SimpleCapGIF.Core.Models;
using SimpleCapGIF.Core.Services;

namespace SimpleCapGIF.Core.Tests;

public sealed class SelectionRegionServiceTests
{
    private static readonly PixelRect Monitor = new(-1920, 0, 1920, 1080);

    [Fact]
    public void MoveClampsToNegativeCoordinateMonitor()
    {
        var result = SelectionRegionService.ApplyDelta(new PixelRect(-1800, 100, 800, 450), Monitor, ResizeHandle.Move, -500, -200);
        Assert.Equal(new PixelRect(-1920, 0, 800, 450), result);
    }

    [Fact]
    public void ResizeHonorsMinimumPhysicalPixelSize()
    {
        var region = new PixelRect(-1000, 100, 800, 450);
        var result = SelectionRegionService.ApplyDelta(region, Monitor, ResizeHandle.BottomRight, -1000, -1000);
        Assert.Equal(new PixelRect(-1000, 100, 160, 90), result);
    }

    [Fact]
    public void ResizeCannotCrossMonitorBoundary()
    {
        var region = new PixelRect(-1000, 100, 800, 450);
        var result = SelectionRegionService.ApplyDelta(region, Monitor, ResizeHandle.TopRight, 500, -500);
        Assert.Equal(new PixelRect(-1000, 0, 1000, 550), result);
    }
}
