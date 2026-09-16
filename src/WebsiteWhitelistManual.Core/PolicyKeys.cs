namespace WebsiteWhitelistManual.Core;

/// <summary>
/// Canonical registry key/value names, verified against manual.html.
/// Never use "BrowserGuestModeEnabled" — that name only ever appeared
/// in an unverified UI mockup, not the source-of-truth manual.
/// </summary>
public static class PolicyKeys
{
    public const string EdgeRootPath = @"SOFTWARE\Policies\Microsoft\Edge";
    public const string ChromeRootPath = @"SOFTWARE\Policies\Google\Chrome";

    public const string UrlBlocklistSubKey = "URLBlocklist";
    public const string UrlAllowlistSubKey = "URLAllowlist";

    public const string EdgeIncognitoValueName = "InPrivateModeAvailability";
    public const string ChromeIncognitoValueName = "IncognitoModeAvailability";

    public const string BrowserSigninValueName = "BrowserSignin";
    public const string DeveloperToolsAvailabilityValueName = "DeveloperToolsAvailability";

    /// <summary>
    /// Edge-only. Edge's New Tab Page fetches live MSN content
    /// (ntp.msn.com and related hosts) unless this is disabled — with
    /// URLBlocklist="*" and no matching allowlist entry, that fetch gets
    /// blocked and Edge renders the entire New Tab surface as a bare
    /// error page with no address bar, effectively locking the browser.
    /// Disabling this (unlike NewTabPageLocation) has no domain-join or
    /// MDM-enrollment requirement, so it works on an unmanaged home PC.
    /// Chrome's default New Tab Page is served locally and isn't affected.
    /// </summary>
    public const string EdgeNewTabPageContentEnabledValueName = "NewTabPageContentEnabled";
}
