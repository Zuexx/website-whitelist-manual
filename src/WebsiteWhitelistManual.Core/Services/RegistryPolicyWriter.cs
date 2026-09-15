using WebsiteWhitelistManual.Core.Abstractions;
using WebsiteWhitelistManual.Core.Models;

namespace WebsiteWhitelistManual.Core.Services;

public sealed class RegistryPolicyWriter : IRegistryPolicyWriter
{
    private readonly IWindowsRegistry _registry;

    public RegistryPolicyWriter(IWindowsRegistry registry)
    {
        _registry = registry;
    }

    public void Apply(WizardConfiguration configuration)
    {
        foreach (var target in configuration.BrowserTargets)
        {
            ApplyToBrowser(target, configuration);
        }
    }

    private void ApplyToBrowser(BrowserTarget target, WizardConfiguration configuration)
    {
        _registry.EnsureSubKeyExists(target.RootPath);

        var blocklistPath = $@"{target.RootPath}\{PolicyKeys.UrlBlocklistSubKey}";
        ClearValues(blocklistPath);
        _registry.SetStringValue(blocklistPath, "1", "*");

        var allowlistPath = $@"{target.RootPath}\{PolicyKeys.UrlAllowlistSubKey}";
        ClearValues(allowlistPath);
        for (var i = 0; i < configuration.AllowlistSites.Count; i++)
        {
            _registry.SetStringValue(allowlistPath, (i + 1).ToString(), configuration.AllowlistSites[i].Domain);
        }

        if (configuration.AdvancedOptions.DisableIncognito)
        {
            _registry.SetDwordValue(target.RootPath, target.IncognitoValueName, 1);
        }

        if (configuration.AdvancedOptions.DisableAccountSwitching)
        {
            _registry.SetDwordValue(target.RootPath, PolicyKeys.BrowserSigninValueName, 0);
        }

        if (configuration.AdvancedOptions.DisableDeveloperTools)
        {
            _registry.SetDwordValue(target.RootPath, PolicyKeys.DeveloperToolsAvailabilityValueName, 2);
        }
    }

    private void ClearValues(string subKeyPath)
    {
        _registry.EnsureSubKeyExists(subKeyPath);
        foreach (var valueName in _registry.GetValueNames(subKeyPath).ToList())
        {
            _registry.DeleteValue(subKeyPath, valueName);
        }
    }
}
