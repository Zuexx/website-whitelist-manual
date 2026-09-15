using WebsiteWhitelistManual.Core.Tests.Fakes;
using Xunit;

namespace WebsiteWhitelistManual.Core.Tests.Fakes;

public class FakeWindowsRegistryTests
{
    [Fact]
    public void SubKeyExists_ReturnsFalse_ForKeyNeverCreated()
    {
        var registry = new FakeWindowsRegistry();

        Assert.False(registry.SubKeyExists(@"SOFTWARE\Policies\Microsoft\Edge"));
    }

    [Fact]
    public void EnsureSubKeyExists_MakesSubKeyExistsTrue()
    {
        var registry = new FakeWindowsRegistry();

        registry.EnsureSubKeyExists(@"SOFTWARE\Policies\Microsoft\Edge");

        Assert.True(registry.SubKeyExists(@"SOFTWARE\Policies\Microsoft\Edge"));
    }

    [Fact]
    public void SetAndGetStringValue_RoundTrips()
    {
        var registry = new FakeWindowsRegistry();
        registry.EnsureSubKeyExists(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist");

        registry.SetStringValue(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist", "1", "example.com");

        Assert.Equal("example.com", registry.GetStringValue(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist", "1"));
    }

    [Fact]
    public void GetStringValue_ReturnsNull_WhenValueMissing()
    {
        var registry = new FakeWindowsRegistry();
        registry.EnsureSubKeyExists(@"SOFTWARE\Policies\Microsoft\Edge");

        Assert.Null(registry.GetStringValue(@"SOFTWARE\Policies\Microsoft\Edge", "NoSuchValue"));
    }

    [Fact]
    public void SetAndGetDwordValue_RoundTrips()
    {
        var registry = new FakeWindowsRegistry();
        registry.EnsureSubKeyExists(@"SOFTWARE\Policies\Microsoft\Edge");

        registry.SetDwordValue(@"SOFTWARE\Policies\Microsoft\Edge", "BrowserSignin", 0);

        Assert.Equal(0, registry.GetDwordValue(@"SOFTWARE\Policies\Microsoft\Edge", "BrowserSignin"));
    }

    [Fact]
    public void GetDwordValue_ReturnsNull_WhenValueMissing()
    {
        var registry = new FakeWindowsRegistry();
        registry.EnsureSubKeyExists(@"SOFTWARE\Policies\Microsoft\Edge");

        Assert.Null(registry.GetDwordValue(@"SOFTWARE\Policies\Microsoft\Edge", "NoSuchValue"));
    }

    [Fact]
    public void GetValueNames_ReturnsAllNamesUnderSubKey()
    {
        var registry = new FakeWindowsRegistry();
        registry.EnsureSubKeyExists(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist");
        registry.SetStringValue(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist", "1", "example.com");
        registry.SetStringValue(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist", "2", "www.example.com");

        var names = registry.GetValueNames(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist");

        Assert.Equal(new[] { "1", "2" }, names);
    }

    [Fact]
    public void GetValueNames_ReturnsEmpty_ForSubKeyThatDoesNotExist()
    {
        var registry = new FakeWindowsRegistry();

        var names = registry.GetValueNames(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist");

        Assert.Empty(names);
    }

    [Fact]
    public void DeleteValue_RemovesValue()
    {
        var registry = new FakeWindowsRegistry();
        registry.EnsureSubKeyExists(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist");
        registry.SetStringValue(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist", "1", "example.com");

        registry.DeleteValue(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist", "1");

        Assert.Null(registry.GetStringValue(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist", "1"));
        Assert.Empty(registry.GetValueNames(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist"));
    }

    [Fact]
    public void DeleteValue_OnMissingValue_IsNoOp()
    {
        var registry = new FakeWindowsRegistry();
        registry.EnsureSubKeyExists(@"SOFTWARE\Policies\Microsoft\Edge");

        var exception = Record.Exception(() =>
            registry.DeleteValue(@"SOFTWARE\Policies\Microsoft\Edge", "NoSuchValue"));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureSubKeyExists_AlsoCreatesAncestorKeys()
    {
        var registry = new FakeWindowsRegistry();

        registry.EnsureSubKeyExists(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist");

        Assert.True(registry.SubKeyExists(@"SOFTWARE"));
        Assert.True(registry.SubKeyExists(@"SOFTWARE\Policies"));
        Assert.True(registry.SubKeyExists(@"SOFTWARE\Policies\Microsoft"));
        Assert.True(registry.SubKeyExists(@"SOFTWARE\Policies\Microsoft\Edge"));
        Assert.True(registry.SubKeyExists(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist"));
    }

    [Fact]
    public void ValueNames_AreCaseInsensitive()
    {
        var registry = new FakeWindowsRegistry();
        registry.EnsureSubKeyExists(@"SOFTWARE\Policies\Microsoft\Edge");

        registry.SetStringValue(@"SOFTWARE\Policies\Microsoft\Edge", "PolicyName", "value1");

        Assert.Equal("value1", registry.GetStringValue(@"SOFTWARE\Policies\Microsoft\Edge", "policyname"));
        Assert.Equal("value1", registry.GetStringValue(@"SOFTWARE\Policies\Microsoft\Edge", "POLICYNAME"));
        Assert.Equal("value1", registry.GetStringValue(@"SOFTWARE\Policies\Microsoft\Edge", "PolicyName"));
    }
}
