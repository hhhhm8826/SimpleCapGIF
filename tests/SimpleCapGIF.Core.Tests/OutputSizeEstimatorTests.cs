using SimpleCapGIF.Core.Models;
using SimpleCapGIF.Core.Services;

namespace SimpleCapGIF.Core.Tests;

public sealed class OutputSizeEstimatorTests
{
    [Fact]
    public void WarningChangesAtExactDecimalThirtyMegabyteBoundary()
    {
        var sut = new OutputSizeEstimator();
        sut.Reset(new EstimateProfile(AnimationFormat.Gif, OutputPreset.P800x450, 800, 450, 10));
        sut.AddSample(new EstimateSample(30_000_000, TimeSpan.FromSeconds(10), 800, 450, 10));
        Assert.False(sut.GetEstimate(TimeSpan.FromSeconds(9.999)).IsWarning);
        Assert.True(sut.GetEstimate(TimeSpan.FromSeconds(10)).IsWarning);
    }

    [Fact]
    public void CalibrationIsClampedAndSmoothed()
    {
        Assert.Equal(1.5, OutputSizeEstimator.UpdateCalibration(1, 1000, 100), 3);
        Assert.Equal(0.75, OutputSizeEstimator.UpdateCalibration(1, 500, 1000), 3);
        Assert.InRange(OutputSizeEstimator.UpdateCalibration(0.25, 1, 1000), 0.25, 2.0);
    }

    [Fact]
    public void AccurateCalibratedEstimateDoesNotDriftBackTowardOne()
    {
        Assert.Equal(0.8, OutputSizeEstimator.UpdateCalibration(0.8, 1_000, 1_000), 3);
    }

    [Fact]
    public void StaticSampleDoesNotRepeatFirstFrameCostForEverySecond()
    {
        var sut = new OutputSizeEstimator();
        sut.Reset(new EstimateProfile(AnimationFormat.Gif, OutputPreset.P800x450, 800, 450, 10));
        Assert.False(sut.HasSample);
        sut.AddSample(new EstimateSample(1_000, TimeSpan.FromSeconds(1), 800, 450, 10, FirstFrameEncodedBytes: 1_000));

        Assert.True(sut.HasSample);
        Assert.Equal(1_000, sut.GetEstimate(TimeSpan.FromSeconds(10)).Bytes);
    }

    [Fact]
    public void ResolutionScalingAccountsForSublinearCompressionGrowth()
    {
        var sut = new OutputSizeEstimator();
        sut.Reset(new EstimateProfile(AnimationFormat.Gif, OutputPreset.P800x450, 800, 450, 10));
        sut.AddSample(new EstimateSample(1_000, TimeSpan.FromSeconds(1), 320, 180, 10));

        Assert.InRange(sut.GetEstimate(TimeSpan.FromSeconds(1)).Bytes, 4_740, 4_750);
    }

    [Fact]
    public void SamplesUseExpectedEwmaWeight()
    {
        var sut = new OutputSizeEstimator();
        sut.Reset(new EstimateProfile(AnimationFormat.Gif, OutputPreset.P800x450, 100, 100, 10));
        sut.AddSample(new EstimateSample(1_000, TimeSpan.FromSeconds(1), 100, 100, 10));
        sut.AddSample(new EstimateSample(3_000, TimeSpan.FromSeconds(1), 100, 100, 10));

        Assert.Equal(17_000, sut.GetEstimate(TimeSpan.FromSeconds(10)).Bytes);
    }
}
