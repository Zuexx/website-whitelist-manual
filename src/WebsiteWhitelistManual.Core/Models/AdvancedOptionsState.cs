namespace WebsiteWhitelistManual.Core.Models;

public sealed record AdvancedOptionsState(
    bool DisableIncognito = true,
    bool DisableAccountSwitching = true,
    bool DisableDeveloperTools = true,
    // Defaults to off, unlike the other three: it's a new opt-in feature,
    // not a security-critical default the parent should be forced into.
    bool ForceYouTubeRestrict = false);
