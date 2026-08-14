using SimpleCapGIF.App.ViewModels;
using SimpleCapGIF.Core.Geometry;
using SimpleCapGIF.Core.Models;

namespace SimpleCapGIF.IntegrationTests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void RegionLabelShowsOnlyFinalImageDimensions()
    {
        var viewModel = new MainWindowViewModel
        {
            Region = new PixelRect(10, 20, 800, 450),
            SelectedPreset = OutputPreset.P640x360,
        };

        Assert.Equal("640×360", viewModel.RegionLabel);
        Assert.DoesNotContain("→", viewModel.RegionLabel);
    }
}
