# WPF App Shell Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build `WebsiteWhitelistManual.App`, the WPF executable project — a buildable, DI-wired, UAC-elevated shell with the real Windows-backed implementations of `Core`'s three abstractions and a `FluentWindow`+`NavigationView` skeleton — as a thin vertical slice that proves the whole toolchain end-to-end before any of the 6 wizard pages get real content.

**Architecture:** `WebsiteWhitelistManual.App` (`net10.0-windows`, `UseWPF=true`) references `WebsiteWhitelistManual.Core` and supplies `WindowsRegistryAdapter`, `WindowsProcessRunner`, `WindowsLocalAccountSource` — real implementations of `IWindowsRegistry`/`IProcessRunner`/`ILocalAccountSource` backed by `Microsoft.Win32.Registry`, `System.Diagnostics.Process`, and `System.DirectoryServices.AccountManagement` respectively. A `Microsoft.Extensions.Hosting` generic host wires everything through DI in `App.xaml.cs`. `MainWindow` is a WPF-UI `FluentWindow` containing a `NavigationView` with the 6 menu items from the spec, but **no page-switching logic yet** — that, and the pages' real content, is explicitly out of scope for this plan (next plan's job).

**Tech Stack:** .NET 10 SDK, WPF (`net10.0-windows`), WPF-UI (Fluent Design controls), `CommunityToolkit.Mvvm` (added now for the next plan's ViewModels, unused by this plan's code), `Microsoft.Extensions.Hosting` (DI composition root), `System.DirectoryServices.AccountManagement`.

**Spec:** `docs/superpowers/specs/2026-09-15-website-whitelist-wpf-app-design.md`

## Global Constraints

