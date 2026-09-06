using System.Collections.Specialized;
using System.IO;
using System.Windows;

namespace SimpleCapGIF.App;

internal static class SavedFileClipboard
{
    public static void Copy(string path) => Clipboard.SetFileDropList(CreateFileDropList(path));

    internal static StringCollection CreateFileDropList(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath)) throw new FileNotFoundException(null, fullPath);
        return new StringCollection { fullPath };
    }
}
