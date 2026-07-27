using SimpleCapGIF.Core.Geometry;
using SimpleCapGIF.Core.Models;
using SimpleCapGIF.Core.Services;

namespace SimpleCapGIF.Core.Tests;

public sealed class OutputSizeCalculatorTests
{
    private readonly OutputSizeCalculator _sut = new();

    [Theory]
    [InlineData(1600, 900, 800, 450)]
    [InlineData(900, 1600, 450, 800)]
    [InlineData(1000, 1000, 600, 600)]
    [InlineData(1600, 400, 800, 200)]
    public void RecommendedPresetHonorsLongEdgeAreaAndAspectRatio(int width, int height, int expectedWidth, int expectedHeight)
    {
        Assert.Equal(new PixelSize(expectedWidth, expectedHeight), _sut.Calculate(new PixelSize(width, height), OutputPreset.P800x450));
    }

    [Fact]
    public void DoesNotUpscaleAndRoundsDownToEvenDimensions()
    {
        Assert.Equal(new PixelSize(122, 76), _sut.Calculate(new PixelSize(123, 77), OutputPreset.Original));
        Assert.Equal(new PixelSize(122, 76), _sut.Calculate(new PixelSize(123, 77), OutputPreset.P1280x720));
    }
}