- `WebsiteWhitelistManual.App`'s `TargetFramework` is `net10.0-windows`, with `<EnableWindowsTargeting>true</EnableWindowsTargeting>` in the csproj. **This has been verified empirically on this machine**: with that property set, `dotnet build` (including WPF-UI's XAML markup compilation and `System.DirectoryServices.AccountManagement`) succeeds on this macOS machine without a Windows install. This overturns the older assumption recorded in `HANDOFF.md` that WPF cannot build on Mac at all.
- **What `dotnet build` on this machine can and cannot prove:** it proves the code compiles (C# syntax/types, XAML→BAML markup compilation, NuGet package resolution). It proves NOTHING about runtime behavior — the app cannot be launched, no window can render, no UAC prompt can fire, and no registry/account API actually executes, because there is no Windows runtime here. Every task's verification step in this plan is `dotnet build`, not `dotnet run` or manual testing — the final task hands off to the user to actually run it on Windows.
- Package installation: use `dotnet add package <name>` (not hand-written XML versions) so NuGet resolves versions compatible with the installed SDK, matching the Core plan's approach.
- Registry key names/values, when referenced, must match the canonical table in the spec exactly (already enforced in `Core`, not re-litigated here).
- `WebsiteWhitelistManual.App` must never duplicate a model, interface, or constant that already exists in `WebsiteWhitelistManual.Core` — it consumes `Core` via `ProjectReference` and only adds Windows-real implementations of `Core`'s abstractions.
- No project or file in this plan is a separate cross-platform-testable ViewModels project — the user explicitly decided ViewModels live inside the single `net10.0-windows` App project for simplicity, accepting that ViewModel logic can only be verified by building/running on Windows (not by `dotnet test` here). This plan doesn't yet add any ViewModels — that's the next plan.

---

### Task 1: Harden `IProcessRunner` to take an argument list

This is a refactor of already-merged `Core` code, done now because the next task in this plan (`WindowsProcessRunner`) is the real implementation the deferred spec note was about — fixing the interface now avoids ever shipping the unsafe manually-quoted-string shape.

**Files:**
- Modify: `src/WebsiteWhitelistManual.Core/Abstractions/IProcessRunner.cs`
- Modify: `src/WebsiteWhitelistManual.Core/Services/RegistryBackupService.cs`
- Modify: `tests/WebsiteWhitelistManual.Core.Tests/Fakes/FakeProcessRunner.cs`
- Test: `tests/WebsiteWhitelistManual.Core.Tests/RegistryBackupServiceTests.cs`

**Interfaces:**
- Produces: `IProcessRunner.Run(string fileName, IReadOnlyList<string> arguments)` — replaces the old `Run(string fileName, string arguments)`. `WindowsProcessRunner` (Task 4) implements this via `ProcessStartInfo.ArgumentList`, so no argument is ever manually quoted.

- [ ] **Step 1: Update the interface**

```csharp
// src/WebsiteWhitelistManual.Core/Abstractions/IProcessRunner.cs
namespace WebsiteWhitelistManual.Core.Abstractions;

public interface IProcessRunner
{
    ProcessResult Run(string fileName, IReadOnlyList<string> arguments);
}
```

- [ ] **Step 2: Run `dotnet build` to confirm it now fails**

Run: `dotnet build`
Expected: FAIL — `RegistryBackupService.cs` passes a `string` where `IReadOnlyList<string>` is expected, and `FakeProcessRunner.cs` no longer implements the interface (`CS0535`).

- [ ] **Step 3: Update the call site in `RegistryBackupService`**

```csharp
// src/WebsiteWhitelistManual.Core/Services/RegistryBackupService.cs — replace these two lines (currently around line 47-48):
//     var arguments = $"export \"HKLM\\{target.RootPath}\" \"{filePath}\" /y";
//     var result = _processRunner.Run("reg.exe", arguments);
// with:
            var arguments = new[] { "export", $@"HKLM\{target.RootPath}", filePath, "/y" };
            var result = _processRunner.Run("reg.exe", arguments);
```

- [ ] **Step 4: Update `FakeProcessRunner`**

```csharp
// tests/WebsiteWhitelistManual.Core.Tests/Fakes/FakeProcessRunner.cs
using WebsiteWhitelistManual.Core.Abstractions;

namespace WebsiteWhitelistManual.Core.Tests.Fakes;

public sealed record RecordedInvocation(string FileName, IReadOnlyList<string> Arguments);

public sealed class FakeProcessRunner : IProcessRunner
{
    private readonly ProcessResult _result;

    public FakeProcessRunner(ProcessResult result)
    {
        _result = result;
    }

    public List<RecordedInvocation> Invocations { get; } = new();

    public ProcessResult Run(string fileName, IReadOnlyList<string> arguments)
    {
        Invocations.Add(new RecordedInvocation(fileName, arguments));
        return _result;
    }
}
```

- [ ] **Step 5: Run the full suite — confirm the existing 57 tests pass unchanged**

Run: `dotnet test`
Expected: `Passed! ... Total: 57`. The existing assertions like `i.Arguments.Contains(@"HKLM\SOFTWARE\Policies\Microsoft\Edge")` still pass: `Arguments` is now `IReadOnlyList<string>`, so `.Contains(string)` resolves to `Enumerable.Contains` (exact-element match) instead of `string.Contains` (substring match) — and the arguments list's second element is exactly that HKLM path string, so the check still succeeds.

- [ ] **Step 6: Add a test that pins the exact argument list shape**

This closes a gap the final Core review flagged as a Minor finding ("no test asserts the exact argument string — `/y` could go missing").

```csharp
// tests/WebsiteWhitelistManual.Core.Tests/RegistryBackupServiceTests.cs — add this test inside the class
    [Fact]
    public void Backup_BuildsExportArgumentsInExpectedOrderAndCount()
    {
        var runner = new FakeProcessRunner(new ProcessResult(0, string.Empty, string.Empty));
        var registry = MakeRegistryWithExistingPolicyKeys(BrowserTarget.Edge);
        var service = new RegistryBackupService(runner, registry);

        service.Backup(new[] { BrowserTarget.Edge }, _tempDirectory, FixedTimestamp);

        var invocation = Assert.Single(runner.Invocations);
        var expectedFilePath = Path.Combine(_tempDirectory, "20260915_164200", "Edge.reg");
        Assert.Equal(
            new[] { "export", @"HKLM\SOFTWARE\Policies\Microsoft\Edge", expectedFilePath, "/y" },
            invocation.Arguments);
    }
```

- [ ] **Step 7: Run the full suite — confirm 58/58**

Run: `dotnet test`
Expected: `Passed! ... Total: 58`

- [ ] **Step 8: Commit**

```bash
git add src/WebsiteWhitelistManual.Core/Abstractions/IProcessRunner.cs src/WebsiteWhitelistManual.Core/Services/RegistryBackupService.cs tests/WebsiteWhitelistManual.Core.Tests/Fakes/FakeProcessRunner.cs tests/WebsiteWhitelistManual.Core.Tests/RegistryBackupServiceTests.cs
git commit -m "refactor: IProcessRunner takes an argument list instead of a pre-joined string"
```

---

### Task 2: App project scaffold

**Files:**
- Create: `src/WebsiteWhitelistManual.App/WebsiteWhitelistManual.App.csproj`
- Create: `src/WebsiteWhitelistManual.App/app.manifest`
- Modify: `WebsiteWhitelistManual.sln`

**Interfaces:**
- Produces: an empty (template-default) WPF project that builds, references `WebsiteWhitelistManual.Core`, and is wired for admin elevation. Later tasks overwrite the template's default `App.xaml`/`MainWindow.xaml` content.

- [ ] **Step 1: Scaffold the WPF project**

```bash
dotnet new wpf -n WebsiteWhitelistManual.App -o src/WebsiteWhitelistManual.App
```

- [ ] **Step 2: Edit the csproj**

Open `src/WebsiteWhitelistManual.App/WebsiteWhitelistManual.App.csproj` and replace its contents with:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWPF>true</UseWPF>
    <EnableWindowsTargeting>true</EnableWindowsTargeting>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <RootNamespace>WebsiteWhitelistManual.App</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\WebsiteWhitelistManual.Core\WebsiteWhitelistManual.Core.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Create `app.manifest`**

```xml
<!-- src/WebsiteWhitelistManual.App/app.manifest -->
<?xml version="1.0" encoding="utf-8"?>
<assembly xmlns="urn:schemas-microsoft-com:asm.v1" manifestVersion="1.0">
  <assemblyIdentity version="1.0.0.0" name="WebsiteWhitelistManual.App"/>
  <trustInfo xmlns="urn:schemas-microsoft-com:asm.v2">
    <security>
      <requestedPrivileges xmlns="urn:schemas-microsoft-com:asm.v3">
        <requestedExecutionLevel level="requireAdministrator" uiAccess="false" />
      </requestedPrivileges>
    </security>
  </trustInfo>
  <compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1">
    <application>
      <!-- Windows Vista -->
      <supportedOS Id="{e2011457-1546-43c5-a5fe-008deee3d3f0}"/>
      <!-- Windows 7 -->
      <supportedOS Id="{35138b9a-5d96-4fbd-8e2d-a2440225f93a}"/>
      <!-- Windows 8 -->
      <supportedOS Id="{4a2f28e3-53b9-4441-ba9c-d69d4a4a6e38}"/>
      <!-- Windows 8.1 -->
      <supportedOS Id="{1f676c76-80e1-4239-95bb-83d0f6d0da78}"/>
      <!-- Windows 10 and Windows 11 -->
      <supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}"/>
    </application>
  </compatibility>
</assembly>
```

- [ ] **Step 4: Add the project to the solution**

```bash
dotnet sln WebsiteWhitelistManual.sln add src/WebsiteWhitelistManual.App/WebsiteWhitelistManual.App.csproj
```

- [ ] **Step 5: Verify the whole solution builds**

Run: `dotnet build`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)` across all three projects (`Core`, `Core.Tests`, `App`).

- [ ] **Step 6: Commit**

```bash
git add src/WebsiteWhitelistManual.App WebsiteWhitelistManual.sln
git commit -m "chore: scaffold WPF App project with EnableWindowsTargeting and requireAdministrator manifest"
```

---

### Task 3: WindowsRegistryAdapter

**Files:**
- Create: `src/WebsiteWhitelistManual.App/Services/WindowsRegistryAdapter.cs`

**Interfaces:**
- Consumes: `IWindowsRegistry` (`Core`, already merged).
- Produces: `WindowsRegistryAdapter : IWindowsRegistry`, registered in DI in Task 6.

- [ ] **Step 1: Implement the adapter**

```csharp
// src/WebsiteWhitelistManual.App/Services/WindowsRegistryAdapter.cs
using Microsoft.Win32;
using WebsiteWhitelistManual.Core.Abstractions;

