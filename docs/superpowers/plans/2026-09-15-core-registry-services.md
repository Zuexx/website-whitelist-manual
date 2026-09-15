# Core Registry Services Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the platform-independent `WebsiteWhitelistManual.Core` class library — the models, registry-policy read/write/backup services, and local-account inspection logic the WPF wizard will call — fully covered by unit tests that run and pass on macOS via `dotnet test`.

**Architecture:** All registry and process access goes through two thin abstractions, `IWindowsRegistry` and `IProcessRunner`, that the real Windows implementations (added in a later, Windows-only plan) will satisfy with `Microsoft.Win32.Registry` and `System.Diagnostics.Process`. This plan only builds and tests the interfaces, the pure domain logic, and in-memory fakes — no code in this plan touches the real Windows registry, so every task is verifiable on this machine right now.

**Tech Stack:** .NET 10 SDK (`net10.0` TFM, no `-windows` suffix — this library stays cross-platform on purpose), xUnit for tests.

**Spec:** `docs/superpowers/specs/2026-09-15-website-whitelist-wpf-app-design.md`

## Global Constraints

- Target framework for every project in this plan: `net10.0` (plain, not `net10.0-windows` — that suffix is reserved for the future WPF app project).
- `Nullable` and `ImplicitUsings` enabled on every project.
- Registry key names and values must match the spec's canonical table exactly: `URLBlocklist`, `URLAllowlist`, `InPrivateModeAvailability` (Edge)/`IncognitoModeAvailability` (Chrome), `BrowserSignin` (0 = disabled), `DeveloperToolsAvailability` (2 = disabled). Never use `BrowserGuestModeEnabled` — that name appeared only in the Stitch mockup and was never verified against `manual.html`.
- No code in this plan may call `Microsoft.Win32.Registry`, `System.Diagnostics.Process`, or `System.DirectoryServices.AccountManagement` directly — those are Windows-only and belong to the future WPF app project. Everything here goes through `IWindowsRegistry` / `IProcessRunner` / `ILocalAccountSource`.
- Every task must end with a green `dotnet test` run pasted into the step output before moving on.

## Scope note (read before starting)

The full spec covers both this cross-platform Core library and a WPF UI shell (`FluentWindow` + `NavigationView`, 6 pages). Per `HANDOFF.md`'s established constraint, WPF projects cannot build on macOS (`PresentationBuildTasks` is Windows-only), so nothing in that half of the spec can be verified in this environment — not even a successful build, let alone a test. This plan therefore covers **only** the Core library: models, registry read/write/backup services, and local-account inspection, all behind interfaces with in-memory fakes, so every single task here has a real, machine-checked green test run. The WPF app project (the actual `IWindowsRegistry`/`IProcessRunner`/`ILocalAccountSource` implementations backed by real Windows APIs, plus all XAML pages) is a separate follow-up plan, written next and verified by the user on Windows per the existing HANDOFF.md workflow.

---

### Task 1: Solution and project scaffolding

**Files:**
- Create: `WebsiteWhitelistManual.sln`
- Create: `src/WebsiteWhitelistManual.Core/WebsiteWhitelistManual.Core.csproj`
- Create: `tests/WebsiteWhitelistManual.Core.Tests/WebsiteWhitelistManual.Core.Tests.csproj`

**Interfaces:**
- Produces: an empty `WebsiteWhitelistManual.Core` project that `WebsiteWhitelistManual.Core.Tests` references, both building under `net10.0`.

- [ ] **Step 1: Scaffold the class library**

```bash
mkdir -p src tests
dotnet new classlib -n WebsiteWhitelistManual.Core -o src/WebsiteWhitelistManual.Core -f net10.0
```

