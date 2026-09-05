using System.Diagnostics;
using System.IO;
using SimpleCapGIF.Core.Geometry;
using SimpleCapGIF.Core.Models;
using SimpleCapGIF.Windows.Encoding;

namespace SimpleCapGIF.IntegrationTests;

public sealed class SyntheticEncodingTests
{
    public static TheoryData<string, string> Fixtures => new()
    {
        { "static-ui", "color=c=#f3f4f6:s=800x450" },
        { "scroll-ui", "testsrc2=s=800x450" },
        { "medium-change", "smptebars=s=800x450" },
        { "high-change", "nullsrc=s=800x450,geq=random(1)/hypot(X-cos(N*0.07)*W/3-W/2\\,Y-sin(N*0.05)*H/3-H/2)*255:128:128" },
    };

    [Theory]
    [MemberData(nameof(Fixtures))]
    public async Task DefaultGifProfileProducesValidEightSecondFileUnderThirtyMegabytes(string fixtureName, string sourceFilter)
    {
        await RunFixtureAsync(fixtureName, sourceFilter, AnimationFormat.Gif, 10);
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public async Task DefaultWebPProfileProducesValidEightSecondFileUnderThirtyMegabytes(string fixtureName, string sourceFilter)
    {
        await RunFixtureAsync(fixtureName, sourceFilter, AnimationFormat.WebP, 15);
    }

    [Fact]
    public async Task WebPVerifierAcceptsStaticTimelineCoalescedIntoFewerFrames()
    {
        var toolchain = FfmpegToolchain.Resolve();
        await toolchain.VerifyAsync();
        var directory = Path.Combine(Path.GetTempPath(), $"SimpleCapGIF-coalesced-webp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var output = Path.Combine(directory, "static.webp");
            await GenerateStillWebPAsync(toolchain.FfmpegPath, output);
            var singleFrameTimeline = new RecordedSession(string.Empty, new PixelSize(32, 32), 15, TimeSpan.FromSeconds(8), 1);
            await AnimatedWebPNormalizer.EnsureAnimatedAsync(output, singleFrameTimeline, CancellationToken.None);

            var capturedTimeline = singleFrameTimeline with { FrameCount = 120 };
            await AnimationFileVerifier.VerifyAsync(toolchain, AnimationFormat.WebP, output, capturedTimeline, CancellationToken.None);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(AnimationFormat.Gif)]
    [InlineData(AnimationFormat.WebP)]
    public async Task EncoderPreservesWallClockDurationWhenCaptureMissesRequestedFps(AnimationFormat format)
    {
        var toolchain = FfmpegToolchain.Resolve();
        await toolchain.VerifyAsync();
        var directory = Path.Combine(Path.GetTempPath(), $"SimpleCapGIF-slow-capture-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var temporaryVideo = Path.Combine(directory, "capture.mkv");
            var output = Path.Combine(directory, format == AnimationFormat.Gif ? "result.gif" : "result.webp");
            await GenerateFixedFrameFixtureAsync(toolchain.FfmpegPath, 30, 40, temporaryVideo);
            var session = new RecordedSession(temporaryVideo, new PixelSize(160, 90), 30, TimeSpan.FromSeconds(2), 40);

            var result = await new FfmpegAnimationEncoder(toolchain, format).EncodeAsync(session, output, null, CancellationToken.None);

            Assert.Equal(TimeSpan.FromSeconds(2), result.Duration);
            Assert.Equal(40, result.FrameCount);
            Assert.True(File.Exists(output));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task RunFixtureAsync(string fixtureName, string sourceFilter, AnimationFormat format, int framesPerSecond)
    {
        var toolchain = FfmpegToolchain.Resolve();
        await toolchain.VerifyAsync();
        var directory = Path.Combine(Path.GetTempPath(), $"SimpleCapGIF-integration-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var temporaryVideo = Path.Combine(directory, "capture.mkv");
            var output = Path.Combine(directory, fixtureName + (format == AnimationFormat.Gif ? ".gif" : ".webp"));
            await GenerateFixtureAsync(toolchain.FfmpegPath, sourceFilter, framesPerSecond, temporaryVideo);
            var session = new RecordedSession(temporaryVideo, new PixelSize(800, 450), framesPerSecond, TimeSpan.FromSeconds(8), framesPerSecond * 8L);
            var result = await new FfmpegAnimationEncoder(toolchain, format).EncodeAsync(session, output, null, CancellationToken.None);

            Assert.True(File.Exists(output));
            Assert.Equal(session.FrameCount, result.FrameCount);
            Assert.Equal(session.Duration, result.Duration);
            Assert.InRange(result.Bytes, 1, 29_999_999);
            Assert.Empty(Directory.EnumerateFiles(directory, "*.partial.*"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task GenerateFixtureAsync(string ffmpegPath, string sourceFilter, int framesPerSecond, string destination)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        string[] arguments =
        [
            "-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i", sourceFilter,
            "-r", framesPerSecond.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "-t", "8", "-pix_fmt", "bgra", "-c:v", "ffv1", "-level", "3", "-y", destination,
        ];
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("합성 프레임 FFmpeg를 시작하지 못했습니다.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        _ = await outputTask;
        var error = await errorTask;
        Assert.True(process.ExitCode == 0, error);
        Assert.True(File.Exists(destination));
    }

    private static async Task GenerateStillWebPAsync(string ffmpegPath, string destination)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        string[] arguments =
        [
            "-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i", "color=c=#f3f4f6:s=32x32",
            "-frames:v", "1", "-c:v", "libwebp", "-quality", "75", "-y", destination,
        ];
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("정지 WebP FFmpeg를 시작하지 못했습니다.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        _ = await outputTask;
        var error = await errorTask;
        Assert.True(process.ExitCode == 0, error);
        Assert.True(File.Exists(destination));
    }

    private static async Task GenerateFixedFrameFixtureAsync(string ffmpegPath, int framesPerSecond, int frameCount, string destination)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        string[] arguments =
        [
            "-hide_banner", "-loglevel", "error", "-f", "lavfi", "-i", $"testsrc2=s=160x90:r={framesPerSecond}",
            "-frames:v", frameCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "-pix_fmt", "bgra", "-c:v", "ffv1", "-level", "3", "-y", destination,
        ];
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("고정 프레임 FFmpeg를 시작하지 못했습니다.");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        _ = await outputTask;
        var error = await errorTask;
        Assert.True(process.ExitCode == 0, error);
        Assert.True(File.Exists(destination));
    }
}