namespace WebsiteWhitelistManual.App.Services;

/// <summary>
/// Real HKEY_LOCAL_MACHINE-backed implementation of IWindowsRegistry.
/// Its behavior is designed to match FakeWindowsRegistry's documented
/// semantics (WebsiteWhitelistManual.Core.Tests) exactly — this class
/// itself cannot be unit tested outside a real Windows machine.
/// </summary>
public sealed class WindowsRegistryAdapter : IWindowsRegistry
{
    public bool SubKeyExists(string subKeyPath)
    {
        using var key = Registry.LocalMachine.OpenSubKey(subKeyPath, writable: false);
        return key is not null;
    }

    public void EnsureSubKeyExists(string subKeyPath)
    {
        using var key = Registry.LocalMachine.CreateSubKey(subKeyPath, writable: true);
    }

    public IReadOnlyList<string> GetValueNames(string subKeyPath)
    {
        using var key = Registry.LocalMachine.OpenSubKey(subKeyPath, writable: false);
        return key?.GetValueNames() ?? Array.Empty<string>();
    }

    public string? GetStringValue(string subKeyPath, string valueName)
    {
        using var key = Registry.LocalMachine.OpenSubKey(subKeyPath, writable: false);
        return key?.GetValue(valueName) as string;
    }

    public int? GetDwordValue(string subKeyPath, string valueName)
    {
        using var key = Registry.LocalMachine.OpenSubKey(subKeyPath, writable: false);
        return key?.GetValue(valueName) as int?;
    }

