using System.Diagnostics;
using WebsiteWhitelistManual.Core.Abstractions;

namespace WebsiteWhitelistManual.App.Services;

/// <summary>
/// Real OS-process-backed implementation of IProcessRunner, used to shell
/// out to reg.exe for registry backups. Uses ProcessStartInfo.ArgumentList
/// so arguments are never manually quoted or vulnerable to quoting bugs.
/// </summary>
public sealed class WindowsProcessRunner : IProcessRunner
{
    public ProcessResult Run(string fileName, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start process '{fileName}'.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        process.WaitForExit();

        var standardOutput = stdoutTask.GetAwaiter().GetResult();
        var standardError = stderrTask.GetAwaiter().GetResult();

        return new ProcessResult(process.ExitCode, standardOutput, standardError);
    }
}
