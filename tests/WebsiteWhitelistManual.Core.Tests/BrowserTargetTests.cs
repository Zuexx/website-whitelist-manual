using WebsiteWhitelistManual.Core.Models;
using Xunit;

namespace WebsiteWhitelistManual.Core.Tests;

public class BrowserTargetTests
{
    [Fact]
    public void Edge_HasEdgeRegistryPathAndIncognitoValueName()
    {
        var target = BrowserTarget.Edge;

        Assert.Equal(BrowserId.Edge, target.Id);
        Assert.Equal(@"SOFTWARE\Policies\Microsoft\Edge", target.RootPath);
        Assert.Equal("InPrivateModeAvailability", target.IncognitoValueName);
        Assert.Equal("Microsoft Edge", target.DisplayName);
    }

    [Fact]
    public void Chrome_HasChromeRegistryPathAndIncognitoValueName()
    {
        var target = BrowserTarget.Chrome;

        Assert.Equal(BrowserId.Chrome, target.Id);
        Assert.Equal(@"SOFTWARE\Policies\Google\Chrome", target.RootPath);
        Assert.Equal("IncognitoModeAvailability", target.IncognitoValueName);
        Assert.Equal("Google Chrome", target.DisplayName);
    }

    [Theory]
    [InlineData(BrowserId.Edge)]
    [InlineData(BrowserId.Chrome)]
    public void FromId_ReturnsMatchingTarget(BrowserId id)
    {
        var target = BrowserTarget.FromId(id);

        Assert.Equal(id, target.Id);
    }
}
