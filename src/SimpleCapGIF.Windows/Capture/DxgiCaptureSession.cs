using System.Diagnostics;
using SimpleCapGIF.Core.Models;
using SimpleCapGIF.Localization;
using SimpleCapGIF.Windows.Encoding;

namespace SimpleCapGIF.Windows.Capture;

public sealed class DxgiCaptureSession : ICaptureSession
{
    private static readonly TimeSpan InitialFrameSettleTime = TimeSpan.FromMilliseconds(125);
    private readonly FfmpegToolchain _toolchain;
    private readonly ICaptureFrameObserver? _frameObserver;
    private readonly ICaptureFrameSourceFactory _frameSourceFactory;
    private readonly ICursorFrameCompositor _cursorCompositor;
    private readonly object _sync = new();
    private CancellationTokenSource? _lifetimeCancellation;
    private Task<RecordedSession>? _captureTask;
    private Process? _process;
    private Stopwatch? _clock;
    private long _framesWritten;
    private long _bytesWritten;
    private long _backlogTicks;
    private int _stopRequested;
    private bool _disposed;

    public DxgiCaptureSession(FfmpegToolchain toolchain, ICaptureFrameObserver? frameObserver = null)
        : this(toolchain, new DxgiFrameSourceFactory(), new WindowsCursorFrameCompositor(), frameObserver)
    {
    }

    internal DxgiCaptureSession(
        FfmpegToolchain toolchain,
        ICaptureFrameSourceFactory frameSourceFactory,
        ICursorFrameCompositor cursorCompositor,
        ICaptureFrameObserver? frameObserver = null)
    {
        _toolchain = toolchain;
        _frameSourceFactory = frameSourceFactory;
        _cursorCompositor = cursorCompositor;
        _frameObserver = frameObserver;
    }

    public CaptureStatistics Statistics => new(
        _clock?.Elapsed ?? TimeSpan.Zero,
        Interlocked.Read(ref _framesWritten),
        Interlocked.Read(ref _bytesWritten),
        TimeSpan.FromTicks(Interlocked.Read(ref _backlogTicks)));

    public Task? Completion
    {
        get
        {
            lock (_sync) return _captureTask;
        }
    }

