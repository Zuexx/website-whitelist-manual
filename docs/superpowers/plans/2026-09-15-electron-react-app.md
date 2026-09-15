# Electron + React + .NET API Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the deleted WPF UI with a React SPA packaged as a single Electron desktop app, aiming for close visual fidelity to the Stitch mockups (`stitch_website_allowlist_desktop_app/`). The SPA talks over local HTTP to a new, thin ASP.NET Core minimal API (`WebsiteWhitelistManual.Api`) that is the only thing running elevated and the only thing touching `HKLM` — it does this entirely by reusing the already-built, already-tested `WebsiteWhitelistManual.Core` library (59 passing xUnit tests, untouched by this plan) plus three Windows-real adapter classes recovered verbatim from git history (see Task 1).

**Architecture:**

```
Electron app (single .exe the parent double-clicks)
├── main process (Node.js, not elevated)
│   ├── spawns WebsiteWhitelistManual.Api.exe as a child process on app ready
│   │   → that exe carries its own app.manifest (requireAdministrator),
│   │     so Windows elevates ONLY the child process via a UAC prompt
│   │     the instant Electron spawns it — the Electron shell itself
│   │     never needs elevation. See Task 3's elevation note for why
│   │     this manifest-on-child-process approach is the right (and
│   │     only clean) mechanism here, and what to watch for.
│   │   → child process listens on http://127.0.0.1:5292 (loopback only)
│   ├── kills that child process on app quit (all quit paths: normal
│   │     quit, window-all-closed, before-quit — see Task 3)
│   └── loads the renderer: Vite dev server URL in development,
│         built static files via a local static file server in production
│         (not raw `file://` — see Task 2's routing note for why)
└── renderer process (Chromium, React SPA)
      → fetch() calls to http://127.0.0.1:5292/api/... for every
        registry read/write; the renderer itself never touches
        the filesystem or registry directly
```

`WebsiteWhitelistManual.Api` (ASP.NET Core minimal API, `net10.0-windows`) → `WebsiteWhitelistManual.Core` (`net10.0`, unchanged) → `IWindowsRegistry`/`IProcessRunner`/`ILocalAccountSource` implemented by `WindowsRegistryAdapter`/`WindowsProcessRunner`/`WindowsLocalAccountSource` (recovered from git history, Task 1) → real `HKLM` access via `Microsoft.Win32.Registry`, `System.Diagnostics.Process`, and `System.DirectoryServices.AccountManagement`.

**Tech Stack:** .NET 10 SDK (API: `net10.0-windows`, `OutputType=Exe`, ASP.NET Core minimal API — no MVC, no Razor), React 18 + TypeScript + Vite (frontend), React Router (client-side routing across the 6 pages), plain CSS with custom properties for the Guardian Clear design tokens (no CSS framework — the token set is small and a framework would fight the exact Stitch values), Electron 33+ with `electron-builder` for packaging.

**Spec:** `docs/superpowers/specs/2026-09-15-website-whitelist-wpf-app-design.md` — every registry key/value, error-handling rule, and scope exclusion in that document applies verbatim; only the UI technology changes. Superseded (WPF-specific) plans: `2026-09-15-wpf-app-shell.md`, `2026-09-15-wpf-pages-and-navigation.md` — kept as historical record, not live instructions.

## Global Constraints

- **No fabricated data, anywhere** (carried forward verbatim from the WPF plan — the underlying reason is a `Core`/Windows-API limitation, not a UI-technology one, so it applies exactly the same to React):
  1. **Pure fiction with no real substitute** (今日已阻絕 N 次, 過去 7 天防護成效折線圖): omit entirely. Chromium's `URLBlocklist` policy has no readable block-count log.
  2. **Fiction with a real substitute** (Step 5's "已抽樣測試 HTTP 200" / "已導向封鎖頁"): replace with a registry-diff-based check — re-read the policy snapshot after `Apply()` and compare each expected key/value against what was actually read back. Label these rows exactly "機碼已寫入並核對" (key written and confirmed), never "網站已測試可開啟" — the API never makes an HTTP request to any allowlisted or blocked domain.
  3. **Real action, manual confirmation** (Step 5's "開啟受控瀏覽器" button): `window.open()` (renderer-side, via Electron's default external-link handling — see Task 8) against one allowlisted URL, for the parent to eyeball. No polling, no result captured, no checkmark tied to its outcome.
  4. **Step 2's category-percentage breakdown is the one Stitch stat that stays fully real**: computed client-side from the `AllowlistSite.CategoryLabel` values the parent has actually entered in the current in-memory wizard state — not invented, just make sure it recomputes from live state.
- **Never use `BrowserGuestModeEnabled`.** Real mechanism for "disable account switching" is `PolicyKeys.BrowserSigninValueName` (`BrowserSignin`) = `0`, already implemented in `RegistryPolicyWriter`/`RegistryPolicyReader`. Every "停用帳號切換" UI label binds to `AdvancedOptionsState.DisableAccountSwitching`.
- **`DeveloperToolsAvailability` = `2` disables dev tools** (per `PolicyKeys.DeveloperToolsAvailabilityValueName`) — already correct in `Core`, do not reinterpret.
- **Firefox is out of scope for v1.** No Firefox/other-browser card, toggle, or API field anywhere.
- **`WebsiteWhitelistManual.Core` is not modified by this plan.** Every one of its 59 tests must still pass unchanged after every task (`dotnet test tests/WebsiteWhitelistManual.Core.Tests`). If a task seems to need a `Core` change, stop and reconsider the API layer instead — `Core`'s job is already correctly scoped and reviewed.
- **The API is the only elevated, only registry-touching process.** The Electron main process and the React renderer never call `Microsoft.Win32.Registry`, never shell out to `reg.exe`, and never read `HKLM` themselves. All of that goes through HTTP calls to the API.
- **The API binds to loopback only** (`127.0.0.1`, never `0.0.0.0`) and is not reachable from the network. See Task 1's networking note for the additional shared-secret header used to stop *other local processes on the same machine* from hitting the elevated API while it's running (loopback binding alone does not stop another local process from connecting to a loopback port).
- **Registry access from the API must never block a request thread indefinitely.** `IRegistryBackupService.Backup` shells out to `reg.exe` via `IProcessRunner`, which has no timeout at the `Core` level (documented, deliberate — see the WPF plan's "Known follow-ups" section). The API's backup endpoint wraps the call in `Task.Run(...)` raced against a 10-second `Task.Delay` via `Task.WhenAny`, exactly as the WPF plan's Step4 ViewModel did — same reasoning, same timeout value, just relocated from a ViewModel method to an endpoint handler.
- **Every page's data comes from the API on load/refresh, never from client-side cached state that could drift from the real registry** — mirrors the WPF spec's Dashboard rule ("每次進入 Dashboard 即時讀登錄檔實際值，不另存本地設定檔快取").
- **The wizard's cross-step state (`WizardConfiguration`-equivalent: chosen browsers, allowlist sites, advanced options)** lives in a single React Context provider at the app root — analogous to the WPF plan's `WizardConfigurationStore` singleton — so it survives navigating between the 5 step routes without being persisted to disk or re-fetched from the API (the API has no session/state endpoint for this; it is pure request/response over `WizardConfiguration`-shaped JSON only at Step 4's apply time).
- Run `dotnet build`/`npm run build` (as appropriate to the task) at the end of every task; final manual Windows verification (UAC prompt, actual registry writes, Electron packaging) is the user's job per Task 9's handoff document — nothing in this plan can be verified end-to-end from a non-Windows or non-admin environment.

---

### Task 1: `WebsiteWhitelistManual.Api` — scaffold, recovered Windows adapters, DI, elevation manifest

**Files:**
- Create: `src/WebsiteWhitelistManual.Api/WebsiteWhitelistManual.Api.csproj`
- Create: `src/WebsiteWhitelistManual.Api/app.manifest`
- Create: `src/WebsiteWhitelistManual.Api/Program.cs`
- Create: `src/WebsiteWhitelistManual.Api/Services/WindowsRegistryAdapter.cs`
- Create: `src/WebsiteWhitelistManual.Api/Services/WindowsProcessRunner.cs`
- Create: `src/WebsiteWhitelistManual.Api/Services/WindowsLocalAccountSource.cs`
- Create: `src/WebsiteWhitelistManual.Api/Endpoints/PolicyEndpoints.cs`
- Create: `src/WebsiteWhitelistManual.Api/Endpoints/AccountEndpoints.cs`
- Create: `src/WebsiteWhitelistManual.Api/Contracts/ApiModels.cs`
- Create: `src/WebsiteWhitelistManual.Api/SharedSecretMiddleware.cs`
- Modify: `WebsiteWhitelistManual.sln`

**Interfaces:**
- Consumes: `WebsiteWhitelistManual.Core`'s `IRegistryPolicyReader`, `IRegistryPolicyWriter`, `IRegistryBackupService`, `ILocalAccountInspector`, and every model in `WebsiteWhitelistManual.Core.Models` (all read in full during plan-drafting — signatures below are exact, not guessed).
- Produces: a runnable `WebsiteWhitelistManual.Api.exe` exposing `GET /api/policy/snapshot`, `GET /api/accounts?scope=all|relevant`, `POST /api/policy/apply`, `GET /api/health`.

**Networking/auth note (read before Step 1):** The API binds to `http://127.0.0.1:5292` only — never `0.0.0.0`, never any other interface — via `WebApplication.Run("http://127.0.0.1:5292")`. Loopback binding stops remote machines from reaching it, but **does not** stop another unprivileged process already running on the same Windows machine from also connecting to `127.0.0.1:5292` and, e.g., silently triggering a registry write while riding on this app's elevation. To close that gap without over-engineering a full auth system: on startup the API generates a random 32-byte token, writes it to a file only the current user can read (`%LOCALAPPDATA%\WebsiteWhitelistManual\api-token.txt`, via `File.WriteAllText` — inherits the ACL of `LocalApplicationData`, which is already user-scoped), and requires every request to carry that token in an `X-Api-Token` header (enforced by `SharedSecretMiddleware`, Step 6 below). Electron's main process reads that same file right after spawning the child process and forwards the token to the renderer via a preload-script-exposed IPC call (Task 3) — the renderer never reads the file itself (renderers should not have raw filesystem access). This is "good enough for a loopback parental-control tool," not a general-purpose security boundary — do not over-build it.

- [ ] **Step 1: Scaffold the API project**

```bash
dotnet new web -n WebsiteWhitelistManual.Api -o src/WebsiteWhitelistManual.Api
```

- [ ] **Step 2: Replace the generated `.csproj`**

```xml
<!-- src/WebsiteWhitelistManual.Api/WebsiteWhitelistManual.Api.csproj -->
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <EnableWindowsTargeting>true</EnableWindowsTargeting>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <RootNamespace>WebsiteWhitelistManual.Api</RootNamespace>
    <InvariantGlobalization>true</InvariantGlobalization>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\WebsiteWhitelistManual.Core\WebsiteWhitelistManual.Core.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="System.DirectoryServices.AccountManagement" Version="10.0.12" />
  </ItemGroup>

</Project>
```

`Microsoft.NET.Sdk.Web` (not the plain SDK) is required for `WebApplication`/minimal API hosting. `net10.0-windows` (not plain `net10.0`) is required because this project references `System.DirectoryServices.AccountManagement` and `Microsoft.Win32.Registry` directly, same reasoning as the old WPF App project.

- [ ] **Step 3: Recover `app.manifest` verbatim from git history**

This exact file existed at `src/WebsiteWhitelistManual.App/app.manifest` before the WPF project was deleted (commit `003204b`). Recover it byte-for-byte rather than retyping:

