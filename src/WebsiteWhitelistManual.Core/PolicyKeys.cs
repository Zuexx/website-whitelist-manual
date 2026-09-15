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
}
