namespace WebsiteWhitelistManual.Core.Models;

public sealed record AdvancedOptionsState(
    bool DisableIncognito = true,
    bool DisableAccountSwitching = true,
    bool DisableDeveloperTools = true);
