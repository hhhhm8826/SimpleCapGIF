namespace SimpleCapGIF.Core.Models;

public sealed record CaptureSettings(
    AnimationFormat Format,
    OutputPreset OutputPreset,
    int FramesPerSecond,
    bool FormatUserSelected,
    bool PresetUserSelected,
    bool FpsUserSelected)
{
    public static CaptureSettings Default { get; } = new(
        AnimationFormat.Gif,
        OutputPreset.P800x450,
        10,
        FormatUserSelected: false,
        PresetUserSelected: false,
        FpsUserSelected: false);

    public CaptureSettings WithFormat(AnimationFormat format, bool userSelected = true)
    {
        var fps = FpsUserSelected ? FramesPerSecond : format == AnimationFormat.WebP ? 15 : 10;
        return this with { Format = format, FramesPerSecond = fps, FormatUserSelected = userSelected };
    }

    public CaptureSettings Validate()
    {
        if (FramesPerSecond is not (5 or 10 or 15 or 20 or 30))
        {
            throw new ArgumentOutOfRangeException(nameof(FramesPerSecond));
        }

        return this;
    }
}
