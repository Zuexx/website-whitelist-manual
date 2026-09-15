using WebsiteWhitelistManual.Core.Models;
using WebsiteWhitelistManual.Core.Services;
using WebsiteWhitelistManual.Core.Tests.Fakes;
using Xunit;

namespace WebsiteWhitelistManual.Core.Tests;

public class LocalAccountInspectorTests
{
    [Fact]
    public void GetRelevantAccounts_ExcludesBuiltInAccounts()
    {
        var source = new FakeLocalAccountSource(new[]
        {
            new LocalAccountInfo("Administrator", IsAdministrator: true, IsBuiltIn: true),
            new LocalAccountInfo("Guest", IsAdministrator: false, IsBuiltIn: true),
            new LocalAccountInfo("王小明", IsAdministrator: false, IsBuiltIn: false),
        });
        var inspector = new LocalAccountInspector(source);

        var relevant = inspector.GetRelevantAccounts();

        var account = Assert.Single(relevant);
        Assert.Equal("王小明", account.AccountName);
    }

    [Fact]
    public void GetRelevantAccounts_KeepsBothStandardAndAdministratorNonBuiltInAccounts()
    {
        var source = new FakeLocalAccountSource(new[]
        {
            new LocalAccountInfo("家長", IsAdministrator: true, IsBuiltIn: false),
            new LocalAccountInfo("王小明", IsAdministrator: false, IsBuiltIn: false),
        });
        var inspector = new LocalAccountInspector(source);

        var relevant = inspector.GetRelevantAccounts();

        Assert.Equal(2, relevant.Count);
        Assert.Contains(relevant, a => a.AccountName == "家長" && a.IsAdministrator);
        Assert.Contains(relevant, a => a.AccountName == "王小明" && !a.IsAdministrator);
    }

    [Fact]
    public void GetRelevantAccounts_ReturnsEmpty_WhenOnlyBuiltInAccountsExist()
    {
        var source = new FakeLocalAccountSource(new[]
        {
            new LocalAccountInfo("Administrator", IsAdministrator: true, IsBuiltIn: true),
        });
        var inspector = new LocalAccountInspector(source);

        Assert.Empty(inspector.GetRelevantAccounts());
    }
}
