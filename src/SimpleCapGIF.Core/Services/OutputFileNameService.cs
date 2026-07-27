using SimpleCapGIF.Core.Models;

namespace SimpleCapGIF.Core.Services;

public sealed class OutputFileNameService
{
    public static string CreateAvailablePath(string folder, AnimationFormat format, DateTimeOffset timestamp)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folder);
        var extension = format == AnimationFormat.Gif ? ".gif" : ".webp";
        var baseName = $"SimpleCapGIF_{timestamp:yyyyMMdd_HHmmss}";
        var candidate = Path.Combine(folder, baseName + extension);
        for (var suffix = 2; File.Exists(candidate); suffix++)
        {
            candidate = Path.Combine(folder, $"{baseName}_{suffix}{extension}");
        }

        return candidate;
    }
}