    public void SetStringValue(string subKeyPath, string valueName, string value)
    {
        using var key = Registry.LocalMachine.CreateSubKey(subKeyPath, writable: true)
            ?? throw new InvalidOperationException($"Could not create or open registry key '{subKeyPath}'.");
        key.SetValue(valueName, value, RegistryValueKind.String);
    }

    public void SetDwordValue(string subKeyPath, string valueName, int value)
    {
        using var key = Registry.LocalMachine.CreateSubKey(subKeyPath, writable: true)
            ?? throw new InvalidOperationException($"Could not create or open registry key '{subKeyPath}'.");
        key.SetValue(valueName, value, RegistryValueKind.DWord);
    }

    public void DeleteValue(string subKeyPath, string valueName)
    {
        using var key = Registry.LocalMachine.OpenSubKey(subKeyPath, writable: true);
        key?.DeleteValue(valueName, throwOnMissingValue: false);
    }
}
```

- [ ] **Step 2: Verify it builds**

Run: `dotnet build`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`. No automated behavioral test is possible here — self-review against `IWindowsRegistry`'s contract (and `FakeWindowsRegistry`'s tested semantics) is the only verification available in this environment; real behavior is confirmed by the user on Windows in Task 7.

- [ ] **Step 3: Commit**

```bash
git add src/WebsiteWhitelistManual.App/Services/WindowsRegistryAdapter.cs
git commit -m "feat: add WindowsRegistryAdapter (real IWindowsRegistry via Microsoft.Win32.Registry)"
```

---

### Task 4: WindowsProcessRunner

**Files:**
- Create: `src/WebsiteWhitelistManual.App/Services/WindowsProcessRunner.cs`

