using WebsiteWhitelistManual.Core.Models;
using WebsiteWhitelistManual.Core.Services;
using WebsiteWhitelistManual.Core.Tests.Fakes;
using Xunit;

namespace WebsiteWhitelistManual.Core.Tests;

public class RegistryPolicyWriterTests
{
    private static AllowlistSite MakeSite(string domain)
    {
        AllowlistSite.TryCreate(domain, null, out var site, out _);
        return site!;
    }

    [Fact]
    public void Apply_WritesBlocklistWildcard()
    {
        var registry = new FakeWindowsRegistry();
        var writer = new RegistryPolicyWriter(registry);
        var config = new WizardConfiguration(
            new[] { BrowserTarget.Edge }, new[] { MakeSite("example.com") }, new AdvancedOptionsState());

        writer.Apply(config);

        Assert.Equal("*", registry.GetStringValue(@"SOFTWARE\Policies\Microsoft\Edge\URLBlocklist", "1"));
    }

    [Fact]
    public void Apply_WritesNumberedAllowlistEntries()
    {
        var registry = new FakeWindowsRegistry();
        var writer = new RegistryPolicyWriter(registry);
        var config = new WizardConfiguration(
            new[] { BrowserTarget.Edge },
            new[] { MakeSite("example.com"), MakeSite("www.example.com") },
            new AdvancedOptionsState());

        writer.Apply(config);

        Assert.Equal("example.com", registry.GetStringValue(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist", "1"));
        Assert.Equal("www.example.com", registry.GetStringValue(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist", "2"));
    }

    [Fact]
    public void Apply_ClearsStaleAllowlistEntries_WhenListShrinks()
    {
        var registry = new FakeWindowsRegistry();
        registry.SetStringValue(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist", "1", "old-one.com");
        registry.SetStringValue(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist", "2", "old-two.com");
        registry.SetStringValue(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist", "3", "old-three.com");
        var writer = new RegistryPolicyWriter(registry);
        var config = new WizardConfiguration(
            new[] { BrowserTarget.Edge }, new[] { MakeSite("example.com") }, new AdvancedOptionsState());

        writer.Apply(config);

        var names = registry.GetValueNames(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist");
        Assert.Single(names);
        Assert.Equal("example.com", registry.GetStringValue(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist", "1"));
    }

    [Fact]
    public void Apply_WritesEdgeIncognitoValueName_ForEdge()
    {
        var registry = new FakeWindowsRegistry();
        var writer = new RegistryPolicyWriter(registry);
        var config = new WizardConfiguration(
            new[] { BrowserTarget.Edge }, new[] { MakeSite("example.com") },
            new AdvancedOptionsState(DisableIncognito: true, DisableAccountSwitching: false, DisableDeveloperTools: false));

        writer.Apply(config);

        Assert.Equal(1, registry.GetDwordValue(@"SOFTWARE\Policies\Microsoft\Edge", "InPrivateModeAvailability"));
    }

    [Fact]
    public void Apply_WritesChromeIncognitoValueName_ForChrome()
    {
        var registry = new FakeWindowsRegistry();
        var writer = new RegistryPolicyWriter(registry);
        var config = new WizardConfiguration(
            new[] { BrowserTarget.Chrome }, new[] { MakeSite("example.com") },
            new AdvancedOptionsState(DisableIncognito: true, DisableAccountSwitching: false, DisableDeveloperTools: false));

        writer.Apply(config);

        Assert.Equal(1, registry.GetDwordValue(@"SOFTWARE\Policies\Google\Chrome", "IncognitoModeAvailability"));
    }

    [Fact]
    public void Apply_SkipsDisabledAdvancedOptionFlags()
    {
        var registry = new FakeWindowsRegistry();
        var writer = new RegistryPolicyWriter(registry);
        var config = new WizardConfiguration(
            new[] { BrowserTarget.Edge }, new[] { MakeSite("example.com") },
            new AdvancedOptionsState(DisableIncognito: false, DisableAccountSwitching: false, DisableDeveloperTools: false));

        writer.Apply(config);

        Assert.Null(registry.GetDwordValue(@"SOFTWARE\Policies\Microsoft\Edge", "InPrivateModeAvailability"));
        Assert.Null(registry.GetDwordValue(@"SOFTWARE\Policies\Microsoft\Edge", "BrowserSignin"));
        Assert.Null(registry.GetDwordValue(@"SOFTWARE\Policies\Microsoft\Edge", "DeveloperToolsAvailability"));
    }

    [Fact]
    public void Apply_ClearsPreviouslySetFlag_WhenAdvancedOptionTurnedOff()
    {
        var registry = new FakeWindowsRegistry();
        registry.SetDwordValue(@"SOFTWARE\Policies\Microsoft\Edge", "InPrivateModeAvailability", 1);
        var writer = new RegistryPolicyWriter(registry);
        var config = new WizardConfiguration(
            new[] { BrowserTarget.Edge }, new[] { MakeSite("example.com") },
            new AdvancedOptionsState(DisableIncognito: false, DisableAccountSwitching: false, DisableDeveloperTools: false));

        writer.Apply(config);

        Assert.Null(registry.GetDwordValue(@"SOFTWARE\Policies\Microsoft\Edge", "InPrivateModeAvailability"));
    }

    [Fact]
    public void Apply_WritesToEveryBrowserTarget()
    {
        var registry = new FakeWindowsRegistry();
        var writer = new RegistryPolicyWriter(registry);
        var config = new WizardConfiguration(
            new[] { BrowserTarget.Edge, BrowserTarget.Chrome }, new[] { MakeSite("example.com") },
            new AdvancedOptionsState());

        writer.Apply(config);

        Assert.Equal("*", registry.GetStringValue(@"SOFTWARE\Policies\Microsoft\Edge\URLBlocklist", "1"));
        Assert.Equal("*", registry.GetStringValue(@"SOFTWARE\Policies\Google\Chrome\URLBlocklist", "1"));
    }

    [Fact]
    public void RemoveAll_DeletesThePolicyKeyEntirely()
    {
        var registry = new FakeWindowsRegistry();
        var writer = new RegistryPolicyWriter(registry);
        var config = new WizardConfiguration(
            new[] { BrowserTarget.Edge }, new[] { MakeSite("example.com") }, new AdvancedOptionsState());
        writer.Apply(config);

        writer.RemoveAll(new[] { BrowserTarget.Edge });

        Assert.False(registry.SubKeyExists(@"SOFTWARE\Policies\Microsoft\Edge"));
        Assert.False(registry.SubKeyExists(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist"));
        Assert.False(registry.SubKeyExists(@"SOFTWARE\Policies\Microsoft\Edge\URLBlocklist"));
    }

    [Fact]
    public void RemoveAll_RemovesEveryRequestedTarget()
    {
        var registry = new FakeWindowsRegistry();
        var writer = new RegistryPolicyWriter(registry);
        var config = new WizardConfiguration(
            new[] { BrowserTarget.Edge, BrowserTarget.Chrome }, new[] { MakeSite("example.com") }, new AdvancedOptionsState());
        writer.Apply(config);

        writer.RemoveAll(new[] { BrowserTarget.Edge, BrowserTarget.Chrome });

        Assert.False(registry.SubKeyExists(@"SOFTWARE\Policies\Microsoft\Edge"));
        Assert.False(registry.SubKeyExists(@"SOFTWARE\Policies\Google\Chrome"));
    }

    [Fact]
    public void RemoveAll_DoesNotThrow_WhenTargetWasNeverConfigured()
    {
        var registry = new FakeWindowsRegistry();
        var writer = new RegistryPolicyWriter(registry);

        var exception = Record.Exception(() => writer.RemoveAll(new[] { BrowserTarget.Edge }));

        Assert.Null(exception);
    }

    [Fact]
    public void RemoveAll_DoesNotTouchUnrequestedTargets()
    {
        var registry = new FakeWindowsRegistry();
        var writer = new RegistryPolicyWriter(registry);
        var config = new WizardConfiguration(
            new[] { BrowserTarget.Edge, BrowserTarget.Chrome }, new[] { MakeSite("example.com") }, new AdvancedOptionsState());
        writer.Apply(config);

        writer.RemoveAll(new[] { BrowserTarget.Edge });

        Assert.False(registry.SubKeyExists(@"SOFTWARE\Policies\Microsoft\Edge"));
        Assert.True(registry.SubKeyExists(@"SOFTWARE\Policies\Google\Chrome"));
    }
}
