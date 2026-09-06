using System.IO;
using SimpleCapGIF.App;

namespace SimpleCapGIF.IntegrationTests;

public sealed class RecordingCompletionTests
{
    [Fact]
    public async Task SuccessfulSettingsSaveReturnsNoError()
    {
        var saved = false;

        var error = await MainWindow.TrySaveSettingsAsync(() =>
        {
            saved = true;
            return Task.CompletedTask;
        });

        Assert.True(saved);
        Assert.Null(error);
    }

    [Fact]
    public async Task SettingsSaveFailureIsReturnedWithoutBeingRethrown()
    {
        var expected = new IOException("Settings are read-only.");

        var error = await MainWindow.TrySaveSettingsAsync(() => Task.FromException(expected));

        Assert.Same(expected, error);
    }

    [Fact]
    public void SuccessfulClipboardCopyReturnsNoError()
    {
        const string path = @"C:\recordings\capture.gif";
        string? copiedPath = null;

        var error = MainWindow.TryCopySavedFile(path, value => copiedPath = value);

        Assert.Equal(path, copiedPath);
        Assert.Null(error);
    }

    [Fact]
    public void ClipboardCopyFailureIsReturnedWithoutChangingSaveCompletion()
    {
        var expected = new IOException("Clipboard is busy.");

        var error = MainWindow.TryCopySavedFile(@"C:\recordings\capture.webp", _ => throw expected);

        Assert.Same(expected, error);
    }

    [Fact]
    public async Task AutomaticStopInvokesRecordingStopAfterDelay()
    {
        var stopped = false;

        await MainWindow.WaitForAutomaticStopAsync(
            TimeSpan.FromMilliseconds(10),
            () =>
            {
                stopped = true;
                return Task.CompletedTask;
            },
            CancellationToken.None);

        Assert.True(stopped);
    }

    [Fact]
    public async Task CanceledAutomaticStopDoesNotInvokeRecordingStop()
    {
        var stopped = false;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => MainWindow.WaitForAutomaticStopAsync(
            TimeSpan.FromSeconds(1),
            () =>
            {
                stopped = true;
                return Task.CompletedTask;
            },
            cancellation.Token));

        Assert.False(stopped);
    }
}
