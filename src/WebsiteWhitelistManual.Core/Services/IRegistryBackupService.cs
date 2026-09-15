using WebsiteWhitelistManual.Core.Models;

namespace WebsiteWhitelistManual.Core.Services;

public interface IRegistryBackupService
{
    BackupResult Backup(IReadOnlyList<BrowserTarget> targets, string baseDirectory, DateTimeOffset timestamp);
}
