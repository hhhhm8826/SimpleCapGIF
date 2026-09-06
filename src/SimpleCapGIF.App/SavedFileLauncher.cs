using System.Diagnostics;
using System.IO;
using System.Security;
using Microsoft.Win32;

namespace SimpleCapGIF.App;

internal static class SavedFileLauncher
{
    private const string EdgeExecutableName = "msedge.exe";
    private const string EdgeRelativePath = @"Microsoft\Edge\Application\msedge.exe";
    private const string AppPathsRegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe";

    public static void Open(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _ = Process.Start(CreateStartInfo(path, FindEdgeExecutablePath()));
    }

    internal static ProcessStartInfo CreateStartInfo(string path, string? edgeExecutablePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (Path.GetExtension(path).Equals(".webp", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(edgeExecutablePath))
        {
            var startInfo = new ProcessStartInfo(edgeExecutablePath)
            {
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add(path);
            return startInfo;
        }

        return new ProcessStartInfo(path) { UseShellExecute = true };
    }

    internal static string? FindEdgeExecutablePath()
    {
        foreach (var candidate in GetRegistryCandidates().Concat(GetConventionalCandidates()))
        {
            if (!string.IsNullOrWhiteSpace(candidate) && File.Exists(candidate)) return candidate;
        }

        return null;
    }

    private static IEnumerable<string?> GetRegistryCandidates()
    {
        yield return ReadRegisteredEdgePath(RegistryHive.CurrentUser, RegistryView.Default);
        yield return ReadRegisteredEdgePath(RegistryHive.LocalMachine, RegistryView.Registry64);
        yield return ReadRegisteredEdgePath(RegistryHive.LocalMachine, RegistryView.Registry32);
    }

    private static IEnumerable<string> GetConventionalCandidates()
    {
        var folders = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        };
        return folders
            .Where(static folder => !string.IsNullOrWhiteSpace(folder))
            .Select(static folder => Path.Combine(folder, EdgeRelativePath));
    }

    private static string? ReadRegisteredEdgePath(RegistryHive hive, RegistryView view)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var appPath = baseKey.OpenSubKey(AppPathsRegistryPath);
            return (appPath?.GetValue(null) as string)?.Trim().Trim('"');
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            return null;
        }
    }
}
