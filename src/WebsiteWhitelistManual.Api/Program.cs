using WebsiteWhitelistManual.Api;
using WebsiteWhitelistManual.Api.Endpoints;
using WebsiteWhitelistManual.Api.Services;
using WebsiteWhitelistManual.Core.Abstractions;
using WebsiteWhitelistManual.Core.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IWindowsRegistry, WindowsRegistryAdapter>();
builder.Services.AddSingleton<IProcessRunner, WindowsProcessRunner>();
builder.Services.AddSingleton<ILocalAccountSource, WindowsLocalAccountSource>();
builder.Services.AddSingleton<IRegistryPolicyReader, RegistryPolicyReader>();
builder.Services.AddSingleton<IRegistryPolicyWriter, RegistryPolicyWriter>();
builder.Services.AddSingleton<IRegistryBackupService, RegistryBackupService>();
builder.Services.AddSingleton<ILocalAccountInspector, LocalAccountInspector>();

builder.Services.AddCors(options =>
{
    // The Electron renderer's origin in development is Vite's dev server
    // (http://localhost:5173, fixed). In production, main.cts serves the
    // built static files over loopback HTTP on a port chosen at random by
    // server.listen(0, ...) every launch (needed because Chromium refuses
    // module scripts loaded from a file:// origin) — so no fixed port can
    // be whitelisted here. Matching any 127.0.0.1/localhost origin on any
    // port is still safe: this API isn't reachable from the network (see
    // the loopback-only bind below), and every request additionally still
    // requires the X-Api-Token shared secret, which is the real boundary.
    options.AddDefaultPolicy(policy => policy
        .SetIsOriginAllowed(origin =>
        {
            var uri = new Uri(origin);
            return uri.IsLoopback;
        })
        .AllowAnyHeader()
        .AllowAnyMethod());
});

var app = builder.Build();

var apiToken = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
var tokenDirectory = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "WebsiteWhitelistManual");
Directory.CreateDirectory(tokenDirectory);
var tokenFilePath = Path.Combine(tokenDirectory, "api-token.txt");
await File.WriteAllTextAsync(tokenFilePath, apiToken);

app.UseCors();
app.UseMiddleware<SharedSecretMiddleware>(apiToken);

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapPolicyEndpoints();
app.MapAccountEndpoints();

// The Electron main process runs unelevated and cannot terminate this
// (elevated) process itself — Windows' Mandatory Integrity Control blocks
// a medium-integrity process from killing a high-integrity one even when
// owned by the same user. main.cts calls this on app quit instead so the
// API shuts itself down rather than becoming an orphaned background
// process.
app.MapPost("/api/shutdown", (IHostApplicationLifetime lifetime) =>
{
    _ = Task.Run(async () =>
    {
        await Task.Delay(200);
        lifetime.StopApplication();
    });
    return Results.Ok();
});

app.Run("http://127.0.0.1:5292");
