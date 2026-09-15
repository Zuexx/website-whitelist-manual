namespace WebsiteWhitelistManual.Api;

/// <summary>
/// Rejects every request that doesn't carry the X-Api-Token header matching
/// the token this process wrote to disk at startup (see Program.cs). This is
/// not a general auth system — it exists only to stop another unprivileged
/// local process from riding this elevated API's HKLM access over the
/// loopback port, which plain 127.0.0.1 binding alone does not prevent.
/// </summary>
public sealed class SharedSecretMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _expectedToken;

    public SharedSecretMiddleware(RequestDelegate next, string expectedToken)
    {
        _next = next;
        _expectedToken = expectedToken;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path == "/api/health")
        {
            // The health check has to be callable before the Electron main
            // process has read the token file back, so it stays open.
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue("X-Api-Token", out var provided) ||
            provided.ToString() != _expectedToken)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new Contracts.ApiErrorResponse("Missing or invalid X-Api-Token header."));
            return;
        }

        await _next(context);
    }
}
