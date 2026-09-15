namespace WebsiteWhitelistManual.Core.Abstractions;

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
