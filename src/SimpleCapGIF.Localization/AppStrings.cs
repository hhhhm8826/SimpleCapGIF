using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Runtime.CompilerServices;

namespace SimpleCapGIF.Localization;

public static class AppStrings
{
    private static readonly ResourceManager Manager = new("SimpleCapGIF.Localization.Resources.Strings", Assembly.GetExecutingAssembly());

    internal static ResourceManager ResourceManager => Manager;

    public static string Get(string key, CultureInfo? culture = null) =>
        Manager.GetString(key, culture ?? CultureInfo.CurrentUICulture) ?? throw new MissingManifestResourceException($"Missing resource: {key}");

    public static string Format(string format, params object?[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, format, arguments);

    private static string Value([CallerMemberName] string key = "") => Get(key);

    public static string AlreadyRecording => Value();
    public static string AnimationSaveFailedFormat => Value();
    public static string AppName => Value();
    public static string CaptureStoppedError => Value();
    public static string CapturePerformanceWarningFormat => Value();
    public static string CaptureUiExclusionUnavailable => Value();
    public static string ChangeSaveLocation => Value();
    public static string CancelWithoutSaving => Value();
    public static string CancelWithoutSavingHotKey => Value();
    public static string DestinationExists => Value();
    public static string DiskSpaceLow => Value();
    public static string DisplayChanged => Value();
    public static string DurationFormat => Value();
    public static string EmptyEncodingResult => Value();
    public static string ErrorDialogTitle => Value();
    public static string EstimateProfileMissing => Value();
    public static string EstimateSampleFailed => Value();
    public static string EstimatedSizeFormat => Value();
    public static string Exit => Value();
    public static string FfmpegChecksumMismatch => Value();
    public static string FfmpegMissing => Value();
    public static string FfmpegStartFailed => Value();
    public static string Folder => Value();
    public static string FolderDialogTitle => Value();
    public static string FormatLabel => Value();
    public static string FrameCountFormat => Value();
    public static string FrameRate => Value();
    public static string FullScreen => Value();
    public static string GifBlockInvalid => Value();
    public static string GifHeaderMissing => Value();
    public static string GifLoopMissing => Value();
    public static string GifTruncated => Value();
    public static string GifVerificationFailedFormat => Value();
    public static string GpuScalingUnavailable => Value();
    public static string HdrWarning => Value();
    public static string HdrWarningTitle => Value();
    public static string HotKeyDisabled => Value();
    public static string HotKeyUnavailableFormat => Value();
    public static string IncludeCursor => Value();
    public static string InvalidSettingsFolder => Value();
    public static string InvalidStateReturnFormat => Value();
    public static string InvalidStateTransitionFormat => Value();
    public static string Location => Value();
    public static string MoveCaptureRegion => Value();
    public static string NoDisplayOutput => Value();
    public static string NotRecording => Value();
    public static string OpenSaveFolder => Value();
    public static string OpenSavedFile => Value();
    public static string OpenSavedFileError => Value();
    public static string Original => Value();
    public static string OutputSize => Value();
    public static string OverlayPlacementFailed => Value();
    public static string PerformanceTooSlow => Value();
    public static string Record => Value();
    public static string RecordingCountdown => Value();
    public static string RecordingHotKey => Value();
    public static string ResolutionTooLarge => Value();
    public static string ReturnToRegion => Value();
    public static string SaveAnimationErrorFormat => Value();
    public static string Saved => Value();
    public static string SavedSizeFormat => Value();
    public static string SavedSizeOpenFormat => Value();
    public static string SaveFolderSettingsError => Value();
    public static string SaveSettingsError => Value();
    public static string Saving => Value();
    public static string StartAppError => Value();
    public static string StartDelay => Value();
    public static string StartDelayFiveSeconds => Value();
    public static string StartDelayImmediate => Value();
    public static string StartDelayThreeSeconds => Value();
    public static string StartCaptureError => Value();
    public static string Stop => Value();
    public static string StorageDeviceUnavailable => Value();
    public static string Settings => Value();
    public static string TempCaptureFailedFormat => Value();
    public static string ToolbarTitle => Value();
    public static string UnexpectedSessionPath => Value();
    public static string UnsupportedFormat => Value();
    public static string Verifying => Value();
    public static string WebpAnimationMissing => Value();
    public static string WebpCanvasMissing => Value();
    public static string WebpChunkInvalid => Value();
    public static string WebpContainerInvalid => Value();
    public static string WebpFrameCountFormat => Value();
    public static string WebpImageMissing => Value();
    public static string WebpRiffMissing => Value();
    public static string WindowCaptureExclusionFailed => Value();
    public static string WrongAnimationFormat => Value();
}
