using WebsiteWhitelistManual.Core.Models;

namespace WebsiteWhitelistManual.Core.Services;

public interface ILocalAccountInspector
{
    IReadOnlyList<LocalAccountInfo> GetRelevantAccounts();

    /// <summary>
    /// Every local account on the machine, including built-ins — used by the
    /// Dashboard so a parent can visually confirm which entry is their
    /// child's account among everything present, built-in accounts included.
    /// </summary>
    IReadOnlyList<LocalAccountInfo> GetAllAccounts();
}
