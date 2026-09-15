using WebsiteWhitelistManual.Api.Contracts;
using WebsiteWhitelistManual.Core.Services;

namespace WebsiteWhitelistManual.Api.Endpoints;

public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
        app.MapGet("/api/accounts", (string? scope, ILocalAccountInspector inspector) =>
        {
            var accounts = string.Equals(scope, "relevant", StringComparison.OrdinalIgnoreCase)
                ? inspector.GetRelevantAccounts()
                : inspector.GetAllAccounts();

            return Results.Ok(accounts.Select(LocalAccountResponse.FromDomain).ToList());
        });
    }
}
