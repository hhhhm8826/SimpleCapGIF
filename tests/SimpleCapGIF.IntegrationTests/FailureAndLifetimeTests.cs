using System.Diagnostics;
using System.IO;
using SimpleCapGIF.Core.Geometry;
using SimpleCapGIF.Core.Models;
using SimpleCapGIF.Windows.Capture;
using SimpleCapGIF.Windows.Encoding;
using SimpleCapGIF.Windows.Storage;
using SimpleCapGIF.Localization;

namespace SimpleCapGIF.IntegrationTests;

public sealed class FailureAndLifetimeTests
{
    [Fact]
    public async Task MissingAndModifiedFfmpegAreRejectedBeforeRecording()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            Assert.Throws<FileNotFoundException>(() => FfmpegToolchain.Resolve(directory));
            await File.WriteAllTextAsync(Path.Combine(directory, "ffmpeg.exe"), "modified");
            await File.WriteAllTextAsync(Path.Combine(directory, "ffprobe.exe"), "modified");
            var toolchain = FfmpegToolchain.Resolve(directory);
            var exception = await Assert.ThrowsAsync<InvalidDataException>(() => toolchain.VerifyAsync());
            Assert.Equal(AppStrings.FfmpegChecksumMismatch, exception.Message);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task LockedOutputIsNotReplacedAndPartialIsNotDamaged()
    {
        var directory = CreateTemporaryDirectory();
        var destination = Path.Combine(directory, "result.gif");
        var partial = Path.Combine(directory, "result.partial.gif");
        var sentinel = new byte[] { 1, 2, 3, 4 };
        try
        {
            await File.WriteAllBytesAsync(partial, sentinel);
            await using var locked = new FileStream(partial, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            var session = new RecordedSession(Path.Combine(directory, "capture.mkv"), new PixelSize(160, 90), 5, TimeSpan.FromSeconds(1), 5);
            var encoder = new FfmpegAnimationEncoder(FfmpegToolchain.Resolve(), AnimationFormat.Gif);

            await Assert.ThrowsAsync<IOException>(() => encoder.EncodeAsync(session, destination, null, CancellationToken.None));

            Assert.False(File.Exists(destination));
            locked.Position = 0;
            var actual = new byte[sentinel.Length];
            _ = await locked.ReadAsync(actual);
            Assert.Equal(sentinel, actual);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task CancellationTerminatesChildFfmpegProcess()
    {
        var toolchain = FfmpegToolchain.Resolve();
        await toolchain.VerifyAsync();
        using var process = FfmpegProcess.Start(
            toolchain.FfmpegPath,
            ["-hide_banner", "-loglevel", "error", "-f", "rawvideo", "-pixel_format", "bgra", "-video_size", "160x90", "-framerate", "5", "-i", "pipe:0", "-f", "null", "-"],
            redirectInput: true);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => FfmpegProcess.WaitForExitAsync(process, cancellation.Token));

        Assert.True(process.HasExited);
    }

    [Fact]
    public async Task EncodingCancellationDeletesPartialFile()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var toolchain = FfmpegToolchain.Resolve();
            await toolchain.VerifyAsync();
            var video = Path.Combine(directory, "capture.mkv");
            await GenerateFixtureAsync(toolchain.FfmpegPath, video, durationSeconds: 12);
            var destination = Path.Combine(directory, "cancelled.gif");
            var session = new RecordedSession(video, new PixelSize(800, 450), 30, TimeSpan.FromSeconds(12), 360);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(75));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                new FfmpegAnimationEncoder(toolchain, AnimationFormat.Gif).EncodeAsync(session, destination, null, cancellation.Token));