```bash
git show 003204b^:src/WebsiteWhitelistManual.App/app.manifest > src/WebsiteWhitelistManual.Api/app.manifest
```

Confirm the recovered file's `requestedExecutionLevel` still reads `level="requireAdministrator" uiAccess="false"` — if the file doesn't exist at that path in that commit (e.g. commit hash changed after a rebase), fall back to:

```bash
git log --all --diff-filter=D --summary -- '*/app.manifest'
```

to find the actual deleting commit, then substitute its hash (with `^` appended) in the `git show` command above.

- [ ] **Step 4: Recover the three Windows-real adapters verbatim from git history**

Same recovery approach — these three files are unchanged from the deleted WPF App project except their `namespace` line, updated to this project's namespace:

```bash
git show 003204b^:src/WebsiteWhitelistManual.App/Services/WindowsRegistryAdapter.cs > src/WebsiteWhitelistManual.Api/Services/WindowsRegistryAdapter.cs
git show 003204b^:src/WebsiteWhitelistManual.App/Services/WindowsProcessRunner.cs > src/WebsiteWhitelistManual.Api/Services/WindowsProcessRunner.cs
git show 003204b^:src/WebsiteWhitelistManual.App/Services/WindowsLocalAccountSource.cs > src/WebsiteWhitelistManual.Api/Services/WindowsLocalAccountSource.cs
```

Then in all three recovered files, change:

```csharp
namespace WebsiteWhitelistManual.App.Services;
```

to:

```csharp
namespace WebsiteWhitelistManual.Api.Services;
```

That is the *only* edit these three files need. For reference, here is what each one does (already reviewed/hardened in the original WPF plan — do not simplify or "improve" these during recovery):

- **`WindowsRegistryAdapter`**: thin `Microsoft.Win32.Registry.LocalMachine`-backed implementation of `IWindowsRegistry`. `Get*Value` methods return `null` on type mismatch (`as string`/`as int?`) rather than throwing, matching `Core`'s fake's documented semantics.
- **`WindowsProcessRunner`**: `System.Diagnostics.Process`-backed `IProcessRunner`, using `ProcessStartInfo.ArgumentList` (never manually-quoted strings). Resolves a bare filename like `"reg.exe"` against `Environment.SystemDirectory` explicitly before starting it — because this process runs elevated, letting `Process.Start`'s default search order check the app's own directory first would let a standard user plant a malicious `reg.exe` next to the exe and get it executed as Administrator.
- **`WindowsLocalAccountSource`**: `System.DirectoryServices.AccountManagement`-backed `ILocalAccountSource`. Looks up the built-in Administrators group by its well-known SID (`S-1-5-32-544`), not the localized display name "Administrators" — the name is only valid on English-locale Windows and would silently misclassify every account as non-administrator on any other UI language.

- [ ] **Step 5: Define the HTTP contract types**

```csharp
// src/WebsiteWhitelistManual.Api/Contracts/ApiModels.cs
using WebsiteWhitelistManual.Core.Models;

namespace WebsiteWhitelistManual.Api.Contracts;

// ----- Requests -----

public sealed record AllowlistSiteRequest(string Domain, string? CategoryLabel);

public sealed record AdvancedOptionsRequest(
    bool DisableIncognito,
    bool DisableAccountSwitching,
    bool DisableDeveloperTools);

public sealed record ApplyPolicyRequest(
    IReadOnlyList<string> BrowserIds, // "Edge" and/or "Chrome", matching BrowserId enum names
    IReadOnlyList<AllowlistSiteRequest> AllowlistSites,
    AdvancedOptionsRequest AdvancedOptions);

// ----- Responses -----

public sealed record BrowserPolicySnapshotResponse(
    string BrowserId,
    bool PolicyKeyExists,
    IReadOnlyList<string> BlockedUrls,
    IReadOnlyList<string> AllowedUrls,
    bool? IncognitoDisabled,
    bool? BrowserSigninDisabled,
    bool? DeveloperToolsDisabled)
{
    public static BrowserPolicySnapshotResponse FromDomain(BrowserPolicySnapshot snapshot) => new(
        snapshot.BrowserId.ToString(),
        snapshot.PolicyKeyExists,
        snapshot.BlockedUrls,
        snapshot.AllowedUrls,
        snapshot.IncognitoDisabled,
        snapshot.BrowserSigninDisabled,
        snapshot.DeveloperToolsDisabled);
}

public sealed record PolicySnapshotResponse(IReadOnlyList<BrowserPolicySnapshotResponse> Browsers)
{
    public static PolicySnapshotResponse FromDomain(PolicySnapshot snapshot) =>
        new(snapshot.Browsers.Select(BrowserPolicySnapshotResponse.FromDomain).ToList());
}

public sealed record LocalAccountResponse(string AccountName, bool IsAdministrator, bool IsBuiltIn)
{
    public static LocalAccountResponse FromDomain(LocalAccountInfo info) =>
        new(info.AccountName, info.IsAdministrator, info.IsBuiltIn);
}

public sealed record ApplyPolicyResponse(
    bool Success,
    string? BackupDirectory,
    IReadOnlyList<string> BackupFilePaths,
    string? ErrorMessage,
    PolicySnapshotResponse? ResultingSnapshot);

public sealed record ApiErrorResponse(string Message);
```

- [ ] **Step 6: Shared-secret middleware**

```csharp
// src/WebsiteWhitelistManual.Api/SharedSecretMiddleware.cs
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
```

- [ ] **Step 7: Policy endpoints**

```csharp
// src/WebsiteWhitelistManual.Api/Endpoints/PolicyEndpoints.cs
using WebsiteWhitelistManual.Api.Contracts;
using WebsiteWhitelistManual.Core.Models;
using WebsiteWhitelistManual.Core.Services;

namespace WebsiteWhitelistManual.Api.Endpoints;

public static class PolicyEndpoints
{
    private static readonly TimeSpan BackupTimeout = TimeSpan.FromSeconds(10);

    public static void MapPolicyEndpoints(this WebApplication app)
    {
        app.MapGet("/api/policy/snapshot", (IRegistryPolicyReader reader) =>
        {
            var snapshot = reader.ReadSnapshot(new[] { BrowserTarget.Edge, BrowserTarget.Chrome });
            return Results.Ok(PolicySnapshotResponse.FromDomain(snapshot));
        });

        app.MapPost("/api/policy/apply", async (
            ApplyPolicyRequest request,
            IRegistryPolicyReader reader,
            IRegistryBackupService backupService,
            IRegistryPolicyWriter writer) =>
        {
            var browserTargets = new List<BrowserTarget>();
            foreach (var id in request.BrowserIds)
            {
                if (!Enum.TryParse<BrowserId>(id, ignoreCase: true, out var parsed))
                {
                    return Results.BadRequest(new ApiErrorResponse($"Unknown browser id '{id}'. Expected 'Edge' or 'Chrome'."));
                }
                browserTargets.Add(BrowserTarget.FromId(parsed));
            }

            var allowlistSites = new List<AllowlistSite>();
            foreach (var site in request.AllowlistSites)
            {
                if (!AllowlistSite.TryCreate(site.Domain, site.CategoryLabel, out var created, out var error))
                {
                    return Results.BadRequest(new ApiErrorResponse(error!));
                }
                allowlistSites.Add(created!);
            }

            var advancedOptions = new AdvancedOptionsState(
                request.AdvancedOptions.DisableIncognito,
                request.AdvancedOptions.DisableAccountSwitching,
                request.AdvancedOptions.DisableDeveloperTools);

            var configuration = new WizardConfiguration(browserTargets, allowlistSites, advancedOptions);

            if (!configuration.CanApply)
            {
                return Results.BadRequest(new ApiErrorResponse("至少需要選擇一個瀏覽器和一個允許的網站才能套用。"));
            }

            var backupBaseDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WebsiteWhitelistManual", "Backups");

            var backupTask = Task.Run(() => backupService.Backup(configuration.BrowserTargets, backupBaseDirectory, DateTimeOffset.Now));
            var completed = await Task.WhenAny(backupTask, Task.Delay(BackupTimeout));

            if (completed != backupTask)
            {
                return Results.Ok(new ApplyPolicyResponse(
                    Success: false,
                    BackupDirectory: null,
                    BackupFilePaths: Array.Empty<string>(),
                    ErrorMessage: "備份逾時（超過 10 秒沒有回應），已中止套用。請重試一次；若持續逾時，請確認沒有其他程式鎖住登錄檔。",
                    ResultingSnapshot: null));
            }

            var backupResult = await backupTask;
            if (!backupResult.Success)
            {
                return Results.Ok(new ApplyPolicyResponse(
                    Success: false,
                    BackupDirectory: backupResult.BackupDirectory,
                    BackupFilePaths: backupResult.BackupFilePaths,
                    ErrorMessage: $"備份失敗，已中止套用（未寫入任何變更）：{backupResult.ErrorMessage}",
                    ResultingSnapshot: null));
            }

            try
            {
                await Task.Run(() => writer.Apply(configuration));
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException)
            {
                return Results.Ok(new ApplyPolicyResponse(
                    Success: false,
                    BackupDirectory: backupResult.BackupDirectory,
                    BackupFilePaths: backupResult.BackupFilePaths,
                    ErrorMessage: $"寫入登錄檔時權限不足：{ex.Message}（理論上系統管理員權限已由 UAC 保證，若持續發生請重新啟動本工具）。",
                    ResultingSnapshot: null));
            }

            var resultingSnapshot = reader.ReadSnapshot(browserTargets);

            return Results.Ok(new ApplyPolicyResponse(
                Success: true,
                BackupDirectory: backupResult.BackupDirectory,
                BackupFilePaths: backupResult.BackupFilePaths,
                ErrorMessage: null,
                ResultingSnapshot: PolicySnapshotResponse.FromDomain(resultingSnapshot)));
        });
    }
}
```

- [ ] **Step 8: Account endpoints**

```csharp
// src/WebsiteWhitelistManual.Api/Endpoints/AccountEndpoints.cs
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
```

- [ ] **Step 9: `Program.cs`** — DI wiring, token generation, middleware, CORS for the Electron renderer's origin

```csharp
// src/WebsiteWhitelistManual.Api/Program.cs
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
```

- [ ] **Step 10: Add the API project to the solution**

```bash
dotnet sln WebsiteWhitelistManual.sln add src/WebsiteWhitelistManual.Api/WebsiteWhitelistManual.Api.csproj
```

- [ ] **Step 11: Verify it builds**

