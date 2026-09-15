namespace WebsiteWhitelistManual.Core.Models;

public sealed record PolicySnapshot(IReadOnlyList<BrowserPolicySnapshot> Browsers);
