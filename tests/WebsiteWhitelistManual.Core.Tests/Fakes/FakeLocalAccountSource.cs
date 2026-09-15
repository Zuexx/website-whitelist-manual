using WebsiteWhitelistManual.Core.Abstractions;
using WebsiteWhitelistManual.Core.Models;

namespace WebsiteWhitelistManual.Core.Tests.Fakes;

public sealed class FakeLocalAccountSource : ILocalAccountSource
{
    private readonly IReadOnlyList<LocalAccountInfo> _accounts;

    public FakeLocalAccountSource(IReadOnlyList<LocalAccountInfo> accounts)
    {
        _accounts = accounts;
    }

    public IReadOnlyList<LocalAccountInfo> GetLocalAccounts() => _accounts;
}