    public async Task StartAsync(CaptureRequest request, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(request);
        request = request with { FramesPerSecond = ValidateFps(request.FramesPerSecond) };
        lock (_sync)
        {
            if (_captureTask is not null) throw new InvalidOperationException(AppStrings.AlreadyRecording);
        }

        await _toolchain.VerifyAsync(cancellationToken).ConfigureAwait(false);
        Directory.CreateDirectory(request.SessionDirectory);
        EnsureDiskSpace(request.SessionDirectory, request.OutputSize.Area * 4 * request.FramesPerSecond * 10);
        var videoPath = Path.Combine(request.SessionDirectory, "capture.mkv");
        var arguments = new[]
        {
            "-hide_banner", "-loglevel", "warning",
            "-f", "rawvideo",
            "-pixel_format", "bgra",
            "-video_size", $"{request.OutputSize.Width}x{request.OutputSize.Height}",
            "-framerate", request.FramesPerSecond.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "-i", "pipe:0",
            "-an", "-c:v", "ffv1", "-level", "3", "-y", videoPath,
        };

        var process = FfmpegProcess.Start(_toolchain.FfmpegPath, arguments, redirectInput: true);
        var lifetimeCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var initialized = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (_sync)
        {
            _process = process;
            _lifetimeCancellation = lifetimeCancellation;
            _clock = Stopwatch.StartNew();
            _framesWritten = 0;
            _bytesWritten = 0;
            _backlogTicks = 0;
            _stopRequested = 0;
            _captureTask = Task.Factory.StartNew(
                () => CaptureLoopAsync(request, videoPath, process, initialized, lifetimeCancellation.Token),
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default).Unwrap();
        }

        try
        {
            await initialized.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            lifetimeCancellation.Cancel();
            await FfmpegProcess.TerminateAsync(process).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<RecordedSession> StopAsync(CancellationToken cancellationToken)
    {
        Task<RecordedSession> captureTask;
        lock (_sync)
        {
            captureTask = _captureTask ?? throw new InvalidOperationException(AppStrings.NotRecording);
            Interlocked.Exchange(ref _stopRequested, 1);
        }

        try
        {
            return await captureTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _lifetimeCancellation?.Cancel();
            if (_process is not null) await FfmpegProcess.TerminateAsync(_process).ConfigureAwait(false);
            throw;
        }
        finally
        {
            lock (_sync)
            {
                _captureTask = null;
                _process?.Dispose();
                _process = null;
                _lifetimeCancellation?.Dispose();
                _lifetimeCancellation = null;
                _clock?.Stop();
            }
        }
    }

    private async Task<RecordedSession> CaptureLoopAsync(CaptureRequest request, string videoPath, Process process, TaskCompletionSource initialized, CancellationToken cancellationToken)
    {
        var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var frame = new byte[checked(request.OutputSize.Width * request.OutputSize.Height * 4)];
        var frameDurationTicks = Stopwatch.Frequency / (double)request.FramesPerSecond;
        var nextFrameTimestamp = 0L;
        var consecutiveSlowWrites = 0;

        try
        {
            using var source = _frameSourceFactory.Create(request.Region, request.OutputSize);
            await AcquireInitialFrameAsync(source, frame, cancellationToken).ConfigureAwait(false);
            await SettleInitialFrameAsync(source, frame, cancellationToken).ConfigureAwait(false);
            _clock?.Restart();
            initialized.TrySetResult();
            while (Volatile.Read(ref _stopRequested) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var now = Stopwatch.GetTimestamp();
                var remaining = nextFrameTimestamp - now;
                if (nextFrameTimestamp != 0 && remaining > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(remaining / (double)Stopwatch.Frequency), cancellationToken).ConfigureAwait(false);
                }

                if (source.TryAcquireLatest(1))
                {
                    source.CopyCurrentFrame(frame);
                }

                _cursorCompositor.Composite(frame, request.OutputSize, request.Region);
                _frameObserver?.OnFrame(frame, request.OutputSize, _clock?.Elapsed ?? TimeSpan.Zero);
                var writeStart = Stopwatch.GetTimestamp();
                await process.StandardInput.BaseStream.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
                var writeDuration = Stopwatch.GetElapsedTime(writeStart);
                Interlocked.Exchange(ref _backlogTicks, writeDuration.Ticks);
                consecutiveSlowWrites = writeDuration >= TimeSpan.FromMilliseconds(500) ? consecutiveSlowWrites + 1 : 0;
                if (consecutiveSlowWrites >= 2)
                {
                    throw new InvalidOperationException(AppStrings.PerformanceTooSlow);
                }

                Interlocked.Increment(ref _framesWritten);
                Interlocked.Add(ref _bytesWritten, frame.Length);
                if (_framesWritten % (request.FramesPerSecond * 2L) == 0)
                {
                    EnsureDiskSpace(request.SessionDirectory, request.OutputSize.Area * 4 * request.FramesPerSecond * 5);
                }

                nextFrameTimestamp = nextFrameTimestamp == 0
                    ? Stopwatch.GetTimestamp() + (long)frameDurationTicks
                    : nextFrameTimestamp + (long)frameDurationTicks;
                var lateness = Stopwatch.GetTimestamp() - nextFrameTimestamp;
                if (lateness > Stopwatch.Frequency / 2)
                {
                    throw new InvalidOperationException(AppStrings.PerformanceTooSlow);
                }
            }

            await process.StandardInput.BaseStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            process.StandardInput.Close();
            var exitCode = await FfmpegProcess.WaitForExitAsync(process, cancellationToken).ConfigureAwait(false);
            var standardError = await standardErrorTask.ConfigureAwait(false);
            _ = await standardOutputTask.ConfigureAwait(false);
            if (exitCode != 0 || !File.Exists(videoPath))
            {
                throw new InvalidOperationException(AppStrings.Format(AppStrings.TempCaptureFailedFormat, exitCode, standardError));
            }

            var frames = Interlocked.Read(ref _framesWritten);
            return new RecordedSession(videoPath, request.OutputSize, request.FramesPerSecond, TimeSpan.FromSeconds(frames / (double)request.FramesPerSecond), frames);
        }
        catch (Exception exception)
        {
            initialized.TrySetException(exception);
            await FfmpegProcess.TerminateAsync(process).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task AcquireInitialFrameAsync(ICaptureFrameSource source, byte[] frame, CancellationToken cancellationToken)
    {
        while (!source.TryAcquireLatest(16))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
        }

        source.CopyCurrentFrame(frame);
    }

    private static async Task SettleInitialFrameAsync(ICaptureFrameSource source, byte[] frame, CancellationToken cancellationToken)
    {
        var deadline = Stopwatch.GetTimestamp() + (long)(InitialFrameSettleTime.TotalSeconds * Stopwatch.Frequency);
        while (Stopwatch.GetTimestamp() < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (source.TryAcquireLatest(16)) source.CopyCurrentFrame(frame);
            await Task.Yield();
        }
    }

    private static int ValidateFps(int framesPerSecond) => framesPerSecond is 5 or 10 or 15 or 20 or 30
        ? framesPerSecond
        : throw new ArgumentOutOfRangeException(nameof(framesPerSecond));

    private static void EnsureDiskSpace(string path, long requiredBytes)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(path)) ?? throw new IOException(AppStrings.StorageDeviceUnavailable);
        if (new DriveInfo(root).AvailableFreeSpace < Math.Max(requiredBytes, 100_000_000))
        {
            throw new IOException(AppStrings.DiskSpaceLow);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        Interlocked.Exchange(ref _stopRequested, 1);
        if (_captureTask is not null)
        {
            _lifetimeCancellation?.Cancel();
            try
            {
                await _captureTask.ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }

        if (_process is not null)
        {
            await FfmpegProcess.TerminateAsync(_process).ConfigureAwait(false);
            _process.Dispose();
        }

        _lifetimeCancellation?.Dispose();
    }
}
