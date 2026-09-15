using WebsiteWhitelistManual.Core.Models;
using WebsiteWhitelistManual.Core.Services;
using WebsiteWhitelistManual.Core.Tests.Fakes;
using Xunit;

namespace WebsiteWhitelistManual.Core.Tests;

/// <summary>
/// Locks the cross-task contract between RegistryPolicyWriter and
/// RegistryPolicyReader: whatever the writer applies from a
/// WizardConfiguration must be exactly what the reader reports back.
/// </summary>
public class WriterReaderRoundTripTests
{
    private static AllowlistSite MakeSite(string domain)
    {
        AllowlistSite.TryCreate(domain, null, out var site, out _);
        return site!;
    }

    [Fact]
    public void ApplyThenRead_ProducesSnapshotConsistentWithConfiguration()
    {
        var registry = new FakeWindowsRegistry();
        var writer = new RegistryPolicyWriter(registry);
        var reader = new RegistryPolicyReader(registry);

        var config = new WizardConfiguration(
            new[] { BrowserTarget.Edge, BrowserTarget.Chrome },
            new[] { MakeSite("example.com"), MakeSite("www.example.com"), MakeSite("docs.example.com") },
            new AdvancedOptionsState(DisableIncognito: true, DisableAccountSwitching: false, DisableDeveloperTools: true));

        writer.Apply(config);
        var snapshot = reader.ReadSnapshot(config.BrowserTargets);

        Assert.Equal(2, snapshot.Browsers.Count);

        var edge = snapshot.Browsers.Single(b => b.BrowserId == BrowserId.Edge);
        var chrome = snapshot.Browsers.Single(b => b.BrowserId == BrowserId.Chrome);

        foreach (var browser in new[] { edge, chrome })
        {
            Assert.True(browser.PolicyKeyExists);
            Assert.Equal(new[] { "example.com", "www.example.com", "docs.example.com" }, browser.AllowedUrls);
            Assert.Equal(new[] { "*" }, browser.BlockedUrls);
            Assert.True(browser.IncognitoDisabled);
            // DisableAccountSwitching was false, so the writer now deletes
            // BrowserSignin rather than writing 0 (Finding 3's fix) — the
            // reader correctly reports this as "unknown" (null), not false.
            Assert.Null(browser.BrowserSigninDisabled);
            Assert.True(browser.DeveloperToolsDisabled);
        }
    }

    [Fact]
    public void ApplyTwice_ClearsIncognitoFlag_WhenTurnedOffOnSecondApply()
    {
        var registry = new FakeWindowsRegistry();
        var writer = new RegistryPolicyWriter(registry);
        var reader = new RegistryPolicyReader(registry);

        var sites = new[] { MakeSite("example.com") };
        var targets = new[] { BrowserTarget.Edge };

        var firstConfig = new WizardConfiguration(
            targets, sites,
            new AdvancedOptionsState(DisableIncognito: true, DisableAccountSwitching: true, DisableDeveloperTools: true));
        writer.Apply(firstConfig);

        var secondConfig = new WizardConfiguration(
            targets, sites,
            new AdvancedOptionsState(DisableIncognito: false, DisableAccountSwitching: true, DisableDeveloperTools: true));
        writer.Apply(secondConfig);

        var snapshot = reader.ReadSnapshot(targets);
        var edge = Assert.Single(snapshot.Browsers);

        Assert.Null(edge.IncognitoDisabled);
    }
}
