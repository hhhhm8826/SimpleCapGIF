using SimpleCapGIF.Core.Geometry;
using SimpleCapGIF.Core.Models;
using SimpleCapGIF.Core.Services;

namespace SimpleCapGIF.Core.Tests;

public sealed class ToolbarPlacementServiceTests
{
    private readonly ToolbarPlacementService _sut = new();
    private static readonly PixelRect Monitor = new(0, 0, 1920, 1080);
    private static readonly PixelRect WorkArea = new(0, 0, 1920, 1040);
    private static readonly PixelSize Toolbar = new(420, 48);

    [Fact]
    public void UsesBottomOutsideCandidateForCenteredSelection()
    {
        var point = _sut.Calculate(new PixelRect(560, 300, 800, 450), Monitor, WorkArea, Toolbar, CaptureUiState.Selecting);
        Assert.Equal(new PixelPoint(750, 758), point);
    }

    [Theory]
    [InlineData(0, 0, 600, 300)]
    [InlineData(1320, 0, 600, 300)]
    [InlineData(0, 740, 600, 300)]
    [InlineData(1320, 740, 600, 300)]
    public void NeverPlacesToolbarOutsideWorkAreaAtCorners(int x, int y, int width, int height)
    {
        var point = _sut.Calculate(new PixelRect(x, y, width, height), Monitor, WorkArea, Toolbar, CaptureUiState.Selecting);
        Assert.True(WorkArea.Contains(new PixelRect(point.X, point.Y, Toolbar.Width, Toolbar.Height)));
    }

    [Fact]
    public void UsesTopCenterInsideForFullScreen()
    {
        Assert.Equal(new PixelPoint(750, 12), _sut.Calculate(Monitor, Monitor, WorkArea, Toolbar, CaptureUiState.Recording));
    }

    [Fact]
    public void SupportsNegativeMonitorCoordinates()
    {
        var monitor = new PixelRect(-2560, -200, 2560, 1440);
        var workArea = new PixelRect(-2560, -200, 2560, 1400);
        var selection = new PixelRect(-2200, 100, 800, 450);
        var point = _sut.Calculate(selection, monitor, workArea, Toolbar, CaptureUiState.Selecting);
        Assert.True(workArea.Contains(new PixelRect(point.X, point.Y, Toolbar.Width, Toolbar.Height)));
    }

    [Fact]
    public void SmallSelectionStillProducesVisibleToolbar()
    {
        var selection = new PixelRect(900, 500, 160, 90);
        var point = _sut.Calculate(selection, Monitor, WorkArea, Toolbar, CaptureUiState.Selecting);
        Assert.True(WorkArea.Contains(new PixelRect(point.X, point.Y, Toolbar.Width, Toolbar.Height)));
    }

    [Theory]
    [InlineData(560, 0, 800, 300)]
    [InlineData(560, 740, 800, 300)]
    [InlineData(0, 300, 600, 300)]
    [InlineData(1320, 300, 600, 300)]
    public void KeepsToolbarVisibleAtEveryScreenEdge(int x, int y, int width, int height)
    {
        var point = _sut.Calculate(new PixelRect(x, y, width, height), Monitor, WorkArea, Toolbar, CaptureUiState.Selecting);
        Assert.True(WorkArea.Contains(new PixelRect(point.X, point.Y, Toolbar.Width, Toolbar.Height)));
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public void PlacementRemainsInsidePhysicalBoundsAtCommonDpiScales(double scale)
    {
        var monitor = new PixelRect(0, 0, (int)(1536 * scale), (int)(864 * scale));
        var toolbar = new PixelSize((int)(420 * scale), (int)(48 * scale));
        var selection = new PixelRect(0, 0, (int)(640 * scale), (int)(360 * scale));
        var point = _sut.Calculate(selection, monitor, monitor, toolbar, CaptureUiState.Selecting);
        Assert.True(monitor.Contains(new PixelRect(point.X, point.Y, toolbar.Width, toolbar.Height)));
    }
}
