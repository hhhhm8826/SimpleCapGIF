using SimpleCapGIF.Core.Geometry;
using SimpleCapGIF.Core.Models;
using SimpleCapGIF.Core.Services;

namespace SimpleCapGIF.Core.Tests;

public sealed class PresetRegionServiceTests
{
    private static readonly PixelRect Monitor = new(-1920, 0, 1920, 1080);

    [Theory]
    [InlineData(OutputPreset.P640x360, 640, 360)]
    [InlineData(OutputPreset.P800x450, 800, 450)]
    [InlineData(OutputPreset.P960x540, 960, 540)]
    [InlineData(OutputPreset.P1280x720, 1280, 720)]
    public void CreatesExactPhysicalPixelRegionAroundCurrentCenter(OutputPreset preset, int width, int height)
    {
        var current = new PixelRect(-1500, 400, 500, 300);

        var success = PresetRegionService.TryCreateRegion(current, Monitor, preset, out var actual);

        Assert.True(success);
        Assert.Equal(new PixelSize(width, height), actual.Size);
        Assert.Equal(current.Center, actual.Center);
    }

    [Fact]
    public void ClampsPositionWithoutChangingRequestedSize()
    {
        var current = new PixelRect(-1900, 0, 200, 100);

        Assert.True(PresetRegionService.TryCreateRegion(current, Monitor, OutputPreset.P800x450, out var actual));

        Assert.Equal(new PixelRect(-1920, 0, 800, 450), actual);
    }

    [Fact]
    public void RejectsPresetLargerThanMonitorAndPreservesRegion()
    {
        var monitor = new PixelRect(0, 0, 1024, 600);
        var current = new PixelRect(100, 100, 640, 360);

        var success = PresetRegionService.TryCreateRegion(current, monitor, OutputPreset.P1280x720, out var actual);

        Assert.False(success);
        Assert.Equal(current, actual);
    }

    [Fact]
    public void OriginalLeavesCurrentRegionUnchanged()
    {
        var current = new PixelRect(-1400, 100, 713, 401);

        Assert.True(PresetRegionService.TryCreateRegion(current, Monitor, OutputPreset.Original, out var actual));
        Assert.Equal(current, actual);
    }
}
