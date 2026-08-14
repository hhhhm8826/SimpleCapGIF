using System.Security.Cryptography;
using SimpleCapGIF.Localization;

namespace SimpleCapGIF.Windows.Encoding;

public sealed class FfmpegToolchain
{
    public const string Version = "n8.1.2-31-g8c9502e9b0-20260726";
    public const string ArchiveSha256 = "923522df4e21c84cf6bd533ad690ea9b134087b38a95535a35abd786c25445c9";
    public const string FfmpegSha256 = "e674aa31bc9e6f56f955c7ce87a194a5f949f67545b59f723f155053acb1269d";
    public const string FfprobeSha256 = "ac9bf61f6f6f642e7f655e86ff60c7fe5670eebd16c18de3a9bbf81af03c50db";
    private readonly object _verificationSync = new();
    private Task? _verificationTask;
    private volatile bool _verified;

    private FfmpegToolchain(string root)
    {
        Root = root;
        FfmpegPath = Path.Combine(root, "ffmpeg.exe");
        FfprobePath = Path.Combine(root, "ffprobe.exe");
    }

    public string Root { get; }
    public string FfmpegPath { get; }
    public string FfprobePath { get; }

    public static FfmpegToolchain Resolve(string? explicitRoot = null)
    {
        if (explicitRoot is not null)
        {
            var explicitToolchain = new FfmpegToolchain(explicitRoot);
            if (File.Exists(explicitToolchain.FfmpegPath) && File.Exists(explicitToolchain.FfprobePath)) return explicitToolchain;
            throw new FileNotFoundException(AppStrings.FfmpegMissing);
        }

        var candidates = new[]
        {
            Environment.GetEnvironmentVariable("SIMPLECAPGIF_FFMPEG_ROOT"),
            Path.Combine(AppContext.BaseDirectory, "resources", "ffmpeg"),
            FindDevelopmentToolchainRoot(),
            Path.Combine(AppContext.BaseDirectory, "ffmpeg"),
            AppContext.BaseDirectory,
        };

        foreach (var root in candidates.Where(static value => !string.IsNullOrWhiteSpace(value)))
        {
            var toolchain = new FfmpegToolchain(root!);
            if (File.Exists(toolchain.FfmpegPath) && File.Exists(toolchain.FfprobePath))
            {
                return toolchain;
            }
        }

        throw new FileNotFoundException(AppStrings.FfmpegMissing);
    }

    private static string? FindDevelopmentToolchainRoot()
    {
        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            var directory = new DirectoryInfo(start);
            while (directory is not null)
            {
                var candidate = Path.Combine(directory.FullName, "artifacts", "dependencies", "ffmpeg", "8.1.2-31-g8c9502e9b0", "ffmpeg-n8.1.2-31-g8c9502e9b0-win64-lgpl-8.1", "bin");
                if (File.Exists(Path.Combine(candidate, "ffmpeg.exe"))) return candidate;
                directory = directory.Parent;
            }
        }

        return null;
    }

    public async Task VerifyAsync(CancellationToken cancellationToken = default)
    {
        if (_verified) return;
        Task verificationTask;
        lock (_verificationSync)
        {
            verificationTask = _verificationTask ??= VerifyCoreAsync();
        }
        await verificationTask.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task VerifyCoreAsync()
    {
        var ffmpegHash = await ComputeSha256Async(FfmpegPath, CancellationToken.None).ConfigureAwait(false);
        var ffprobeHash = await ComputeSha256Async(FfprobePath, CancellationToken.None).ConfigureAwait(false);
        if (!ffmpegHash.Equals(FfmpegSha256, StringComparison.OrdinalIgnoreCase) ||
            !ffprobeHash.Equals(FfprobeSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(AppStrings.FfmpegChecksumMismatch);
        }

        _verified = true;
    }

    private static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexStringLower(hash);
    }
}
