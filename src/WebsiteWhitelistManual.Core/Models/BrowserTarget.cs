namespace WebsiteWhitelistManual.Core.Models;

public sealed class BrowserTarget
{
    public BrowserId Id { get; }
    public string RootPath { get; }
    public string IncognitoValueName { get; }
    public string DisplayName { get; }

    private BrowserTarget(BrowserId id, string rootPath, string incognitoValueName, string displayName)
    {
        Id = id;
        RootPath = rootPath;
        IncognitoValueName = incognitoValueName;
        DisplayName = displayName;
    }

    public static BrowserTarget Edge { get; } = new(
        BrowserId.Edge, PolicyKeys.EdgeRootPath, PolicyKeys.EdgeIncognitoValueName, "Microsoft Edge");

    public static BrowserTarget Chrome { get; } = new(
        BrowserId.Chrome, PolicyKeys.ChromeRootPath, PolicyKeys.ChromeIncognitoValueName, "Google Chrome");

    public static BrowserTarget FromId(BrowserId id) => id switch
    {
        BrowserId.Edge => Edge,
        BrowserId.Chrome => Chrome,
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown browser id.")
    };
}
