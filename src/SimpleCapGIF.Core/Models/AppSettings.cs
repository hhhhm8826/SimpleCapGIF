using SimpleCapGIF.Core.Geometry;

namespace SimpleCapGIF.Core.Models;

public sealed record AppSettings
{
    public CaptureSettings Capture { get; init; } = CaptureSettings.Default;
    public RecordingPreferences Recording { get; init; } = RecordingPreferences.Default;
    public string SaveFolder { get; init; } = string.Empty;
    public PixelSize LastCustomRegionSize { get; init; } = new(800, 450);
    public Dictionary<string, double> CalibrationRatios { get; init; } = [];
}
