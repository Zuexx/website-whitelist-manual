using WebsiteWhitelistManual.Core.Models;
using Xunit;

namespace WebsiteWhitelistManual.Core.Tests;

public class AllowlistSiteTests
{
    [Fact]
    public void TryCreate_AcceptsPlainDomain()
    {
        var created = AllowlistSite.TryCreate("example.com", null, out var site, out var error);

        Assert.True(created);
        Assert.NotNull(site);
        Assert.Equal("example.com", site!.Domain);
        Assert.Null(site.CategoryLabel);
        Assert.Null(error);
    }

    [Fact]
    public void TryCreate_TrimsWhitespaceAndCategoryLabel()
    {
        var created = AllowlistSite.TryCreate("  example.com  ", "  自訂測驗入口  ", out var site, out _);

        Assert.True(created);
        Assert.Equal("example.com", site!.Domain);
        Assert.Equal("自訂測驗入口", site.CategoryLabel);
    }

    [Fact]
    public void TryCreate_TreatsBlankCategoryLabelAsNull()
    {
        AllowlistSite.TryCreate("example.com", "   ", out var site, out _);

        Assert.Null(site!.CategoryLabel);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryCreate_RejectsBlankInput(string? input)
    {
        var created = AllowlistSite.TryCreate(input, null, out var site, out var error);

        Assert.False(created);
        Assert.Null(site);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryCreate_RejectsInputContainingSpace()
    {
        var created = AllowlistSite.TryCreate("example .com", null, out var site, out var error);

        Assert.False(created);
        Assert.Null(site);
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("http://example.com")]
    [InlineData("https://example.com")]
    public void TryCreate_RejectsInputWithScheme(string input)
    {
        var created = AllowlistSite.TryCreate(input, null, out var site, out var error);

        Assert.False(created);
        Assert.Null(site);
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("example")]
    [InlineData(".example.com")]
    [InlineData("example.com.")]
    public void TryCreate_RejectsInputWithoutValidDomainShape(string input)
    {
        var created = AllowlistSite.TryCreate(input, null, out var site, out var error);

        Assert.False(created);
        Assert.Null(site);
        Assert.NotNull(error);
    }
}
