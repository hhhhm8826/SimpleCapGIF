using SimpleCapGIF.Core.Geometry;

namespace SimpleCapGIF.Core.Models;

public sealed record CaptureRequest(PixelRect Region, PixelSize OutputSize, int FramesPerSecond, string SessionDirectory, bool IncludeCursor = true);
public sealed record CaptureStatistics(
    TimeSpan Elapsed,
    long FramesWritten,
    long BytesWritten,
    TimeSpan EncoderBacklog,
    double ActualFramesPerSecond,
    bool IsPerformanceDegraded);
public sealed record RecordedSession(string TemporaryVideoPath, PixelSize FrameSize, int FramesPerSecond, TimeSpan Duration, long FrameCount);
public sealed record EncodeResult(string Path, long Bytes, TimeSpan Duration, long FrameCount);
public readonly record struct EncodeProgress(double Fraction, string Message);

public interface ICaptureSession : IAsyncDisposable
{
    CaptureStatistics Statistics { get; }
    Task StartAsync(CaptureRequest request, CancellationToken cancellationToken);
    Task<RecordedSession> StopAsync(CancellationToken cancellationToken);
}

public interface IAnimationEncoder
{
    AnimationFormat Format { get; }
    Task<EncodeResult> EncodeAsync(RecordedSession session, string destinationPath, IProgress<EncodeProgress>? progress, CancellationToken cancellationToken);
}
