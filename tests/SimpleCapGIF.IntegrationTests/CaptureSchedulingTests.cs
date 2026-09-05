using System.Diagnostics;
using SimpleCapGIF.Windows.Capture;

namespace SimpleCapGIF.IntegrationTests;

public sealed class CaptureSchedulingTests
{
    [Fact]
    public void FirstFrameSchedulesFromCurrentTimestamp()
    {
        var frameDuration = Stopwatch.Frequency / 5;
        var currentTimestamp = Stopwatch.Frequency * 10L;

        var nextTimestamp = DxgiCaptureSession.AdvanceFrameSchedule(0, currentTimestamp, frameDuration);

        Assert.Equal(currentTimestamp + frameDuration, nextTimestamp);
    }

    [Fact]
    public void SmallDelayKeepsOriginalCadence()
    {
        var frameDuration = Stopwatch.Frequency / 5;
        var previousTimestamp = Stopwatch.Frequency * 10L;
        var scheduledTimestamp = previousTimestamp + frameDuration;
        var currentTimestamp = scheduledTimestamp + (Stopwatch.Frequency / 4);

        var nextTimestamp = DxgiCaptureSession.AdvanceFrameSchedule(previousTimestamp, currentTimestamp, frameDuration);

        Assert.Equal(scheduledTimestamp, nextTimestamp);
    }

    [Fact]
    public void LargeSchedulerDelayResynchronizesInsteadOfFailing()
    {
        var frameDuration = Stopwatch.Frequency / 5;
        var previousTimestamp = Stopwatch.Frequency * 10L;
        var currentTimestamp = previousTimestamp + frameDuration + Stopwatch.Frequency;

        var nextTimestamp = DxgiCaptureSession.AdvanceFrameSchedule(previousTimestamp, currentTimestamp, frameDuration);

        Assert.Equal(currentTimestamp + frameDuration, nextTimestamp);
    }

    [Fact]
    public void HealthyThroughputDoesNotWarnOrStop()
    {
        var monitor = new CapturePerformanceMonitor(30);
        CapturePerformanceSnapshot snapshot = default;

        for (var second = 0; second <= 20; second++)
        {
            snapshot = monitor.Observe(TimeSpan.FromSeconds(second), second * 30L);
        }

        Assert.InRange(snapshot.ActualFramesPerSecond, 29.99, 30.01);
        Assert.False(snapshot.IsDegraded);
        Assert.False(snapshot.ShouldStop);
    }

    [Fact]
    public void ModerateSustainedFrameLossWarnsWithoutStopping()
    {
        var monitor = new CapturePerformanceMonitor(30);
        CapturePerformanceSnapshot snapshot = default;

        for (var second = 0; second <= 30; second++)
        {
            snapshot = monitor.Observe(TimeSpan.FromSeconds(second), second * 25L);
        }

        Assert.InRange(snapshot.ActualFramesPerSecond, 24.99, 25.01);
        Assert.True(snapshot.IsDegraded);
        Assert.False(snapshot.ShouldStop);
    }

    [Fact]
    public void SevereSustainedFrameLossStopsAfterTenObservedSeconds()
    {
        var monitor = new CapturePerformanceMonitor(30);
        CapturePerformanceSnapshot snapshot = default;

        for (var second = 0; second <= 17; second++)
        {
            snapshot = monitor.Observe(TimeSpan.FromSeconds(second), second * 10L);
        }

        Assert.True(snapshot.IsDegraded);
        Assert.False(snapshot.ShouldStop);

        snapshot = monitor.Observe(TimeSpan.FromSeconds(18), 180);

        Assert.True(snapshot.ShouldStop);
    }

    [Fact]
    public void ThroughputRecoveryResetsSeverePerformanceTimer()
    {
        var monitor = new CapturePerformanceMonitor(30);
        long frames = 0;
        CapturePerformanceSnapshot snapshot = default;

        for (var second = 0; second <= 14; second++)
        {
            frames = second * 10L;
            snapshot = monitor.Observe(TimeSpan.FromSeconds(second), frames);
        }

        for (var second = 15; second <= 30; second++)
        {
            frames += 30;
            snapshot = monitor.Observe(TimeSpan.FromSeconds(second), frames);
        }

        Assert.False(snapshot.IsDegraded);
        Assert.False(snapshot.ShouldStop);
    }
}
