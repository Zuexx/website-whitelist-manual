using WebsiteWhitelistManual.Core.Abstractions;
using WebsiteWhitelistManual.Core.Models;

namespace WebsiteWhitelistManual.Core.Services;

public sealed class RegistryBackupService : IRegistryBackupService
{
    private readonly IProcessRunner _processRunner;
    private readonly IWindowsRegistry _registry;

    public RegistryBackupService(IProcessRunner processRunner, IWindowsRegistry registry)
    {
        _processRunner = processRunner;
        _registry = registry;
    }

    public BackupResult Backup(IReadOnlyList<BrowserTarget> targets, string baseDirectory, DateTimeOffset timestamp)
    {
        var backupDirectory = Path.Combine(baseDirectory, timestamp.ToString("yyyyMMdd_HHmmss"));

        try
        {
            Directory.CreateDirectory(backupDirectory);
        }
        catch (Exception ex)
        {
            return new BackupResult(
                Success: false,
                BackupDirectory: backupDirectory,
                BackupFilePaths: Array.Empty<string>(),
                ErrorMessage: ex.Message);
        }

        var filePaths = new List<string>();

        foreach (var target in targets)
        {
            if (!_registry.SubKeyExists(target.RootPath))
            {
                // Nothing to back up: this policy key has never been applied
                // on this machine (e.g. a fresh install), so there is no
                // existing state to protect before Apply() runs.
                continue;
            }

            var filePath = Path.Combine(backupDirectory, $"{target.Id}.reg");
            var arguments = $"export \"HKLM\\{target.RootPath}\" \"{filePath}\" /y";
            var result = _processRunner.Run("reg.exe", arguments);

            if (result.ExitCode != 0)
            {
                return new BackupResult(
                    Success: false,
                    BackupDirectory: backupDirectory,
                    BackupFilePaths: filePaths,
                    ErrorMessage: string.IsNullOrWhiteSpace(result.StandardError)
                        ? $"reg.exe export exited with code {result.ExitCode}."
                        : result.StandardError);
            }

            filePaths.Add(filePath);
        }

        return new BackupResult(Success: true, BackupDirectory: backupDirectory, BackupFilePaths: filePaths, ErrorMessage: null);
    }
}
