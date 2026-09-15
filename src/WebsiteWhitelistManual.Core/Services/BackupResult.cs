namespace WebsiteWhitelistManual.Core.Services;

public sealed record BackupResult(
    bool Success,
    string BackupDirectory,
    IReadOnlyList<string> BackupFilePaths,
    string? ErrorMessage);