Run: `dotnet build`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)` across `Core`, `Core.Tests`, and `Api`.

- [ ] **Step 12: Confirm `Core`'s test suite is still untouched and green**

Run: `dotnet test tests/WebsiteWhitelistManual.Core.Tests`
Expected: `Passed! ... Total: 59` — unchanged, since this task added a new project and never modified `Core`.

- [ ] **Step 13: Smoke-test the running API** (Windows-only — the elevation prompt and any real registry read only happen there; on a non-Windows dev machine `dotnet run` will fail to start because the `WindowsRegistryAdapter`/`System.DirectoryServices.AccountManagement` calls are Windows-only at runtime even though they compile everywhere via `EnableWindowsTargeting`)

```powershell
dotnet run --project src/WebsiteWhitelistManual.Api
```

Confirm a UAC prompt appears (or silently elevates, if UAC prompts are suppressed on that machine — same behavior the WPF app had). Once running, in a second terminal:

```powershell
$token = Get-Content "$env:LOCALAPPDATA\WebsiteWhitelistManual\api-token.txt"
Invoke-RestMethod -Uri "http://127.0.0.1:5292/api/policy/snapshot" -Headers @{ "X-Api-Token" = $token }
Invoke-RestMethod -Uri "http://127.0.0.1:5292/api/accounts?scope=all" -Headers @{ "X-Api-Token" = $token }
```

Expected: JSON responses matching `PolicySnapshotResponse`/`LocalAccountResponse[]` shapes — likely showing `policyKeyExists: false` for both browsers on a machine that has never had the tool applied, and a real list of this machine's local accounts.

- [ ] **Step 14: Commit**

```bash
git add src/WebsiteWhitelistManual.Api WebsiteWhitelistManual.sln
git commit -m "feat: add WebsiteWhitelistManual.Api, a thin elevated HTTP wrapper around Core"
```

---

### Task 2: React SPA scaffold — design tokens, routing shell, API client

**Files:**
- Create: `frontend/` (new top-level directory, sibling to `src/`, `tests/`, `docs/`)
- Create: `frontend/package.json`, `frontend/vite.config.ts`, `frontend/tsconfig.json`, `frontend/index.html`
- Create: `frontend/src/main.tsx`, `frontend/src/App.tsx`
- Create: `frontend/src/styles/tokens.css`, `frontend/src/styles/global.css`
- Create: `frontend/src/api/client.ts`, `frontend/src/api/types.ts`
- Create: `frontend/src/state/WizardContext.tsx`
- Create: `frontend/src/components/AppShell.tsx`, `frontend/src/components/NavRail.tsx`

**Interfaces:**
- Consumes: the API from Task 1 (`GET /api/policy/snapshot`, `GET /api/accounts`, `POST /api/policy/apply`), plus a token the API client reads from `window.electronApi.getApiToken()` — a preload-exposed IPC call that doesn't exist until Task 3; for this task, stub it with a `localStorage`-based fallback so the frontend is independently runnable via `npm run dev` before Electron exists.
- Produces: `WizardContext` (the React-side equivalent of the WPF plan's `WizardConfigurationStore` singleton — cross-step state for chosen browsers/allowlist sites/advanced options).

- [ ] **Step 1: Scaffold with Vite**

```bash
npm create vite@latest frontend -- --template react-ts
cd frontend
npm install
npm install react-router-dom
cd ..
```

- [ ] **Step 2: Design tokens as CSS custom properties** — every value here is taken directly from `stitch_website_allowlist_desktop_app/guardian_clear/DESIGN.md`'s "Colors"/"Shapes"/"Layout & Spacing" sections (the flat hex values in that doc's prose, e.g. Primary `#0F766E`/Secondary `#10B981`/Warning `#D97706`, not the Material-generated YAML frontmatter's tonal-palette values, which are a different, unused derivation the mockup generator produced alongside them)

```css
/* frontend/src/styles/tokens.css */
:root {
  /* Colors */
  --color-primary: #0f766e;
  --color-primary-hover: #115e59;
  --color-primary-pressed: #134e4a;

  --color-secondary: #10b981;
  --color-secondary-tint: #ecfdf5;

  --color-tertiary: #3b82f6;
  --color-tertiary-tint: #eff6ff;

  --color-warning: #d97706;
  --color-warning-strong: #c2410c;
  --color-warning-tint: #fffbeb;

  --color-text-primary: #1e293b;
  --color-text-body: #334155;
  --color-text-muted: #64748b;

  --color-border: #e2e8f0;
  --color-canvas: #f8f9fa;
  --color-surface: #ffffff;

  --color-destructive-tint: #fef2f2;
  --color-destructive-text: #b91c1c;
  --color-destructive-hover: #fee2e2;

  /* Radii */
  --radius-control: 8px;
  --radius-card: 12px;
  --radius-pill: 9999px;

  /* Spacing */
  --spacing-gutter: 16px;
  --spacing-margin: 24px;
  --spacing-xs: 4px;
  --spacing-sm: 8px;
  --spacing-md: 14px;
  --spacing-lg: 20px;
  --spacing-xl: 28px;

  /* Typography */
  --font-family: Inter, "Noto Sans TC", "Microsoft JhengHei", sans-serif;
  --font-headline-lg: 700 26px/34px var(--font-family);
  --font-headline-md: 600 20px/28px var(--font-family);
  --font-headline-sm: 600 16px/24px var(--font-family);
  --font-body-lg: 400 15px/24px var(--font-family);
  --font-body-md: 400 13.5px/20px var(--font-family);
  --font-body-sm: 400 12px/18px var(--font-family);
  --font-label-md: 500 13.5px/18px var(--font-family);
  --font-label-sm: 600 11.5px/16px var(--font-family);

  /* Elevation */
  --shadow-card: 0 1px 3px rgba(0, 0, 0, 0.05), 0 4px 12px rgba(0, 0, 0, 0.03);
  --shadow-modal: 0 8px 24px rgba(15, 23, 42, 0.08), 0 2px 6px rgba(15, 23, 42, 0.04);
  --focus-ring: 0 0 0 2px #ffffff, 0 0 0 4px var(--color-primary);
}
```

- [ ] **Step 3: Global layout/reset styles**

```css
/* frontend/src/styles/global.css */
@import "./tokens.css";

* {
  box-sizing: border-box;
}

html, body, #root {
  height: 100%;
  margin: 0;
}

body {
  font: var(--font-body-md);
  color: var(--color-text-body);
  background: var(--color-canvas);
}

h1, h2, h3, p {
  margin: 0;
}

button {
  font-family: var(--font-family);
  cursor: pointer;
}

button:focus-visible, input:focus-visible, a:focus-visible {
  outline: none;
  box-shadow: var(--focus-ring);
}

.card {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-card);
  box-shadow: var(--shadow-card);
  padding: var(--spacing-lg);
}

.btn {
  height: 40px;
  padding: 0 20px;
  border-radius: var(--radius-control);
  border: none;
  font: var(--font-label-md);
}

.btn-primary {
  background: var(--color-primary);
  color: #fff;
}
.btn-primary:hover { background: var(--color-primary-hover); }
.btn-primary:active { background: var(--color-primary-pressed); }
.btn-primary:disabled { background: var(--color-border); color: var(--color-text-muted); cursor: not-allowed; }

.btn-secondary {
  background: #f1f5f9;
  color: var(--color-text-body);
  border: 1px solid var(--color-border);
}
.btn-secondary:hover { background: var(--color-border); }

.btn-destructive {
  background: var(--color-destructive-tint);
  color: var(--color-destructive-text);
}
.btn-destructive:hover { background: var(--color-destructive-hover); }

.pill {
  display: inline-flex;
  align-items: center;
  padding: 2px 10px;
  border-radius: var(--radius-pill);
  font: var(--font-label-sm);
}
.pill-success { background: var(--color-secondary-tint); color: var(--color-secondary); }
.pill-info { background: var(--color-tertiary-tint); color: var(--color-tertiary); }
.pill-warning { background: var(--color-warning-tint); color: var(--color-warning); }
```

- [ ] **Step 4: API types (hand-mirrored from `Contracts/ApiModels.cs` — keep these two files in sync manually; this is a two-project polyglot repo with no shared codegen step, and introducing one is out of scope for v1)**

```typescript
// frontend/src/api/types.ts
export type BrowserId = "Edge" | "Chrome";

export interface BrowserPolicySnapshot {
  browserId: string;
  policyKeyExists: boolean;
  blockedUrls: string[];
  allowedUrls: string[];
  incognitoDisabled: boolean | null;
  browserSigninDisabled: boolean | null;
  developerToolsDisabled: boolean | null;
}

export interface PolicySnapshot {
  browsers: BrowserPolicySnapshot[];
}

export interface LocalAccount {
  accountName: string;
  isAdministrator: boolean;
  isBuiltIn: boolean;
}

export interface AllowlistSiteInput {
  domain: string;
  categoryLabel: string | null;
}

export interface AdvancedOptions {
  disableIncognito: boolean;
  disableAccountSwitching: boolean;
  disableDeveloperTools: boolean;
}

export interface ApplyPolicyRequest {
  browserIds: BrowserId[];
  allowlistSites: AllowlistSiteInput[];
  advancedOptions: AdvancedOptions;
}

export interface ApplyPolicyResponse {
  success: boolean;
  backupDirectory: string | null;
  backupFilePaths: string[];
  errorMessage: string | null;
  resultingSnapshot: PolicySnapshot | null;
}

export interface ApiError {
  message: string;
}
```

- [ ] **Step 5: API client** — resolves the token via a small abstraction so this file works identically whether `window.electronApi` exists yet (Task 3) or not (plain `npm run dev`)

```typescript
// frontend/src/api/client.ts
import type { ApplyPolicyRequest, ApplyPolicyResponse, LocalAccount, PolicySnapshot } from "./types";

const API_BASE_URL = "http://127.0.0.1:5292";

declare global {
  interface Window {
    electronApi?: {
      getApiToken: () => Promise<string>;
    };
  }
}

async function getToken(): Promise<string> {
  if (window.electronApi) {
    return window.electronApi.getApiToken();
  }
  // Dev-only fallback: `npm run dev` outside Electron has no IPC bridge, so
  // read the token file's content pasted manually into localStorage for
  // local frontend iteration against a `dotnet run` API instance. Never
  // reached in the packaged app, where window.electronApi always exists.
  const stored = window.localStorage.getItem("dev-api-token");
  if (!stored) {
    throw new Error(
      "No API token available. Running outside Electron: paste the contents of " +
      "%LOCALAPPDATA%\\WebsiteWhitelistManual\\api-token.txt into " +
      "localStorage.setItem('dev-api-token', '<token>') in the browser devtools console.",
    );
  }
  return stored;
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const token = await getToken();
  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      "X-Api-Token": token,
      ...init?.headers,
    },
  });

  if (!response.ok) {
    const body = await response.json().catch(() => ({ message: response.statusText }));
    throw new Error(body.message ?? `Request to ${path} failed with status ${response.status}`);
  }

  return response.json() as Promise<T>;
}

export const apiClient = {
  getSnapshot: () => request<PolicySnapshot>("/api/policy/snapshot"),
  getAccounts: (scope: "all" | "relevant") => request<LocalAccount[]>(`/api/accounts?scope=${scope}`),
  applyPolicy: (body: ApplyPolicyRequest) =>
    request<ApplyPolicyResponse>("/api/policy/apply", { method: "POST", body: JSON.stringify(body) }),
};
```

- [ ] **Step 6: Wizard state context** — the React-side `WizardConfigurationStore` equivalent

```typescript
// frontend/src/state/WizardContext.tsx
import { createContext, useContext, useMemo, useState, type ReactNode } from "react";
import type { AdvancedOptions, AllowlistSiteInput, BrowserId } from "../api/types";

interface WizardState {
  selectedBrowsers: BrowserId[];
  allowlistSites: AllowlistSiteInput[];
  advancedOptions: AdvancedOptions;
}

interface WizardContextValue extends WizardState {
  setSelectedBrowsers: (browsers: BrowserId[]) => void;
  setAllowlistSites: (sites: AllowlistSiteInput[]) => void;
  setAdvancedOptions: (options: AdvancedOptions) => void;
  canApply: boolean;
}

const defaultAdvancedOptions: AdvancedOptions = {
  disableIncognito: true,
  disableAccountSwitching: true,
  disableDeveloperTools: true,
};

const WizardContext = createContext<WizardContextValue | null>(null);

export function WizardProvider({ children }: { children: ReactNode }) {
  const [selectedBrowsers, setSelectedBrowsers] = useState<BrowserId[]>([]);
  const [allowlistSites, setAllowlistSites] = useState<AllowlistSiteInput[]>([]);
  const [advancedOptions, setAdvancedOptions] = useState<AdvancedOptions>(defaultAdvancedOptions);

  const value = useMemo<WizardContextValue>(() => ({
    selectedBrowsers,
    allowlistSites,
    advancedOptions,
    setSelectedBrowsers,
    setAllowlistSites,
    setAdvancedOptions,
    canApply: selectedBrowsers.length > 0 && allowlistSites.length > 0,
  }), [selectedBrowsers, allowlistSites, advancedOptions]);

  return <WizardContext.Provider value={value}>{children}</WizardContext.Provider>;
}

export function useWizard(): WizardContextValue {
  const context = useContext(WizardContext);
  if (!context) {
    throw new Error("useWizard must be used within a WizardProvider");
  }
  return context;
}
```