**Interfaces:**
- Consumes: `IProcessRunner.Run(string fileName, IReadOnlyList<string> arguments)` (Task 1's hardened signature).
- Produces: `WindowsProcessRunner : IProcessRunner`, registered in DI in Task 6.

- [ ] **Step 1: Implement the runner**

```csharp
// src/WebsiteWhitelistManual.App/Services/WindowsProcessRunner.cs
using System.Diagnostics;
using WebsiteWhitelistManual.Core.Abstractions;

namespace WebsiteWhitelistManual.App.Services;

/// <summary>
/// Real OS-process-backed implementation of IProcessRunner, used to shell
/// out to reg.exe for registry backups. Uses ProcessStartInfo.ArgumentList
/// so arguments are never manually quoted or vulnerable to quoting bugs.
/// </summary>
public sealed class WindowsProcessRunner : IProcessRunner
{
    public ProcessResult Run(string fileName, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start process '{fileName}'.");

        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new ProcessResult(process.ExitCode, standardOutput, standardError);
    }
}
```

- [ ] **Step 2: Verify it builds**

Run: `dotnet build`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] **Step 3: Commit**

```bash
git add src/WebsiteWhitelistManual.App/Services/WindowsProcessRunner.cs
git commit -m "feat: add WindowsProcessRunner (real IProcessRunner via System.Diagnostics.Process)"
```

---

### Task 5: WindowsLocalAccountSource

**Files:**
- Create: `src/WebsiteWhitelistManual.App/Services/WindowsLocalAccountSource.cs`

**Interfaces:**
- Consumes: `ILocalAccountSource` and `LocalAccountInfo(string AccountName, bool IsAdministrator, bool IsBuiltIn)` (`Core`, already merged).
- Produces: `WindowsLocalAccountSource : ILocalAccountSource`, registered in DI in Task 6.

- [ ] **Step 1: Add the `System.DirectoryServices.AccountManagement` package**

```bash
dotnet add src/WebsiteWhitelistManual.App/WebsiteWhitelistManual.App.csproj package System.DirectoryServices.AccountManagement
```

- [ ] **Step 2: Implement the source**

```csharp
// src/WebsiteWhitelistManual.App/Services/WindowsLocalAccountSource.cs
using System.DirectoryServices.AccountManagement;
using WebsiteWhitelistManual.Core.Abstractions;
using WebsiteWhitelistManual.Core.Models;

namespace WebsiteWhitelistManual.App.Services;

/// <summary>
/// Real local-machine-backed implementation of ILocalAccountSource, used
/// by Core's LocalAccountInspector to list Windows user accounts.
/// </summary>
public sealed class WindowsLocalAccountSource : ILocalAccountSource
{
    private static readonly HashSet<string> BuiltInAccountNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Administrator", "Guest", "DefaultAccount", "WDAGUtilityAccount"
    };

    public IReadOnlyList<LocalAccountInfo> GetLocalAccounts()
    {
        using var context = new PrincipalContext(ContextType.Machine);
        var administratorNames = GetAdministratorGroupMemberNames(context);

        using var userPrincipalTemplate = new UserPrincipal(context);
        using var searcher = new PrincipalSearcher(userPrincipalTemplate);
        using var results = searcher.FindAll();

        var accounts = new List<LocalAccountInfo>();
        foreach (var result in results)
        {
            using var user = result as UserPrincipal;
            if (user?.SamAccountName is null)
            {
                continue;
            }

            accounts.Add(new LocalAccountInfo(
                AccountName: user.SamAccountName,
                IsAdministrator: administratorNames.Contains(user.SamAccountName),
                IsBuiltIn: BuiltInAccountNames.Contains(user.SamAccountName)));
        }

        return accounts;
    }

    private static HashSet<string> GetAdministratorGroupMemberNames(PrincipalContext context)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var administrators = GroupPrincipal.FindByIdentity(context, "Administrators");
        if (administrators is null)
        {
            return names;
        }

        var members = administrators.GetMembers();
        foreach (var member in members)
        {
            using var disposableMember = member;
            if (disposableMember.SamAccountName is not null)
            {
                names.Add(disposableMember.SamAccountName);
            }
        }

        return names;
    }
}
```

- [ ] **Step 3: Verify it builds**

