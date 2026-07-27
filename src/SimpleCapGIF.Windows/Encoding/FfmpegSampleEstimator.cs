using System.Diagnostics;
using SimpleCapGIF.Core.Geometry;
using SimpleCapGIF.Core.Models;
using SimpleCapGIF.Core.Services;
using SimpleCapGIF.Localization;
using SimpleCapGIF.Windows.Capture;

namespace SimpleCapGIF.Windows.Encoding;

public sealed class FfmpegSampleEstimator : ICaptureFrameObserver, IAsyncDisposable
{
    private const int SampleFramesPerSecond = 6;
    private const int SampleWindowSeconds = 2;
    private readonly object _sync = new();
    private readonly FfmpegToolchain _toolchain;
    private readonly AnimationFormat _format;
    private readonly string _workingDirectory;
    private readonly Action<EstimateSample> _sampleReady;
    private readonly Queue<byte[]> _samples = new();
    private readonly CancellationTokenSource _cancellation = new();
    private TimeSpan? _lastSampleAt;
    private TimeSpan? _lastEncodeAt;
    private long? _firstFrameEncodedBytes;
    private Task? _encodingTask;
    private bool _disposed;

    public FfmpegSampleEstimator(FfmpegToolchain toolchain, AnimationFormat format, string workingDirectory, Action<EstimateSample> sampleReady)
    {
        _toolchain = toolchain;
        _format = format;
        _workingDirectory = workingDirectory;
        _sampleReady = sampleReady;
    }

    public void OnFrame(ReadOnlySpan<byte> frame, PixelSize size, TimeSpan elapsed)
    {
        if (_disposed || _lastSampleAt is { } lastSampleAt && elapsed - lastSampleAt < TimeSpan.FromSeconds(1d / SampleFramesPerSecond)) return;
        _lastSampleAt = elapsed;
        var sampleSize = CalculateSampleSize(size);
        var sample = Downscale(frame, size, sampleSize);
        lock (_sync)
        {
            _samples.Enqueue(sample);
            while (_samples.Count > SampleFramesPerSecond * SampleWindowSeconds) _samples.Dequeue();
            if (elapsed < TimeSpan.FromSeconds(1) ||
                _lastEncodeAt is { } lastEncodeAt && elapsed - lastEncodeAt < TimeSpan.FromSeconds(2) ||
                _encodingTask is { IsCompleted: false }) return;
            _lastEncodeAt = elapsed;
            var snapshot = _samples.Select(static value => value.ToArray()).ToArray();
            _encodingTask = Task.Run(() => EncodeSampleSafelyAsync(snapshot, sampleSize, _cancellation.Token), CancellationToken.None);
        }
    }

    private async Task EncodeSampleSafelyAsync(byte[][] frames, PixelSize size, CancellationToken cancellationToken)
    {
        try
        {
            await EncodeSampleAsync(frames, size, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            // Estimation is advisory; a failed low-priority sample must never abort the recording.
            Debug.WriteLine($"SimpleCapGIF size estimation sample failed: {exception}");
        }
    }

    private async Task EncodeSampleAsync(byte[][] frames, PixelSize size, CancellationToken cancellationToken)
    {
        if (frames.Length == 0) return;
        Directory.CreateDirectory(_workingDirectory);
        var extension = _format == AnimationFormat.Gif ? ".gif" : ".webp";
        var outputPath = Path.Combine(_workingDirectory, "estimate" + extension);
        if (_firstFrameEncodedBytes is null)
        {
            var firstFramePath = Path.Combine(_workingDirectory, "first-frame" + extension);
            _firstFrameEncodedBytes = await EncodeFramesAsync([frames[0]], size, firstFramePath, cancellationToken).ConfigureAwait(false);
        }

        var encodedBytes = await EncodeFramesAsync(frames, size, outputPath, cancellationToken).ConfigureAwait(false);
        _sampleReady(new EstimateSample(
            encodedBytes,
            TimeSpan.FromSeconds(frames.Length / (double)SampleFramesPerSecond),
            size.Width,
            size.Height,
            SampleFramesPerSecond,
            _firstFrameEncodedBytes.Value));
    }

    private async Task<long> EncodeFramesAsync(byte[][] frames, PixelSize size, string outputPath, CancellationToken cancellationToken)
    {
        File.Delete(outputPath);
        var arguments = CreateArguments(size, outputPath);
        using var process = FfmpegProcess.Start(_toolchain.FfmpegPath, arguments, redirectInput: true);
        try
        {
            try { process.PriorityClass = ProcessPriorityClass.Idle; } catch (InvalidOperationException) { }
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            foreach (var frame in frames)
            {
                await process.StandardInput.BaseStream.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
            }

            process.StandardInput.Close();
            var exitCode = await FfmpegProcess.WaitForExitAsync(process, cancellationToken).ConfigureAwait(false);
            _ = await outputTask.ConfigureAwait(false);
            _ = await errorTask.ConfigureAwait(false);
            if (exitCode != 0 || !File.Exists(outputPath))
            {
                throw new InvalidOperationException(AppStrings.EstimateSampleFailed);
            }

            return new FileInfo(outputPath).Length;
        }
        finally
        {
            File.Delete(outputPath);
        }
    }

    private string[] CreateArguments(PixelSize size, string outputPath)
    {
        var common = new List<string>
        {
            "-hide_banner", "-loglevel", "error", "-threads", "1", "-f", "rawvideo", "-pixel_format", "bgra",
            "-video_size", $"{size.Width}x{size.Height}", "-framerate", SampleFramesPerSecond.ToString(System.Globalization.CultureInfo.InvariantCulture), "-i", "pipe:0",
        };
        if (_format == AnimationFormat.Gif)
        {
            common.AddRange(["-filter_complex", "[0:v]split[a][b];[a]palettegen=max_colors=192:stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=3:diff_mode=rectangle", "-loop", "0"]);
        }
        else
        {
            common.AddRange(["-an", "-c:v", "libwebp_anim", "-lossless", "0", "-quality", "75", "-compression_level", "4", "-loop", "0"]);
        }

        common.AddRange(["-y", outputPath]);
        return common.ToArray();
    }

    private static PixelSize CalculateSampleSize(PixelSize source)
    {
        var scale = Math.Min(1d, 320d / Math.Max(source.Width, source.Height));
        var width = Math.Max(2, ((int)Math.Floor(source.Width * scale)) & ~1);
        var height = Math.Max(2, ((int)Math.Floor(source.Height * scale)) & ~1);
        return new PixelSize(width, height);
    }

    private static byte[] Downscale(ReadOnlySpan<byte> source, PixelSize sourceSize, PixelSize destinationSize)
    {
        var destination = new byte[checked(destinationSize.Width * destinationSize.Height * 4)];
        for (var y = 0; y < destinationSize.Height; y++)
        {
            var sourceY = Math.Min(sourceSize.Height - 1, y * sourceSize.Height / destinationSize.Height);
            for (var x = 0; x < destinationSize.Width; x++)
            {
                var sourceX = Math.Min(sourceSize.Width - 1, x * sourceSize.Width / destinationSize.Width);
                var sourceIndex = ((sourceY * sourceSize.Width) + sourceX) * 4;
                var destinationIndex = ((y * destinationSize.Width) + x) * 4;
                source.Slice(sourceIndex, 4).CopyTo(destination.AsSpan(destinationIndex, 4));
            }
        }

        return destination;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _cancellation.Cancel();
        Task? task;
        lock (_sync) task = _encodingTask;
        if (task is not null)
        {
            try { await task.ConfigureAwait(false); } catch (OperationCanceledException) { }
        }

        _cancellation.Dispose();
    }
}
