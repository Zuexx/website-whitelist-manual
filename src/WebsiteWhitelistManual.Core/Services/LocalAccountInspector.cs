using WebsiteWhitelistManual.Core.Abstractions;
using WebsiteWhitelistManual.Core.Models;

namespace WebsiteWhitelistManual.Core.Services;

public sealed class LocalAccountInspector : ILocalAccountInspector
{
    private readonly ILocalAccountSource _source;

    public LocalAccountInspector(ILocalAccountSource source)
    {
        _source = source;
    }

    public IReadOnlyList<LocalAccountInfo> GetRelevantAccounts()
        => _source.GetLocalAccounts().Where(a => !a.IsBuiltIn).ToList();

    public IReadOnlyList<LocalAccountInfo> GetAllAccounts()
        => _source.GetLocalAccounts();
}
