namespace WebsiteWhitelistManual.Core.Models;

public sealed record BrowserPolicySnapshot(
    BrowserId BrowserId,
    bool PolicyKeyExists,
    IReadOnlyList<string> BlockedUrls,
    IReadOnlyList<string> AllowedUrls,
    bool? IncognitoDisabled,
    bool? BrowserSigninDisabled,
    bool? DeveloperToolsDisabled,
    bool? YouTubeRestrictEnabled);
