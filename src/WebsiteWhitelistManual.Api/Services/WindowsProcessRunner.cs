using System.Diagnostics;
using System.IO;
using WebsiteWhitelistManual.Core.Abstractions;

namespace WebsiteWhitelistManual.Api.Services;

/// <summary>
/// Real OS-process-backed implementation of IProcessRunner, used to shell
/// out to reg.exe for registry backups. Uses ProcessStartInfo.ArgumentList
/// so arguments are never manually quoted or vulnerable to quoting bugs.
/// </summary>
public sealed class WindowsProcessRunner : IProcessRunner
{
    public ProcessResult Run(string fileName, IReadOnlyList<string> arguments)
    {
        // Resolve a bare filename (e.g. "reg.exe") against the system
        // directory explicitly, rather than letting Process.Start fall back
        // to Windows's default search order, which checks this app's own
        // directory first. Because this app runs elevated
        // (requireAdministrator), running it from a directory a standard
        // user can write to would otherwise let them plant their own
        // reg.exe alongside it and get it executed as Administrator.
        var resolvedFileName = Path.IsPathRooted(fileName)
            ? fileName
            : Path.Combine(Environment.SystemDirectory, fileName);

        var startInfo = new ProcessStartInfo(resolvedFileName)
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