- [ ] **Step 7: Nav rail** — two-section layout matching the Stitch left rail (首頁 pinned above, 5 numbered steps under a "設定精靈導覽" label)

```tsx
// frontend/src/components/NavRail.tsx
import { NavLink } from "react-router-dom";
import "./NavRail.css";

const steps = [
  { to: "/step1", label: "1. 選擇瀏覽器" },
  { to: "/step2", label: "2. 允許的網站" },
  { to: "/step3", label: "3. 進階選項" },
  { to: "/step4", label: "4. 套用前確認" },
  { to: "/step5", label: "5. 完成與驗證" },
];

export function NavRail() {
  return (
    <nav className="nav-rail">
      <div className="nav-rail-brand">
        <span className="nav-rail-title">白名單防護</span>
        <span className="nav-rail-subtitle">家長防護管理模式</span>
      </div>
      <NavLink to="/" end className={({ isActive }) => `nav-item ${isActive ? "nav-item-active" : ""}`}>
        首頁
      </NavLink>
      <div className="nav-rail-section-label">設定精靈導覽</div>
      {steps.map((step) => (
        <NavLink
          key={step.to}
          to={step.to}
          className={({ isActive }) => `nav-item ${isActive ? "nav-item-active" : ""}`}
        >
          {step.label}
        </NavLink>
      ))}
    </nav>
  );
}
```

```css
/* frontend/src/components/NavRail.css */
.nav-rail {
  width: 240px;
  flex-shrink: 0;
  background: var(--color-surface);
  border-right: 1px solid var(--color-border);
  padding: var(--spacing-lg) var(--spacing-md);
  display: flex;
  flex-direction: column;
  gap: 2px;
}

.nav-rail-brand {
  display: flex;
  flex-direction: column;
  margin-bottom: var(--spacing-lg);
}
.nav-rail-title { font: var(--font-headline-sm); color: var(--color-text-primary); }
.nav-rail-subtitle { font: var(--font-body-sm); color: var(--color-text-muted); }

.nav-rail-section-label {
  font: var(--font-label-sm);
  color: var(--color-text-muted);
  text-transform: uppercase;
  padding: var(--spacing-md) var(--spacing-sm) var(--spacing-xs);
}

.nav-item {
  display: block;
  padding: 10px var(--spacing-sm);
  border-radius: var(--radius-control);
  color: var(--color-text-body);
  text-decoration: none;
  font: var(--font-label-md);
}
.nav-item:hover { background: var(--color-canvas); }
.nav-item-active { background: var(--color-primary); color: #fff; }
```

- [ ] **Step 8: App shell layout**

```tsx
// frontend/src/components/AppShell.tsx
import { Outlet } from "react-router-dom";
import { NavRail } from "./NavRail";
import "./AppShell.css";

export function AppShell() {
  return (
    <div className="app-shell">
      <NavRail />
      <main className="app-shell-content">
        <Outlet />
      </main>
    </div>
  );
}
```

```css
/* frontend/src/components/AppShell.css */
.app-shell {
  display: flex;
  height: 100vh;
}
.app-shell-content {
  flex: 1;
  overflow-y: auto;
  padding: var(--spacing-margin);
}
```

- [ ] **Step 9: Router + app root** (page components referenced here are created in Tasks 3–7 below; this task's build will not fully type-check until then — see Step 11)

```tsx
// frontend/src/App.tsx
import { HashRouter, Route, Routes } from "react-router-dom";
import { AppShell } from "./components/AppShell";
import { WizardProvider } from "./state/WizardContext";
import { DashboardPage } from "./pages/DashboardPage";
import { Step1BrowserPage } from "./pages/Step1BrowserPage";
import { Step2SitesPage } from "./pages/Step2SitesPage";
import { Step3AdvancedPage } from "./pages/Step3AdvancedPage";
import { Step4ConfirmPage } from "./pages/Step4ConfirmPage";
import { Step5CompletePage } from "./pages/Step5CompletePage";

export function App() {
  return (
    <WizardProvider>
      <HashRouter>
        <Routes>
          <Route element={<AppShell />}>
            <Route index element={<DashboardPage />} />
            <Route path="step1" element={<Step1BrowserPage />} />
            <Route path="step2" element={<Step2SitesPage />} />
            <Route path="step3" element={<Step3AdvancedPage />} />
            <Route path="step4" element={<Step4ConfirmPage />} />
            <Route path="step5" element={<Step5CompletePage />} />
          </Route>
        </Routes>
      </HashRouter>
    </WizardProvider>
  );
}
```

**Routing note:** `HashRouter`, not `BrowserRouter`. A packaged Electron app in production serves the React build from a local static file server (Task 3), but even so, hash-based routing avoids any dependency on that server correctly handling deep-link fallback (`/step3` on a hard refresh returning `index.html` instead of a 404) — `HashRouter` keeps all routing client-side (`#/step3`) regardless of how the static files are served, which is simpler and sufficient for a single-window desktop app with no need for shareable URLs.

- [ ] **Step 10: `main.tsx`**

```tsx
// frontend/src/main.tsx
import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { App } from "./App";
import "./styles/global.css";

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
```

- [ ] **Step 11: Placeholder pages so the app type-checks and runs before Tasks 3–7 build them out for real**

```bash
mkdir -p frontend/src/pages
```

```tsx
// frontend/src/pages/DashboardPage.tsx (placeholder — Task 3 replaces this)
export function DashboardPage() {
  return <div className="card">首頁（尚未實作）</div>;
}
```

Repeat the same one-line placeholder pattern for `Step1BrowserPage.tsx`, `Step2SitesPage.tsx`, `Step3AdvancedPage.tsx`, `Step4ConfirmPage.tsx`, `Step5CompletePage.tsx` (each exporting a differently-named component, e.g. `export function Step1BrowserPage() { return <div className="card">步驟 1（尚未實作）</div>; }`).

- [ ] **Step 12: Verify it builds and runs**

```bash
cd frontend
npm run build
npm run dev
```

Expected: `npm run build` produces `frontend/dist/` with no TypeScript errors; `npm run dev` serves at `http://localhost:5173` showing the nav rail and a placeholder card per route (the API calls will fail with the token error from Step 5 until Task 1's API is actually running and a token is pasted into `localStorage` — that's expected at this stage, not a bug).

- [ ] **Step 13: Commit**

```bash
git add frontend
git commit -m "feat: scaffold React SPA with Guardian Clear design tokens, routing shell, and API client"
```

---

### Task 3: Dashboard page

**Files:**
- Create: `frontend/src/pages/DashboardPage.tsx`, `frontend/src/pages/DashboardPage.css`
- Create: `frontend/src/components/PolicyDetailDialog.tsx`

**Interfaces:**
- Consumes: `apiClient.getSnapshot()`, `apiClient.getAccounts("all")`.

- [ ] **Step 1: Policy detail dialog** — read-only, formatted to mirror `manual.html`'s key/value table layout (same content the WPF plan's `PolicyDetailDialog` produced, now as a React modal instead of a WPF `Window`)

```tsx
// frontend/src/components/PolicyDetailDialog.tsx
import type { PolicySnapshot } from "../api/types";
import "./PolicyDetailDialog.css";

function formatSnapshot(snapshot: PolicySnapshot): string {
  return snapshot.browsers
    .map((browser) => [
      `=== ${browser.browserId} ===`,
      `政策機碼存在: ${browser.policyKeyExists}`,
      `URLBlocklist: ${browser.blockedUrls.join(", ")}`,
      `URLAllowlist: ${browser.allowedUrls.join(", ")}`,
      `停用無痕模式: ${browser.incognitoDisabled}`,
      `停用帳號切換 (BrowserSignin=0): ${browser.browserSigninDisabled}`,
      `停用開發人員工具: ${browser.developerToolsDisabled}`,
      "",
    ].join("\n"))
    .join("\n");
}

export function PolicyDetailDialog({ snapshot, onClose }: { snapshot: PolicySnapshot; onClose: () => void }) {
  return (
    <div className="dialog-overlay" onClick={onClose}>
      <div className="dialog-panel" onClick={(event) => event.stopPropagation()}>
        <div className="dialog-header">
          <h2>完整設定值</h2>
          <button className="btn btn-secondary" onClick={onClose}>關閉</button>
        </div>
        <pre className="dialog-content">{formatSnapshot(snapshot)}</pre>
      </div>
    </div>
  );
}
```

```css
/* frontend/src/components/PolicyDetailDialog.css */
.dialog-overlay {
  position: fixed;
  inset: 0;
  background: rgba(15, 23, 42, 0.4);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 100;
}
.dialog-panel {
  width: 640px;
  max-height: 480px;
  background: var(--color-surface);
  border-radius: var(--radius-card);
  box-shadow: var(--shadow-modal);
  display: flex;
  flex-direction: column;
  padding: var(--spacing-lg);
}
.dialog-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: var(--spacing-md);
}
.dialog-content {
  overflow: auto;
  font-family: Consolas, monospace;
  font-size: 12px;
  white-space: pre;
  background: var(--color-canvas);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-control);
  padding: var(--spacing-md);
}
```

