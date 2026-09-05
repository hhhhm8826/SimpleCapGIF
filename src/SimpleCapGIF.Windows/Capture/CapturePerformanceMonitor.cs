namespace SimpleCapGIF.Windows.Capture;

internal sealed class CapturePerformanceMonitor
{
    internal static readonly TimeSpan WarmupDuration = TimeSpan.FromSeconds(3);
    internal static readonly TimeSpan ObservationWindow = TimeSpan.FromSeconds(5);
    internal static readonly TimeSpan SeverePerformanceDuration = TimeSpan.FromSeconds(10);
    internal const double WarningRatio = 0.90;
    internal const double StopRatio = 0.50;

    private readonly int _requestedFramesPerSecond;
    private readonly Queue<Sample> _samples = new();
    private Sample? _windowBaseline;
    private TimeSpan? _severePerformanceStartedAt;

    public CapturePerformanceMonitor(int requestedFramesPerSecond)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requestedFramesPerSecond);
        _requestedFramesPerSecond = requestedFramesPerSecond;
    }

    public CapturePerformanceSnapshot Observe(TimeSpan elapsed, long framesWritten)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(elapsed, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfNegative(framesWritten);
        if (elapsed < WarmupDuration)
        {
            return new CapturePerformanceSnapshot(_requestedFramesPerSecond, 1, false, false);
        }

        var current = new Sample(elapsed, framesWritten);
        _samples.Enqueue(current);
        var cutoff = elapsed - ObservationWindow;
        while (_samples.Count > 0 && _samples.Peek().Elapsed <= cutoff)
        {
            _windowBaseline = _samples.Dequeue();
        }

        var baseline = _windowBaseline ?? _samples.Peek();
        var observedDuration = elapsed - baseline.Elapsed;
        if (observedDuration < ObservationWindow)
        {
            return new CapturePerformanceSnapshot(_requestedFramesPerSecond, 1, false, false);
        }

        var actualFramesPerSecond = Math.Max(0, framesWritten - baseline.FramesWritten) / observedDuration.TotalSeconds;
        var deliveryRatio = actualFramesPerSecond / _requestedFramesPerSecond;
        var isDegraded = deliveryRatio < WarningRatio;
        if (deliveryRatio < StopRatio)
        {
            _severePerformanceStartedAt ??= elapsed;
        }
        else
        {
            _severePerformanceStartedAt = null;
        }

        var shouldStop = _severePerformanceStartedAt is { } startedAt
            && elapsed - startedAt >= SeverePerformanceDuration;
        return new CapturePerformanceSnapshot(actualFramesPerSecond, deliveryRatio, isDegraded, shouldStop);
    }

    private readonly record struct Sample(TimeSpan Elapsed, long FramesWritten);
}

internal readonly record struct CapturePerformanceSnapshot(
    double ActualFramesPerSecond,
    double DeliveryRatio,
    bool IsDegraded,
    bool ShouldStop);