            Assert.False(File.Exists(destination));
            Assert.Empty(Directory.EnumerateFiles(directory, "*.partial.*"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task SampleEstimatorAcceptsFirstFrameAtZeroWithoutTimeSpanOverflow()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var frame = new byte[8 * 8 * 4];
            await using var estimator = new FfmpegSampleEstimator(
                FfmpegToolchain.Resolve(),
                AnimationFormat.Gif,
                directory,
                _ => { });

            estimator.OnFrame(frame, new PixelSize(8, 8), TimeSpan.Zero);
            estimator.OnFrame(frame, new PixelSize(8, 8), TimeSpan.FromSeconds(1));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task CaptureWaitsForStableDesktopFrameBeforeWritingStaticContent()
    {
        var directory = CreateTemporaryDirectory();
        var observer = new FirstPixelObserver(requiredFrames: 2);
        try
        {
            await using var capture = new DxgiCaptureSession(
                FfmpegToolchain.Resolve(),
                new TransitioningFrameSourceFactory(),
                new NoCursorCompositor(),
                observer);
            var request = new CaptureRequest(new PixelRect(0, 0, 160, 90), new PixelSize(160, 90), 5, directory);

            await capture.StartAsync(request, CancellationToken.None);
            await observer.Ready.WaitAsync(TimeSpan.FromSeconds(10));
            var session = await capture.StopAsync(CancellationToken.None);

            Assert.True(session.FrameCount >= 2);
            Assert.NotEmpty(observer.BlueValues);
            Assert.All(observer.BlueValues, value => Assert.Equal((byte)64, value));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CaptureRequestControlsCursorComposition(bool includeCursor)
    {
        var directory = CreateTemporaryDirectory();
        var compositor = new CountingCursorCompositor();
        var observer = new FrameSignalObserver(requiredFrames: 2);
        try
        {
            await using var capture = new DxgiCaptureSession(
                FfmpegToolchain.Resolve(),
                new TransitioningFrameSourceFactory(),
                compositor,
                observer);
            var request = new CaptureRequest(
                new PixelRect(0, 0, 160, 90),
                new PixelSize(160, 90),
                5,
                directory,
                includeCursor);

            await capture.StartAsync(request, CancellationToken.None);
            await observer.Ready.WaitAsync(TimeSpan.FromSeconds(10));
            _ = await capture.StopAsync(CancellationToken.None);

            if (includeCursor) Assert.True(compositor.CallCount > 0);
            else Assert.Equal(0, compositor.CallCount);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void SessionStorageRemovesOnlyOldOrExplicitInRootSessions()
    {
        var directory = CreateTemporaryDirectory();
        var outside = CreateTemporaryDirectory();
        try
        {
            var storage = new SessionStorage(directory);
            var oldSession = storage.CreateSessionDirectory();
            var recentSession = storage.CreateSessionDirectory();
            Directory.SetLastWriteTimeUtc(oldSession, DateTime.UtcNow - TimeSpan.FromHours(25));
            Directory.SetLastWriteTimeUtc(recentSession, DateTime.UtcNow - TimeSpan.FromHours(1));

            storage.CleanupOrphans(DateTimeOffset.UtcNow);

            Assert.False(Directory.Exists(oldSession));
            Assert.True(Directory.Exists(recentSession));
            Assert.Throws<InvalidOperationException>(() => storage.CleanupSession(outside));
            Assert.True(Directory.Exists(outside));
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
            if (Directory.Exists(outside)) Directory.Delete(outside, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"SimpleCapGIF-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class TransitioningFrameSourceFactory : ICaptureFrameSourceFactory
    {
        public ICaptureFrameSource Create(PixelRect region, PixelSize outputSize) => new TransitioningFrameSource();
    }

    private sealed class TransitioningFrameSource : ICaptureFrameSource
    {
        private int _acquisitions;
        private bool _stable;

        public bool TryAcquireLatest(uint timeoutMilliseconds)
        {
            _acquisitions++;
            if (_acquisitions == 1) return true;
            if (_acquisitions != 4) return false;
            _stable = true;
            return true;
        }

        public void CopyCurrentFrame(Span<byte> destination)
        {
            destination.Clear();
            if (!_stable) return;
            for (var pixel = 0; pixel < destination.Length; pixel += 4)
            {
                destination[pixel] = 64;
                destination[pixel + 1] = 128;
                destination[pixel + 2] = 192;
                destination[pixel + 3] = byte.MaxValue;
            }
        }

        public void Dispose()
        {
        }
    }

    private sealed class NoCursorCompositor : ICursorFrameCompositor
    {
        public void Composite(byte[] target, PixelSize targetSize, PixelRect sourceRegion)
        {
        }
    }

    private sealed class CountingCursorCompositor : ICursorFrameCompositor
    {
        public int CallCount { get; private set; }

        public void Composite(byte[] target, PixelSize targetSize, PixelRect sourceRegion) => CallCount++;
    }

    private sealed class FirstPixelObserver(int requiredFrames) : ICaptureFrameObserver
    {
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<byte> BlueValues { get; } = [];
        public Task Ready => _ready.Task;

        public void OnFrame(ReadOnlySpan<byte> frame, PixelSize size, TimeSpan elapsed)
        {
            BlueValues.Add(frame[0]);
            if (BlueValues.Count >= requiredFrames) _ready.TrySetResult();
        }
    }

    private sealed class FrameSignalObserver(int requiredFrames) : ICaptureFrameObserver
    {
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _frameCount;

        public Task Ready => _ready.Task;

        public void OnFrame(ReadOnlySpan<byte> frame, PixelSize size, TimeSpan elapsed)
        {
            if (Interlocked.Increment(ref _frameCount) >= requiredFrames) _ready.TrySetResult();
        }
    }

    private static async Task GenerateFixtureAsync(string ffmpegPath, string destination, int durationSeconds)
    {
        using var process = FfmpegProcess.Start(
            ffmpegPath,
            [
                "-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i", "testsrc2=s=800x450:r=30",
                "-t", durationSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "-pix_fmt", "bgra", "-c:v", "ffv1", "-level", "3", "-y", destination,
            ],
            redirectInput: false);
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        var exitCode = await FfmpegProcess.WaitForExitAsync(process, CancellationToken.None);
        _ = await outputTask;
        var error = await errorTask;
        Assert.True(exitCode == 0, error);
    }
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class LifetimeTestGroup
{
    public const string Name = "Capture lifetime";
}

[Collection(LifetimeTestGroup.Name)]
public sealed class SixtySecondCaptureLifetimeTests
{
    [Fact(Timeout = 90_000)]
    [Trait("Category", "Soak")]
    public async Task SixtySecondRecordingHasBoundedManagedMemoryAndCleansUp()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"SimpleCapGIF-lifetime-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var observer = new MemoryObserver();
        try
        {
            await using var capture = new DxgiCaptureSession(FfmpegToolchain.Resolve(), new SyntheticFrameSourceFactory(), new NoCursorCompositor(), observer);
            var request = new CaptureRequest(new PixelRect(0, 0, 160, 90), new PixelSize(160, 90), 5, directory);
            await capture.StartAsync(request, CancellationToken.None);
            await Task.Delay(TimeSpan.FromSeconds(60));
            var session = await capture.StopAsync(CancellationToken.None);

            Assert.InRange(session.FrameCount, 250, 310);
            Assert.Equal(TimeSpan.FromSeconds(session.FrameCount / 5d), session.Duration);
            Assert.True(File.Exists(session.TemporaryVideoPath));
            Assert.True(observer.Samples.Count >= 10);
            Assert.True(observer.Samples[^1] <= observer.Samples[0] + (32 * 1024 * 1024),
                $"Managed memory grew from {observer.Samples[0]:N0} to {observer.Samples[^1]:N0} bytes.");
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class SyntheticFrameSourceFactory : ICaptureFrameSourceFactory
    {
        public ICaptureFrameSource Create(PixelRect region, PixelSize outputSize) => new SyntheticFrameSource(outputSize);
    }

    private sealed class NoCursorCompositor : ICursorFrameCompositor
    {
        public void Composite(byte[] target, PixelSize targetSize, PixelRect sourceRegion)
        {
        }
    }

    private sealed class SyntheticFrameSource(PixelSize size) : ICaptureFrameSource
    {
        private byte _value;

        public bool TryAcquireLatest(uint timeoutMilliseconds) => true;

        public void CopyCurrentFrame(Span<byte> destination)
        {
            destination.Fill(_value++);
            for (var pixel = 3; pixel < size.Area * 4; pixel += 4) destination[pixel] = byte.MaxValue;
        }

        public void Dispose()
        {
        }
    }

    private sealed class MemoryObserver : ICaptureFrameObserver
    {
        private long _lastSecond = -1;
        public List<long> Samples { get; } = [];

        public void OnFrame(ReadOnlySpan<byte> frame, PixelSize size, TimeSpan elapsed)
        {
            var second = (long)elapsed.TotalSeconds;
            if (second < 5 || second % 5 != 0 || second == _lastSecond) return;
            _lastSecond = second;
            Samples.Add(GC.GetTotalMemory(forceFullCollection: false));
        }
    }
}