Open `src/WebsiteWhitelistManual.Core/WebsiteWhitelistManual.Core.csproj` and confirm it looks like this (add `Nullable`/`ImplicitUsings` if the template didn't already set them):

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>WebsiteWhitelistManual.Core</RootNamespace>
  </PropertyGroup>

</Project>
```

Delete the template's `Class1.cs` — this library starts empty:

```bash
rm src/WebsiteWhitelistManual.Core/Class1.cs
```

- [ ] **Step 2: Scaffold the test project**

```bash
dotnet new xunit -n WebsiteWhitelistManual.Core.Tests -o tests/WebsiteWhitelistManual.Core.Tests -f net10.0
cd tests/WebsiteWhitelistManual.Core.Tests
dotnet add reference ../../src/WebsiteWhitelistManual.Core/WebsiteWhitelistManual.Core.csproj
cd ../..
rm tests/WebsiteWhitelistManual.Core.Tests/UnitTest1.cs
```

- [ ] **Step 3: Create the solution and add both projects**

```bash
dotnet new sln -n WebsiteWhitelistManual
dotnet sln add src/WebsiteWhitelistManual.Core/WebsiteWhitelistManual.Core.csproj
dotnet sln add tests/WebsiteWhitelistManual.Core.Tests/WebsiteWhitelistManual.Core.Tests.csproj
```

- [ ] **Step 4: Verify it builds and tests run (zero tests, but the runner must report success)**

Run: `dotnet test`
Expected: `Passed! ... Total: 0` (no test files exist yet, so 0 tests is correct here — the goal is confirming restore/build/test-discovery all work before any real code exists)

- [ ] **Step 5: Commit**

```bash
git add WebsiteWhitelistManual.sln src/WebsiteWhitelistManual.Core tests/WebsiteWhitelistManual.Core.Tests
git commit -m "chore: scaffold Core class library and test project"
```

---

### Task 2: Policy key constants and browser target model

**Files:**
- Create: `src/WebsiteWhitelistManual.Core/PolicyKeys.cs`
- Create: `src/WebsiteWhitelistManual.Core/Models/BrowserId.cs`
- Create: `src/WebsiteWhitelistManual.Core/Models/BrowserTarget.cs`
- Test: `tests/WebsiteWhitelistManual.Core.Tests/BrowserTargetTests.cs`

**Interfaces:**
- Produces: `PolicyKeys` (static constants), `BrowserId` enum (`Edge`, `Chrome`), `BrowserTarget` class with `Id`, `RootPath`, `IncognitoValueName`, `DisplayName`, static `Edge`/`Chrome` instances, and `BrowserTarget.FromId(BrowserId)`.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/WebsiteWhitelistManual.Core.Tests/BrowserTargetTests.cs
using WebsiteWhitelistManual.Core.Models;
using Xunit;

namespace WebsiteWhitelistManual.Core.Tests;

public class BrowserTargetTests
{
    [Fact]
    public void Edge_HasEdgeRegistryPathAndIncognitoValueName()
    {
        var target = BrowserTarget.Edge;

        Assert.Equal(BrowserId.Edge, target.Id);
        Assert.Equal(@"SOFTWARE\Policies\Microsoft\Edge", target.RootPath);
        Assert.Equal("InPrivateModeAvailability", target.IncognitoValueName);
        Assert.Equal("Microsoft Edge", target.DisplayName);
    }

    [Fact]
    public void Chrome_HasChromeRegistryPathAndIncognitoValueName()
    {
        var target = BrowserTarget.Chrome;

        Assert.Equal(BrowserId.Chrome, target.Id);
        Assert.Equal(@"SOFTWARE\Policies\Google\Chrome", target.RootPath);
        Assert.Equal("IncognitoModeAvailability", target.IncognitoValueName);
        Assert.Equal("Google Chrome", target.DisplayName);
    }

    [Theory]
    [InlineData(BrowserId.Edge)]
    [InlineData(BrowserId.Chrome)]
    public void FromId_ReturnsMatchingTarget(BrowserId id)
    {
        var target = BrowserTarget.FromId(id);

        Assert.Equal(id, target.Id);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail to compile**

Run: `dotnet test`
Expected: build error — `PolicyKeys`, `BrowserId`, `BrowserTarget` do not exist yet.

- [ ] **Step 3: Implement `PolicyKeys`**

```csharp
// src/WebsiteWhitelistManual.Core/PolicyKeys.cs
namespace WebsiteWhitelistManual.Core;

/// <summary>
/// Canonical registry key/value names, verified against manual.html.
/// Never use "BrowserGuestModeEnabled" — that name only ever appeared
/// in an unverified UI mockup, not the source-of-truth manual.
/// </summary>
public static class PolicyKeys
{
    public const string EdgeRootPath = @"SOFTWARE\Policies\Microsoft\Edge";
    public const string ChromeRootPath = @"SOFTWARE\Policies\Google\Chrome";

    public const string UrlBlocklistSubKey = "URLBlocklist";
    public const string UrlAllowlistSubKey = "URLAllowlist";

    public const string EdgeIncognitoValueName = "InPrivateModeAvailability";
    public const string ChromeIncognitoValueName = "IncognitoModeAvailability";

    public const string BrowserSigninValueName = "BrowserSignin";
    public const string DeveloperToolsAvailabilityValueName = "DeveloperToolsAvailability";
}
```

- [ ] **Step 4: Implement `BrowserId` and `BrowserTarget`**

```csharp
// src/WebsiteWhitelistManual.Core/Models/BrowserId.cs
namespace WebsiteWhitelistManual.Core.Models;

public enum BrowserId
{
    Edge,
    Chrome
}
```

```csharp
// src/WebsiteWhitelistManual.Core/Models/BrowserTarget.cs
namespace WebsiteWhitelistManual.Core.Models;

public sealed class BrowserTarget
{
    public BrowserId Id { get; }
    public string RootPath { get; }
    public string IncognitoValueName { get; }
    public string DisplayName { get; }

    private BrowserTarget(BrowserId id, string rootPath, string incognitoValueName, string displayName)
    {
        Id = id;
        RootPath = rootPath;
        IncognitoValueName = incognitoValueName;
        DisplayName = displayName;
    }

    public static BrowserTarget Edge { get; } = new(
        BrowserId.Edge, PolicyKeys.EdgeRootPath, PolicyKeys.EdgeIncognitoValueName, "Microsoft Edge");

    public static BrowserTarget Chrome { get; } = new(
        BrowserId.Chrome, PolicyKeys.ChromeRootPath, PolicyKeys.ChromeIncognitoValueName, "Google Chrome");

    public static BrowserTarget FromId(BrowserId id) => id switch
    {
        BrowserId.Edge => Edge,
        BrowserId.Chrome => Chrome,
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown browser id.")
    };
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test`
Expected: `Passed! ... Total: 4`

- [ ] **Step 6: Commit**

```bash
git add src/WebsiteWhitelistManual.Core/PolicyKeys.cs src/WebsiteWhitelistManual.Core/Models/BrowserId.cs src/WebsiteWhitelistManual.Core/Models/BrowserTarget.cs tests/WebsiteWhitelistManual.Core.Tests/BrowserTargetTests.cs
git commit -m "feat: add policy key constants and BrowserTarget model"
```

---

### Task 3: Registry abstraction and in-memory fake

**Files:**
- Create: `src/WebsiteWhitelistManual.Core/Abstractions/IWindowsRegistry.cs`
- Create: `tests/WebsiteWhitelistManual.Core.Tests/Fakes/FakeWindowsRegistry.cs`
- Test: `tests/WebsiteWhitelistManual.Core.Tests/Fakes/FakeWindowsRegistryTests.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `IWindowsRegistry` with `SubKeyExists(string)`, `EnsureSubKeyExists(string)`, `GetValueNames(string)`, `GetStringValue(string, string)`, `GetDwordValue(string, string)`, `SetStringValue(string, string, string)`, `SetDwordValue(string, string, int)`, `DeleteValue(string, string)`. All paths are relative to `HKEY_LOCAL_MACHINE` (e.g. `SOFTWARE\Policies\Microsoft\Edge\URLAllowlist`) — the interface never takes a hive, because this app only ever touches HKLM. `FakeWindowsRegistry` is a public test double every later test task will construct and pass to real services.

This task is worth its own gate because every later service's tests trust this fake to model real registry semantics (missing key ≠ empty string, deleting a value that doesn't exist is a no-op, etc.) — get it wrong here and every downstream test is testing against the wrong ground truth.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/WebsiteWhitelistManual.Core.Tests/Fakes/FakeWindowsRegistryTests.cs
using WebsiteWhitelistManual.Core.Tests.Fakes;
using Xunit;

namespace WebsiteWhitelistManual.Core.Tests.Fakes;

public class FakeWindowsRegistryTests
{
    [Fact]
    public void SubKeyExists_ReturnsFalse_ForKeyNeverCreated()
    {
        var registry = new FakeWindowsRegistry();

        Assert.False(registry.SubKeyExists(@"SOFTWARE\Policies\Microsoft\Edge"));
    }

    [Fact]
    public void EnsureSubKeyExists_MakesSubKeyExistsTrue()
    {
        var registry = new FakeWindowsRegistry();

        registry.EnsureSubKeyExists(@"SOFTWARE\Policies\Microsoft\Edge");

        Assert.True(registry.SubKeyExists(@"SOFTWARE\Policies\Microsoft\Edge"));
    }

    [Fact]
    public void SetAndGetStringValue_RoundTrips()
    {
        var registry = new FakeWindowsRegistry();
        registry.EnsureSubKeyExists(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist");

        registry.SetStringValue(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist", "1", "example.com");

        Assert.Equal("example.com", registry.GetStringValue(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist", "1"));
    }

    [Fact]
    public void GetStringValue_ReturnsNull_WhenValueMissing()
    {
        var registry = new FakeWindowsRegistry();
        registry.EnsureSubKeyExists(@"SOFTWARE\Policies\Microsoft\Edge");

        Assert.Null(registry.GetStringValue(@"SOFTWARE\Policies\Microsoft\Edge", "NoSuchValue"));
    }

    [Fact]
    public void SetAndGetDwordValue_RoundTrips()
    {
        var registry = new FakeWindowsRegistry();
        registry.EnsureSubKeyExists(@"SOFTWARE\Policies\Microsoft\Edge");

        registry.SetDwordValue(@"SOFTWARE\Policies\Microsoft\Edge", "BrowserSignin", 0);

        Assert.Equal(0, registry.GetDwordValue(@"SOFTWARE\Policies\Microsoft\Edge", "BrowserSignin"));
    }

    [Fact]
    public void GetDwordValue_ReturnsNull_WhenValueMissing()
    {
        var registry = new FakeWindowsRegistry();
        registry.EnsureSubKeyExists(@"SOFTWARE\Policies\Microsoft\Edge");

        Assert.Null(registry.GetDwordValue(@"SOFTWARE\Policies\Microsoft\Edge", "NoSuchValue"));
    }

    [Fact]
    public void GetValueNames_ReturnsAllNamesUnderSubKey()
    {
        var registry = new FakeWindowsRegistry();
        registry.EnsureSubKeyExists(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist");
        registry.SetStringValue(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist", "1", "example.com");
        registry.SetStringValue(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist", "2", "www.example.com");

        var names = registry.GetValueNames(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist");

        Assert.Equal(new[] { "1", "2" }, names);
    }

    [Fact]
    public void GetValueNames_ReturnsEmpty_ForSubKeyThatDoesNotExist()
    {
        var registry = new FakeWindowsRegistry();

        var names = registry.GetValueNames(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist");

        Assert.Empty(names);
    }

    [Fact]
    public void DeleteValue_RemovesValue()
    {
        var registry = new FakeWindowsRegistry();
        registry.EnsureSubKeyExists(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist");
        registry.SetStringValue(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist", "1", "example.com");

        registry.DeleteValue(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist", "1");

        Assert.Null(registry.GetStringValue(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist", "1"));
        Assert.Empty(registry.GetValueNames(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist"));
    }

    [Fact]
    public void DeleteValue_OnMissingValue_IsNoOp()
    {
        var registry = new FakeWindowsRegistry();
        registry.EnsureSubKeyExists(@"SOFTWARE\Policies\Microsoft\Edge");

        var exception = Record.Exception(() =>
            registry.DeleteValue(@"SOFTWARE\Policies\Microsoft\Edge", "NoSuchValue"));

        Assert.Null(exception);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail to compile**

Run: `dotnet test`
Expected: build error — `IWindowsRegistry` and `FakeWindowsRegistry` do not exist yet.

- [ ] **Step 3: Implement `IWindowsRegistry`**

```csharp
// src/WebsiteWhitelistManual.Core/Abstractions/IWindowsRegistry.cs
namespace WebsiteWhitelistManual.Core.Abstractions;

/// <summary>
/// Thin abstraction over HKEY_LOCAL_MACHINE. Every path is relative to HKLM,
/// e.g. "SOFTWARE\Policies\Microsoft\Edge\URLAllowlist". The app never reads
/// or writes any other hive, so the hive itself is not a parameter.
/// </summary>
public interface IWindowsRegistry
{
    bool SubKeyExists(string subKeyPath);
    void EnsureSubKeyExists(string subKeyPath);
    IReadOnlyList<string> GetValueNames(string subKeyPath);
    string? GetStringValue(string subKeyPath, string valueName);
    int? GetDwordValue(string subKeyPath, string valueName);
    void SetStringValue(string subKeyPath, string valueName, string value);
    void SetDwordValue(string subKeyPath, string valueName, int value);
    void DeleteValue(string subKeyPath, string valueName);
}
```

- [ ] **Step 4: Implement `FakeWindowsRegistry`**

```csharp
// tests/WebsiteWhitelistManual.Core.Tests/Fakes/FakeWindowsRegistry.cs
using WebsiteWhitelistManual.Core.Abstractions;

namespace WebsiteWhitelistManual.Core.Tests.Fakes;

public sealed class FakeWindowsRegistry : IWindowsRegistry
{
    private readonly Dictionary<string, Dictionary<string, object>> _store =
        new(StringComparer.OrdinalIgnoreCase);

    public bool SubKeyExists(string subKeyPath) => _store.ContainsKey(subKeyPath);

    public void EnsureSubKeyExists(string subKeyPath)
    {
        if (!_store.ContainsKey(subKeyPath))
        {
            _store[subKeyPath] = new Dictionary<string, object>();
        }
    }

    public IReadOnlyList<string> GetValueNames(string subKeyPath)
    {
        if (!_store.TryGetValue(subKeyPath, out var values))
        {
            return Array.Empty<string>();
        }
        return values.Keys.ToList();
    }

    public string? GetStringValue(string subKeyPath, string valueName)
    {
        if (_store.TryGetValue(subKeyPath, out var values) && values.TryGetValue(valueName, out var value))
        {
            return value as string;
        }
        return null;
    }

    public int? GetDwordValue(string subKeyPath, string valueName)
    {
        if (_store.TryGetValue(subKeyPath, out var values) && values.TryGetValue(valueName, out var value))
        {
            return value as int?;
        }
        return null;
    }

    public void SetStringValue(string subKeyPath, string valueName, string value)
    {
        EnsureSubKeyExists(subKeyPath);
        _store[subKeyPath][valueName] = value;
    }

    public void SetDwordValue(string subKeyPath, string valueName, int value)
    {
        EnsureSubKeyExists(subKeyPath);
        _store[subKeyPath][valueName] = value;
    }

    public void DeleteValue(string subKeyPath, string valueName)
    {
        if (_store.TryGetValue(subKeyPath, out var values))
        {
            values.Remove(valueName);
        }
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test`
Expected: `Passed! ... Total: 14` (4 from Task 2 + 10 new)

- [ ] **Step 6: Commit**

```bash
git add src/WebsiteWhitelistManual.Core/Abstractions/IWindowsRegistry.cs tests/WebsiteWhitelistManual.Core.Tests/Fakes/FakeWindowsRegistry.cs tests/WebsiteWhitelistManual.Core.Tests/Fakes/FakeWindowsRegistryTests.cs
git commit -m "feat: add IWindowsRegistry abstraction and in-memory fake"
```

---

### Task 4: Allowlist domain validation

**Files:**
- Create: `src/WebsiteWhitelistManual.Core/Models/AllowlistSite.cs`
- Test: `tests/WebsiteWhitelistManual.Core.Tests/AllowlistSiteTests.cs`

**Interfaces:**
- Produces: `AllowlistSite` with `Domain` and `CategoryLabel` properties, and `static bool TryCreate(string? rawInput, string? categoryLabel, out AllowlistSite? site, out string? error)`.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/WebsiteWhitelistManual.Core.Tests/AllowlistSiteTests.cs
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
```

- [ ] **Step 2: Run the tests to verify they fail to compile**

Run: `dotnet test`
Expected: build error — `AllowlistSite` does not exist yet.

- [ ] **Step 3: Implement `AllowlistSite`**

```csharp
// src/WebsiteWhitelistManual.Core/Models/AllowlistSite.cs
namespace WebsiteWhitelistManual.Core.Models;

public sealed class AllowlistSite
{
    public string Domain { get; }
    public string? CategoryLabel { get; }

    private AllowlistSite(string domain, string? categoryLabel)
    {
        Domain = domain;
        CategoryLabel = categoryLabel;
    }

    public static bool TryCreate(string? rawInput, string? categoryLabel, out AllowlistSite? site, out string? error)
    {
        site = null;

        if (string.IsNullOrWhiteSpace(rawInput))
        {
            error = "網域不能是空白。";
            return false;
        }

        var trimmed = rawInput.Trim();

        if (trimmed.Contains(' '))
        {
            error = "網域不能包含空格。";
            return false;
        }

        if (trimmed.Contains("://"))
        {
            error = "請只填網域，不要包含 http:// 或 https://。";
            return false;
        }

        if (!trimmed.Contains('.') || trimmed.StartsWith('.') || trimmed.EndsWith('.'))
        {
            error = "請輸入完整網域，例如 example.com。";
            return false;
        }

        var trimmedLabel = string.IsNullOrWhiteSpace(categoryLabel) ? null : categoryLabel.Trim();
        site = new AllowlistSite(trimmed, trimmedLabel);
        error = null;
        return true;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test`
Expected: `Passed! ... Total: 26` (14 from before + 12 new)

- [ ] **Step 5: Commit**

```bash
git add src/WebsiteWhitelistManual.Core/Models/AllowlistSite.cs tests/WebsiteWhitelistManual.Core.Tests/AllowlistSiteTests.cs
git commit -m "feat: add AllowlistSite domain validation"
```

---

### Task 5: Advanced options and wizard configuration gating

**Files:**
- Create: `src/WebsiteWhitelistManual.Core/Models/AdvancedOptionsState.cs`
- Create: `src/WebsiteWhitelistManual.Core/Models/WizardConfiguration.cs`
- Test: `tests/WebsiteWhitelistManual.Core.Tests/WizardConfigurationTests.cs`

**Interfaces:**
- Consumes: `BrowserTarget` (Task 2), `AllowlistSite` (Task 4).
- Produces: `AdvancedOptionsState` record with `DisableIncognito`, `DisableAccountSwitching`, `DisableDeveloperTools` (all default `true`). `WizardConfiguration` with `BrowserTargets`, `AllowlistSites`, `AdvancedOptions`, and computed `bool CanApply` (true only when at least one browser and at least one site are selected — this is what gates the Step 4 Apply button per the spec).

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/WebsiteWhitelistManual.Core.Tests/WizardConfigurationTests.cs
using WebsiteWhitelistManual.Core.Models;
using Xunit;

namespace WebsiteWhitelistManual.Core.Tests;

public class WizardConfigurationTests
{
    private static AllowlistSite MakeSite(string domain)
    {
        AllowlistSite.TryCreate(domain, null, out var site, out _);
        return site!;
    }

    [Fact]
    public void AdvancedOptionsState_DefaultsAllThreeProtectionsOn()
    {
        var options = new AdvancedOptionsState();

        Assert.True(options.DisableIncognito);
        Assert.True(options.DisableAccountSwitching);
        Assert.True(options.DisableDeveloperTools);
    }

    [Fact]
    public void CanApply_IsFalse_WhenNoBrowsersSelected()
    {
        var config = new WizardConfiguration(
            browserTargets: Array.Empty<BrowserTarget>(),
            allowlistSites: new[] { MakeSite("example.com") },
            advancedOptions: new AdvancedOptionsState());

        Assert.False(config.CanApply);
    }

    [Fact]
    public void CanApply_IsFalse_WhenNoSitesAdded()
    {
        var config = new WizardConfiguration(
            browserTargets: new[] { BrowserTarget.Edge },
            allowlistSites: Array.Empty<AllowlistSite>(),
            advancedOptions: new AdvancedOptionsState());

        Assert.False(config.CanApply);
    }

    [Fact]
    public void CanApply_IsTrue_WhenAtLeastOneBrowserAndOneSite()
    {
        var config = new WizardConfiguration(
            browserTargets: new[] { BrowserTarget.Edge, BrowserTarget.Chrome },
            allowlistSites: new[] { MakeSite("example.com") },
            advancedOptions: new AdvancedOptionsState());

        Assert.True(config.CanApply);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail to compile**

Run: `dotnet test`
Expected: build error — `AdvancedOptionsState` and `WizardConfiguration` do not exist yet.

- [ ] **Step 3: Implement `AdvancedOptionsState` and `WizardConfiguration`**

```csharp
// src/WebsiteWhitelistManual.Core/Models/AdvancedOptionsState.cs
namespace WebsiteWhitelistManual.Core.Models;

public sealed record AdvancedOptionsState(
    bool DisableIncognito = true,
    bool DisableAccountSwitching = true,
    bool DisableDeveloperTools = true);
```

```csharp
// src/WebsiteWhitelistManual.Core/Models/WizardConfiguration.cs
namespace WebsiteWhitelistManual.Core.Models;

public sealed class WizardConfiguration
{
    public IReadOnlyList<BrowserTarget> BrowserTargets { get; }
    public IReadOnlyList<AllowlistSite> AllowlistSites { get; }
    public AdvancedOptionsState AdvancedOptions { get; }

    public WizardConfiguration(
        IReadOnlyList<BrowserTarget> browserTargets,
        IReadOnlyList<AllowlistSite> allowlistSites,
        AdvancedOptionsState advancedOptions)
    {
        BrowserTargets = browserTargets;
        AllowlistSites = allowlistSites;
        AdvancedOptions = advancedOptions;
    }

    /// <summary>
    /// Gates the Step 4 Apply button: at least one browser and one allowed
    /// site must be chosen before the wizard is allowed to write anything.
    /// </summary>
    public bool CanApply => BrowserTargets.Count > 0 && AllowlistSites.Count > 0;
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test`
Expected: `Passed! ... Total: 30` (26 from before + 4 new)

- [ ] **Step 5: Commit**

```bash
git add src/WebsiteWhitelistManual.Core/Models/AdvancedOptionsState.cs src/WebsiteWhitelistManual.Core/Models/WizardConfiguration.cs tests/WebsiteWhitelistManual.Core.Tests/WizardConfigurationTests.cs
git commit -m "feat: add AdvancedOptionsState and WizardConfiguration.CanApply gating"
```

---

### Task 6: Registry policy reader

**Files:**
- Create: `src/WebsiteWhitelistManual.Core/Models/BrowserPolicySnapshot.cs`
- Create: `src/WebsiteWhitelistManual.Core/Models/PolicySnapshot.cs`
- Create: `src/WebsiteWhitelistManual.Core/Services/IRegistryPolicyReader.cs`
- Create: `src/WebsiteWhitelistManual.Core/Services/RegistryPolicyReader.cs`
- Test: `tests/WebsiteWhitelistManual.Core.Tests/RegistryPolicyReaderTests.cs`

**Interfaces:**
- Consumes: `IWindowsRegistry` (Task 3, tests use `FakeWindowsRegistry`), `BrowserTarget` (Task 2), `PolicyKeys` (Task 2).
- Produces: `BrowserPolicySnapshot(BrowserId BrowserId, bool PolicyKeyExists, IReadOnlyList<string> BlockedUrls, IReadOnlyList<string> AllowedUrls, bool? IncognitoDisabled, bool? BrowserSigninDisabled, bool? DeveloperToolsDisabled)`, `PolicySnapshot(IReadOnlyList<BrowserPolicySnapshot> Browsers)`, `IRegistryPolicyReader.ReadSnapshot(IReadOnlyList<BrowserTarget> targets)`. This is what Dashboard, Step 4's diff preview, and Step 5's post-apply verification will all call in the future WPF plan.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/WebsiteWhitelistManual.Core.Tests/RegistryPolicyReaderTests.cs
using WebsiteWhitelistManual.Core.Models;
using WebsiteWhitelistManual.Core.Services;
using WebsiteWhitelistManual.Core.Tests.Fakes;
using Xunit;

namespace WebsiteWhitelistManual.Core.Tests;

public class RegistryPolicyReaderTests
{
    [Fact]
    public void ReadSnapshot_ReportsPolicyKeyDoesNotExist_WhenNeverCreated()
    {
        var registry = new FakeWindowsRegistry();
        var reader = new RegistryPolicyReader(registry);

        var snapshot = reader.ReadSnapshot(new[] { BrowserTarget.Edge });

        var edge = Assert.Single(snapshot.Browsers);
        Assert.Equal(BrowserId.Edge, edge.BrowserId);
        Assert.False(edge.PolicyKeyExists);
        Assert.Empty(edge.BlockedUrls);
        Assert.Empty(edge.AllowedUrls);
        Assert.Null(edge.IncognitoDisabled);
        Assert.Null(edge.BrowserSigninDisabled);
        Assert.Null(edge.DeveloperToolsDisabled);
    }

    [Fact]
    public void ReadSnapshot_ReadsExistingBlocklistAndAllowlistEntries()
    {
        var registry = new FakeWindowsRegistry();
        registry.SetStringValue(@"SOFTWARE\Policies\Microsoft\Edge\URLBlocklist", "1", "*");
        registry.SetStringValue(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist", "1", "example.com");
        registry.SetStringValue(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist", "2", "www.example.com");
        registry.EnsureSubKeyExists(@"SOFTWARE\Policies\Microsoft\Edge");
        var reader = new RegistryPolicyReader(registry);

        var snapshot = reader.ReadSnapshot(new[] { BrowserTarget.Edge });

        var edge = Assert.Single(snapshot.Browsers);
        Assert.True(edge.PolicyKeyExists);
        Assert.Equal(new[] { "*" }, edge.BlockedUrls);
        Assert.Equal(new[] { "example.com", "www.example.com" }, edge.AllowedUrls);
    }

    [Fact]
    public void ReadSnapshot_ReadsDwordFlags_ForEdge()
    {
        var registry = new FakeWindowsRegistry();
        registry.EnsureSubKeyExists(@"SOFTWARE\Policies\Microsoft\Edge");
        registry.SetDwordValue(@"SOFTWARE\Policies\Microsoft\Edge", "InPrivateModeAvailability", 1);
        registry.SetDwordValue(@"SOFTWARE\Policies\Microsoft\Edge", "BrowserSignin", 0);
        registry.SetDwordValue(@"SOFTWARE\Policies\Microsoft\Edge", "DeveloperToolsAvailability", 2);
        var reader = new RegistryPolicyReader(registry);

        var snapshot = reader.ReadSnapshot(new[] { BrowserTarget.Edge });

        var edge = Assert.Single(snapshot.Browsers);
        Assert.True(edge.IncognitoDisabled);
        Assert.True(edge.BrowserSigninDisabled);
        Assert.True(edge.DeveloperToolsDisabled);
    }

    [Fact]
    public void ReadSnapshot_UsesChromeSpecificIncognitoValueName()
    {
        var registry = new FakeWindowsRegistry();
        registry.EnsureSubKeyExists(@"SOFTWARE\Policies\Google\Chrome");
        registry.SetDwordValue(@"SOFTWARE\Policies\Google\Chrome", "IncognitoModeAvailability", 1);
        var reader = new RegistryPolicyReader(registry);

        var snapshot = reader.ReadSnapshot(new[] { BrowserTarget.Chrome });

        var chrome = Assert.Single(snapshot.Browsers);
        Assert.True(chrome.IncognitoDisabled);
    }

    [Fact]
    public void ReadSnapshot_ReadsMultipleTargetsIndependently()
    {
        var registry = new FakeWindowsRegistry();
        registry.EnsureSubKeyExists(@"SOFTWARE\Policies\Microsoft\Edge");
        var reader = new RegistryPolicyReader(registry);

        var snapshot = reader.ReadSnapshot(new[] { BrowserTarget.Edge, BrowserTarget.Chrome });

        Assert.Equal(2, snapshot.Browsers.Count);
        Assert.True(snapshot.Browsers.Single(b => b.BrowserId == BrowserId.Edge).PolicyKeyExists);
        Assert.False(snapshot.Browsers.Single(b => b.BrowserId == BrowserId.Chrome).PolicyKeyExists);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail to compile**

Run: `dotnet test`
Expected: build error — `BrowserPolicySnapshot`, `PolicySnapshot`, `RegistryPolicyReader` do not exist yet.

- [ ] **Step 3: Implement the snapshot models and reader**

```csharp
// src/WebsiteWhitelistManual.Core/Models/BrowserPolicySnapshot.cs
namespace WebsiteWhitelistManual.Core.Models;

public sealed record BrowserPolicySnapshot(
    BrowserId BrowserId,
    bool PolicyKeyExists,
    IReadOnlyList<string> BlockedUrls,
    IReadOnlyList<string> AllowedUrls,
    bool? IncognitoDisabled,
    bool? BrowserSigninDisabled,
    bool? DeveloperToolsDisabled);
```

```csharp
// src/WebsiteWhitelistManual.Core/Models/PolicySnapshot.cs
namespace WebsiteWhitelistManual.Core.Models;

public sealed record PolicySnapshot(IReadOnlyList<BrowserPolicySnapshot> Browsers);
```

```csharp
// src/WebsiteWhitelistManual.Core/Services/IRegistryPolicyReader.cs
using WebsiteWhitelistManual.Core.Models;

namespace WebsiteWhitelistManual.Core.Services;

public interface IRegistryPolicyReader
{
    PolicySnapshot ReadSnapshot(IReadOnlyList<BrowserTarget> targets);
}
```

```csharp
// src/WebsiteWhitelistManual.Core/Services/RegistryPolicyReader.cs
using WebsiteWhitelistManual.Core.Abstractions;
using WebsiteWhitelistManual.Core.Models;

namespace WebsiteWhitelistManual.Core.Services;

public sealed class RegistryPolicyReader : IRegistryPolicyReader
{
    private readonly IWindowsRegistry _registry;

    public RegistryPolicyReader(IWindowsRegistry registry)
    {
        _registry = registry;
    }

    public PolicySnapshot ReadSnapshot(IReadOnlyList<BrowserTarget> targets)
    {
        var browsers = targets.Select(ReadOne).ToList();
        return new PolicySnapshot(browsers);
    }

    private BrowserPolicySnapshot ReadOne(BrowserTarget target)
    {
        if (!_registry.SubKeyExists(target.RootPath))
        {
            return new BrowserPolicySnapshot(
                target.Id,
                PolicyKeyExists: false,
                BlockedUrls: Array.Empty<string>(),
                AllowedUrls: Array.Empty<string>(),
                IncognitoDisabled: null,
                BrowserSigninDisabled: null,
                DeveloperToolsDisabled: null);
        }

        var blockedUrls = ReadStringList($@"{target.RootPath}\{PolicyKeys.UrlBlocklistSubKey}");
        var allowedUrls = ReadStringList($@"{target.RootPath}\{PolicyKeys.UrlAllowlistSubKey}");

        var incognito = _registry.GetDwordValue(target.RootPath, target.IncognitoValueName);
        var signin = _registry.GetDwordValue(target.RootPath, PolicyKeys.BrowserSigninValueName);
        var devTools = _registry.GetDwordValue(target.RootPath, PolicyKeys.DeveloperToolsAvailabilityValueName);

        return new BrowserPolicySnapshot(
            target.Id,
            PolicyKeyExists: true,
            BlockedUrls: blockedUrls,
            AllowedUrls: allowedUrls,
            IncognitoDisabled: incognito.HasValue ? incognito.Value == 1 : null,
            BrowserSigninDisabled: signin.HasValue ? signin.Value == 0 : null,
            DeveloperToolsDisabled: devTools.HasValue ? devTools.Value == 2 : null);
    }

    private IReadOnlyList<string> ReadStringList(string subKeyPath)
    {
        if (!_registry.SubKeyExists(subKeyPath))
        {
            return Array.Empty<string>();
        }

        var values = new List<string>();
        foreach (var valueName in _registry.GetValueNames(subKeyPath))
        {
            var value = _registry.GetStringValue(subKeyPath, valueName);
            if (value is not null)
            {
                values.Add(value);
            }
        }
        return values;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test`
Expected: `Passed! ... Total: 35` (30 from before + 5 new)

- [ ] **Step 5: Commit**

```bash
git add src/WebsiteWhitelistManual.Core/Models/BrowserPolicySnapshot.cs src/WebsiteWhitelistManual.Core/Models/PolicySnapshot.cs src/WebsiteWhitelistManual.Core/Services/IRegistryPolicyReader.cs src/WebsiteWhitelistManual.Core/Services/RegistryPolicyReader.cs tests/WebsiteWhitelistManual.Core.Tests/RegistryPolicyReaderTests.cs
git commit -m "feat: add RegistryPolicyReader and PolicySnapshot models"
```

---

### Task 7: Registry policy writer

**Files:**
- Create: `src/WebsiteWhitelistManual.Core/Services/IRegistryPolicyWriter.cs`
- Create: `src/WebsiteWhitelistManual.Core/Services/RegistryPolicyWriter.cs`
- Test: `tests/WebsiteWhitelistManual.Core.Tests/RegistryPolicyWriterTests.cs`

**Interfaces:**
- Consumes: `IWindowsRegistry` (Task 3), `WizardConfiguration` (Task 5), `PolicyKeys`/`BrowserTarget` (Task 2).
- Produces: `IRegistryPolicyWriter.Apply(WizardConfiguration configuration)` — writes `URLBlocklist\1 = "*"`, numbered `URLAllowlist` entries, and the three DWORD flags (only the ones enabled in `AdvancedOptionsState`) for every browser in `configuration.BrowserTargets`. Clears stale `URLAllowlist`/`URLBlocklist` entries before rewriting so a shorter list on re-apply doesn't leave orphaned old entries behind.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/WebsiteWhitelistManual.Core.Tests/RegistryPolicyWriterTests.cs
using WebsiteWhitelistManual.Core.Models;
using WebsiteWhitelistManual.Core.Services;
using WebsiteWhitelistManual.Core.Tests.Fakes;
using Xunit;

namespace WebsiteWhitelistManual.Core.Tests;

public class RegistryPolicyWriterTests
{
    private static AllowlistSite MakeSite(string domain)
    {
        AllowlistSite.TryCreate(domain, null, out var site, out _);
        return site!;
    }

    [Fact]
    public void Apply_WritesBlocklistWildcard()
    {
        var registry = new FakeWindowsRegistry();
        var writer = new RegistryPolicyWriter(registry);
        var config = new WizardConfiguration(
            new[] { BrowserTarget.Edge }, new[] { MakeSite("example.com") }, new AdvancedOptionsState());

        writer.Apply(config);

        Assert.Equal("*", registry.GetStringValue(@"SOFTWARE\Policies\Microsoft\Edge\URLBlocklist", "1"));
    }

    [Fact]
    public void Apply_WritesNumberedAllowlistEntries()
    {
        var registry = new FakeWindowsRegistry();
        var writer = new RegistryPolicyWriter(registry);
        var config = new WizardConfiguration(
            new[] { BrowserTarget.Edge },
            new[] { MakeSite("example.com"), MakeSite("www.example.com") },
            new AdvancedOptionsState());

        writer.Apply(config);

        Assert.Equal("example.com", registry.GetStringValue(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist", "1"));
        Assert.Equal("www.example.com", registry.GetStringValue(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist", "2"));
    }

    [Fact]
    public void Apply_ClearsStaleAllowlistEntries_WhenListShrinks()
    {
        var registry = new FakeWindowsRegistry();
        registry.SetStringValue(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist", "1", "old-one.com");
        registry.SetStringValue(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist", "2", "old-two.com");
        registry.SetStringValue(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist", "3", "old-three.com");
        var writer = new RegistryPolicyWriter(registry);
        var config = new WizardConfiguration(
            new[] { BrowserTarget.Edge }, new[] { MakeSite("example.com") }, new AdvancedOptionsState());

        writer.Apply(config);

        var names = registry.GetValueNames(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist");
        Assert.Single(names);
        Assert.Equal("example.com", registry.GetStringValue(@"SOFTWARE\Policies\Microsoft\Edge\URLAllowlist", "1"));
    }

    [Fact]
    public void Apply_WritesEdgeIncognitoValueName_ForEdge()
    {
        var registry = new FakeWindowsRegistry();
        var writer = new RegistryPolicyWriter(registry);
        var config = new WizardConfiguration(
            new[] { BrowserTarget.Edge }, new[] { MakeSite("example.com") },
            new AdvancedOptionsState(DisableIncognito: true, DisableAccountSwitching: false, DisableDeveloperTools: false));

        writer.Apply(config);

        Assert.Equal(1, registry.GetDwordValue(@"SOFTWARE\Policies\Microsoft\Edge", "InPrivateModeAvailability"));
    }

    [Fact]
    public void Apply_WritesChromeIncognitoValueName_ForChrome()
    {
        var registry = new FakeWindowsRegistry();
        var writer = new RegistryPolicyWriter(registry);
        var config = new WizardConfiguration(
            new[] { BrowserTarget.Chrome }, new[] { MakeSite("example.com") },
            new AdvancedOptionsState(DisableIncognito: true, DisableAccountSwitching: false, DisableDeveloperTools: false));

        writer.Apply(config);

        Assert.Equal(1, registry.GetDwordValue(@"SOFTWARE\Policies\Google\Chrome", "IncognitoModeAvailability"));
    }

    [Fact]
    public void Apply_SkipsDisabledAdvancedOptionFlags()
    {
        var registry = new FakeWindowsRegistry();
        var writer = new RegistryPolicyWriter(registry);
        var config = new WizardConfiguration(
            new[] { BrowserTarget.Edge }, new[] { MakeSite("example.com") },
            new AdvancedOptionsState(DisableIncognito: false, DisableAccountSwitching: false, DisableDeveloperTools: false));

        writer.Apply(config);

        Assert.Null(registry.GetDwordValue(@"SOFTWARE\Policies\Microsoft\Edge", "InPrivateModeAvailability"));
        Assert.Null(registry.GetDwordValue(@"SOFTWARE\Policies\Microsoft\Edge", "BrowserSignin"));
        Assert.Null(registry.GetDwordValue(@"SOFTWARE\Policies\Microsoft\Edge", "DeveloperToolsAvailability"));
    }

    [Fact]
    public void Apply_WritesToEveryBrowserTarget()
    {
        var registry = new FakeWindowsRegistry();
        var writer = new RegistryPolicyWriter(registry);
        var config = new WizardConfiguration(
            new[] { BrowserTarget.Edge, BrowserTarget.Chrome }, new[] { MakeSite("example.com") },
            new AdvancedOptionsState());

        writer.Apply(config);

        Assert.Equal("*", registry.GetStringValue(@"SOFTWARE\Policies\Microsoft\Edge\URLBlocklist", "1"));
        Assert.Equal("*", registry.GetStringValue(@"SOFTWARE\Policies\Google\Chrome\URLBlocklist", "1"));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail to compile**

Run: `dotnet test`
Expected: build error — `IRegistryPolicyWriter`, `RegistryPolicyWriter` do not exist yet.

- [ ] **Step 3: Implement the writer**

```csharp
// src/WebsiteWhitelistManual.Core/Services/IRegistryPolicyWriter.cs
using WebsiteWhitelistManual.Core.Models;

namespace WebsiteWhitelistManual.Core.Services;

public interface IRegistryPolicyWriter
{
    void Apply(WizardConfiguration configuration);
}
```

```csharp
// src/WebsiteWhitelistManual.Core/Services/RegistryPolicyWriter.cs
using WebsiteWhitelistManual.Core.Abstractions;
using WebsiteWhitelistManual.Core.Models;

namespace WebsiteWhitelistManual.Core.Services;

public sealed class RegistryPolicyWriter : IRegistryPolicyWriter
{
    private readonly IWindowsRegistry _registry;

    public RegistryPolicyWriter(IWindowsRegistry registry)
    {
        _registry = registry;
    }

    public void Apply(WizardConfiguration configuration)
    {
        foreach (var target in configuration.BrowserTargets)
        {
            ApplyToBrowser(target, configuration);
        }
    }

    private void ApplyToBrowser(BrowserTarget target, WizardConfiguration configuration)
    {
        _registry.EnsureSubKeyExists(target.RootPath);

        var blocklistPath = $@"{target.RootPath}\{PolicyKeys.UrlBlocklistSubKey}";
        ClearValues(blocklistPath);
        _registry.SetStringValue(blocklistPath, "1", "*");

        var allowlistPath = $@"{target.RootPath}\{PolicyKeys.UrlAllowlistSubKey}";
        ClearValues(allowlistPath);
        for (var i = 0; i < configuration.AllowlistSites.Count; i++)
        {
            _registry.SetStringValue(allowlistPath, (i + 1).ToString(), configuration.AllowlistSites[i].Domain);
        }

        if (configuration.AdvancedOptions.DisableIncognito)
        {
            _registry.SetDwordValue(target.RootPath, target.IncognitoValueName, 1);
        }

        if (configuration.AdvancedOptions.DisableAccountSwitching)
        {
            _registry.SetDwordValue(target.RootPath, PolicyKeys.BrowserSigninValueName, 0);
        }

        if (configuration.AdvancedOptions.DisableDeveloperTools)
        {
            _registry.SetDwordValue(target.RootPath, PolicyKeys.DeveloperToolsAvailabilityValueName, 2);
        }
    }

    private void ClearValues(string subKeyPath)
    {
        _registry.EnsureSubKeyExists(subKeyPath);
        foreach (var valueName in _registry.GetValueNames(subKeyPath).ToList())
        {
            _registry.DeleteValue(subKeyPath, valueName);
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test`
Expected: `Passed! ... Total: 42` (35 from before + 7 new)

- [ ] **Step 5: Commit**

```bash
git add src/WebsiteWhitelistManual.Core/Services/IRegistryPolicyWriter.cs src/WebsiteWhitelistManual.Core/Services/RegistryPolicyWriter.cs tests/WebsiteWhitelistManual.Core.Tests/RegistryPolicyWriterTests.cs
git commit -m "feat: add RegistryPolicyWriter"
```

---

### Task 8: Registry backup service

**Files:**
- Create: `src/WebsiteWhitelistManual.Core/Abstractions/IProcessRunner.cs`
- Create: `src/WebsiteWhitelistManual.Core/Abstractions/ProcessResult.cs`
- Create: `src/WebsiteWhitelistManual.Core/Services/BackupResult.cs`
- Create: `src/WebsiteWhitelistManual.Core/Services/IRegistryBackupService.cs`
- Create: `src/WebsiteWhitelistManual.Core/Services/RegistryBackupService.cs`
- Create: `tests/WebsiteWhitelistManual.Core.Tests/Fakes/FakeProcessRunner.cs`
- Test: `tests/WebsiteWhitelistManual.Core.Tests/RegistryBackupServiceTests.cs`

**Interfaces:**
- Consumes: `BrowserTarget` (Task 2).
- Produces: `IProcessRunner.Run(string fileName, string arguments)` returning `ProcessResult(int ExitCode, string StandardOutput, string StandardError)`. `BackupResult(bool Success, string BackupDirectory, IReadOnlyList<string> BackupFilePaths, string? ErrorMessage)`. `IRegistryBackupService.Backup(IReadOnlyList<BrowserTarget> targets, string baseDirectory, DateTimeOffset timestamp)` — the base directory and timestamp are both passed in (not resolved internally) so tests stay deterministic and don't touch the real filesystem location the future WPF app will use (`%LOCALAPPDATA%\WebsiteWhitelistManual\Backups`). Exports each target's registry root key to its own `.reg` file inside a timestamped subfolder via `reg.exe export`, run through `IProcessRunner` — never `System.Diagnostics.Process` directly, so this stays testable on macOS. If any export's exit code is non-zero, `Backup` returns `Success: false` immediately (the caller — added in the future WPF plan — is responsible for refusing to call `IRegistryPolicyWriter.Apply` when this happens, matching the spec's "fail closed" requirement).

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/WebsiteWhitelistManual.Core.Tests/RegistryBackupServiceTests.cs
using WebsiteWhitelistManual.Core.Models;
using WebsiteWhitelistManual.Core.Services;
using WebsiteWhitelistManual.Core.Tests.Fakes;
using Xunit;

namespace WebsiteWhitelistManual.Core.Tests;

public class RegistryBackupServiceTests : IDisposable
{
    private readonly string _tempDirectory = Path.Combine(Path.GetTempPath(), "wwm-backup-tests-" + Guid.NewGuid());

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private static readonly DateTimeOffset FixedTimestamp = new(2026, 9, 15, 16, 42, 0, TimeSpan.Zero);

    [Fact]
    public void Backup_CreatesTimestampedSubfolder()
    {
        var runner = new FakeProcessRunner(new ProcessResult(0, string.Empty, string.Empty));
        var service = new RegistryBackupService(runner);

        var result = service.Backup(new[] { BrowserTarget.Edge }, _tempDirectory, FixedTimestamp);

        Assert.True(Directory.Exists(result.BackupDirectory));
        Assert.Equal(Path.Combine(_tempDirectory, "20260915_164200"), result.BackupDirectory);
    }

    [Fact]
    public void Backup_InvokesRegExportForEachTarget()
    {
        var runner = new FakeProcessRunner(new ProcessResult(0, string.Empty, string.Empty));
        var service = new RegistryBackupService(runner);

        service.Backup(new[] { BrowserTarget.Edge, BrowserTarget.Chrome }, _tempDirectory, FixedTimestamp);

        Assert.Equal(2, runner.Invocations.Count);
        Assert.All(runner.Invocations, invocation => Assert.Equal("reg.exe", invocation.FileName));
        Assert.Contains(runner.Invocations, i => i.Arguments.Contains(@"HKLM\SOFTWARE\Policies\Microsoft\Edge"));
        Assert.Contains(runner.Invocations, i => i.Arguments.Contains(@"HKLM\SOFTWARE\Policies\Google\Chrome"));
    }

    [Fact]
    public void Backup_ReturnsSuccessTrue_WhenAllExportsSucceed()
    {
        var runner = new FakeProcessRunner(new ProcessResult(0, string.Empty, string.Empty));
        var service = new RegistryBackupService(runner);

        var result = service.Backup(new[] { BrowserTarget.Edge }, _tempDirectory, FixedTimestamp);

        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);
        Assert.Single(result.BackupFilePaths);
    }

    [Fact]
    public void Backup_ReturnsSuccessFalse_WhenAnExportFails()
    {
        var runner = new FakeProcessRunner(new ProcessResult(1, string.Empty, "access denied"));
        var service = new RegistryBackupService(runner);

        var result = service.Backup(new[] { BrowserTarget.Edge }, _tempDirectory, FixedTimestamp);

        Assert.False(result.Success);
        Assert.Equal("access denied", result.ErrorMessage);
    }

    [Fact]
    public void Backup_NamesEachFileAfterItsBrowser()
    {
        var runner = new FakeProcessRunner(new ProcessResult(0, string.Empty, string.Empty));
        var service = new RegistryBackupService(runner);

        var result = service.Backup(new[] { BrowserTarget.Edge, BrowserTarget.Chrome }, _tempDirectory, FixedTimestamp);

        Assert.Contains(result.BackupFilePaths, p => p.EndsWith("Edge.reg"));
        Assert.Contains(result.BackupFilePaths, p => p.EndsWith("Chrome.reg"));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail to compile**

Run: `dotnet test`
Expected: build error — `IProcessRunner`, `ProcessResult`, `BackupResult`, `IRegistryBackupService`, `RegistryBackupService`, `FakeProcessRunner` do not exist yet.

- [ ] **Step 3: Implement the abstractions**

```csharp
// src/WebsiteWhitelistManual.Core/Abstractions/ProcessResult.cs
namespace WebsiteWhitelistManual.Core.Abstractions;

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);
```

```csharp
// src/WebsiteWhitelistManual.Core/Abstractions/IProcessRunner.cs
namespace WebsiteWhitelistManual.Core.Abstractions;

public interface IProcessRunner
{
    ProcessResult Run(string fileName, string arguments);
}
```

- [ ] **Step 4: Implement the fake process runner (test double)**

```csharp
// tests/WebsiteWhitelistManual.Core.Tests/Fakes/FakeProcessRunner.cs
using WebsiteWhitelistManual.Core.Abstractions;

namespace WebsiteWhitelistManual.Core.Tests.Fakes;

public sealed record RecordedInvocation(string FileName, string Arguments);

public sealed class FakeProcessRunner : IProcessRunner
{
    private readonly ProcessResult _result;

    public FakeProcessRunner(ProcessResult result)
    {
        _result = result;
    }

    public List<RecordedInvocation> Invocations { get; } = new();

    public ProcessResult Run(string fileName, string arguments)
    {
        Invocations.Add(new RecordedInvocation(fileName, arguments));
        return _result;
    }
}
```

- [ ] **Step 5: Implement `BackupResult` and `IRegistryBackupService`**

```csharp
// src/WebsiteWhitelistManual.Core/Services/BackupResult.cs
namespace WebsiteWhitelistManual.Core.Services;

public sealed record BackupResult(
    bool Success,
    string BackupDirectory,
    IReadOnlyList<string> BackupFilePaths,
    string? ErrorMessage);
```

```csharp
// src/WebsiteWhitelistManual.Core/Services/IRegistryBackupService.cs
using WebsiteWhitelistManual.Core.Models;

namespace WebsiteWhitelistManual.Core.Services;

public interface IRegistryBackupService
{
    BackupResult Backup(IReadOnlyList<BrowserTarget> targets, string baseDirectory, DateTimeOffset timestamp);
}
```

- [ ] **Step 6: Implement `RegistryBackupService`**

```csharp
// src/WebsiteWhitelistManual.Core/Services/RegistryBackupService.cs
using WebsiteWhitelistManual.Core.Abstractions;
using WebsiteWhitelistManual.Core.Models;

namespace WebsiteWhitelistManual.Core.Services;

public sealed class RegistryBackupService : IRegistryBackupService
{
    private readonly IProcessRunner _processRunner;

    public RegistryBackupService(IProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public BackupResult Backup(IReadOnlyList<BrowserTarget> targets, string baseDirectory, DateTimeOffset timestamp)
    {
        var backupDirectory = Path.Combine(baseDirectory, timestamp.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(backupDirectory);

        var filePaths = new List<string>();

        foreach (var target in targets)
        {
            var filePath = Path.Combine(backupDirectory, $"{target.Id}.reg");
            var arguments = $"export \"HKLM\\{target.RootPath}\" \"{filePath}\" /y";
            var result = _processRunner.Run("reg.exe", arguments);

            if (result.ExitCode != 0)
            {
                return new BackupResult(
                    Success: false,
                    BackupDirectory: backupDirectory,
                    BackupFilePaths: filePaths,
                    ErrorMessage: string.IsNullOrWhiteSpace(result.StandardError)
                        ? $"reg.exe export exited with code {result.ExitCode}."
                        : result.StandardError);
            }

            filePaths.Add(filePath);
        }

        return new BackupResult(Success: true, BackupDirectory: backupDirectory, BackupFilePaths: filePaths, ErrorMessage: null);
    }
}
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test`
Expected: `Passed! ... Total: 47` (42 from before + 5 new)

- [ ] **Step 8: Commit**

```bash
git add src/WebsiteWhitelistManual.Core/Abstractions/IProcessRunner.cs src/WebsiteWhitelistManual.Core/Abstractions/ProcessResult.cs src/WebsiteWhitelistManual.Core/Services/BackupResult.cs src/WebsiteWhitelistManual.Core/Services/IRegistryBackupService.cs src/WebsiteWhitelistManual.Core/Services/RegistryBackupService.cs tests/WebsiteWhitelistManual.Core.Tests/Fakes/FakeProcessRunner.cs tests/WebsiteWhitelistManual.Core.Tests/RegistryBackupServiceTests.cs
git commit -m "feat: add RegistryBackupService (reg.exe export via IProcessRunner)"
```

---

### Task 9: Local account inspector

**Files:**
- Create: `src/WebsiteWhitelistManual.Core/Models/LocalAccountInfo.cs`
- Create: `src/WebsiteWhitelistManual.Core/Abstractions/ILocalAccountSource.cs`
- Create: `src/WebsiteWhitelistManual.Core/Services/ILocalAccountInspector.cs`
- Create: `src/WebsiteWhitelistManual.Core/Services/LocalAccountInspector.cs`
- Create: `tests/WebsiteWhitelistManual.Core.Tests/Fakes/FakeLocalAccountSource.cs`
- Test: `tests/WebsiteWhitelistManual.Core.Tests/LocalAccountInspectorTests.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `LocalAccountInfo(string AccountName, bool IsAdministrator, bool IsBuiltIn)`. `ILocalAccountSource.GetLocalAccounts()` — the future Windows-only implementation will fill `IsBuiltIn` using well-known SIDs (Administrator, Guest, DefaultAccount, WDAGUtilityAccount). `ILocalAccountInspector.GetRelevantAccounts()` — filters out built-in accounts so the Dashboard only shows accounts a parent would recognize as real family members.

- [ ] **Step 1: Write the failing tests**

```csharp
// tests/WebsiteWhitelistManual.Core.Tests/LocalAccountInspectorTests.cs
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
```

- [ ] **Step 2: Run the tests to verify they fail to compile**

Run: `dotnet test`
Expected: build error — `LocalAccountInfo`, `ILocalAccountSource`, `ILocalAccountInspector`, `LocalAccountInspector`, `FakeLocalAccountSource` do not exist yet.

- [ ] **Step 3: Implement the model and abstraction**

```csharp
// src/WebsiteWhitelistManual.Core/Models/LocalAccountInfo.cs
namespace WebsiteWhitelistManual.Core.Models;

public sealed record LocalAccountInfo(string AccountName, bool IsAdministrator, bool IsBuiltIn);
```

```csharp
// src/WebsiteWhitelistManual.Core/Abstractions/ILocalAccountSource.cs
using WebsiteWhitelistManual.Core.Models;

namespace WebsiteWhitelistManual.Core.Abstractions;

public interface ILocalAccountSource
{
    IReadOnlyList<LocalAccountInfo> GetLocalAccounts();
}
```

- [ ] **Step 4: Implement the fake account source (test double)**

```csharp
// tests/WebsiteWhitelistManual.Core.Tests/Fakes/FakeLocalAccountSource.cs
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
```

- [ ] **Step 5: Implement `ILocalAccountInspector` and `LocalAccountInspector`**

```csharp
// src/WebsiteWhitelistManual.Core/Services/ILocalAccountInspector.cs
using WebsiteWhitelistManual.Core.Models;

namespace WebsiteWhitelistManual.Core.Services;

public interface ILocalAccountInspector
{
    IReadOnlyList<LocalAccountInfo> GetRelevantAccounts();
}
```

```csharp
// src/WebsiteWhitelistManual.Core/Services/LocalAccountInspector.cs
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
}
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test`
Expected: `Passed! ... Total: 50` (47 from before + 3 new)

- [ ] **Step 7: Commit**

```bash
git add src/WebsiteWhitelistManual.Core/Models/LocalAccountInfo.cs src/WebsiteWhitelistManual.Core/Abstractions/ILocalAccountSource.cs src/WebsiteWhitelistManual.Core/Services/ILocalAccountInspector.cs src/WebsiteWhitelistManual.Core/Services/LocalAccountInspector.cs tests/WebsiteWhitelistManual.Core.Tests/Fakes/FakeLocalAccountSource.cs tests/WebsiteWhitelistManual.Core.Tests/LocalAccountInspectorTests.cs
git commit -m "feat: add LocalAccountInspector"
```

---

## After this plan

All 46 tests pass on macOS, and `WebsiteWhitelistManual.Core` fully implements the spec's registry-facing business logic behind interfaces. The next plan (written separately, after this one is merged) adds the `WebsiteWhitelistManual.App` WPF project: the real `IWindowsRegistry`/`IProcessRunner`/`ILocalAccountSource` implementations backed by `Microsoft.Win32.Registry`, `System.Diagnostics.Process`, and `System.DirectoryServices.AccountManagement`, plus the `FluentWindow` + `NavigationView` shell and all six XAML pages — none of which can be built or tested until the user runs `dotnet build`/`dotnet publish` on Windows.
