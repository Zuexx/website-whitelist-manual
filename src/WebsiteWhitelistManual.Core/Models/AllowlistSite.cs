namespace WebsiteWhitelistManual.Core.Models;

public sealed class AllowlistSite
{
    public string Domain { get; }
    public string? CategoryLabel { get; }

    private AllowlistSite(string domain, string? categoryLabel)
    {
        Domain = domain;
        CategoryLabel = categoryLabel;
    }

    public static bool TryCreate(string? rawInput, string? categoryLabel, out AllowlistSite? site, out string? error)
    {
        site = null;

        if (string.IsNullOrWhiteSpace(rawInput))
        {
            error = "網域不能是空白。";
            return false;
        }

        var trimmed = rawInput.Trim();

        if (trimmed.Contains(' '))
        {
            error = "網域不能包含空格。";
            return false;
        }

        if (trimmed.Contains("://"))
        {
            error = "請只填網域，不要包含 http:// 或 https://。";
            return false;
        }

        if (!trimmed.Contains('.') || trimmed.StartsWith('.') || trimmed.EndsWith('.'))
        {
            error = "請輸入完整網域，例如 example.com。";
            return false;
        }

        var trimmedLabel = string.IsNullOrWhiteSpace(categoryLabel) ? null : categoryLabel.Trim();
        site = new AllowlistSite(trimmed, trimmedLabel);
        error = null;
        return true;
    }
}