Run: `dotnet build`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] **Step 4: Commit**

```bash
git add src/WebsiteWhitelistManual.App/WebsiteWhitelistManual.App.csproj src/WebsiteWhitelistManual.App/Services/WindowsLocalAccountSource.cs
git commit -m "feat: add WindowsLocalAccountSource (real ILocalAccountSource via System.DirectoryServices.AccountManagement)"
```

---

### Task 6: DI composition root and MainWindow shell

**Files:**
- Modify: `src/WebsiteWhitelistManual.App/App.xaml`
- Modify: `src/WebsiteWhitelistManual.App/App.xaml.cs`
- Modify: `src/WebsiteWhitelistManual.App/MainWindow.xaml`
- Modify: `src/WebsiteWhitelistManual.App/MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `WindowsRegistryAdapter`/`WindowsProcessRunner`/`WindowsLocalAccountSource` (Tasks 3-5), `IRegistryPolicyReader`/`IRegistryPolicyWriter`/`IRegistryBackupService`/`ILocalAccountInspector` and their concrete `Core` implementations (already merged).
- Produces: a running (on Windows) `App`/`MainWindow` pair. `RootNavigation` (the `NavigationView`'s `x:Name`) is the anchor the next plan wires page-switching onto — **this task adds no page-switching logic**, only the static menu items.

- [ ] **Step 1: Add the `Microsoft.Extensions.Hosting` package**

```bash
dotnet add src/WebsiteWhitelistManual.App/WebsiteWhitelistManual.App.csproj package Microsoft.Extensions.Hosting
```

- [ ] **Step 2: Replace `App.xaml`**

```xml
<!-- src/WebsiteWhitelistManual.App/App.xaml -->
<Application x:Class="WebsiteWhitelistManual.App.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ui:ControlsDictionary />
                <ui:ThemesDictionary Theme="Light" />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

- [ ] **Step 3: Replace `App.xaml.cs`**

```csharp
// src/WebsiteWhitelistManual.App/App.xaml.cs
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WebsiteWhitelistManual.App.Services;
using WebsiteWhitelistManual.Core.Abstractions;
using WebsiteWhitelistManual.Core.Services;

namespace WebsiteWhitelistManual.App;

public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                services.AddSingleton<IWindowsRegistry, WindowsRegistryAdapter>();
                services.AddSingleton<IProcessRunner, WindowsProcessRunner>();
                services.AddSingleton<ILocalAccountSource, WindowsLocalAccountSource>();
                services.AddSingleton<IRegistryPolicyReader, RegistryPolicyReader>();
                services.AddSingleton<IRegistryPolicyWriter, RegistryPolicyWriter>();
                services.AddSingleton<IRegistryBackupService, RegistryBackupService>();
                services.AddSingleton<ILocalAccountInspector, LocalAccountInspector>();
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        await _host.StartAsync();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync();
        _host.Dispose();
        base.OnExit(e);
    }
}
```

- [ ] **Step 4: Replace `MainWindow.xaml`**

```xml
<!-- src/WebsiteWhitelistManual.App/MainWindow.xaml -->
<ui:FluentWindow x:Class="WebsiteWhitelistManual.App.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
        Title="網站白名單設定工具"
        Height="720" Width="1024"
        ExtendsContentIntoTitleBar="True"
        WindowBackdropType="Mica"
        WindowCornerPreference="Round">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>

        <ui:TitleBar Grid.Row="0" Title="網站白名單設定工具" />

        <ui:NavigationView x:Name="RootNavigation" Grid.Row="1">
            <ui:NavigationView.MenuItems>
                <ui:NavigationViewItem Content="首頁" />
                <ui:NavigationViewItem Content="選擇瀏覽器" />
                <ui:NavigationViewItem Content="允許的網站" />
                <ui:NavigationViewItem Content="進階選項" />
                <ui:NavigationViewItem Content="套用前確認" />
                <ui:NavigationViewItem Content="完成與驗證" />
            </ui:NavigationView.MenuItems>
        </ui:NavigationView>
    </Grid>
</ui:FluentWindow>
```

