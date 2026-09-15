using WebsiteWhitelistManual.Core.Abstractions;
using WebsiteWhitelistManual.Core.Models;

namespace WebsiteWhitelistManual.Core.Services;

public sealed class RegistryPolicyReader : IRegistryPolicyReader
{
    private readonly IWindowsRegistry _registry;

    public RegistryPolicyReader(IWindowsRegistry registry)
    {
        _registry = registry;
    }

    public PolicySnapshot ReadSnapshot(IReadOnlyList<BrowserTarget> targets)
    {
        var browsers = targets.Select(ReadOne).ToList();
        return new PolicySnapshot(browsers);
    }

    private BrowserPolicySnapshot ReadOne(BrowserTarget target)
    {
        if (!_registry.SubKeyExists(target.RootPath))
        {
            return new BrowserPolicySnapshot(
                target.Id,
                PolicyKeyExists: false,
                BlockedUrls: Array.Empty<string>(),
                AllowedUrls: Array.Empty<string>(),
                IncognitoDisabled: null,
                BrowserSigninDisabled: null,
                DeveloperToolsDisabled: null);
        }

        var blockedUrls = ReadStringList($@"{target.RootPath}\{PolicyKeys.UrlBlocklistSubKey}");
        var allowedUrls = ReadStringList($@"{target.RootPath}\{PolicyKeys.UrlAllowlistSubKey}");

        var incognito = _registry.GetDwordValue(target.RootPath, target.IncognitoValueName);
        var signin = _registry.GetDwordValue(target.RootPath, PolicyKeys.BrowserSigninValueName);
        var devTools = _registry.GetDwordValue(target.RootPath, PolicyKeys.DeveloperToolsAvailabilityValueName);

        return new BrowserPolicySnapshot(
            target.Id,
            PolicyKeyExists: true,
            BlockedUrls: blockedUrls,
            AllowedUrls: allowedUrls,
            IncognitoDisabled: incognito.HasValue ? incognito.Value == 1 : null,
            BrowserSigninDisabled: signin.HasValue ? signin.Value == 0 : null,
            DeveloperToolsDisabled: devTools.HasValue ? devTools.Value == 2 : null);
    }

    private IReadOnlyList<string> ReadStringList(string subKeyPath)
    {
        if (!_registry.SubKeyExists(subKeyPath))
        {
            return Array.Empty<string>();
        }

        var values = new List<string>();
        foreach (var valueName in _registry.GetValueNames(subKeyPath))
        {
            var value = _registry.GetStringValue(subKeyPath, valueName);
            if (value is not null)
            {
                values.Add(value);
            }
        }
        return values;
    }
}
