using System.Diagnostics;

using SimpleCapGIF.Localization;

namespace SimpleCapGIF.Windows.Encoding;

internal static class FfmpegProcess
{
    internal static Process Start(string executable, IEnumerable<string> arguments, bool redirectInput)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = redirectInput,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        if (!process.Start())
        {
            process.Dispose();
            throw new InvalidOperationException(AppStrings.FfmpegStartFailed);
        }

        return process;
    }

    internal static async Task<int> WaitForExitAsync(Process process, CancellationToken cancellationToken)
    {
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await TerminateAsync(process).ConfigureAwait(false);
            throw;
        }

        return process.ExitCode;
    }

    internal static async Task TerminateAsync(Process process)
    {
        if (process.HasExited) return;
        try
        {
            process.StandardInput.Close();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
    }
}
