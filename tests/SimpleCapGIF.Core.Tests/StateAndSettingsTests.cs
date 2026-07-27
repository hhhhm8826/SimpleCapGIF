using SimpleCapGIF.Core.Models;
using SimpleCapGIF.Core.Services;
using SimpleCapGIF.Core.Geometry;

namespace SimpleCapGIF.Core.Tests;

public sealed class StateAndSettingsTests
{
    [Fact]
    public void StateMachineFollowsHappyPath()
    {
        var sut = new CaptureStateMachine();
        sut.StartRecording();
        sut.StartEncoding();
        sut.Complete();
        sut.ReturnToSelecting();
        Assert.Equal(CaptureUiState.Selecting, sut.State);
    }

    [Fact]
    public void StateMachineRejectsInvalidTransition()
    {
        var sut = new CaptureStateMachine();
        Assert.Throws<InvalidOperationException>(sut.StartEncoding);
    }

    [Fact]
    public void StateMachineSupportsCountdownAndCancellation()
    {
        var sut = new CaptureStateMachine();
        sut.StartCountdown();
        Assert.Equal(CaptureUiState.Countdown, sut.State);
        sut.StartRecording();
        Assert.Equal(CaptureUiState.Recording, sut.State);
        sut.ReturnToSelecting();
        Assert.Equal(CaptureUiState.Selecting, sut.State);

        sut.StartCountdown();
        sut.ReturnToSelecting();
        Assert.Equal(CaptureUiState.Selecting, sut.State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancellationOrErrorReturnsToSelecting(bool duringEncoding)
    {
        var sut = new CaptureStateMachine();
        sut.StartRecording();
        if (duringEncoding) sut.StartEncoding();

        sut.ReturnToSelecting();

        Assert.Equal(CaptureUiState.Selecting, sut.State);
    }

    [Fact]
    public void WebPSelectUsesFifteenFpsUntilUserOverridesFps()
    {
        Assert.Equal(15, CaptureSettings.Default.WithFormat(AnimationFormat.WebP).FramesPerSecond);
        var userConfigured = CaptureSettings.Default with { FramesPerSecond = 20, FpsUserSelected = true };
        Assert.Equal(20, userConfigured.WithFormat(AnimationFormat.WebP).FramesPerSecond);
    }

    [Fact]
    public async Task CorruptSettingsRecoverToDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"SimpleCapGIF-settings-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(path, "{not json");
            var settings = await new JsonSettingsStore(path).LoadAsync();
            Assert.Equal(CaptureSettings.Default, settings.Capture);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task SettingsRoundTripPreservesUserSelectionsAndCalibration()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"SimpleCapGIF-settings-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "settings.json");
        var expected = new AppSettings
        {
            Capture = CaptureSettings.Default with
            {
                Format = AnimationFormat.WebP,
                FramesPerSecond = 20,
                FormatUserSelected = true,
                FpsUserSelected = true,
            },
            SaveFolder = Path.Combine(directory, "output"),
            Recording = new RecordingPreferences
            {
                IncludeCursor = false,
                StartDelaySeconds = 3,
                GlobalHotKey = GlobalHotKeyPreset.AltF9,
            },
            LastCustomRegionSize = new PixelSize(960, 540),
            CalibrationRatios = new Dictionary<string, double> { ["WebP:Recommended"] = 1.15 },
        };
        try
        {
            var store = new JsonSettingsStore(path);
            await store.SaveAsync(expected);
            var actual = await store.LoadAsync();

            Assert.Equal(expected.Capture, actual.Capture);
            Assert.Equal(expected.SaveFolder, actual.SaveFolder);
            Assert.Equal(expected.Recording, actual.Recording);
            Assert.Equal(expected.LastCustomRegionSize, actual.LastCustomRegionSize);
            Assert.Equal(expected.CalibrationRatios, actual.CalibrationRatios);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task OlderSettingsUseRecordingPreferenceDefaults()
    {
        var path = Path.Combine(Path.GetTempPath(), $"SimpleCapGIF-settings-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(path, """
                {
                  "SaveFolder": "C:\\captures"
                }
                """);

            var settings = await new JsonSettingsStore(path).LoadAsync();

            Assert.Equal(RecordingPreferences.Default, settings.Recording);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(10)]
    public void RecordingPreferencesNormalizeUnsupportedDelay(int seconds)
    {
        var preferences = RecordingPreferences.Default with { StartDelaySeconds = seconds };

        Assert.Equal(0, preferences.Validate().StartDelaySeconds);
    }

    [Fact]
    public void RecordingPreferencesNormalizeUnknownHotKey()
    {
        var preferences = RecordingPreferences.Default with { GlobalHotKey = (GlobalHotKeyPreset)999 };

        Assert.Equal(GlobalHotKeyPreset.F12, preferences.Validate().GlobalHotKey);
    }

    [Fact]
    public void OutputFileNameAddsMonotonicSuffixOnCollision()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"SimpleCapGIF-name-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var timestamp = new DateTimeOffset(2026, 7, 27, 12, 34, 56, TimeSpan.Zero);
            File.WriteAllText(Path.Combine(directory, "SimpleCapGIF_20260727_123456.gif"), string.Empty);
            File.WriteAllText(Path.Combine(directory, "SimpleCapGIF_20260727_123456_2.gif"), string.Empty);

            var path = OutputFileNameService.CreateAvailablePath(directory, AnimationFormat.Gif, timestamp);

            Assert.Equal(Path.Combine(directory, "SimpleCapGIF_20260727_123456_3.gif"), path);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
