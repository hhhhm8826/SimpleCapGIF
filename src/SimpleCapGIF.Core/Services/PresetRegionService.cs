using SimpleCapGIF.Core.Geometry;
using SimpleCapGIF.Core.Models;

namespace SimpleCapGIF.Core.Services;

public static class PresetRegionService
{
    public static bool TryCreateRegion(PixelRect current, PixelRect monitorBounds, OutputPreset preset, out PixelRect region)
    {
        if (preset == OutputPreset.Original)
        {
            region = current;
            return true;
        }

        var size = preset switch
        {
            OutputPreset.P640x360 => new PixelSize(640, 360),
            OutputPreset.P800x450 => new PixelSize(800, 450),
            OutputPreset.P960x540 => new PixelSize(960, 540),
            OutputPreset.P1280x720 => new PixelSize(1280, 720),
            _ => throw new ArgumentOutOfRangeException(nameof(preset)),
        };
        if (size.Width > monitorBounds.Width || size.Height > monitorBounds.Height)
        {
            region = current;
            return false;
        }

        region = new PixelRect(
            current.Center.X - (size.Width / 2),
            current.Center.Y - (size.Height / 2),
            size.Width,
            size.Height).ClampInside(monitorBounds);
        return true;
    }
}