- [ ] **Step 2: Dashboard page** — real registry-backed status banner, full account list (including built-ins), no block-count card, no trend chart (Global Constraint #1)

```tsx
// frontend/src/pages/DashboardPage.tsx
import { useEffect, useState } from "react";
import { apiClient } from "../api/client";
import type { LocalAccount, PolicySnapshot } from "../api/types";
import { PolicyDetailDialog } from "../components/PolicyDetailDialog";
import "./DashboardPage.css";

export function DashboardPage() {
  const [snapshot, setSnapshot] = useState<PolicySnapshot | null>(null);
  const [accounts, setAccounts] = useState<LocalAccount[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [showDetail, setShowDetail] = useState(false);

  async function refresh() {
    setError(null);
    try {
      const [snapshotResult, accountsResult] = await Promise.all([
        apiClient.getSnapshot(),
        apiClient.getAccounts("all"),
      ]);
      setSnapshot(snapshotResult);
      setAccounts(accountsResult);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    }
  }

  useEffect(() => {
    refresh();
  }, []);

  const isProtectionActive = snapshot?.browsers.some(
    (browser) => browser.policyKeyExists && browser.allowedUrls.length > 0,
  ) ?? false;

  const allowedSiteCount = snapshot
    ? new Set(snapshot.browsers.flatMap((browser) => browser.allowedUrls)).size
    : 0;

  return (
    <div className="dashboard-page">
      <h1 className="page-title">首頁</h1>

      {error && <div className="card banner-warning">{error}</div>}

      <div className={`card banner-status ${isProtectionActive ? "banner-active" : ""}`}>
        <div>
          <div className="banner-status-title">
            目前防護狀態：{isProtectionActive ? "已啟用白名單管制" : "尚未設定"}
          </div>
          <div className="banner-status-subtitle">目前允許 {allowedSiteCount} 個網站</div>
        </div>
      </div>

      <button className="btn btn-secondary" onClick={refresh}>重新整理狀態</button>

      <h2 className="section-title">本機使用者帳號</h2>
      <div className="account-list">
        {accounts.map((account) => (
          <div key={account.accountName} className="card account-row">
            <span className="account-name">{account.accountName}</span>
            <span className="account-meta">系統管理員: {String(account.isAdministrator)}</span>
            <span className="account-meta">內建帳號: {String(account.isBuiltIn)}</span>
          </div>
        ))}
      </div>

      <button className="btn btn-secondary" onClick={() => setShowDetail(true)} disabled={!snapshot}>
        查看完整設定值 (進階)
      </button>

      {showDetail && snapshot && (
        <PolicyDetailDialog snapshot={snapshot} onClose={() => setShowDetail(false)} />
      )}
    </div>
  );
}
```

```css
/* frontend/src/pages/DashboardPage.css */
.page-title { font: var(--font-headline-md); color: var(--color-text-primary); margin-bottom: var(--spacing-lg); }
.section-title { font: var(--font-headline-sm); color: var(--color-text-primary); margin: var(--spacing-xl) 0 var(--spacing-sm); }

.banner-status {
  margin-bottom: var(--spacing-lg);
  background: var(--color-canvas);
}
.banner-status.banner-active { background: var(--color-secondary-tint); }
.banner-status-title { font: var(--font-headline-sm); color: var(--color-text-primary); }
.banner-status-subtitle { font: var(--font-body-md); color: var(--color-text-muted); margin-top: 4px; }

.banner-warning {
  background: var(--color-warning-tint);
  color: var(--color-warning-strong);
  margin-bottom: var(--spacing-md);
}

.account-list { display: flex; flex-direction: column; gap: var(--spacing-sm); margin-bottom: var(--spacing-lg); }
.account-row { display: flex; gap: var(--spacing-lg); align-items: center; }
.account-name { font: var(--font-label-md); color: var(--color-text-primary); width: 200px; }
.account-meta { font: var(--font-body-sm); color: var(--color-text-muted); }
```

- [ ] **Step 3: Verify it builds and, if a local API instance from Task 1 is running with a token pasted into `localStorage`, renders real data**

```bash
cd frontend
npm run build
```

- [ ] **Step 4: Commit**

```bash
git add frontend/src/pages/DashboardPage.tsx frontend/src/pages/DashboardPage.css frontend/src/components/PolicyDetailDialog.tsx frontend/src/components/PolicyDetailDialog.css
git commit -m "feat: add DashboardPage with real registry-backed status and full account list"
```

---

### Task 4: Step 1 (browser selection) and Step 2 (allowlist sites) pages

**Files:**
- Create: `frontend/src/pages/Step1BrowserPage.tsx`, `frontend/src/pages/Step1BrowserPage.css`
- Create: `frontend/src/pages/Step2SitesPage.tsx`, `frontend/src/pages/Step2SitesPage.css`

**Interfaces:**
- Consumes/produces: `useWizard()`'s `selectedBrowsers`/`allowlistSites` state (Task 2). Domain validation is reimplemented client-side in TypeScript, mirroring `AllowlistSite.TryCreate`'s exact rules (non-empty, no whitespace, no `://`, must contain a `.` and not start/end with one) — the API's `/api/policy/apply` endpoint re-validates via the real `AllowlistSite.TryCreate` at submit time regardless (Task 1, Step 7), so this client-side copy is purely for instant feedback while typing, never the sole gate before a real write.

- [ ] **Step 1: Step1BrowserPage** — Edge/Chrome selection cards, no Firefox card (Global Constraint: Firefox out of scope)

```tsx
// frontend/src/pages/Step1BrowserPage.tsx
import { useWizard } from "../state/WizardContext";
import type { BrowserId } from "../api/types";
import "./Step1BrowserPage.css";

const browsers: { id: BrowserId; name: string; description: string }[] = [
  { id: "Edge", name: "Microsoft Edge", description: "Windows 內建瀏覽器・系統登錄原則 (Registry/GPO)" },
  { id: "Chrome", name: "Google Chrome", description: "全球主流瀏覽器・企業政策原則 (Policies\\Google\\Chrome)" },
];

export function Step1BrowserPage() {
  const { selectedBrowsers, setSelectedBrowsers } = useWizard();

  function toggle(id: BrowserId) {
    setSelectedBrowsers(
      selectedBrowsers.includes(id)
        ? selectedBrowsers.filter((existing) => existing !== id)
        : [...selectedBrowsers, id],
    );
  }

  return (
    <div className="step1-page">
      <h1 className="page-title">步驟 1：選擇要鎖定的瀏覽器</h1>
      <p className="page-description">
        選擇這台筆電上要鎖定的瀏覽器，可以複選。其餘瀏覽器不受本工具管理。
      </p>

      <div className="browser-cards">
        {browsers.map((browser) => {
          const checked = selectedBrowsers.includes(browser.id);
          return (
            <label key={browser.id} className={`card browser-card ${checked ? "browser-card-selected" : ""}`}>
              <div className="browser-card-header">
                <input type="checkbox" checked={checked} onChange={() => toggle(browser.id)} />
                <span className="browser-card-name">{browser.name}</span>
              </div>
              <p className="browser-card-description">{browser.description}</p>
            </label>
          );
        })}
      </div>

      <div className="card info-banner">
        <strong>為什麼建議同時勾選 Edge 與 Chrome？</strong>
        <p>避免孩子在其中一個瀏覽器被封鎖時，自行切換至另一個未受管的瀏覽器瀏覽未核准網站。</p>
      </div>
    </div>
  );
}
```

```css
/* frontend/src/pages/Step1BrowserPage.css */
.page-title { font: var(--font-headline-md); color: var(--color-text-primary); margin-bottom: var(--spacing-sm); }
.page-description { color: var(--color-text-body); margin-bottom: var(--spacing-lg); max-width: 640px; }

.browser-cards { display: flex; gap: var(--spacing-lg); margin-bottom: var(--spacing-xl); }
.browser-card { width: 320px; cursor: pointer; display: block; }
.browser-card-selected { border-color: var(--color-primary); background: var(--color-secondary-tint); }
.browser-card-header { display: flex; align-items: center; gap: var(--spacing-sm); }
.browser-card-name { font: var(--font-headline-sm); color: var(--color-text-primary); }
.browser-card-description { color: var(--color-text-muted); font: var(--font-body-sm); margin-top: var(--spacing-xs); }

.info-banner { background: var(--color-tertiary-tint); }
.info-banner strong { color: var(--color-text-primary); }
.info-banner p { margin-top: var(--spacing-xs); color: var(--color-text-body); font: var(--font-body-sm); }
```

- [ ] **Step 2: Client-side domain validation** matching `AllowlistSite.TryCreate` exactly

```typescript
// frontend/src/pages/validateDomain.ts
export function validateDomain(rawInput: string): { ok: true } | { ok: false; error: string } {
  if (!rawInput || rawInput.trim().length === 0) {
    return { ok: false, error: "網域不能是空白。" };
  }
  const trimmed = rawInput.trim();
  if (trimmed.includes(" ")) {
    return { ok: false, error: "網域不能包含空格。" };
  }
  if (trimmed.includes("://")) {
    return { ok: false, error: "請只填網域，不要包含 http:// 或 https://。" };
  }
  if (!trimmed.includes(".") || trimmed.startsWith(".") || trimmed.endsWith(".")) {
    return { ok: false, error: "請輸入完整網域，例如 example.com。" };
  }
  return { ok: true };
}
```

- [ ] **Step 3: Step2SitesPage** — allowlist input with validation, live (real, not fabricated — Global Constraint #4) category-percentage breakdown

```tsx
// frontend/src/pages/Step2SitesPage.tsx
import { useMemo, useState } from "react";
import { useWizard } from "../state/WizardContext";
import { validateDomain } from "./validateDomain";
import "./Step2SitesPage.css";

export function Step2SitesPage() {
  const { allowlistSites, setAllowlistSites } = useWizard();
  const [domainInput, setDomainInput] = useState("");
  const [categoryInput, setCategoryInput] = useState("");
  const [validationError, setValidationError] = useState<string | null>(null);

  function addSite() {
    const result = validateDomain(domainInput);
    if (!result.ok) {
      setValidationError(result.error);
      return;
    }
    setValidationError(null);
    setAllowlistSites([
      ...allowlistSites,
      { domain: domainInput.trim(), categoryLabel: categoryInput.trim() || null },
    ]);
    setDomainInput("");
    setCategoryInput("");
  }

  function removeSite(domain: string) {
    setAllowlistSites(allowlistSites.filter((site) => site.domain !== domain));
  }

  const categoryBreakdown = useMemo(() => {
    const total = allowlistSites.length;
    if (total === 0) return [];
    const counts = new Map<string, number>();
    for (const site of allowlistSites) {
      const label = site.categoryLabel ?? "未分類";
      counts.set(label, (counts.get(label) ?? 0) + 1);
    }
    return Array.from(counts.entries())
      .map(([label, count]) => ({ label, count, percentage: Math.round((100 * count) / total) }))
      .sort((a, b) => b.count - a.count);
  }, [allowlistSites]);

  return (
    <div className="step2-page">
      <div className="step2-main">
        <h1 className="page-title">步驟 2：設定允許孩子訪問的網站</h1>
        <p className="page-description">
          除了以下列出的網址外，瀏覽器將自動封鎖所有其他網路訪問。子網域視為不同項目，例如
          www.example.com 和 example.com 需分別加入。
        </p>

        <div className="site-input-row">
          <input
            className="text-input"
            placeholder="輸入網域，例如 classroom.google.com"
            value={domainInput}
            onChange={(event) => setDomainInput(event.target.value)}
          />
          <input
            className="text-input text-input-narrow"
            placeholder="分類標籤 (選填)"
            value={categoryInput}
            onChange={(event) => setCategoryInput(event.target.value)}
          />
          <button className="btn btn-primary" onClick={addSite}>新增至白名單</button>
        </div>
        {validationError && <p className="validation-error">{validationError}</p>}

        <h2 className="section-title">目前已允許的網站清單 (共 {allowlistSites.length} 個網址)</h2>
        <div className="site-list">
          {allowlistSites.map((site) => (
            <div key={site.domain} className="card site-row">
              <span className="site-domain">{site.domain}</span>
              {site.categoryLabel && <span className="pill pill-info">{site.categoryLabel}</span>}
              <button className="btn btn-destructive" onClick={() => removeSite(site.domain)}>刪除</button>
            </div>
          ))}
        </div>
      </div>

      <aside className="step2-sidebar">
        <h2 className="section-title">白名單分類分佈</h2>
        {categoryBreakdown.map((item) => (
          <div key={item.label} className="breakdown-row">
            {item.label} — {item.count} 個 ({item.percentage}%)
          </div>
        ))}
      </aside>
    </div>
  );
}
```

```css
/* frontend/src/pages/Step2SitesPage.css */
.step2-page { display: flex; gap: var(--spacing-lg); }
.step2-main { flex: 1; }
.step2-sidebar { width: 240px; flex-shrink: 0; }

.page-title { font: var(--font-headline-md); color: var(--color-text-primary); margin-bottom: var(--spacing-sm); }
.page-description { color: var(--color-text-body); margin-bottom: var(--spacing-lg); }
.section-title { font: var(--font-headline-sm); color: var(--color-text-primary); margin: var(--spacing-md) 0; }

.site-input-row { display: flex; gap: var(--spacing-sm); margin-bottom: var(--spacing-xs); }
.text-input { height: 42px; border: 1px solid #cbd5e1; border-radius: var(--radius-control); padding: 0 12px; flex: 1; }
.text-input-narrow { flex: none; width: 160px; }
.validation-error { color: var(--color-warning-strong); font: var(--font-body-sm); margin-bottom: var(--spacing-md); }

.site-list { display: flex; flex-direction: column; gap: var(--spacing-sm); }
.site-row { display: flex; align-items: center; gap: var(--spacing-sm); }
.site-domain { flex: 1; font: var(--font-label-md); color: var(--color-text-primary); }

.breakdown-row { font: var(--font-body-sm); color: var(--color-text-body); margin-bottom: var(--spacing-xs); }
```

- [ ] **Step 4: Verify build**

```bash
cd frontend
npm run build
```

- [ ] **Step 5: Commit**

```bash
git add frontend/src/pages/Step1BrowserPage.tsx frontend/src/pages/Step1BrowserPage.css frontend/src/pages/Step2SitesPage.tsx frontend/src/pages/Step2SitesPage.css frontend/src/pages/validateDomain.ts
git commit -m "feat: add Step1BrowserPage and Step2SitesPage with real (non-fabricated) category breakdown"
```

---

### Task 5: Step 3 (advanced options) page

**Files:**
- Create: `frontend/src/pages/Step3AdvancedPage.tsx`, `frontend/src/pages/Step3AdvancedPage.css`

**Interfaces:**
- Consumes/produces: `useWizard()`'s `advancedOptions` (defaults to all three `true`, matching `AdvancedOptionsState`'s C# defaults — the `WizardContext` default object in Task 2 already sets this).

- [ ] **Step 1: Step3AdvancedPage** — three toggles labeled with the exact registry effect, `PolicyKeys`-accurate text, never `BrowserGuestModeEnabled`

```tsx
// frontend/src/pages/Step3AdvancedPage.tsx
import { useWizard } from "../state/WizardContext";
import "./Step3AdvancedPage.css";

const options: { key: "disableIncognito" | "disableDeveloperTools" | "disableAccountSwitching"; title: string; description: string; registryNote: string }[] = [
  {
    key: "disableIncognito",
    title: "停用無痕視窗 (InPrivate / Incognito)",
    description: "防止孩子建立不會留下瀏覽紀錄的無痕分頁。",
    registryNote: "Windows 原則值：InPrivateModeAvailability / IncognitoModeAvailability = 1",
  },
  {
    key: "disableDeveloperTools",
    title: "停用開發人員工具 (Developer Tools / F12)",
    description: "避免具備技術好奇心的孩子透過控制台修改設定或繞過限制。",
    registryNote: "Windows 原則值：DeveloperToolsAvailability = 2",
  },
  {
    key: "disableAccountSwitching",
    title: "停用帳號切換 (BrowserSignin)",
    description: "禁止在瀏覽器中切換至未受管的其他帳號，確保白名單防護不會被多帳號登入繞過。",
    registryNote: "Windows 原則值：BrowserSignin = 0",
  },
];

export function Step3AdvancedPage() {
  const { advancedOptions, setAdvancedOptions } = useWizard();

  function toggle(key: typeof options[number]["key"]) {
    setAdvancedOptions({ ...advancedOptions, [key]: !advancedOptions[key] });
  }

  return (
    <div className="step3-page">
      <h1 className="page-title">步驟 3：設定防繞過與進階防護選項</h1>
      <p className="page-description">建議全數維持開啟以達成無漏洞防護。</p>

      {options.map((option) => (
        <div key={option.key} className="card option-row">
          <div className="option-text">
            <div className="option-title">{option.title}</div>
            <p className="option-description">{option.description}</p>
            <div className="option-registry-note">{option.registryNote}</div>
          </div>
          <label className="toggle">
            <input
              type="checkbox"
              checked={advancedOptions[option.key]}
              onChange={() => toggle(option.key)}
            />
            <span className="toggle-track" />
          </label>
        </div>
      ))}
    </div>
  );
}
```

```css
/* frontend/src/pages/Step3AdvancedPage.css */
.page-title { font: var(--font-headline-md); color: var(--color-text-primary); margin-bottom: var(--spacing-sm); }
.page-description { color: var(--color-text-body); margin-bottom: var(--spacing-lg); }

.option-row { display: flex; justify-content: space-between; align-items: center; margin-bottom: var(--spacing-md); }
.option-title { font: var(--font-headline-sm); color: var(--color-text-primary); }
.option-description { color: var(--color-text-muted); font: var(--font-body-sm); margin: var(--spacing-xs) 0; }
.option-registry-note { font-family: Consolas, monospace; font-size: 11px; color: var(--color-text-muted); }

.toggle { position: relative; display: inline-block; width: 44px; height: 24px; flex-shrink: 0; }
.toggle input { opacity: 0; width: 0; height: 0; }
.toggle-track {
  position: absolute; inset: 0; background: #cbd5e1; border-radius: var(--radius-pill);
  transition: background 0.15s;
}
.toggle-track::before {
  content: ""; position: absolute; width: 18px; height: 18px; left: 3px; top: 3px;
  background: #fff; border-radius: 50%; transition: transform 0.15s;
}
.toggle input:checked + .toggle-track { background: var(--color-primary); }
.toggle input:checked + .toggle-track::before { transform: translateX(20px); }
```

- [ ] **Step 2: Verify build**

```bash
cd frontend
npm run build
```

- [ ] **Step 3: Commit**

```bash
git add frontend/src/pages/Step3AdvancedPage.tsx frontend/src/pages/Step3AdvancedPage.css
git commit -m "feat: add Step3AdvancedPage bound to WizardContext's advancedOptions"
```

---

### Task 6: Step 4 (confirm and apply) page

**Files:**
- Create: `frontend/src/pages/Step4ConfirmPage.tsx`, `frontend/src/pages/Step4ConfirmPage.css`

**Interfaces:**
- Consumes: `useWizard()` (full state, `canApply`), `apiClient.getSnapshot()` (diff preview), `apiClient.applyPolicy(...)`.

- [ ] **Step 1: Step4ConfirmPage** — diff preview, acknowledgement checkbox, apply button gated on `canApply`, error banner on failure (backup timeout / backup failure / write permission error — all three cases the API's `/api/policy/apply` endpoint returns as `success: false` with a specific `errorMessage`, per Task 1 Step 7)

```tsx
// frontend/src/pages/Step4ConfirmPage.tsx
import { useEffect, useState } from "react";
import { apiClient } from "../api/client";
import { useWizard } from "../state/WizardContext";
import type { PolicySnapshot } from "../api/types";
import "./Step4ConfirmPage.css";

export function Step4ConfirmPage() {
  const { selectedBrowsers, allowlistSites, advancedOptions, canApply } = useWizard();
  const [currentSnapshot, setCurrentSnapshot] = useState<PolicySnapshot | null>(null);
  const [acknowledged, setAcknowledged] = useState(false);
  const [isApplying, setIsApplying] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [backupDirectory, setBackupDirectory] = useState<string | null>(null);

  useEffect(() => {
    apiClient.getSnapshot().then(setCurrentSnapshot).catch((err) => setErrorMessage(String(err)));
  }, []);

  async function handleApply() {
    setErrorMessage(null);
    setIsApplying(true);
    try {
      const response = await apiClient.applyPolicy({
        browserIds: selectedBrowsers,
        allowlistSites,
        advancedOptions,
      });
      if (!response.success) {
        setErrorMessage(response.errorMessage);
        return;
      }
      setBackupDirectory(response.backupDirectory);
      setCurrentSnapshot(response.resultingSnapshot);
    } catch (err) {
      setErrorMessage(err instanceof Error ? err.message : String(err));
    } finally {
      setIsApplying(false);
    }
  }

  return (
    <div className="step4-page">
      <h1 className="page-title">步驟 4：套用前確認所有安全性設定</h1>
      <p className="page-description">
        請仔細檢視即將寫入 Windows 系統政策 (Group Policy / Registry) 的防護規則。確認無誤後即可進行系統授權套用。
      </p>

      <div className="card info-banner">
        <strong>這個步驟需要系統管理員權限</strong>
        <p>點擊下方「確認並套用」後，寫入登錄檔的動作全部發生在已提權的本機服務中，不會再另外跳出視窗要求輸入密碼。</p>
      </div>

      {errorMessage && <div className="card banner-error">套用失敗：{errorMessage}</div>}

      <div className="card summary-row">
        目標瀏覽器政策 ({selectedBrowsers.length} 套) ・ 允許訪問網站清單 ({allowlistSites.length} 個網域)
      </div>

      {currentSnapshot && (
        <div className="card current-state">
          <strong>目前登錄檔實際狀態（套用前）</strong>
          {currentSnapshot.browsers.map((browser) => (
            <div key={browser.browserId} className="current-state-row">
              {browser.browserId}：{browser.policyKeyExists ? `已有 ${browser.allowedUrls.length} 筆允許網址` : "尚未設定過"}
            </div>
          ))}
        </div>
      )}

      <label className="acknowledge-row">
        <input type="checkbox" checked={acknowledged} onChange={(event) => setAcknowledged(event.target.checked)} />
        我已仔細檢閱上述政策項目，確認立刻套用上述防護標準
      </label>

      {backupDirectory && <p className="backup-note">上次備份已儲存至：{backupDirectory}</p>}

      <button
        className="btn btn-primary"
        disabled={!canApply || !acknowledged || isApplying}
        onClick={handleApply}
      >
        {isApplying ? "套用中…" : "確認並套用"}
      </button>
    </div>
  );
}
```

```css
/* frontend/src/pages/Step4ConfirmPage.css */
.page-title { font: var(--font-headline-md); color: var(--color-text-primary); margin-bottom: var(--spacing-sm); }
.page-description { color: var(--color-text-body); margin-bottom: var(--spacing-lg); }

.info-banner { background: var(--color-tertiary-tint); margin-bottom: var(--spacing-md); }
.info-banner strong { color: var(--color-text-primary); }
.info-banner p { margin-top: var(--spacing-xs); font: var(--font-body-sm); color: var(--color-text-body); }

.banner-error { background: var(--color-warning-tint); color: var(--color-warning-strong); margin-bottom: var(--spacing-md); }

.summary-row { margin-bottom: var(--spacing-md); font: var(--font-label-md); color: var(--color-text-primary); }

.current-state { margin-bottom: var(--spacing-lg); }
.current-state strong { color: var(--color-text-primary); }
.current-state-row { font: var(--font-body-sm); color: var(--color-text-muted); margin-top: var(--spacing-xs); }

.acknowledge-row { display: flex; align-items: center; gap: var(--spacing-sm); margin-bottom: var(--spacing-md); font: var(--font-body-md); }
.backup-note { color: var(--color-text-muted); font: var(--font-body-sm); margin-bottom: var(--spacing-md); }
```

- [ ] **Step 2: Verify build**

```bash
cd frontend
npm run build
```

- [ ] **Step 3: Commit**

```bash
git add frontend/src/pages/Step4ConfirmPage.tsx frontend/src/pages/Step4ConfirmPage.css
git commit -m "feat: add Step4ConfirmPage wired to POST /api/policy/apply"
```

---

### Task 7: Step 5 (complete and verify) page

**Files:**
- Create: `frontend/src/pages/Step5CompletePage.tsx`, `frontend/src/pages/Step5CompletePage.css`

**Interfaces:**
- Consumes: `useWizard()`, `apiClient.getSnapshot()`.
- Implements Global Constraint #2 (registry-diff "written and confirmed" rows, never "site tested reachable") and #3 (manual browser-open action, zero auto-verification).

- [ ] **Step 1: Step5CompletePage**

```tsx
// frontend/src/pages/Step5CompletePage.tsx
import { useEffect, useState } from "react";
import { apiClient } from "../api/client";
import { useWizard } from "../state/WizardContext";
import type { PolicySnapshot } from "../api/types";
import "./Step5CompletePage.css";

interface VerificationRow {
  label: string;
  passed: boolean;
  detail: string;
}

function buildVerificationRows(snapshot: PolicySnapshot, expectedDomains: string[], expectIncognitoDisabled: boolean): VerificationRow[] {
  const sortedExpected = [...expectedDomains].sort();
  const rows: VerificationRow[] = [];

  for (const browser of snapshot.browsers) {
    rows.push({
      label: `${browser.browserId}：政策機碼已寫入`,
      passed: browser.policyKeyExists,
      detail: browser.policyKeyExists ? "登錄機碼存在並可讀取。" : "尚未偵測到此瀏覽器的政策機碼。",
    });

    const sortedActual = [...browser.allowedUrls].sort();
    const domainsMatch = JSON.stringify(sortedExpected) === JSON.stringify(sortedActual);
    rows.push({
      label: `${browser.browserId}：允許清單機碼已寫入並核對`,
      passed: domainsMatch,
      detail: domainsMatch
        ? `URLAllowlist 內容與設定精靈一致，共 ${sortedActual.length} 筆。`
        : "URLAllowlist 內容與設定精靈不一致，請重新套用一次。",
    });

    rows.push({
      label: `${browser.browserId}：無痕模式機碼已核對`,
      passed: browser.incognitoDisabled === expectIncognitoDisabled,
      detail: `IncognitoModeAvailability/InPrivateModeAvailability 目前值：${browser.incognitoDisabled}`,
    });
  }

  return rows;
}

export function Step5CompletePage() {
  const { allowlistSites, advancedOptions } = useWizard();
  const [rows, setRows] = useState<VerificationRow[]>([]);
  const [error, setError] = useState<string | null>(null);

  const expectedDomains = allowlistSites.map((site) => site.domain);

  async function runVerification() {
    setError(null);
    try {
      const snapshot = await apiClient.getSnapshot();
      setRows(buildVerificationRows(snapshot, expectedDomains, advancedOptions.disableIncognito));
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    }
  }

  useEffect(() => {
    runVerification();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  function openAllowedSiteManually() {
    const firstSite = allowlistSites[0];
    if (!firstSite) return;
    // Fire-and-forget by design: this button's only job is to open the
    // browser for the parent to look at. The app cannot read what happens
    // inside the browser afterward (no block-count/log API exists for
    // Chromium's URLBlocklist policy), so no result is captured, polled,
    // or checked off — see spec 資料流 step 5, and Global Constraint #3.
    window.open(`https://${firstSite.domain}`, "_blank");
  }

  return (
    <div className="step5-page">
      <div className="card banner-success">
        <strong>保護已成功啟用！</strong> 已將設定寫入 Windows 登錄檔原則。
      </div>

      <h2 className="section-title">系統防護自動驗證報告</h2>
      <p className="page-description">
        以下逐項核對登錄機碼是否確實寫入並與設定精靈一致。本工具不會、也無法測試瀏覽器實際能否開啟或封鎖任何網站——Chromium
        政策沒有可讀的攔截紀錄。
      </p>

      {error && <div className="card banner-error">{error}</div>}

      <div className="verification-list">
        {rows.map((row) => (
          <div key={row.label} className="card verification-row">
            <div>
              <div className="verification-label">{row.label}</div>
              <div className="verification-detail">{row.detail}</div>
            </div>
            <span className={`pill ${row.passed ? "pill-success" : "pill-warning"}`}>
              {row.passed ? "驗證通過" : "未通過"}
            </span>
          </div>
        ))}
      </div>

      <button className="btn btn-secondary" onClick={runVerification}>重新驗證</button>

      <div className="card info-banner">
        <strong>想親眼確認？</strong>
        <p>按下方按鈕會用系統預設瀏覽器開啟一個已允許的網址，讓您親自檢查。本工具不會自動判讀開啟結果。</p>
      </div>
      <button className="btn btn-secondary" onClick={openAllowedSiteManually} disabled={allowlistSites.length === 0}>
        開啟已允許的網址 (手動檢查)
      </button>
    </div>
  );
}
```

```css
/* frontend/src/pages/Step5CompletePage.css */
.banner-success { background: var(--color-secondary-tint); margin-bottom: var(--spacing-lg); }
.banner-success strong { color: var(--color-text-primary); }
.banner-error { background: var(--color-warning-tint); color: var(--color-warning-strong); margin-bottom: var(--spacing-md); }

.section-title { font: var(--font-headline-sm); color: var(--color-text-primary); margin-bottom: var(--spacing-xs); }
.page-description { color: var(--color-text-muted); font: var(--font-body-sm); margin-bottom: var(--spacing-md); }

.verification-list { display: flex; flex-direction: column; gap: var(--spacing-sm); margin-bottom: var(--spacing-md); }
.verification-row { display: flex; justify-content: space-between; align-items: center; }
.verification-label { font: var(--font-label-md); color: var(--color-text-primary); }
.verification-detail { font: var(--font-body-sm); color: var(--color-text-muted); margin-top: 2px; }

.info-banner { background: var(--color-tertiary-tint); margin: var(--spacing-md) 0; }
.info-banner strong { color: var(--color-text-primary); }
.info-banner p { margin-top: var(--spacing-xs); font: var(--font-body-sm); color: var(--color-text-body); }
```

- [ ] **Step 2: Verify build**

```bash
cd frontend
npm run build
```

- [ ] **Step 3: Commit**

```bash
git add frontend/src/pages/Step5CompletePage.tsx frontend/src/pages/Step5CompletePage.css
git commit -m "feat: add Step5CompletePage with registry-diff verification, no fabricated behavioral stats"
```

---

### Task 8: Electron shell — spawns the API, loads the SPA, IPC token bridge

**Files:**
- Create: `electron/main.ts`, `electron/preload.ts`, `electron/tsconfig.json`
- Modify: `frontend/package.json` (add Electron dependencies and scripts — Electron's `package.json`/build tooling lives alongside the frontend it wraps, per the standard `electron-vite`-adjacent convention, rather than as a third sibling directory, to keep one `npm install` covering both)
- Modify: `frontend/vite.config.ts`

**Interfaces:**
- Produces: `window.electronApi.getApiToken()`, consumed by `frontend/src/api/client.ts` (Task 2, Step 5).

**Elevation note — the single riskiest assumption in this plan, read carefully:** A manifest embedded in an executable (`app.manifest` with `requestedExecutionLevel level="requireAdministrator"`) is enforced by Windows' process-creation path (`CreateProcess`) itself, not by whatever spawned it. This means when Electron's main process (running as a normal, non-elevated user process — Electron does **not** need or want its own elevation) calls Node's `child_process.spawn()` on `WebsiteWhitelistManual.Api.exe`, Windows sees that target executable's embedded manifest at `CreateProcess` time and triggers the UAC consent prompt for that child process specifically — the same mechanism as double-clicking the exe directly in Explorer. This is standard, documented Windows behavior (manifest-based elevation is a property of the target executable, not of the calling process or how it's invoked) and is not Electron-specific or unusual — it is exactly how the original WPF app's own `app.manifest` worked when the user double-clicked it, just one process-spawn layer removed. The one thing to verify on the user's actual Windows machine (this cannot be verified from here) is that `child_process.spawn` doesn't pass a flag that suppresses this (it doesn't, by default — `shell: false`, the default, spawns the target directly via `CreateProcess`-equivalent Node bindings, preserving manifest-based elevation; only if `shell: true` were used would the child run under `cmd.exe`, which could complicate — but not prevent — elevation). Confirm this in Task 9's manual verification checklist as the very first check, since if it somehow doesn't fire the prompt, the whole packaging approach needs revisiting (the fallback would be `child_process.exec` with an explicit `runas` verb via a helper, which is uglier and not needed unless Step 3 of Task 9's checklist fails).

- [ ] **Step 1: Add Electron dependencies**

```bash
cd frontend
npm install --save-dev electron electron-builder concurrently wait-on
cd ..
```

- [ ] **Step 2: Electron main process** — spawns the API, resolves its path for both dev and packaged builds, tears it down on quit, serves the token to the renderer over IPC

```typescript
// electron/main.ts
import { app, BrowserWindow, ipcMain } from "electron";
import { spawn, type ChildProcess } from "node:child_process";
import path from "node:path";
import fs from "node:fs/promises";
import os from "node:os";

const isDev = !app.isPackaged;
const API_TOKEN_PATH = path.join(
  os.homedir(),
  "AppData", "Local", "WebsiteWhitelistManual", "api-token.txt",
);

let apiProcess: ChildProcess | null = null;
let mainWindow: BrowserWindow | null = null;

function resolveApiExecutablePath(): string {
  // In development, the API is expected to already be running via a
  // separate `dotnet run` (see Task 9's dev-workflow doc) — Electron does
  // not spawn it itself in that mode, since `dotnet run` in dev rebuilds
  // faster than restarting a packaged exe on every change. In a packaged
  // build, electron-builder's extraResources config (Task 9) copies the
  // published self-contained API executable into
  // <installDir>/resources/api/WebsiteWhitelistManual.Api.exe.
  return path.join(process.resourcesPath, "api", "WebsiteWhitelistManual.Api.exe");
}

function startApiProcessIfPackaged(): void {
  if (isDev) {
    return;
  }
  const exePath = resolveApiExecutablePath();
  apiProcess = spawn(exePath, [], {
    // shell:false (the default) is required here — it spawns the target
    // executable directly so Windows honors its embedded app.manifest's
    // requireAdministrator elevation. Setting shell:true would run it
    // under cmd.exe instead, which complicates (though does not
    // necessarily break) manifest-based elevation — do not add it.
    stdio: "ignore",
  });
  apiProcess.on("error", (err) => {
    console.error("Failed to start WebsiteWhitelistManual.Api:", err);
  });
}

async function waitForApiToken(maxAttempts = 40, delayMs = 250): Promise<void> {
  for (let attempt = 0; attempt < maxAttempts; attempt++) {
    try {
      await fs.access(API_TOKEN_PATH);
      return;
    } catch {
      await new Promise((resolve) => setTimeout(resolve, delayMs));
    }
  }
  throw new Error(
    `API token file did not appear at ${API_TOKEN_PATH} after ${maxAttempts * delayMs}ms — ` +
    "the API process may have failed to start, or the UAC prompt was dismissed.",
  );
}

function createMainWindow(): void {
  mainWindow = new BrowserWindow({
    width: 1024,
    height: 720,
    webPreferences: {
      preload: path.join(__dirname, "preload.js"),
      contextIsolation: true,
      nodeIntegration: false,
    },
  });

  if (isDev) {
    mainWindow.loadURL("http://localhost:5173");
  } else {
    mainWindow.loadFile(path.join(process.resourcesPath, "app", "index.html"));
  }
}

app.whenReady().then(async () => {
  startApiProcessIfPackaged();
  try {
    await waitForApiToken();
  } catch (err) {
    console.error(err);
    // The renderer's own error handling (client.ts) surfaces a clear
    // message per-request rather than this main-process code blocking
    // window creation entirely — the window still opens so the user can
    // at least see the app and any in-page error state.
  }
  createMainWindow();

  ipcMain.handle("get-api-token", async () => {
    return fs.readFile(API_TOKEN_PATH, "utf-8");
  });
});

app.on("window-all-closed", () => {
  if (process.platform !== "darwin") {
    app.quit();
  }
});

app.on("before-quit", () => {
  apiProcess?.kill();
});
```

- [ ] **Step 3: Preload script** — the only bridge between the isolated renderer and Node/Electron APIs

```typescript
// electron/preload.ts
import { contextBridge, ipcRenderer } from "electron";

contextBridge.exposeInMainWorld("electronApi", {
  getApiToken: () => ipcRenderer.invoke("get-api-token"),
});
```

- [ ] **Step 4: Electron TypeScript config** (separate from the frontend's, since this compiles to CommonJS for Node/Electron's main process, not ES modules for the browser)

```json
// electron/tsconfig.json
{
  "compilerOptions": {
    "target": "ES2022",
    "module": "CommonJS",
    "outDir": "../frontend/dist-electron",
    "strict": true,
    "esModuleInterop": true,
    "skipLibCheck": true,
    "types": ["node"]
  },
  "include": ["main.ts", "preload.ts"]
}
```

- [ ] **Step 5: Wire up `package.json` scripts** — add to `frontend/package.json`'s existing `scripts` section (do not replace the whole file; this only adds/modifies the `scripts` block and adds a `main` field plus new `devDependencies` already installed in Step 1)

```json
{
  "main": "dist-electron/main.js",
  "scripts": {
    "dev": "vite",
    "build": "tsc -b && vite build",
    "build:electron": "tsc -p ../electron/tsconfig.json",
    "electron:dev": "concurrently -k \"npm run dev\" \"wait-on http://localhost:5173 && npm run build:electron && electron .\"",
    "electron:build": "npm run build && npm run build:electron && electron-builder"
  }
}
```

- [ ] **Step 6: Verify the dev workflow end-to-end** (Windows-only — needs the API from Task 1 actually elevatable)

In one terminal:
```powershell
dotnet run --project src/WebsiteWhitelistManual.Api
```

In a second terminal:
```powershell
cd frontend
npm run electron:dev
```

Expected: a UAC prompt for the `dotnet run` terminal (same as Task 1's standalone smoke test), then an Electron window opens loading the Vite dev server, and the Dashboard page successfully calls the API using a token obtained via `window.electronApi.getApiToken()` — no `localStorage` fallback needed this time, confirming the IPC bridge works.

- [ ] **Step 7: Commit**

```bash
git add electron frontend/package.json frontend/package-lock.json
git commit -m "feat: add Electron main process spawning the .NET API and bridging its token via preload IPC"
```

---

### Task 9: Packaging, publish/verification docs

**Files:**
- Create: `frontend/electron-builder.yml`
- Create: `PUBLISHING.md` (replaces the deleted WPF-era file)
- Create: `WINDOWS_VERIFICATION.md` (replaces the deleted WPF-era file)

**Interfaces:** none — packaging config and documentation only.

- [ ] **Step 1: `electron-builder` config** — bundles the React build, the Electron main/preload output, and a `dotnet publish --self-contained` build of the API into one Windows installer

```yaml
# frontend/electron-builder.yml
appId: com.example.websitewhitelistmanual
productName: 網站白名單設定工具
directories:
  output: ../dist-installer
files:
  - dist/**/*
  - dist-electron/**/*
extraResources:
  - from: ../publish/api
    to: api
win:
  target: nsis
  artifactName: ${productName}-${version}-setup.${ext}
nsis:
  oneClick: false
  allowToChangeInstallationDirectory: true
```

`files` maps to `resources/app/` inside the packaged output by electron-builder's default convention, matching `main.ts`'s `loadFile(path.join(process.resourcesPath, "app", "index.html"))` from Task 8.

- [ ] **Step 2: Publish the API as a self-contained executable before packaging** (this step is a manual pre-packaging command the developer runs, not something electron-builder does itself — document it clearly in `PUBLISHING.md` below)

```powershell
dotnet publish src/WebsiteWhitelistManual.Api/WebsiteWhitelistManual.Api.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -o publish/api
```

- [ ] **Step 3: `PUBLISHING.md`**

```markdown
# 建置與發佈說明

這份工具現在分成三塊：`WebsiteWhitelistManual.Core`（C# 登錄檔邏輯，跨平台可測）、`WebsiteWhitelistManual.Api`（C# 本機 HTTP API，Windows-only，實際讀寫登錄檔的地方）、`frontend/`（React + Electron，畫面）。三塊都要 build 過才能組成完整的安裝檔。

## 開發模式（改程式碼時用）

1. 啟動 API（會跳 UAC，這是預期行為）：
   ```
   dotnet run --project src/WebsiteWhitelistManual.Api
   ```
2. 啟動前端 + Electron：
   ```
   cd frontend
   npm run electron:dev
   ```

畫面改了會自動熱重載；API 程式碼改了要重新 `dotnet run`。

## 正式打包（產生給家長雙擊安裝的 .exe）

1. 發佈 API 為單一自帶執行檔：
   ```
   dotnet publish src/WebsiteWhitelistManual.Api/WebsiteWhitelistManual.Api.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/api
   ```
2. 打包 Electron 安裝檔：
   ```
   cd frontend
   npm run electron:build
   ```
3. 安裝檔輸出在 `dist-installer/`，檔名類似 `網站白名單設定工具-1.0.0-setup.exe`。

## 這一版要驗證什麼

見 `WINDOWS_VERIFICATION.md`。

## 回報方式

- **`dotnet build`/`npm run build` 編譯期錯誤**：把完整錯誤訊息貼回來，我來修。
- **UAC 沒跳、畫面打不開、按鈕沒反應**：截圖 + 描述症狀，我來對照修。
```

- [ ] **Step 4: `WINDOWS_VERIFICATION.md`**

```markdown
# Electron + React + .NET API — Windows 手動驗證清單

`dotnet build`/`npm run build` 都已經在開發端過關，但 UAC 提權、Electron 生子行程、真正的登錄檔讀寫，只能在你自己的 Windows 機器上驗證。請照下面順序點過一次。

## 0. 最重要的一項：確認 UAC 真的會為 API 子行程跳出來

這是整個架構唯一沒辦法在非 Windows 環境驗證的假設：Electron（不需要提權）呼叫 `child_process.spawn()` 啟動帶有 `app.manifest`（`requireAdministrator`）的 `WebsiteWhitelistManual.Api.exe` 時，Windows 應該會對**這個子行程**單獨跳出 UAC 提示，就跟直接雙擊那個 .exe 一樣。

**測試方式**：執行 `npm run electron:dev`（開發模式）或雙擊打包後的安裝程式安裝完成後開啟主程式，確認：
1. 有跳出 UAC 提示（或這台機器已設定永遠允許，就直接以系統管理員身分啟動）。
2. 工作管理員裡看得到兩個行程：`WebsiteWhitelistManual App.exe`（一般權限）和 `WebsiteWhitelistManual.Api.exe`（詳細資料頁籤裡「提高的權限」欄位顯示「是」）。

如果 UAC 完全沒有跳出、也沒有自動提權，回報這個結果，這代表 Electron 生行程的方式需要換一個做法（例如改用明確的 `runas` 提權），不要略過這一步直接往下測。

## 1. 啟動與導覽

1. 開啟主程式，確認視窗出現，左側導覽跟 Stitch 稿子一樣：「首頁」在最上面，下面是「設定精靈導覽」標題（不可點擊），底下 5 個步驟項目。
2. 依序點開全部 6 個項目，確認每個都顯示對應內容且畫面風格接近 Stitch 截圖（卡片、配色、間距），不是空白或明顯陽春的版面。

## 2. 首頁 (Dashboard)

1. 確認「目前防護狀態」與「目前允許 N 個網站」反映登錄檔真實現況（初次應為「尚未設定」「0 個網站」，除非這台機器先前已手動設定過）。
2. 確認「本機使用者帳號」清單包含這台機器上的所有帳號，含 Administrator 等內建帳號。
3. 點「查看完整設定值 (進階)」，確認彈出對話框顯示可讀的機碼內容。

## 3. 步驟 1-3

1. 勾選 Edge 和/或 Chrome，切到別的分頁再切回來，確認勾選狀態還在（`WizardContext` 正確保留狀態）。
2. 到「允許的網站」輸入至少一個網域；嘗試輸入空白或帶 `http://` 的網址，確認出現對應錯誤訊息且不會被加入清單。
3. 到「進階選項」確認三個開關預設都是開啟，且可以正常切換。

## 4. 步驟 4（套用前確認）——**這一步會真正寫入登錄檔，請只在你自己的機器或朋友的筆電上做，不要在公司電腦上測**

1. 勾選「我已仔細檢閱...」，確認按鈕從停用變成可按（若步驟 1 或步驟 2 沒有資料，按鈕應保持停用）。
2. 按下「確認並套用」，確認幾秒內完成、沒有紅色錯誤訊息，且顯示備份路徑。
3. 打開 `%LOCALAPPDATA%\WebsiteWhitelistManual\Backups\<時間戳記>\`，確認裡面有 `Edge.reg`／`Chrome.reg`。
4. 用 `regedit` 手動核對 `HKLM\SOFTWARE\Policies\Microsoft\Edge`（或 `\Google\Chrome`）底下的機碼是否跟精靈設定一致。

## 5. 步驟 5（完成與驗證）

1. 確認每一列驗證結果顯示「驗證通過」，文字是「機碼已寫入並核對」，**不會**出現「已測試網站可開啟」「HTTP 200」字眼。
2. 按「開啟已允許的網址 (手動檢查)」，確認系統預設瀏覽器真的開啟一個剛加入的網址。
3. 按「重新驗證」，確認清單重新讀一次登錄檔並維持「驗證通過」。

## 6. 關閉程式

1. 關閉主視窗，確認工作管理員裡 `WebsiteWhitelistManual.Api.exe` 也一併結束（`before-quit` 的 `apiProcess.kill()` 有生效），不會變成孤兒行程留在背景佔用 5292 埠。

## 回報方式

- **編譯期錯誤**：把完整錯誤訊息貼回來。
- **畫面跟預期不同、當機、按鈕沒反應**：截圖 + 描述在哪一步發生。
- **UAC 沒有正確跳出（見第 0 項）**：這是最優先要回報的項目，會影響整個打包方式是否可行。
```

- [ ] **Step 5: Commit**

```bash
git add frontend/electron-builder.yml PUBLISHING.md WINDOWS_VERIFICATION.md
git commit -m "docs: add electron-builder packaging config and updated build/verification guides"
```

---

## After this plan

`WebsiteWhitelistManual.Core` is unchanged (59 tests, untouched). A new `WebsiteWhitelistManual.Api` project wraps it in a loopback-only, shared-secret-protected HTTP API using the three Windows-real adapters recovered verbatim from the deleted WPF project's git history. A new `frontend/` React SPA renders all 6 pages styled per the Guardian Clear design tokens, wrapped in Electron for single-exe distribution, with the same non-fabricated-data rules enforced as the WPF plan had. The user takes over at `WINDOWS_VERIFICATION.md` — most importantly its Step 0, confirming UAC actually elevates a manifest-carrying child process spawned by Electron, since that is the one assumption in this entire plan that could not be verified outside a real Windows machine.

Known follow-ups deliberately deferred (unchanged from the WPF plan's equivalent list, since none of them are UI-technology-specific):
- No in-app one-click registry restore (parents import the `.reg` backup manually).
- No `.txt`/`.csv` bulk import for the allowlist.
- `IProcessRunner.Run` still has no true process-kill-on-timeout — the API's `Task.WhenAny` bounds the caller's wait but a genuinely hung `reg.exe` process is not killed.
- The shared-secret token approach (Task 1) is "good enough for a loopback parental-control tool," not a hardened auth system — do not extend it without reconsidering whether the added complexity is actually warranted.
