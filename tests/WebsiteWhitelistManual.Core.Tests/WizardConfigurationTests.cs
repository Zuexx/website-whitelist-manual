using WebsiteWhitelistManual.Core.Models;
using Xunit;

namespace WebsiteWhitelistManual.Core.Tests;

public class WizardConfigurationTests
{
    private static AllowlistSite MakeSite(string domain)
    {
        AllowlistSite.TryCreate(domain, null, out var site, out _);
        return site!;
    }

    [Fact]
    public void AdvancedOptionsState_DefaultsAllThreeProtectionsOn()
    {
        var options = new AdvancedOptionsState();

        Assert.True(options.DisableIncognito);
        Assert.True(options.DisableAccountSwitching);
        Assert.True(options.DisableDeveloperTools);
    }

    [Fact]
    public void CanApply_IsFalse_WhenNoBrowsersSelected()
    {
        var config = new WizardConfiguration(
            browserTargets: Array.Empty<BrowserTarget>(),
            allowlistSites: new[] { MakeSite("example.com") },
            advancedOptions: new AdvancedOptionsState());

        Assert.False(config.CanApply);
    }

    [Fact]
    public void CanApply_IsFalse_WhenNoSitesAdded()
    {
        var config = new WizardConfiguration(
            browserTargets: new[] { BrowserTarget.Edge },
            allowlistSites: Array.Empty<AllowlistSite>(),
            advancedOptions: new AdvancedOptionsState());

        Assert.False(config.CanApply);
    }

    [Fact]
    public void CanApply_IsTrue_WhenAtLeastOneBrowserAndOneSite()
    {
        var config = new WizardConfiguration(
            browserTargets: new[] { BrowserTarget.Edge, BrowserTarget.Chrome },
            allowlistSites: new[] { MakeSite("example.com") },
            advancedOptions: new AdvancedOptionsState());

        Assert.True(config.CanApply);
    }
}
