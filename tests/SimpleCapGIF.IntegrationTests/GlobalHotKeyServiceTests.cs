using SimpleCapGIF.Core.Models;
using SimpleCapGIF.Windows.Interop;
using SimpleCapGIF.App;

namespace SimpleCapGIF.IntegrationTests;

public sealed class GlobalHotKeyServiceTests
{
    [Theory]
    [InlineData(GlobalHotKeyPreset.F12, 0u, GlobalHotKeyService.VirtualKeyF12)]
    [InlineData(GlobalHotKeyPreset.AltF9, GlobalHotKeyService.ModAlt, GlobalHotKeyService.VirtualKeyF9)]
    [InlineData(GlobalHotKeyPreset.ControlShiftR, GlobalHotKeyService.ModControl | GlobalHotKeyService.ModShift, GlobalHotKeyService.VirtualKeyR)]
    public void PresetsMapToExpectedWindowsKeys(GlobalHotKeyPreset preset, uint modifiers, uint virtualKey)
    {
        var binding = GlobalHotKeyService.GetBinding(preset);

        Assert.Equal(modifiers, binding.Modifiers);
        Assert.Equal(virtualKey, binding.VirtualKey);
        Assert.NotEqual(0u, GlobalHotKeyService.ModNoRepeat);
    }

    [Fact]
    public void DisabledPresetHasNoWindowsKeyBinding() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => GlobalHotKeyService.GetBinding(GlobalHotKeyPreset.Disabled));

    [Theory]
    [InlineData(GlobalHotKeyAction.ToggleRecording, CaptureUiState.Selecting, (int)RecordingCommand.Start)]
    [InlineData(GlobalHotKeyAction.ToggleRecording, CaptureUiState.Countdown, (int)RecordingCommand.Cancel)]
    [InlineData(GlobalHotKeyAction.ToggleRecording, CaptureUiState.Recording, (int)RecordingCommand.Stop)]
    [InlineData(GlobalHotKeyAction.ToggleRecording, CaptureUiState.Encoding, (int)RecordingCommand.None)]
    [InlineData(GlobalHotKeyAction.ToggleRecording, CaptureUiState.Completed, (int)RecordingCommand.None)]
    [InlineData(GlobalHotKeyAction.CancelRecording, CaptureUiState.Selecting, (int)RecordingCommand.None)]
    [InlineData(GlobalHotKeyAction.CancelRecording, CaptureUiState.Countdown, (int)RecordingCommand.Cancel)]
    [InlineData(GlobalHotKeyAction.CancelRecording, CaptureUiState.Recording, (int)RecordingCommand.Cancel)]
    [InlineData(GlobalHotKeyAction.CancelRecording, CaptureUiState.Encoding, (int)RecordingCommand.None)]
    public void HotKeysRouteOnlyInSupportedStates(
        GlobalHotKeyAction action,
        CaptureUiState state,
        int expected) =>
        Assert.Equal((RecordingCommand)expected, MainWindow.ResolveHotKeyCommand(action, state));
}
