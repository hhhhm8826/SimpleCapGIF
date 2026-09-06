using System.IO;
using SimpleCapGIF.App;

namespace SimpleCapGIF.IntegrationTests;

public sealed class SavedFileLauncherTests
{
    [Theory]
    [InlineData("capture.webp")]
    [InlineData("capture.WEBP")]
    public void WebPUsesEdgeWithTheExactPathAsOneArgument(string fileName)
    {
        var path = Path.Combine(@"C:\recordings with spaces", fileName);
        var edgePath = @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe";

        var startInfo = SavedFileLauncher.CreateStartInfo(path, edgePath);

        Assert.Equal(edgePath, startInfo.FileName);
        Assert.False(startInfo.UseShellExecute);
        Assert.Equal([path], startInfo.ArgumentList);
    }

    [Fact]
    public void GifUsesTheWindowsFileAssociation()
    {
        var path = @"C:\recordings with spaces\capture.gif";

        var startInfo = SavedFileLauncher.CreateStartInfo(path, @"C:\Edge\msedge.exe");

        Assert.Equal(path, startInfo.FileName);
        Assert.True(startInfo.UseShellExecute);
        Assert.Empty(startInfo.ArgumentList);
    }

    [Fact]
    public void WebPFallsBackToTheWindowsFileAssociationWhenEdgeIsUnavailable()
    {
        var path = @"C:\recordings\capture.webp";

        var startInfo = SavedFileLauncher.CreateStartInfo(path, null);

        Assert.Equal(path, startInfo.FileName);
        Assert.True(startInfo.UseShellExecute);
    }
}
