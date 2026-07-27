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
}
