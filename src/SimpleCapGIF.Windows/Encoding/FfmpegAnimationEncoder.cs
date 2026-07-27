using SimpleCapGIF.Core.Models;
using SimpleCapGIF.Localization;

namespace SimpleCapGIF.Windows.Encoding;

public sealed class FfmpegAnimationEncoder(FfmpegToolchain toolchain, AnimationFormat format) : IAnimationEncoder
{
    public AnimationFormat Format { get; } = format;

    public async Task<EncodeResult> EncodeAsync(RecordedSession session, string destinationPath, IProgress<EncodeProgress>? progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        await toolchain.VerifyAsync(cancellationToken).ConfigureAwait(false);
        var directory = Path.GetDirectoryName(destinationPath) ?? throw new InvalidOperationException(AppStrings.InvalidSettingsFolder);
        Directory.CreateDirectory(directory);
        var extension = Path.GetExtension(destinationPath);
        var partialPath = Path.Combine(directory, Path.GetFileNameWithoutExtension(destinationPath) + ".partial" + extension);
        await DeleteFileWithRetryAsync(partialPath).ConfigureAwait(false);
        progress?.Report(new EncodeProgress(0, AppStrings.Saving));

        try
        {
            var arguments = CreateArguments(session.TemporaryVideoPath, partialPath);
            using var process = FfmpegProcess.Start(toolchain.FfmpegPath, arguments, redirectInput: false);
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            var exitCode = await FfmpegProcess.WaitForExitAsync(process, cancellationToken).ConfigureAwait(false);
            _ = await outputTask.ConfigureAwait(false);
            var error = await errorTask.ConfigureAwait(false);
            if (exitCode != 0 || !File.Exists(partialPath))
            {
                throw new InvalidOperationException(AppStrings.Format(AppStrings.AnimationSaveFailedFormat, DisplayName, error));
            }

            progress?.Report(new EncodeProgress(0.9, AppStrings.Verifying));
            if (Format == AnimationFormat.WebP)
            {
                await AnimatedWebPNormalizer.EnsureAnimatedAsync(partialPath, session, cancellationToken).ConfigureAwait(false);
            }
            await AnimationFileVerifier.VerifyAsync(toolchain, Format, partialPath, session, cancellationToken).ConfigureAwait(false);
            if (File.Exists(destinationPath)) throw new IOException(AppStrings.DestinationExists);
            File.Move(partialPath, destinationPath);
            var bytes = new FileInfo(destinationPath).Length;
            progress?.Report(new EncodeProgress(1, AppStrings.Saved));
            return new EncodeResult(destinationPath, bytes, session.Duration, session.FrameCount);
        }
        catch
        {
            await DeleteFileWithRetryAsync(partialPath).ConfigureAwait(false);
            throw;
        }
    }

    private static async Task DeleteFileWithRetryAsync(string path)
    {
        const int maximumAttempts = 5;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                File.Delete(path);
                return;
            }
            catch (IOException) when (attempt < maximumAttempts)
            {
            }
            catch (UnauthorizedAccessException) when (attempt < maximumAttempts)
            {
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50 * attempt), CancellationToken.None).ConfigureAwait(false);
        }
    }

    private string DisplayName => Format == AnimationFormat.Gif ? "GIF" : "WebP";

    private string[] CreateArguments(string inputPath, string outputPath) => Format switch
    {
        AnimationFormat.Gif =>
        [
            "-hide_banner", "-loglevel", "warning", "-i", inputPath,
            "-filter_complex", "[0:v]split[a][b];[a]palettegen=max_colors=192:stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=3:diff_mode=rectangle",
            "-loop", "0", "-y", outputPath,
        ],
        AnimationFormat.WebP =>
        [
            "-hide_banner", "-loglevel", "warning", "-i", inputPath,
            "-an", "-c:v", "libwebp_anim", "-lossless", "0", "-quality", "75", "-compression_level", "4", "-loop", "0", "-y", outputPath,
        ],
        _ => throw new InvalidOperationException(AppStrings.UnsupportedFormat),
    };
}
