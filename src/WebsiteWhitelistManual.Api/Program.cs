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
    // (http://localhost:5173); in production a packaged Electron app's
    // renderer runs from a custom scheme/local static server (see Task 3) —
    // both are added here rather than using AllowAnyOrigin, since this API
    // also carries a shared-secret header and there's no reason to loosen
    // the origin check as well.
    options.AddDefaultPolicy(policy => policy
        .WithOrigins("http://localhost:5173", "http://127.0.0.1:5293")
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

app.Run("http://127.0.0.1:5292");
