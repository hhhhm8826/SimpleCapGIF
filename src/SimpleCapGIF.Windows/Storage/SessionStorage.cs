using SimpleCapGIF.Localization;

namespace SimpleCapGIF.Windows.Storage;

public sealed class SessionStorage(string localAppDataRoot)
{
    public static SessionStorage CreateDefault() => new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SimpleCapGIF"));

    public string SettingsPath => Path.Combine(localAppDataRoot, "settings.json");

    public string CreateSessionDirectory()
    {
        var path = Path.Combine(localAppDataRoot, "Temp", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    public void CleanupSession(string sessionDirectory)
    {
        if (!Directory.Exists(sessionDirectory)) return;
        var expectedRoot = Path.GetFullPath(Path.Combine(localAppDataRoot, "Temp")) + Path.DirectorySeparatorChar;
        var resolved = Path.GetFullPath(sessionDirectory);
        if (!resolved.StartsWith(expectedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(AppStrings.UnexpectedSessionPath);
        }

        Directory.Delete(resolved, recursive: true);
    }

    public void CleanupOrphans(DateTimeOffset now)
    {
        var root = Path.Combine(localAppDataRoot, "Temp");
        if (!Directory.Exists(root)) return;
        foreach (var directory in Directory.EnumerateDirectories(root))
        {
            if (now - Directory.GetLastWriteTimeUtc(directory) > TimeSpan.FromHours(24))
            {
                CleanupSession(directory);
            }
        }
    }
}
