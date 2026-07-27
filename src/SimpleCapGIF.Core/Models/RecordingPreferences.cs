namespace SimpleCapGIF.Core.Models;

public enum GlobalHotKeyPreset
{
    Disabled,
    F12,
    AltF9,
    ControlShiftR,
}

public sealed record RecordingPreferences
{
    public static RecordingPreferences Default { get; } = new();

    public bool IncludeCursor { get; init; } = true;
    public int StartDelaySeconds { get; init; }
    public GlobalHotKeyPreset GlobalHotKey { get; init; } = GlobalHotKeyPreset.F12;

    public RecordingPreferences Validate() => this with
    {
        StartDelaySeconds = StartDelaySeconds is 0 or 3 or 5 ? StartDelaySeconds : 0,
        GlobalHotKey = Enum.IsDefined(GlobalHotKey) ? GlobalHotKey : GlobalHotKeyPreset.F12,
    };
}