- [ ] **Step 5: Replace `MainWindow.xaml.cs`**

```csharp
// src/WebsiteWhitelistManual.App/MainWindow.xaml.cs
using Wpf.Ui.Controls;

namespace WebsiteWhitelistManual.App;

public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
```

- [ ] **Step 6: Verify it builds**

Run: `dotnet build`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)` across all projects.

- [ ] **Step 7: Commit**

```bash
git add src/WebsiteWhitelistManual.App/WebsiteWhitelistManual.App.csproj src/WebsiteWhitelistManual.App/App.xaml src/WebsiteWhitelistManual.App/App.xaml.cs src/WebsiteWhitelistManual.App/MainWindow.xaml src/WebsiteWhitelistManual.App/MainWindow.xaml.cs
git commit -m "feat: wire DI composition root and FluentWindow+NavigationView shell"
```

---

### Task 7: Publishing instructions and Windows handoff

**Files:**
- Create: `PUBLISHING.md`

**Interfaces:** none — this is documentation only, and the task where the user takes over verification.

- [ ] **Step 1: Write the publishing/handoff doc**

```markdown
# 建置與發佈說明

這份工具用 .NET 10 + WPF 寫成，**必須在 Windows 上實際執行測試**。`EnableWindowsTargeting=true` 讓程式碼可以在 Mac 上用 `dotnet build` 編譯檢查（本專案開發過程一路都是這樣驗證的），但看畫面、跑 UAC、寫登錄檔都只能在真正的 Windows 上做。

## 開發用 build（快速檢查編譯錯誤）

```
dotnet build
```

## 正式發佈（單一 .exe，免另裝 .NET Runtime）

```
dotnet publish src/WebsiteWhitelistManual.App/WebsiteWhitelistManual.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

發佈出來的執行檔在：
`src/WebsiteWhitelistManual.App/bin/Release/net10.0-windows/win-x64/publish/`

## 這一版要驗證什麼

1. `dotnet build` 沒有錯誤或警告（應該已經在這裡的 Mac 上驗證過了，但你自己的 Windows 機器上再跑一次確認環境一致）。
2. 執行 `dotnet run --project src/WebsiteWhitelistManual.App`（或雙擊發佈出來的 .exe）會跳 UAC 要求系統管理員權限——這是 `app.manifest` 裡 `requireAdministrator` 的效果，是預期行為。
3. 同意 UAC 後，應該看到一個標題「網站白名單設定工具」的視窗，左側有 NavigationView，選單依序是：首頁、選擇瀏覽器、允許的網站、進階選項、套用前確認、完成與驗證。**這一版選單項目點了還不會切換畫面內容**——實際頁面內容跟導覽邏輯是下一份計畫的範圍，這裡只驗證外殼、DI 組裝、UAC 提權能不能正常運作。

## 不要在公司電腦上測

照專案既有的規則，UAC 授權跟登錄檔讀寫操作只在你自己的機器或朋友的筆電上測，不要在公司電腦上跑。

## 回報方式

- **`dotnet build`/`dotnet publish` 編譯期錯誤**：把完整錯誤訊息貼回來，我來修。
- **跑不起來、UAC 沒跳、視窗長得不對、當掉**：截圖 + 描述症狀，我來對照修。
```

- [ ] **Step 2: Commit**

```bash
git add PUBLISHING.md
git commit -m "docs: add build/publish instructions and Windows verification handoff"
```

---

## After this plan

The App project builds cleanly on this machine (verified per-task via `dotnet build`), with real Windows-backed services wired through DI and a `FluentWindow`+`NavigationView` shell showing the 6 menu items from the spec. Nothing in this plan has been run — the user takes over at `PUBLISHING.md` to build, launch, and confirm UAC/window rendering on their own Windows machine. Once that's confirmed working, the next plan adds: page-switching wiring on `RootNavigation`, the 6 real pages (Dashboard + Step 1-5) with their ViewModels and actual content per the spec's 元件/資料流 sections, and the "查看完整設定值" dialog.
