using System.IO;
using SimpleCapGIF.Core.Geometry;
using SimpleCapGIF.Core.Models;
using SimpleCapGIF.Core.Services;
using SimpleCapGIF.Windows.Capture;
using SimpleCapGIF.Windows.Encoding;
using Xunit.Abstractions;

namespace SimpleCapGIF.IntegrationTests;

[Collection(LifetimeTestGroup.Name)]
public sealed class EstimateAccuracyTests
{
    private readonly ITestOutputHelper _output;

    public EstimateAccuracyTests(ITestOutputHelper output) => _output = output;

    [Theory(Timeout = 30_000)]
    [InlineData(AnimationFormat.Gif)]
    [InlineData(AnimationFormat.WebP)]
    public async Task StaticRecordingEstimateStaysCloseToActualFile(AnimationFormat format)
        => await RunAccuracyTestAsync(format, changingFrames: false);

    [Theory(Timeout = 30_000)]
    [InlineData(AnimationFormat.Gif)]
    [InlineData(AnimationFormat.WebP)]
    public async Task ChangingRecordingEstimateStaysCloseToActualFile(AnimationFormat format)
        => await RunAccuracyTestAsync(format, changingFrames: true);

    private async Task RunAccuracyTestAsync(AnimationFormat format, bool changingFrames)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"SimpleCapGIF-estimate-accuracy-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var toolchain = FfmpegToolchain.Resolve();
            var estimator = new OutputSizeEstimator();
            estimator.Reset(new EstimateProfile(format, OutputPreset.Original, 160, 90, 5));
            var sampleReady = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            await using var sampler = new FfmpegSampleEstimator(
                toolchain,
                format,
                Path.Combine(directory, "Estimate"),
                sample =>
                {
                    estimator.AddSample(sample);
                    sampleReady.TrySetResult(true);
                });
            ICaptureFrameSourceFactory frameSourceFactory = changingFrames
                ? new ChangingFrameSourceFactory()
                : new StaticFrameSourceFactory();
            await using var capture = new DxgiCaptureSession(toolchain, frameSourceFactory, new NoCursorCompositor(), sampler);
            var request = new CaptureRequest(new PixelRect(0, 0, 160, 90), new PixelSize(160, 90), 5, directory);
            await capture.StartAsync(request, CancellationToken.None);
            await sampleReady.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await Task.Delay(TimeSpan.FromSeconds(5));
            var session = await capture.StopAsync(CancellationToken.None);
            await sampler.DisposeAsync();

            var extension = format == AnimationFormat.Gif ? ".gif" : ".webp";
            var output = Path.Combine(directory, "actual" + extension);
            var result = await new FfmpegAnimationEncoder(toolchain, format).EncodeAsync(session, output, null, CancellationToken.None);
            var estimatedBytes = estimator.GetEstimate(session.Duration).Bytes;
            var ratio = estimatedBytes / (double)result.Bytes;

            var content = changingFrames ? "Changing" : "Static";
            _output.WriteLine($"{content} {format}: estimated={estimatedBytes:N0}, actual={result.Bytes:N0}, ratio={ratio:0.000}");
            Assert.InRange(ratio, 0.5, 2.0);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class StaticFrameSourceFactory : ICaptureFrameSourceFactory
    {
        public ICaptureFrameSource Create(PixelRect region, PixelSize outputSize) => new StaticFrameSource(outputSize);
    }

    private sealed class ChangingFrameSourceFactory : ICaptureFrameSourceFactory
    {
        public ICaptureFrameSource Create(PixelRect region, PixelSize outputSize) => new ChangingFrameSource(outputSize);
    }

    private sealed class NoCursorCompositor : ICursorFrameCompositor
    {
        public void Composite(byte[] target, PixelSize targetSize, PixelRect sourceRegion)
        {
        }
    }

    private sealed class StaticFrameSource(PixelSize size) : ICaptureFrameSource
    {
        public bool TryAcquireLatest(uint timeoutMilliseconds) => true;

        public void CopyCurrentFrame(Span<byte> destination)
        {
            destination.Fill(0xF3);
            for (var pixel = 3; pixel < size.Area * 4; pixel += 4) destination[pixel] = byte.MaxValue;
        }

        public void Dispose()
        {
        }
    }

    private sealed class ChangingFrameSource(PixelSize size) : ICaptureFrameSource
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
}
