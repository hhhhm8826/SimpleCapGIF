using System.IO;
using SimpleCapGIF.App;

namespace SimpleCapGIF.IntegrationTests;

public sealed class SavedFileClipboardTests
{
    [Theory]
    [InlineData("gif")]
    [InlineData("webp")]
    public void FileDropListContainsOnlyTheAbsoluteSavedFilePath(string extension)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"SimpleCapGIF-clipboard-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, $"recording with spaces.{extension}");
            File.WriteAllBytes(path, [1]);

            var files = SavedFileClipboard.CreateFileDropList(path);

            var copiedPath = Assert.Single(files.Cast<string>());
            Assert.Equal(Path.GetFullPath(path), copiedPath);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void MissingSavedFileCannotBeCopied()
    {
        var path = Path.Combine(Path.GetTempPath(), $"SimpleCapGIF-missing-{Guid.NewGuid():N}.gif");

        var exception = Assert.Throws<FileNotFoundException>(() => SavedFileClipboard.CreateFileDropList(path));

        Assert.Equal(Path.GetFullPath(path), exception.FileName);
    }
}
