using WebsiteWhitelistManual.Core.Models;

namespace WebsiteWhitelistManual.Api.Contracts;

// ----- Requests -----

public sealed record AllowlistSiteRequest(string Domain, string? CategoryLabel);

public sealed record AdvancedOptionsRequest(
    bool DisableIncognito,
    bool DisableAccountSwitching,
    bool DisableDeveloperTools,
    bool ForceYouTubeRestrict);

public sealed record ApplyPolicyRequest(
    IReadOnlyList<string> BrowserIds, // "Edge" and/or "Chrome", matching BrowserId enum names
    IReadOnlyList<AllowlistSiteRequest> AllowlistSites,
    AdvancedOptionsRequest AdvancedOptions);

// ----- Responses -----

public sealed record BrowserPolicySnapshotResponse(
    string BrowserId,
    bool PolicyKeyExists,
    IReadOnlyList<string> BlockedUrls,
    IReadOnlyList<string> AllowedUrls,
    bool? IncognitoDisabled,
    bool? BrowserSigninDisabled,
    bool? DeveloperToolsDisabled,
    bool? YouTubeRestrictEnabled)
{
    public static BrowserPolicySnapshotResponse FromDomain(BrowserPolicySnapshot snapshot) => new(
        snapshot.BrowserId.ToString(),
        snapshot.PolicyKeyExists,
        snapshot.BlockedUrls,
        snapshot.AllowedUrls,
        snapshot.IncognitoDisabled,
        snapshot.BrowserSigninDisabled,
        snapshot.DeveloperToolsDisabled,
        snapshot.YouTubeRestrictEnabled);
}

public sealed record PolicySnapshotResponse(IReadOnlyList<BrowserPolicySnapshotResponse> Browsers)
{
    public static PolicySnapshotResponse FromDomain(PolicySnapshot snapshot) =>
        new(snapshot.Browsers.Select(BrowserPolicySnapshotResponse.FromDomain).ToList());
}

public sealed record LocalAccountResponse(string AccountName, bool IsAdministrator, bool IsBuiltIn)
{
    public static LocalAccountResponse FromDomain(LocalAccountInfo info) =>
        new(info.AccountName, info.IsAdministrator, info.IsBuiltIn);
}

public sealed record ApplyPolicyResponse(
    bool Success,
    string? BackupDirectory,
    IReadOnlyList<string> BackupFilePaths,
    string? ErrorMessage,
    PolicySnapshotResponse? ResultingSnapshot);

public sealed record ApiErrorResponse(string Message);
