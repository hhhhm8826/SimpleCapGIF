using SimpleCapGIF.Core.Models;
using SimpleCapGIF.Localization;

namespace SimpleCapGIF.Core.Services;

public sealed record EstimateProfile(AnimationFormat Format, OutputPreset Preset, int Width, int Height, int FramesPerSecond, double CalibrationRatio = 1d);
public readonly record struct EstimateSample(
    long EncodedBytes,
    TimeSpan Duration,
    int Width,
    int Height,
    int FramesPerSecond,
    long FirstFrameEncodedBytes = 0);
public readonly record struct OutputSizeEstimate(long Bytes, bool IsWarning)
{
    public const long WarningThresholdBytes = 30_000_000;
}

public interface IOutputSizeEstimator
{
    void Reset(EstimateProfile profile);
    void AddSample(EstimateSample sample);
    OutputSizeEstimate GetEstimate(TimeSpan elapsed);
}

public sealed class OutputSizeEstimator : IOutputSizeEstimator
{
    private const double Alpha = 0.35;
    private const double CalibrationAlpha = 0.5;
    private const double SpatialScaleExponent = 0.85;
    private const double FrameRateScaleExponent = 0.8;
    private const int WebPAnimationWrapperBytes = 56;
    private const double MinimumCalibration = 0.25;
    private const double MaximumCalibration = 2.0;
    private EstimateProfile? _profile;
    private double? _fixedBytes;
    private double? _variableBytesPerSecond;

    public bool HasSample => _variableBytesPerSecond is not null;

    public void Reset(EstimateProfile profile)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(profile.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(profile.Height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(profile.FramesPerSecond);
        _profile = profile;
        _fixedBytes = null;
        _variableBytesPerSecond = null;
    }

    public void AddSample(EstimateSample sample)
    {
        var profile = _profile ?? throw new InvalidOperationException(AppStrings.EstimateProfileMissing);
        if (sample.EncodedBytes <= 0 || sample.Duration <= TimeSpan.Zero || sample.Width <= 0 || sample.Height <= 0 || sample.FramesPerSecond <= 0)
        {
            return;
        }

        var pixelRatio = ((double)profile.Width * profile.Height) / ((double)sample.Width * sample.Height);
        var pixelScale = Math.Pow(pixelRatio, SpatialScaleExponent);
        var fpsRatio = profile.FramesPerSecond / (double)sample.FramesPerSecond;
        var fpsScale = Math.Pow(fpsRatio, FrameRateScaleExponent);
        var calibration = Math.Clamp(profile.CalibrationRatio, MinimumCalibration, MaximumCalibration);

        if (sample.FirstFrameEncodedBytes > 0)
        {
            var firstFrameBytes = Math.Min(sample.FirstFrameEncodedBytes, sample.EncodedBytes);
            var animationWrapperBytes = profile.Format == AnimationFormat.WebP ? WebPAnimationWrapperBytes : 0;
            var observedFixed = ((firstFrameBytes * pixelScale) + animationWrapperBytes) * calibration;
            _fixedBytes = _fixedBytes is null ? observedFixed : (Alpha * observedFixed) + ((1 - Alpha) * _fixedBytes.Value);

            var firstFrameDuration = TimeSpan.FromSeconds(1d / sample.FramesPerSecond);
            var variableDuration = Math.Max(firstFrameDuration.TotalSeconds, (sample.Duration - firstFrameDuration).TotalSeconds);
            var observedVariable = ((sample.EncodedBytes - firstFrameBytes) / variableDuration) * pixelScale * fpsScale * calibration;
            _variableBytesPerSecond = _variableBytesPerSecond is null
                ? observedVariable
                : (Alpha * observedVariable) + ((1 - Alpha) * _variableBytesPerSecond.Value);
            return;
        }

        var observed = (sample.EncodedBytes / sample.Duration.TotalSeconds) * pixelScale * fpsScale * calibration;
        _variableBytesPerSecond = _variableBytesPerSecond is null
            ? observed
            : (Alpha * observed) + ((1 - Alpha) * _variableBytesPerSecond.Value);
    }

    public OutputSizeEstimate GetEstimate(TimeSpan elapsed)
    {
        var profile = _profile ?? throw new InvalidOperationException(AppStrings.EstimateProfileMissing);
        var baseline = profile.Format == AnimationFormat.Gif ? 1_150_000d : 700_000d;
        double estimate;
        if (_variableBytesPerSecond is { } variableRate)
        {
            var variableSeconds = _fixedBytes is null
                ? Math.Max(0, elapsed.TotalSeconds)
                : Math.Max(0, elapsed.TotalSeconds - (1d / profile.FramesPerSecond));
            estimate = (_fixedBytes ?? 0) + (variableSeconds * variableRate);
        }
        else
        {
            var rate = baseline * (((double)profile.Width * profile.Height) / 360_000d) * (profile.FramesPerSecond / 10d);
            estimate = Math.Max(0, elapsed.TotalSeconds) * rate;
        }

        var bytes = checked((long)Math.Ceiling(estimate));
        return new OutputSizeEstimate(bytes, bytes >= OutputSizeEstimate.WarningThresholdBytes);
    }

    public static double UpdateCalibration(double current, long actualBytes, long estimatedBytes)
    {
        if (actualBytes <= 0 || estimatedBytes <= 0)
        {
            return Math.Clamp(current, MinimumCalibration, MaximumCalibration);
        }

        var normalizedCurrent = Math.Clamp(current, MinimumCalibration, MaximumCalibration);
        var target = Math.Clamp(normalizedCurrent * (actualBytes / (double)estimatedBytes), MinimumCalibration, MaximumCalibration);
        return Math.Clamp((CalibrationAlpha * target) + ((1 - CalibrationAlpha) * normalizedCurrent), MinimumCalibration, MaximumCalibration);
    }
}
