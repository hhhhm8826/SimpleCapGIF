using SimpleCapGIF.Core.Geometry;
using SimpleCapGIF.Core.Models;

namespace SimpleCapGIF.Core.Services;

public interface IOutputSizeCalculator
{
    PixelSize Calculate(PixelSize source, OutputPreset preset);
}

public sealed class OutputSizeCalculator : IOutputSizeCalculator
{
    public PixelSize Calculate(PixelSize source, OutputPreset preset)
    {
        if (preset == OutputPreset.Original)
        {
            return MakeEvenWithoutUpscaling(source, source);
        }

        var budget = preset switch
        {
            OutputPreset.P640x360 => new PixelSize(640, 360),
            OutputPreset.P800x450 => new PixelSize(800, 450),
            OutputPreset.P960x540 => new PixelSize(960, 540),
            OutputPreset.P1280x720 => new PixelSize(1280, 720),
            _ => throw new ArgumentOutOfRangeException(nameof(preset)),
        };

        var scaleByLongEdge = Math.Max(budget.Width, budget.Height) / (double)Math.Max(source.Width, source.Height);
        var scaleByArea = Math.Sqrt(budget.Area / (double)source.Area);
        var scale = Math.Min(1d, Math.Min(scaleByLongEdge, scaleByArea));
        var scaled = new PixelSize(
            Math.Max(2, (int)Math.Floor(source.Width * scale)),
            Math.Max(2, (int)Math.Floor(source.Height * scale)));
        return MakeEvenWithoutUpscaling(scaled, source);
    }

    private static PixelSize MakeEvenWithoutUpscaling(PixelSize size, PixelSize source)
    {
        var width = Math.Min(source.Width, size.Width) & ~1;
        var height = Math.Min(source.Height, size.Height) & ~1;
        return new PixelSize(Math.Max(2, width), Math.Max(2, height));
    }
}
