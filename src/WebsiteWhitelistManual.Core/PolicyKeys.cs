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

    /// <summary>
    /// Same value name on both Edge and Chrome. Does NOT stop a child from
    /// clicking a sidebar/recommended video within an already-open YouTube
    /// tab — YouTube's single-page-app navigation updates the URL via the
    /// History API, which URLBlocklist/URLAllowlist never intercepts (this
    /// is Google's documented, by-design behavior, not a bug). What this
    /// does instead is force YouTube's own server-side Restricted Mode, so
    /// whatever gets recommended/playable is filtered at the source.
    /// 0 = off, 1 = Moderate, 2 = Strict.
    /// </summary>
    public const string ForceYouTubeRestrictValueName = "ForceYouTubeRestrict";
}
