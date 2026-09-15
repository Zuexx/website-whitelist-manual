using WebsiteWhitelistManual.Core.Abstractions;
using WebsiteWhitelistManual.Core.Models;

namespace WebsiteWhitelistManual.Core.Services;

public sealed class RegistryBackupService : IRegistryBackupService
{
    private readonly IProcessRunner _processRunner;

    public RegistryBackupService(IProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public BackupResult Backup(IReadOnlyList<BrowserTarget> targets, string baseDirectory, DateTimeOffset timestamp)
    {
        var backupDirectory = Path.Combine(baseDirectory, timestamp.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(backupDirectory);

        var filePaths = new List<string>();

        foreach (var target in targets)
        {
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
