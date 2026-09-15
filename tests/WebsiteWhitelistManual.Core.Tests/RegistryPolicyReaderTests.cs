using WebsiteWhitelistManual.Core.Models;
using WebsiteWhitelistManual.Core.Services;
using WebsiteWhitelistManual.Core.Tests.Fakes;
using Xunit;

namespace WebsiteWhitelistManual.Core.Tests;

public class RegistryPolicyReaderTests
{
    [Fact]
    public void ReadSnapshot_ReportsPolicyKeyDoesNotExist_WhenNeverCreated()
    {
        var registry = new FakeWindowsRegistry();
        var reader = new RegistryPolicyReader(registry);

        var snapshot = reader.ReadSnapshot(new[] { BrowserTarget.Edge });

        var edge = Assert.Single(snapshot.Browsers);
        Assert.Equal(BrowserId.Edge, edge.BrowserId);
        Assert.False(edge.PolicyKeyExists);
        Assert.Empty(edge.BlockedUrls);
        Assert.Empty(edge.AllowedUrls);
        Assert.Null(edge.IncognitoDisabled);
        Assert.Null(edge.BrowserSigninDisabled);
        Assert.Null(edge.DeveloperToolsDisabled);
    }

    [Fact]
    public void ReadSnapshot_ReadsExistingBlocklistAndAllowlistEntries()
    {
        var registry = new FakeWindowsRegistry();
        registry.SetStringValue(@"SOFTWARE\Policies\Microsoft\Edge\URLBlocklist", "1", "*");
        registry.SetStringValue(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist", "1", "example.com");
        registry.SetStringValue(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist", "2", "www.example.com");
        var reader = new RegistryPolicyReader(registry);

        var snapshot = reader.ReadSnapshot(new[] { BrowserTarget.Edge });

        var edge = Assert.Single(snapshot.Browsers);
        Assert.True(edge.PolicyKeyExists);
        Assert.Equal(new[] { "*" }, edge.BlockedUrls);
        Assert.Equal(new[] { "example.com", "www.example.com" }, edge.AllowedUrls);
    }

    [Fact]
    public void ReadSnapshot_ReadsDwordFlags_ForEdge()
    {
        var registry = new FakeWindowsRegistry();
        registry.EnsureSubKeyExists(@"SOFTWARE\Policies\Microsoft\Edge");
        registry.SetDwordValue(@"SOFTWARE\Policies\Microsoft\Edge", "InPrivateModeAvailability", 1);
        registry.SetDwordValue(@"SOFTWARE\Policies\Microsoft\Edge", "BrowserSignin", 0);
        registry.SetDwordValue(@"SOFTWARE\Policies\Microsoft\Edge", "DeveloperToolsAvailability", 2);
        var reader = new RegistryPolicyReader(registry);

        var snapshot = reader.ReadSnapshot(new[] { BrowserTarget.Edge });

        var edge = Assert.Single(snapshot.Browsers);
        Assert.True(edge.IncognitoDisabled);
        Assert.True(edge.BrowserSigninDisabled);
        Assert.True(edge.DeveloperToolsDisabled);
    }

    [Fact]
    public void ReadSnapshot_UsesChromeSpecificIncognitoValueName()
    {
        var registry = new FakeWindowsRegistry();
        registry.EnsureSubKeyExists(@"SOFTWARE\Policies\Google\Chrome");
        registry.SetDwordValue(@"SOFTWARE\Policies\Google\Chrome", "IncognitoModeAvailability", 1);
        var reader = new RegistryPolicyReader(registry);

        var snapshot = reader.ReadSnapshot(new[] { BrowserTarget.Chrome });

        var chrome = Assert.Single(snapshot.Browsers);
        Assert.True(chrome.IncognitoDisabled);
    }

    [Fact]
    public void ReadSnapshot_ReadsMultipleTargetsIndependently()
    {
        var registry = new FakeWindowsRegistry();
        registry.EnsureSubKeyExists(@"SOFTWARE\Policies\Microsoft\Edge");
        var reader = new RegistryPolicyReader(registry);

        var snapshot = reader.ReadSnapshot(new[] { BrowserTarget.Edge, BrowserTarget.Chrome });

        Assert.Equal(2, snapshot.Browsers.Count);
        Assert.True(snapshot.Browsers.Single(b => b.BrowserId == BrowserId.Edge).PolicyKeyExists);
        Assert.False(snapshot.Browsers.Single(b => b.BrowserId == BrowserId.Chrome).PolicyKeyExists);
    }
}
