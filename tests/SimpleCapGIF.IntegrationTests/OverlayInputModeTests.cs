using System.Runtime.ExceptionServices;
using System.Windows.Interop;
using SimpleCapGIF.App;
using SimpleCapGIF.Core.Models;
using SimpleCapGIF.Windows.Display;
using SimpleCapGIF.Windows.Interop;

namespace SimpleCapGIF.IntegrationTests;

public sealed class OverlayInputModeTests
{
    [Theory]
    [InlineData(CaptureUiState.Selecting, false, false)]
    [InlineData(CaptureUiState.Selecting, true, true)]
    [InlineData(CaptureUiState.Countdown, false, true)]
    [InlineData(CaptureUiState.Recording, false, true)]
    [InlineData(CaptureUiState.Encoding, false, true)]
    [InlineData(CaptureUiState.Completed, false, true)]
    public void OverlayPassesMouseInputThroughWhenRegionEditingIsUnavailable(
        CaptureUiState state,
        bool isFullScreen,
        bool expected) =>
        Assert.Equal(expected, MainWindow.ShouldOverlayPassThroughInput(state, isFullScreen));

    [Fact]
    public void ClickThroughWindowStyleCanBeEnabledAndDisabled()
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                using var source = new HwndSource(new HwndSourceParameters(nameof(ClickThroughWindowStyleCanBeEnabledAndDisabled)));

                OverlayWindowService.SetClickThrough(source.Handle, enabled: true);
                Assert.NotEqual(
                    0,
                    NativeMethods.GetWindowLongPtr(source.Handle, NativeMethods.GwlExStyle).ToInt64() & NativeMethods.WsExTransparent);

                OverlayWindowService.SetClickThrough(source.Handle, enabled: false);
                Assert.Equal(
                    0,
                    NativeMethods.GetWindowLongPtr(source.Handle, NativeMethods.GwlExStyle).ToInt64() & NativeMethods.WsExTransparent);
            }
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "The window-style test did not complete in time.");
        failure?.Throw();
    }
}
