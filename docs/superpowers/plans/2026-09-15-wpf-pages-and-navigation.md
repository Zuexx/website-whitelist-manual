# WPF Pages and Navigation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give `WebsiteWhitelistManual.App`'s `NavigationView` shell real content — a `DashboardPage` plus five wizard step pages (`Step1BrowserPage` … `Step5CompletePage`), each with a `CommunityToolkit.Mvvm`-based ViewModel, wired to the already-merged `Core` services (`IRegistryPolicyReader`, `IRegistryPolicyWriter`, `IRegistryBackupService`, `ILocalAccountInspector`) through the DI container built in the `wpf-app-shell` plan. Visual design follows the Stitch mockups (`stitch_website_allowlist_desktop_app/`) and its `guardian_clear/DESIGN.md` color/type/spacing tokens, with one deliberate departure: every number or status claim that the app cannot actually verify (block counts, 7-day trend charts, "site reachable" HTTP checks) is either dropped or replaced with a real, registry-diff-based equivalent.

**Architecture:** `RootNavigation` (the `NavigationView` from `wpf-app-shell`) gets a `SelectionChanged` handler that resolves the target page from the DI container (`IServiceProvider`) and assigns it to `RootNavigation.Content` — not WPF-UI's `INavigationViewPageProvider`/`TargetPageType` convention, because that convention expects parameterless page constructors and this app's pages need constructor-injected ViewModels. Each page is a plain `Page` registered `Transient` in DI (a fresh page+ViewModel per navigation, so Step pages always reflect the current `WizardConfiguration` state); `WizardConfiguration` itself is a mutable `Singleton` — the one piece of state carried between steps for the lifetime of the app. `MainWindow` owns the `IServiceProvider` reference (constructor-injected) so its navigation handler can resolve pages on demand.

**Tech Stack:** .NET 10 SDK, WPF (`net10.0-windows`), WPF-UI 4.3.0 (`FluentWindow`, `NavigationView`, `InfoBar`, `ToggleSwitch`, `CardControl` etc.), `CommunityToolkit.Mvvm` (`ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`, `IAsyncRelayCommand` for the Step 4 apply flow), `Microsoft.Extensions.DependencyInjection`.

**Spec:** `docs/superpowers/specs/2026-09-15-website-whitelist-wpf-app-design.md`

## Global Constraints

- **No fabricated data, anywhere.** Three categories, three different treatments — do not blur them:
  1. **Pure fiction with no real substitute** (今日已阻絕 N 次, 過去 7 天防護成效折線圖): omit entirely. Chromium's `URLBlocklist` policy has no readable block-count log; there is nothing honest to put in that space, so the space simply isn't in these pages.
  2. **Fiction with a real substitute** (「已抽樣測試 HTTP 200」「已導向封鎖頁」在 Step 5): replace with a registry-diff-based check — re-read the policy snapshot after `Apply()` and compare each expected key/value against what was actually read back. Label these rows exactly as what they are: "機碼已寫入並核對" (key written and confirmed), never "網站已測試可開啟" (site tested reachable) — the app never makes an HTTP request to any allowlisted or blocked domain.
  3. **Real action, manual confirmation** (Stitch's「即時開啟受控瀏覽器驗證」button on Step 5): keep as a literal `Process.Start` of the OS default browser against one allowlisted URL, for the parent to eyeball. No polling, no result captured, no checkmark tied to its outcome — the button's job ends the moment the browser window opens.
  4. **Step 2's「白名單分類分佈」donut/percentage breakdown is the one Stitch stat that stays fully real**: it is computed live from `AllowlistSite.CategoryLabel` values the parent has actually entered in the current `WizardConfiguration`, so unlike the block-count stats it is not invented — just make sure it recomputes from live state, never from a hardcoded sample.
- **Never use `BrowserGuestModeEnabled`.** That key name appears only in the Stitch mockup and was never verified against `manual.html`. The real, spec-canonical mechanism for "disable account switching" is `PolicyKeys.BrowserSigninValueName` (`BrowserSignin`) = `0`, already implemented in `RegistryPolicyWriter`/`RegistryPolicyReader`. Every "停用帳號切換" UI label in this plan binds to `AdvancedOptionsState.DisableAccountSwitching`, never to a new field.
- **`DeveloperToolsAvailability` = `2` disables dev tools** (per `PolicyKeys.DeveloperToolsAvailabilityValueName` and `RegistryPolicyWriter.ApplyToBrowser`) — already correct in `Core`, do not reinterpret this value in any new UI code or copy text.
- **Firefox is out of scope for v1.** Do not add a Firefox/other-browser card, toggle, or model member anywhere in these pages.
- **`WizardConfiguration` is immutable-record-shaped but the wizard needs to accumulate edits across pages.** Since `WizardConfiguration`'s constructor takes final `BrowserTargets`/`AllowlistSites`/`AdvancedOptions` and exposes no setters, the DI-registered singleton is actually a mutable wrapper, `WizardConfigurationStore` (introduced in Task 4), that holds a replaceable `WizardConfiguration` instance and raises a change notification; pages read/write through the store, never by constructing `WizardConfiguration` for themselves except when calling the store's update method.
- **Navigation is not linear-locked.** Every page is reachable directly from the left nav at any time (per spec's 架構 section) — only the Step 4 apply button is gated (`WizardConfigurationStore.Current.CanApply`), never the nav itself.
- **Registry access from a ViewModel must never block the UI thread.** `IRegistryPolicyReader.ReadSnapshot`, `IRegistryBackupService.Backup`, and `IRegistryPolicyWriter.Apply` are synchronous Core APIs; every call site in this plan wraps them in `Task.Run` from an `IAsyncRelayCommand`, never called directly on the UI thread's synchronous path.
- **`IProcessRunner.Run` (used transitively by `RegistryBackupService`, which shells out to `reg.exe`) has no timeout today.** This plan does not change `IProcessRunner`'s signature (that would ripple back into the already-merged, already-tested `Core` project). Instead, the Step 4 ViewModel's backup call runs inside `Task.Run(..., cancellationToken)` racing a 10-second `Task.Delay` via `Task.WhenAny`; if the delay wins, the ViewModel reports a timeout error and does not proceed to `Apply()`. This bounds the *caller's* wait without needing to touch `Core`. A real process-kill-on-timeout belongs in `WindowsProcessRunner` itself and is out of scope here (note it, don't fix it).
- **Every page's code-behind stays minimal**: constructor takes the ViewModel via DI, sets `DataContext`, calls `InitializeComponent()`. All logic lives in the ViewModel. No event handlers in code-behind except where WPF-UI controls require a CLR event with no command hook (rare; call this out inline if it happens).
- **Colors and type come from a shared `ResourceDictionary`**, not per-page hardcoded hex strings — built once in Task 3, referenced by `DynamicResource`/`StaticResource` everywhere after.
- Run `dotnet build` after every task; the final task's manual-verification handoff is the only step that requires the user's own Windows machine (page rendering, InfoBar visuals, real registry reads).

## Design tokens (from `stitch_website_allowlist_desktop_app/guardian_clear/DESIGN.md`)

| Token | Value | Use |
|---|---|---|
| `Brush.Primary` | `#0F766E` | Primary buttons, active nav item, focus ring, active toggle |
| `Brush.PrimaryHover` | `#115E59` | Primary button hover |
| `Brush.PrimaryPressed` | `#134E4A` | Primary button pressed |
| `Brush.Secondary` (emerald) | `#10B981` | Safe/verified/allowlist status, success checkmarks |
| `Brush.SecondaryTint` | `#ECFDF5` | Success status container background |
| `Brush.Tertiary` (slate blue) | `#3B82F6` | Info badges, help callouts |
| `Brush.TertiaryTint` | `#EFF6FF` | Info container background |
| `Brush.Warning` | `#D97706` | Blocked/warning text |
| `Brush.WarningStrong` | `#C2410C` | Destructive confirmation text |
| `Brush.WarningTint` | `#FFFBEB` | Warning container background |
| `Brush.TextPrimary` | `#1E293B` | Headlines |
| `Brush.TextBody` | `#334155` | Body text |
| `Brush.TextMuted` | `#64748B` | Helper/caption text |
| `Brush.Border` | `#E2E8F0` | Card borders, dividers |
| `Brush.CanvasBackground` | `#F8F9FA` | Window/page background |
| `CornerRadius.Control` | `8` | Buttons, inputs, badges |
| `CornerRadius.Card` | `12` | Cards, panels |
| `CornerRadius.Pill` | `9999` | Status chips |
| `Spacing.Gutter` | `16` | Between cards |
| `Spacing.Margin` | `24` | Outer page padding |

---

### Task 1: Add `CommunityToolkit.Mvvm`

**Files:**
- Modify: `src/WebsiteWhitelistManual.App/WebsiteWhitelistManual.App.csproj`

**Interfaces:**
- Produces: `CommunityToolkit.Mvvm`'s source generators (`ObservableObject`, `[ObservableProperty]`, `[RelayCommand]`) available to every ViewModel added in this plan.

- [ ] **Step 1: Add the package**

```bash
dotnet add src/WebsiteWhitelistManual.App/WebsiteWhitelistManual.App.csproj package CommunityToolkit.Mvvm
```

- [ ] **Step 2: Verify it builds**

Run: `dotnet build`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] **Step 3: Commit**

```bash
git add src/WebsiteWhitelistManual.App/WebsiteWhitelistManual.App.csproj
git commit -m "chore: add CommunityToolkit.Mvvm package"
```

---

### Task 2: `ILocalAccountInspector.GetAllAccounts()` for the Dashboard's full account list

**Decision:** The spec's data-flow section says the Dashboard uses `ILocalAccountInspector` "顯示本機帳號清單供核對" (show the local account list for the parent to check against) — the whole point is letting a parent visually confirm which entry is their child's account among *everything* on the machine, including the built-in `Administrator`/`Guest`/`DefaultAccount` rows a parent might otherwise mistake for a real user. `GetRelevantAccounts()` (filters out `IsBuiltIn`) was designed for a different, narrower purpose that no current call site actually needs yet. Rather than repurpose `GetRelevantAccounts()` and risk a future caller silently losing the filter, this task adds a second method, `GetAllAccounts()`, side by side — both stay meaningful, and the Dashboard is explicit about which one it wants.

**Files:**
- Modify: `src/WebsiteWhitelistManual.Core/Services/ILocalAccountInspector.cs`
- Modify: `src/WebsiteWhitelistManual.Core/Services/LocalAccountInspector.cs`
- Modify: `tests/WebsiteWhitelistManual.Core.Tests/LocalAccountInspectorTests.cs` (add coverage; do not remove existing tests)

**Interfaces:**
- Produces: `ILocalAccountInspector.GetAllAccounts()` — returns every account `ILocalAccountSource.GetLocalAccounts()` provides, unfiltered, in the same order.

- [ ] **Step 1: Extend the interface**

```csharp
// src/WebsiteWhitelistManual.Core/Services/ILocalAccountInspector.cs
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
```

- [ ] **Step 2: Implement it**

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

    public IReadOnlyList<LocalAccountInfo> GetAllAccounts()
        => _source.GetLocalAccounts();
}
```

- [ ] **Step 3: Add a test pinning the new method's behavior**

Open `tests/WebsiteWhitelistManual.Core.Tests/LocalAccountInspectorTests.cs`, find the existing fake-account setup used by the `GetRelevantAccounts` tests, and add:

```csharp
    [Fact]
    public void GetAllAccounts_IncludesBuiltInAccounts()
    {
        var source = new FakeLocalAccountSource(new[]
        {
            new LocalAccountInfo("Kid", IsAdministrator: false, IsBuiltIn: false),
            new LocalAccountInfo("Administrator", IsAdministrator: true, IsBuiltIn: true),
        });
        var inspector = new LocalAccountInspector(source);

        var accounts = inspector.GetAllAccounts();

        Assert.Equal(2, accounts.Count);
        Assert.Contains(accounts, a => a.AccountName == "Administrator" && a.IsBuiltIn);
    }
```

(Reuse whatever fake source class name the existing tests in this file already use in place of `FakeLocalAccountSource` if it differs — check the file first.)

- [ ] **Step 4: Run the full suite**

Run: `dotnet test`
Expected: `Passed! ... Total: 59` (58 existing + 1 new).

- [ ] **Step 5: Commit**

```bash
git add src/WebsiteWhitelistManual.Core/Services/ILocalAccountInspector.cs src/WebsiteWhitelistManual.Core/Services/LocalAccountInspector.cs tests/WebsiteWhitelistManual.Core.Tests/LocalAccountInspectorTests.cs
git commit -m "feat: add ILocalAccountInspector.GetAllAccounts for the Dashboard's full account list"
```

---

### Task 3: Design-token `ResourceDictionary`

**Files:**
- Create: `src/WebsiteWhitelistManual.App/Theme/GuardianClearColors.xaml`
- Modify: `src/WebsiteWhitelistManual.App/App.xaml`

**Interfaces:**
- Produces: a merged `ResourceDictionary` of named `SolidColorBrush` resources (see the token table above) and two `CornerRadius` resources, available via `{StaticResource ...}` in every page added later in this plan.

- [ ] **Step 1: Create the resource dictionary**

```xml
<!-- src/WebsiteWhitelistManual.App/Theme/GuardianClearColors.xaml -->
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                     xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <SolidColorBrush x:Key="Brush.Primary" Color="#0F766E"/>
    <SolidColorBrush x:Key="Brush.PrimaryHover" Color="#115E59"/>
    <SolidColorBrush x:Key="Brush.PrimaryPressed" Color="#134E4A"/>

    <SolidColorBrush x:Key="Brush.Secondary" Color="#10B981"/>
    <SolidColorBrush x:Key="Brush.SecondaryTint" Color="#ECFDF5"/>

    <SolidColorBrush x:Key="Brush.Tertiary" Color="#3B82F6"/>
    <SolidColorBrush x:Key="Brush.TertiaryTint" Color="#EFF6FF"/>

    <SolidColorBrush x:Key="Brush.Warning" Color="#D97706"/>
    <SolidColorBrush x:Key="Brush.WarningStrong" Color="#C2410C"/>
    <SolidColorBrush x:Key="Brush.WarningTint" Color="#FFFBEB"/>

    <SolidColorBrush x:Key="Brush.TextPrimary" Color="#1E293B"/>
    <SolidColorBrush x:Key="Brush.TextBody" Color="#334155"/>
    <SolidColorBrush x:Key="Brush.TextMuted" Color="#64748B"/>

    <SolidColorBrush x:Key="Brush.Border" Color="#E2E8F0"/>
    <SolidColorBrush x:Key="Brush.CanvasBackground" Color="#F8F9FA"/>

    <CornerRadius x:Key="CornerRadius.Control">8</CornerRadius>
    <CornerRadius x:Key="CornerRadius.Card">12</CornerRadius>
    <CornerRadius x:Key="CornerRadius.Pill">9999</CornerRadius>

    <Thickness x:Key="Spacing.CardPadding">16</Thickness>
    <Thickness x:Key="Spacing.PageMargin">24</Thickness>

</ResourceDictionary>
```

- [ ] **Step 2: Merge it into `App.xaml`**

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
                <ResourceDictionary Source="Theme/GuardianClearColors.xaml" />
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

- [ ] **Step 3: Verify it builds**

Run: `dotnet build`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] **Step 4: Commit**

```bash
git add src/WebsiteWhitelistManual.App/Theme/GuardianClearColors.xaml src/WebsiteWhitelistManual.App/App.xaml
git commit -m "feat: add Guardian Clear design-token ResourceDictionary"
```

---

### Task 4: `WizardConfigurationStore`, DI registration, and page-switching navigation

**Files:**
- Create: `src/WebsiteWhitelistManual.App/State/WizardConfigurationStore.cs`
- Modify: `src/WebsiteWhitelistManual.App/App.xaml.cs`
- Modify: `src/WebsiteWhitelistManual.App/MainWindow.xaml`
- Modify: `src/WebsiteWhitelistManual.App/MainWindow.xaml.cs`

**Interfaces:**
- Produces: `WizardConfigurationStore` (singleton, mutable wrapper around `WizardConfiguration`, raises `Changed` event), registered pages resolved by `MainWindow`'s navigation handler.
- Consumes: nothing new from `Core` — this is pure `App`-layer plumbing.

- [ ] **Step 1: `WizardConfigurationStore`**

```csharp
// src/WebsiteWhitelistManual.App/State/WizardConfigurationStore.cs
using WebsiteWhitelistManual.Core.Models;

namespace WebsiteWhitelistManual.App.State;

/// <summary>
/// Mutable, app-lifetime holder for the one WizardConfiguration instance the
/// five step pages accumulate edits into. WizardConfiguration itself is an
/// immutable value type (Core, tested independently) — this store is the
/// thin, App-only layer that lets pages replace it as the parent moves
/// through the wizard, and notifies anything watching (Dashboard, Step 4)
/// when that happens.
/// </summary>
public sealed class WizardConfigurationStore
{
    public WizardConfiguration Current { get; private set; } = new(
        browserTargets: Array.Empty<BrowserTarget>(),
        allowlistSites: Array.Empty<AllowlistSite>(),
        advancedOptions: new AdvancedOptionsState());

    public event Action? Changed;

    public void Update(WizardConfiguration next)
    {
        Current = next;
        Changed?.Invoke();
    }
}
```

- [ ] **Step 2: Register the store and the (not-yet-created) pages in `App.xaml.cs`**

Add `using WebsiteWhitelistManual.App.State;` and `using WebsiteWhitelistManual.App.Pages;` at the top, and inside `ConfigureServices`, after the existing `services.AddSingleton<MainWindow>();` line, add:

```csharp
                    services.AddSingleton<WizardConfigurationStore>();

                    services.AddTransient<DashboardPage>();
                    services.AddTransient<Step1BrowserPage>();
                    services.AddTransient<Step2SitesPage>();
                    services.AddTransient<Step3AdvancedPage>();
                    services.AddTransient<Step4ConfirmPage>();
                    services.AddTransient<Step5CompletePage>();
```

These six `AddTransient` lines will not compile until Tasks 5–10 create the page classes — that is expected and resolved task-by-task; do not skip ahead and stub them out.

- [ ] **Step 3: Rewrite `MainWindow.xaml`** — two-section nav (首頁 pinned above, the 5 steps grouped under a "設定精靈導覽" header, matching the Stitch left rail), icons on every item, and a `Frame`-free content host (`ContentControl`) that the code-behind swaps directly:

```xml
<!-- src/WebsiteWhitelistManual.App/MainWindow.xaml -->
<ui:FluentWindow x:Class="WebsiteWhitelistManual.App.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
        Title="網站白名單設定工具"
        Height="720" Width="1024"
        Background="{StaticResource Brush.CanvasBackground}"
        ExtendsContentIntoTitleBar="True"
        WindowBackdropType="Mica"
        WindowCornerPreference="Round">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>

        <ui:TitleBar Grid.Row="0" Title="網站白名單設定工具" />

        <ui:NavigationView x:Name="RootNavigation" Grid.Row="1"
                            SelectionChanged="RootNavigation_SelectionChanged">
            <ui:NavigationView.MenuItems>
                <ui:NavigationViewItem x:Name="DashboardNavItem" Content="首頁" Icon="{ui:SymbolIcon Home24}" Tag="Dashboard"/>
                <ui:NavigationViewItem Content="設定精靈導覽" IsEnabled="False" FontWeight="SemiBold"/>
                <ui:NavigationViewItem x:Name="Step1NavItem" Content="1. 選擇瀏覽器" Icon="{ui:SymbolIcon Globe24}" Tag="Step1"/>
                <ui:NavigationViewItem x:Name="Step2NavItem" Content="2. 允許的網站" Icon="{ui:SymbolIcon CheckmarkCircle24}" Tag="Step2"/>
                <ui:NavigationViewItem x:Name="Step3NavItem" Content="3. 進階選項" Icon="{ui:SymbolIcon Options24}" Tag="Step3"/>
                <ui:NavigationViewItem x:Name="Step4NavItem" Content="4. 套用前確認" Icon="{ui:SymbolIcon ShieldCheckmark24}" Tag="Step4"/>
                <ui:NavigationViewItem x:Name="Step5NavItem" Content="5. 完成與驗證" Icon="{ui:SymbolIcon CheckmarkStarburst24}" Tag="Step5"/>
            </ui:NavigationView.MenuItems>
            <ContentControl x:Name="PageHost" Margin="{StaticResource Spacing.PageMargin}"/>
        </ui:NavigationView>
    </Grid>
</ui:FluentWindow>
```

(If `ui:SymbolIcon`'s named-symbol markup extension syntax doesn't resolve at compile time on the user's exact WPF-UI 4.3.0 install, fall back to `<ui:SymbolIcon Symbol="Home24"/>` as a child element instead of the `Icon="{ui:SymbolIcon ...}"` attribute form — both are valid WPF-UI patterns; try the attribute form first since it keeps the XAML flatter, and switch to the element form only if `dotnet build` reports an XAML markup-extension error on that line.)

- [ ] **Step 4: `MainWindow.xaml.cs`** — resolve pages from DI on selection change:

```csharp
// src/WebsiteWhitelistManual.App/MainWindow.xaml.cs
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using WebsiteWhitelistManual.App.Pages;
using Wpf.Ui.Controls;

namespace WebsiteWhitelistManual.App;

public partial class MainWindow : FluentWindow
{
    private readonly IServiceProvider _services;

    public MainWindow(IServiceProvider services)
    {
        _services = services;
        InitializeComponent();

        PageHost.Content = _services.GetRequiredService<DashboardPage>();
        RootNavigation.SelectedItem = DashboardNavItem;
    }

    private void RootNavigation_SelectionChanged(NavigationView sender, RoutedEventArgs args)
    {
        if (RootNavigation.SelectedItem is not NavigationViewItem { Tag: string tag })
        {
            return;
        }

        PageHost.Content = tag switch
        {
            "Dashboard" => _services.GetRequiredService<DashboardPage>(),
            "Step1" => _services.GetRequiredService<Step1BrowserPage>(),
            "Step2" => _services.GetRequiredService<Step2SitesPage>(),
            "Step3" => _services.GetRequiredService<Step3AdvancedPage>(),
            "Step4" => _services.GetRequiredService<Step4ConfirmPage>(),
            "Step5" => _services.GetRequiredService<Step5CompletePage>(),
            _ => PageHost.Content
        };
    }
}
```

`MainWindow` now takes `IServiceProvider` as a constructor parameter — `Microsoft.Extensions.Hosting`'s container satisfies this automatically since `IServiceProvider` is always resolvable. No change needed to `MainWindow`'s `AddSingleton<MainWindow>()` registration.

- [ ] **Step 5: This task will not compile until Tasks 5–10 exist**

This is expected — Task 4 wires the navigation *mechanism*; Tasks 5–10 supply the six `Page`/ViewModel pairs it references. Do not run `dotnet build` as a pass/fail gate for Task 4 alone. Instead, verify Task 4's own code is correct by inspection (namespaces match, `Tag` strings match the `switch` arms exactly, `WizardConfigurationStore` has no typos), then proceed straight into Task 5 and only run `dotnet build` once Task 5's `DashboardPage` exists — at that point four of the six `AddTransient`/`switch` references still won't resolve, so build errors are expected until Task 10 completes. Each of Tasks 5–10 below ends with its own `dotnet build`, and the error count should strictly decrease task by task; Task 10's build is the first one required to be clean.

- [ ] **Step 6: Commit**

```bash
git add src/WebsiteWhitelistManual.App/State/WizardConfigurationStore.cs src/WebsiteWhitelistManual.App/App.xaml.cs src/WebsiteWhitelistManual.App/MainWindow.xaml src/WebsiteWhitelistManual.App/MainWindow.xaml.cs
git commit -m "feat: wire WizardConfigurationStore and DI-resolved page navigation on RootNavigation"
```

---

### Task 5: `DashboardPage` + `DashboardViewModel` + "查看完整設定值" dialog

**Files:**
- Create: `src/WebsiteWhitelistManual.App/Pages/DashboardPage.xaml`
- Create: `src/WebsiteWhitelistManual.App/Pages/DashboardPage.xaml.cs`
- Create: `src/WebsiteWhitelistManual.App/ViewModels/DashboardViewModel.cs`
- Create: `src/WebsiteWhitelistManual.App/Pages/PolicyDetailDialog.xaml`
- Create: `src/WebsiteWhitelistManual.App/Pages/PolicyDetailDialog.xaml.cs`
- Modify: `src/WebsiteWhitelistManual.App/App.xaml.cs` (register `DashboardViewModel`)

**Interfaces:**
- Consumes: `IRegistryPolicyReader.ReadSnapshot(IReadOnlyList<BrowserTarget>)`, `ILocalAccountInspector.GetAllAccounts()` (Task 2), `WizardConfigurationStore.Current` (read-only, to know which browsers are in scope for the snapshot — defaults to both Edge and Chrome so the Dashboard always shows both browsers' real state regardless of wizard progress).
- Produces: `DashboardPage`, `DashboardViewModel`, `PolicyDetailDialog` (a plain read-only `Window`, opened modally from both `DashboardPage` and, later, `Step4ConfirmPage`).

- [ ] **Step 1: `DashboardViewModel`**

```csharp
// src/WebsiteWhitelistManual.App/ViewModels/DashboardViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebsiteWhitelistManual.Core.Models;
using WebsiteWhitelistManual.Core.Services;

namespace WebsiteWhitelistManual.App.ViewModels;

public sealed partial class DashboardViewModel : ObservableObject
{
    private readonly IRegistryPolicyReader _policyReader;
    private readonly ILocalAccountInspector _accountInspector;

    public DashboardViewModel(IRegistryPolicyReader policyReader, ILocalAccountInspector accountInspector)
    {
        _policyReader = policyReader;
        _accountInspector = accountInspector;
        Refresh();
    }

    [ObservableProperty]
    private PolicySnapshot _snapshot = new(Array.Empty<BrowserPolicySnapshot>());

    [ObservableProperty]
    private IReadOnlyList<LocalAccountInfo> _accounts = Array.Empty<LocalAccountInfo>();

    public bool IsProtectionActive => Snapshot.Browsers.Any(b => b.PolicyKeyExists && b.AllowedUrls.Count > 0);

    public int AllowedSiteCount => Snapshot.Browsers.SelectMany(b => b.AllowedUrls).Distinct().Count();

    [RelayCommand]
    private void Refresh()
    {
        Snapshot = _policyReader.ReadSnapshot(new[] { BrowserTarget.Edge, BrowserTarget.Chrome });
        Accounts = _accountInspector.GetAllAccounts();
        OnPropertyChanged(nameof(IsProtectionActive));
        OnPropertyChanged(nameof(AllowedSiteCount));
    }
}
```

- [ ] **Step 2: `DashboardPage.xaml`** — status banner (real `IsProtectionActive`/`AllowedSiteCount`, no block-count card, no trend chart), account list, entry point to the detail dialog:

```xml
<!-- src/WebsiteWhitelistManual.App/Pages/DashboardPage.xaml -->
<Page x:Class="WebsiteWhitelistManual.App.Pages.DashboardPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
      Title="首頁">
    <ScrollViewer VerticalScrollBarVisibility="Auto">
        <StackPanel>

            <ui:CardControl Margin="0,0,0,16"
                             Background="{StaticResource Brush.SecondaryTint}">
                <StackPanel Orientation="Horizontal">
                    <ui:SymbolIcon Symbol="ShieldCheckmark24" Foreground="{StaticResource Brush.Secondary}" FontSize="28" Margin="0,0,12,0"/>
                    <StackPanel>
                        <TextBlock Text="{Binding IsProtectionActive, StringFormat='目前防護狀態：{0}', Converter={StaticResource BoolToProtectionTextConverter}}"
                                   FontSize="16" FontWeight="SemiBold" Foreground="{StaticResource Brush.TextPrimary}"/>
                        <TextBlock Text="{Binding AllowedSiteCount, StringFormat='目前允許 {0} 個網站'}"
                                   Foreground="{StaticResource Brush.TextMuted}" Margin="0,4,0,0"/>
                    </StackPanel>
                </StackPanel>
            </ui:CardControl>

            <ui:Button Content="重新整理狀態" Command="{Binding RefreshCommand}" Margin="0,0,0,16" HorizontalAlignment="Left"/>

            <TextBlock Text="本機使用者帳號" FontSize="16" FontWeight="SemiBold" Margin="0,0,0,8"/>
            <ItemsControl ItemsSource="{Binding Accounts}">
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <ui:CardControl Margin="0,0,0,8">
                            <StackPanel Orientation="Horizontal">
                                <TextBlock Text="{Binding AccountName}" FontWeight="SemiBold" Width="200"/>
                                <TextBlock Text="{Binding IsAdministrator, StringFormat='系統管理員: {0}'}" Width="160" Foreground="{StaticResource Brush.TextMuted}"/>
                                <TextBlock Text="{Binding IsBuiltIn, StringFormat='內建帳號: {0}'}" Foreground="{StaticResource Brush.TextMuted}"/>
                            </StackPanel>
                        </ui:CardControl>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>

            <ui:Button Content="查看完整設定值 (進階)" Click="OnViewFullPolicyClick" Margin="0,16,0,0" HorizontalAlignment="Left"/>

        </StackPanel>
    </ScrollViewer>
</Page>
```

- [ ] **Step 3: `DashboardPage.xaml.cs`**

```csharp
// src/WebsiteWhitelistManual.App/Pages/DashboardPage.xaml.cs
using System.Windows.Controls;
using WebsiteWhitelistManual.App.ViewModels;

namespace WebsiteWhitelistManual.App.Pages;

public partial class DashboardPage : Page
{
    private readonly DashboardViewModel _viewModel;

    public DashboardPage(DashboardViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = _viewModel;
        InitializeComponent();
    }

    private void OnViewFullPolicyClick(object sender, System.Windows.RoutedEventArgs e)
    {
        var dialog = new PolicyDetailDialog(_viewModel.Snapshot) { Owner = System.Windows.Application.Current.MainWindow };
        dialog.ShowDialog();
    }
}
```

A converter (`BoolToProtectionTextConverter`, mapping `true`→"已啟用白名單管制", `false`→"尚未設定") is referenced above but not yet defined — add it now:

```csharp
// src/WebsiteWhitelistManual.App/Converters/BoolToProtectionTextConverter.cs
using System.Globalization;
using System.Windows.Data;

namespace WebsiteWhitelistManual.App.Converters;

public sealed class BoolToProtectionTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "已啟用白名單管制" : "尚未設定";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
```

Register it as a resource in `App.xaml`, inside `<Application.Resources><ResourceDictionary>` (as a sibling of `<ResourceDictionary.MergedDictionaries>`, so add it as a direct child alongside that element, not inside it):

```xml
    <local:BoolToProtectionTextConverter x:Key="BoolToProtectionTextConverter" xmlns:local="clr-namespace:WebsiteWhitelistManual.App.Converters"/>
```

(Adjust the `xmlns:local` declaration to sit on `<Application>` itself if WPF's XAML parser rejects a namespace declared inline on a resource element — check the first `dotnet build` in this task; if that line errors, move `xmlns:local="clr-namespace:WebsiteWhitelistManual.App.Converters"` up onto the root `<Application ...>` tag instead and drop it from the resource line.)

- [ ] **Step 4: `PolicyDetailDialog`** — read-only text view of a `PolicySnapshot`, formatted to match `manual.html`'s key/value table layout:

```xml
<!-- src/WebsiteWhitelistManual.App/Pages/PolicyDetailDialog.xaml -->
<Window x:Class="WebsiteWhitelistManual.App.Pages.PolicyDetailDialog"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="完整設定值" Width="640" Height="480"
        WindowStartupLocation="CenterOwner">
    <ScrollViewer Margin="16">
        <TextBox x:Name="ContentTextBox" IsReadOnly="True" TextWrapping="NoWrap"
                 FontFamily="Consolas" AcceptsReturn="True"
                 VerticalScrollBarVisibility="Auto" HorizontalScrollBarVisibility="Auto"/>
    </ScrollViewer>
</Window>
```

```csharp
// src/WebsiteWhitelistManual.App/Pages/PolicyDetailDialog.xaml.cs
using System.Text;
using System.Windows;
using WebsiteWhitelistManual.Core.Models;

namespace WebsiteWhitelistManual.App.Pages;

public partial class PolicyDetailDialog : Window
{
    public PolicyDetailDialog(PolicySnapshot snapshot)
    {
        InitializeComponent();
        ContentTextBox.Text = Format(snapshot);
    }

    private static string Format(PolicySnapshot snapshot)
    {
        var builder = new StringBuilder();
        foreach (var browser in snapshot.Browsers)
        {
            builder.AppendLine($"=== {browser.BrowserId} ===");
            builder.AppendLine($"政策機碼存在: {browser.PolicyKeyExists}");
            builder.AppendLine($"URLBlocklist: {string.Join(", ", browser.BlockedUrls)}");
            builder.AppendLine($"URLAllowlist: {string.Join(", ", browser.AllowedUrls)}");
            builder.AppendLine($"停用無痕模式: {browser.IncognitoDisabled}");
            builder.AppendLine($"停用帳號切換 (BrowserSignin=0): {browser.BrowserSigninDisabled}");
            builder.AppendLine($"停用開發人員工具: {browser.DeveloperToolsDisabled}");
            builder.AppendLine();
        }
        return builder.ToString();
    }
}
```

- [ ] **Step 5: Register `DashboardViewModel` in DI**

In `App.xaml.cs`'s `ConfigureServices`, add `services.AddTransient<DashboardViewModel>();` next to the `DashboardPage` registration from Task 4.

- [ ] **Step 6: Attempt a build**

Run: `dotnet build`
Expected: still FAILS — `Step1BrowserPage` through `Step5CompletePage` don't exist yet (Task 4's `switch`/`AddTransient` lines reference them). Confirm the *only* remaining errors are "type or namespace 'Step1BrowserPage' could not be found" (and Step2–5), not anything inside `DashboardPage`/`DashboardViewModel`/`PolicyDetailDialog`. If any error points inside this task's own new files, fix it now before proceeding.

- [ ] **Step 7: Commit**

```bash
git add src/WebsiteWhitelistManual.App/Pages/DashboardPage.xaml src/WebsiteWhitelistManual.App/Pages/DashboardPage.xaml.cs src/WebsiteWhitelistManual.App/ViewModels/DashboardViewModel.cs src/WebsiteWhitelistManual.App/Pages/PolicyDetailDialog.xaml src/WebsiteWhitelistManual.App/Pages/PolicyDetailDialog.xaml.cs src/WebsiteWhitelistManual.App/Converters/BoolToProtectionTextConverter.cs src/WebsiteWhitelistManual.App/App.xaml.cs src/WebsiteWhitelistManual.App/App.xaml
git commit -m "feat: add DashboardPage with real registry-backed status and full-account list"
```

---

### Task 6: `Step1BrowserPage` + `Step1BrowserViewModel`

**Files:**
- Create: `src/WebsiteWhitelistManual.App/Pages/Step1BrowserPage.xaml`
- Create: `src/WebsiteWhitelistManual.App/Pages/Step1BrowserPage.xaml.cs`
- Create: `src/WebsiteWhitelistManual.App/ViewModels/Step1BrowserViewModel.cs`
- Modify: `src/WebsiteWhitelistManual.App/App.xaml.cs`

**Interfaces:**
- Consumes: `WizardConfigurationStore` (Task 4), `BrowserTarget.Edge`/`BrowserTarget.Chrome` (`Core`).
- Produces: `Step1BrowserPage`, `Step1BrowserViewModel`.

- [ ] **Step 1: `Step1BrowserViewModel`**

```csharp
// src/WebsiteWhitelistManual.App/ViewModels/Step1BrowserViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using WebsiteWhitelistManual.App.State;
using WebsiteWhitelistManual.Core.Models;

namespace WebsiteWhitelistManual.App.ViewModels;

public sealed partial class Step1BrowserViewModel : ObservableObject
{
    private readonly WizardConfigurationStore _store;

    public Step1BrowserViewModel(WizardConfigurationStore store)
    {
        _store = store;
        var current = _store.Current.BrowserTargets;
        _isEdgeSelected = current.Any(t => t.Id == BrowserId.Edge);
        _isChromeSelected = current.Any(t => t.Id == BrowserId.Chrome);
    }

    [ObservableProperty]
    private bool _isEdgeSelected;

    [ObservableProperty]
    private bool _isChromeSelected;

    partial void OnIsEdgeSelectedChanged(bool value) => Persist();
    partial void OnIsChromeSelectedChanged(bool value) => Persist();

    private void Persist()
    {
        var targets = new List<BrowserTarget>();
        if (IsEdgeSelected) targets.Add(BrowserTarget.Edge);
        if (IsChromeSelected) targets.Add(BrowserTarget.Chrome);

        var current = _store.Current;
        _store.Update(new WizardConfiguration(targets, current.AllowlistSites, current.AdvancedOptions));
    }
}
```

- [ ] **Step 2: `Step1BrowserPage.xaml`** — two selectable browser cards (Edge, Chrome), no Firefox card per v1 scope:

```xml
<!-- src/WebsiteWhitelistManual.App/Pages/Step1BrowserPage.xaml -->
<Page x:Class="WebsiteWhitelistManual.App.Pages.Step1BrowserPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
      Title="步驟 1：選擇瀏覽器">
    <StackPanel>
        <TextBlock Text="步驟 1：選擇要鎖定的瀏覽器" FontSize="20" FontWeight="Bold" Foreground="{StaticResource Brush.TextPrimary}"/>
        <TextBlock Text="選擇這台筆電上要鎖定的瀏覽器，可以複選。其餘瀏覽器不受本工具管理。"
                   Foreground="{StaticResource Brush.TextBody}" Margin="0,8,0,16" TextWrapping="Wrap"/>

        <StackPanel Orientation="Horizontal">
            <ui:CardControl Width="320" Margin="0,0,16,0">
                <StackPanel>
                    <CheckBox Content="Microsoft Edge" FontWeight="SemiBold" FontSize="16"
                              IsChecked="{Binding IsEdgeSelected, Mode=TwoWay}"/>
                    <TextBlock Text="Windows 內建瀏覽器・系統登錄原則 (Registry/GPO)" Foreground="{StaticResource Brush.TextMuted}" Margin="0,4,0,0" TextWrapping="Wrap"/>
                </StackPanel>
            </ui:CardControl>

            <ui:CardControl Width="320">
                <StackPanel>
                    <CheckBox Content="Google Chrome" FontWeight="SemiBold" FontSize="16"
                              IsChecked="{Binding IsChromeSelected, Mode=TwoWay}"/>
                    <TextBlock Text="全球主流瀏覽器・企業政策原則 (Policies\Google\Chrome)" Foreground="{StaticResource Brush.TextMuted}" Margin="0,4,0,0" TextWrapping="Wrap"/>
                </StackPanel>
            </ui:CardControl>
        </StackPanel>

        <ui:InfoBar Title="為什麼建議同時勾選 Edge 與 Chrome？"
                    Message="避免孩子在其中一個瀏覽器被封鎖時，自行切換至另一個未受管的瀏覽器瀏覽未核准網站。"
                    IsOpen="True" Severity="Informational" Margin="0,24,0,0"/>
    </StackPanel>
</Page>
```

- [ ] **Step 3: `Step1BrowserPage.xaml.cs`**

```csharp
// src/WebsiteWhitelistManual.App/Pages/Step1BrowserPage.xaml.cs
using System.Windows.Controls;
using WebsiteWhitelistManual.App.ViewModels;

namespace WebsiteWhitelistManual.App.Pages;

public partial class Step1BrowserPage : Page
{
    public Step1BrowserPage(Step1BrowserViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
```

- [ ] **Step 4: Register the ViewModel**

In `App.xaml.cs`, add `services.AddTransient<Step1BrowserViewModel>();`.

- [ ] **Step 5: Build**

Run: `dotnet build`
Expected: FAILS only on missing `Step2SitesPage`…`Step5CompletePage` — same rule as Task 5's Step 6.

- [ ] **Step 6: Commit**

```bash
git add src/WebsiteWhitelistManual.App/Pages/Step1BrowserPage.xaml src/WebsiteWhitelistManual.App/Pages/Step1BrowserPage.xaml.cs src/WebsiteWhitelistManual.App/ViewModels/Step1BrowserViewModel.cs src/WebsiteWhitelistManual.App/App.xaml.cs
git commit -m "feat: add Step1BrowserPage bound to WizardConfigurationStore"
```

---

### Task 7: `Step2SitesPage` + `Step2SitesViewModel`

**Files:**
- Create: `src/WebsiteWhitelistManual.App/Pages/Step2SitesPage.xaml`
- Create: `src/WebsiteWhitelistManual.App/Pages/Step2SitesPage.xaml.cs`
- Create: `src/WebsiteWhitelistManual.App/ViewModels/Step2SitesViewModel.cs`
- Create: `src/WebsiteWhitelistManual.App/ViewModels/CategoryBreakdownItem.cs`
- Modify: `src/WebsiteWhitelistManual.App/App.xaml.cs`

**Interfaces:**
- Consumes: `WizardConfigurationStore`, `AllowlistSite.TryCreate(string?, string?, out AllowlistSite?, out string?)` (`Core`, already validates non-empty/no-whitespace/rough-domain-shape per spec's error-handling section — this page must not re-implement or loosen that validation).
- Produces: `Step2SitesPage`, `Step2SitesViewModel`, `CategoryBreakdownItem` (real percentage-by-tag record, computed from live `AllowlistSite.CategoryLabel` values — the one Stitch stat this plan keeps, per Global Constraints).

- [ ] **Step 1: `CategoryBreakdownItem`**

```csharp
// src/WebsiteWhitelistManual.App/ViewModels/CategoryBreakdownItem.cs
namespace WebsiteWhitelistManual.App.ViewModels;

public sealed record CategoryBreakdownItem(string Label, int Count, double Percentage);
```

- [ ] **Step 2: `Step2SitesViewModel`**

```csharp
// src/WebsiteWhitelistManual.App/ViewModels/Step2SitesViewModel.cs
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebsiteWhitelistManual.App.State;
using WebsiteWhitelistManual.Core.Models;

namespace WebsiteWhitelistManual.App.ViewModels;

public sealed partial class Step2SitesViewModel : ObservableObject
{
    private readonly WizardConfigurationStore _store;

    public Step2SitesViewModel(WizardConfigurationStore store)
    {
        _store = store;
        Sites = new ObservableCollection<AllowlistSite>(_store.Current.AllowlistSites);
        Sites.CollectionChanged += (_, _) => { Persist(); RecomputeBreakdown(); };
        RecomputeBreakdown();
    }

    public ObservableCollection<AllowlistSite> Sites { get; }

    [ObservableProperty]
    private string _newDomainInput = string.Empty;

    [ObservableProperty]
    private string? _newCategoryLabel;

    [ObservableProperty]
    private string? _validationError;

    [ObservableProperty]
    private IReadOnlyList<CategoryBreakdownItem> _categoryBreakdown = Array.Empty<CategoryBreakdownItem>();

    [RelayCommand]
    private void AddSite()
    {
        if (!AllowlistSite.TryCreate(NewDomainInput, NewCategoryLabel, out var site, out var error))
        {
            ValidationError = error;
            return;
        }

        ValidationError = null;
        Sites.Add(site!);
        NewDomainInput = string.Empty;
        NewCategoryLabel = null;
    }

    [RelayCommand]
    private void RemoveSite(AllowlistSite site)
    {
        Sites.Remove(site);
    }

    private void Persist()
    {
        var current = _store.Current;
        _store.Update(new WizardConfiguration(current.BrowserTargets, Sites.ToList(), current.AdvancedOptions));
    }

    private void RecomputeBreakdown()
    {
        var total = Sites.Count;
        if (total == 0)
        {
            CategoryBreakdown = Array.Empty<CategoryBreakdownItem>();
            return;
        }

        CategoryBreakdown = Sites
            .GroupBy(s => s.CategoryLabel ?? "未分類")
            .Select(g => new CategoryBreakdownItem(g.Key, g.Count(), Math.Round(100.0 * g.Count() / total, 0)))
            .OrderByDescending(item => item.Count)
            .ToList();
    }
}
```

- [ ] **Step 3: `Step2SitesPage.xaml`** — domain input with validation message, live category breakdown list (no donut chart control needed — a simple percentage list is enough and avoids pulling in a charting library):

```xml
<!-- src/WebsiteWhitelistManual.App/Pages/Step2SitesPage.xaml -->
<Page x:Class="WebsiteWhitelistManual.App.Pages.Step2SitesPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
      Title="步驟 2：允許的網站">
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="*"/>
            <ColumnDefinition Width="240"/>
        </Grid.ColumnDefinitions>

        <StackPanel Grid.Column="0" Margin="0,0,16,0">
            <TextBlock Text="步驟 2：設定允許孩子訪問的網站" FontSize="20" FontWeight="Bold" Foreground="{StaticResource Brush.TextPrimary}"/>
            <TextBlock Text="除了以下列出的網址外，瀏覽器將自動封鎖所有其他網路訪問。子網域視為不同項目，例如 www.example.com 和 example.com 需分別加入。"
                       Foreground="{StaticResource Brush.TextBody}" Margin="0,8,0,16" TextWrapping="Wrap"/>

            <StackPanel Orientation="Horizontal" Margin="0,0,0,4">
                <TextBox Text="{Binding NewDomainInput, UpdateSourceTrigger=PropertyChanged}" Width="280" Margin="0,0,8,0"
                         ui:TextBoxHelper.Placeholder="輸入網域，例如 classroom.google.com"/>
                <TextBox Text="{Binding NewCategoryLabel, UpdateSourceTrigger=PropertyChanged}" Width="160" Margin="0,0,8,0"
                         ui:TextBoxHelper.Placeholder="分類標籤 (選填)"/>
                <ui:Button Content="新增至白名單" Command="{Binding AddSiteCommand}"/>
            </StackPanel>
            <TextBlock Text="{Binding ValidationError}" Foreground="{StaticResource Brush.WarningStrong}" Margin="0,0,0,12"
                       Visibility="{Binding ValidationError, Converter={StaticResource NullToVisibilityConverter}}"/>

            <TextBlock Text="{Binding Sites.Count, StringFormat='目前已允許的網站清單 (共 {0} 個網址)'}" FontWeight="SemiBold" Margin="0,8,0,8"/>
            <ItemsControl ItemsSource="{Binding Sites}">
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <ui:CardControl Margin="0,0,0,8">
                            <Grid>
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="*"/>
                                    <ColumnDefinition Width="Auto"/>
                                    <ColumnDefinition Width="Auto"/>
                                </Grid.ColumnDefinitions>
                                <TextBlock Grid.Column="0" Text="{Binding Domain}" FontWeight="SemiBold"/>
                                <ui:Badge Grid.Column="1" Content="{Binding CategoryLabel}" Margin="8,0" Appearance="Info"/>
                                <ui:Button Grid.Column="2" Content="刪除"
                                           Command="{Binding DataContext.RemoveSiteCommand, RelativeSource={RelativeSource AncestorType=Page}}"
                                           CommandParameter="{Binding}"/>
                            </Grid>
                        </ui:CardControl>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
        </StackPanel>

        <StackPanel Grid.Column="1">
            <TextBlock Text="白名單分類分佈" FontWeight="SemiBold" Margin="0,0,0,8"/>
            <ItemsControl ItemsSource="{Binding CategoryBreakdown}">
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <StackPanel Margin="0,0,0,8">
                            <TextBlock>
                                <Run Text="{Binding Label}"/>
                                <Run Text=" — "/>
                                <Run Text="{Binding Count}"/>
                                <Run Text=" 個 ("/>
                                <Run Text="{Binding Percentage}"/>
                                <Run Text="%)"/>
                            </TextBlock>
                        </StackPanel>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
        </StackPanel>
    </Grid>
</Page>
```

A `NullToVisibilityConverter` is referenced and needs adding (mirrors the pattern from Task 5's converter):

```csharp
// src/WebsiteWhitelistManual.App/Converters/NullToVisibilityConverter.cs
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WebsiteWhitelistManual.App.Converters;

public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
```

Register it in `App.xaml` next to `BoolToProtectionTextConverter` from Task 5.

- [ ] **Step 4: `Step2SitesPage.xaml.cs`**

```csharp
// src/WebsiteWhitelistManual.App/Pages/Step2SitesPage.xaml.cs
using System.Windows.Controls;
using WebsiteWhitelistManual.App.ViewModels;

namespace WebsiteWhitelistManual.App.Pages;

public partial class Step2SitesPage : Page
{
    public Step2SitesPage(Step2SitesViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
```

- [ ] **Step 5: Register the ViewModel**

In `App.xaml.cs`, add `services.AddTransient<Step2SitesViewModel>();`.

- [ ] **Step 6: Build**

Run: `dotnet build`
Expected: FAILS only on missing `Step3AdvancedPage`…`Step5CompletePage`.

- [ ] **Step 7: Commit**

```bash
git add src/WebsiteWhitelistManual.App/Pages/Step2SitesPage.xaml src/WebsiteWhitelistManual.App/Pages/Step2SitesPage.xaml.cs src/WebsiteWhitelistManual.App/ViewModels/Step2SitesViewModel.cs src/WebsiteWhitelistManual.App/ViewModels/CategoryBreakdownItem.cs src/WebsiteWhitelistManual.App/Converters/NullToVisibilityConverter.cs src/WebsiteWhitelistManual.App/App.xaml.cs src/WebsiteWhitelistManual.App/App.xaml
git commit -m "feat: add Step2SitesPage with real (non-fabricated) category breakdown"
```

---

### Task 8: `Step3AdvancedPage` + `Step3AdvancedViewModel`

**Files:**
- Create: `src/WebsiteWhitelistManual.App/Pages/Step3AdvancedPage.xaml`
- Create: `src/WebsiteWhitelistManual.App/Pages/Step3AdvancedPage.xaml.cs`
- Create: `src/WebsiteWhitelistManual.App/ViewModels/Step3AdvancedViewModel.cs`
- Modify: `src/WebsiteWhitelistManual.App/App.xaml.cs`

**Interfaces:**
- Consumes: `WizardConfigurationStore`, `AdvancedOptionsState` (`Core` — already defaults all three flags to `true`, matching the Stitch mockup's "全部預設開啟" pattern; this page must not change that default, only reflect/edit it).

- [ ] **Step 1: `Step3AdvancedViewModel`**

```csharp
// src/WebsiteWhitelistManual.App/ViewModels/Step3AdvancedViewModel.cs
using CommunityToolkit.Mvvm.ComponentModel;
using WebsiteWhitelistManual.App.State;
using WebsiteWhitelistManual.Core.Models;

namespace WebsiteWhitelistManual.App.ViewModels;

public sealed partial class Step3AdvancedViewModel : ObservableObject
{
    private readonly WizardConfigurationStore _store;

    public Step3AdvancedViewModel(WizardConfigurationStore store)
    {
        _store = store;
        var current = _store.Current.AdvancedOptions;
        _disableIncognito = current.DisableIncognito;
        _disableAccountSwitching = current.DisableAccountSwitching;
        _disableDeveloperTools = current.DisableDeveloperTools;
    }

    [ObservableProperty]
    private bool _disableIncognito;

    [ObservableProperty]
    private bool _disableAccountSwitching;

    [ObservableProperty]
    private bool _disableDeveloperTools;

    partial void OnDisableIncognitoChanged(bool value) => Persist();
    partial void OnDisableAccountSwitchingChanged(bool value) => Persist();
    partial void OnDisableDeveloperToolsChanged(bool value) => Persist();

    private void Persist()
    {
        var current = _store.Current;
        _store.Update(new WizardConfiguration(
            current.BrowserTargets,
            current.AllowlistSites,
            new AdvancedOptionsState(DisableIncognito, DisableAccountSwitching, DisableDeveloperTools)));
    }
}
```

- [ ] **Step 2: `Step3AdvancedPage.xaml`** — three toggles, each labeled with the exact registry effect (matches Stitch's "Windows 原則值" caption pattern, using `PolicyKeys`-accurate text, never `BrowserGuestModeEnabled`):

```xml
<!-- src/WebsiteWhitelistManual.App/Pages/Step3AdvancedPage.xaml -->
<Page x:Class="WebsiteWhitelistManual.App.Pages.Step3AdvancedPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
      Title="步驟 3：進階選項">
    <StackPanel>
        <TextBlock Text="步驟 3：設定防繞過與進階防護選項" FontSize="20" FontWeight="Bold" Foreground="{StaticResource Brush.TextPrimary}"/>
        <TextBlock Text="建議全數維持開啟以達成無漏洞防護。" Foreground="{StaticResource Brush.TextBody}" Margin="0,8,0,16"/>

        <ui:CardControl Margin="0,0,0,12">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>
                <StackPanel Grid.Column="0">
                    <TextBlock Text="停用無痕視窗 (InPrivate / Incognito)" FontWeight="SemiBold"/>
                    <TextBlock Text="防止孩子建立不會留下瀏覽紀錄的無痕分頁。" Foreground="{StaticResource Brush.TextMuted}" TextWrapping="Wrap"/>
                    <TextBlock Text="Windows 原則值：InPrivateModeAvailability / IncognitoModeAvailability = 1" FontFamily="Consolas" FontSize="11" Foreground="{StaticResource Brush.TextMuted}" Margin="0,4,0,0"/>
                </StackPanel>
                <ui:ToggleSwitch Grid.Column="1" IsChecked="{Binding DisableIncognito, Mode=TwoWay}"/>
            </Grid>
        </ui:CardControl>

        <ui:CardControl Margin="0,0,0,12">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>
                <StackPanel Grid.Column="0">
                    <TextBlock Text="停用開發人員工具 (Developer Tools / F12)" FontWeight="SemiBold"/>
                    <TextBlock Text="避免具備技術好奇心的孩子透過控制台修改設定或繞過限制。" Foreground="{StaticResource Brush.TextMuted}" TextWrapping="Wrap"/>
                    <TextBlock Text="Windows 原則值：DeveloperToolsAvailability = 2" FontFamily="Consolas" FontSize="11" Foreground="{StaticResource Brush.TextMuted}" Margin="0,4,0,0"/>
                </StackPanel>
                <ui:ToggleSwitch Grid.Column="1" IsChecked="{Binding DisableDeveloperTools, Mode=TwoWay}"/>
            </Grid>
        </ui:CardControl>

        <ui:CardControl>
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>
                <StackPanel Grid.Column="0">
                    <TextBlock Text="停用帳號切換 (BrowserSignin)" FontWeight="SemiBold"/>
                    <TextBlock Text="禁止在瀏覽器中切換至未受管的其他帳號，確保白名單防護不會被多帳號登入繞過。" Foreground="{StaticResource Brush.TextMuted}" TextWrapping="Wrap"/>
                    <TextBlock Text="Windows 原則值：BrowserSignin = 0" FontFamily="Consolas" FontSize="11" Foreground="{StaticResource Brush.TextMuted}" Margin="0,4,0,0"/>
                </StackPanel>
                <ui:ToggleSwitch Grid.Column="1" IsChecked="{Binding DisableAccountSwitching, Mode=TwoWay}"/>
            </Grid>
        </ui:CardControl>
    </StackPanel>
</Page>
```

- [ ] **Step 3: `Step3AdvancedPage.xaml.cs`**

```csharp
// src/WebsiteWhitelistManual.App/Pages/Step3AdvancedPage.xaml.cs
using System.Windows.Controls;
using WebsiteWhitelistManual.App.ViewModels;

namespace WebsiteWhitelistManual.App.Pages;

public partial class Step3AdvancedPage : Page
{
    public Step3AdvancedPage(Step3AdvancedViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
```

- [ ] **Step 4: Register the ViewModel**

In `App.xaml.cs`, add `services.AddTransient<Step3AdvancedViewModel>();`.

- [ ] **Step 5: Build**

Run: `dotnet build`
Expected: FAILS only on missing `Step4ConfirmPage`, `Step5CompletePage`.

- [ ] **Step 6: Commit**

```bash
git add src/WebsiteWhitelistManual.App/Pages/Step3AdvancedPage.xaml src/WebsiteWhitelistManual.App/Pages/Step3AdvancedPage.xaml.cs src/WebsiteWhitelistManual.App/ViewModels/Step3AdvancedViewModel.cs src/WebsiteWhitelistManual.App/App.xaml.cs
git commit -m "feat: add Step3AdvancedPage bound to AdvancedOptionsState"
```

---

### Task 9: `Step4ConfirmPage` + `Step4ConfirmViewModel` — diff preview, backup+apply with timeout, InfoBar error handling

**Files:**
- Create: `src/WebsiteWhitelistManual.App/Pages/Step4ConfirmPage.xaml`
- Create: `src/WebsiteWhitelistManual.App/Pages/Step4ConfirmPage.xaml.cs`
- Create: `src/WebsiteWhitelistManual.App/ViewModels/Step4ConfirmViewModel.cs`
- Modify: `src/WebsiteWhitelistManual.App/App.xaml.cs`

**Interfaces:**
- Consumes: `WizardConfigurationStore`, `IRegistryPolicyReader.ReadSnapshot`, `IRegistryBackupService.Backup(IReadOnlyList<BrowserTarget>, string, DateTimeOffset)`, `IRegistryPolicyWriter.Apply(WizardConfiguration)`.
- Produces: `Step4ConfirmPage`, `Step4ConfirmViewModel`. This is the task that implements the Global Constraints' 10-second backup timeout and the `UnauthorizedAccessException`/`SecurityException` `InfoBar` handling from the spec's 錯誤處理 section.

- [ ] **Step 1: `Step4ConfirmViewModel`**

```csharp
// src/WebsiteWhitelistManual.App/ViewModels/Step4ConfirmViewModel.cs
using System.Security;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebsiteWhitelistManual.App.State;
using WebsiteWhitelistManual.Core.Models;
using WebsiteWhitelistManual.Core.Services;

namespace WebsiteWhitelistManual.App.ViewModels;

public sealed partial class Step4ConfirmViewModel : ObservableObject
{
    private static readonly TimeSpan BackupTimeout = TimeSpan.FromSeconds(10);

    private readonly WizardConfigurationStore _store;
    private readonly IRegistryPolicyReader _policyReader;
    private readonly IRegistryBackupService _backupService;
    private readonly IRegistryPolicyWriter _policyWriter;

    public Step4ConfirmViewModel(
        WizardConfigurationStore store,
        IRegistryPolicyReader policyReader,
        IRegistryBackupService backupService,
        IRegistryPolicyWriter policyWriter)
    {
        _store = store;
        _policyReader = policyReader;
        _backupService = backupService;
        _policyWriter = policyWriter;
        RefreshDiff();
    }

    [ObservableProperty]
    private PolicySnapshot _currentSnapshot = new(Array.Empty<BrowserPolicySnapshot>());

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _hasAcknowledged;

    [ObservableProperty]
    private bool _isApplying;

    [ObservableProperty]
    private string? _lastBackupDirectory;

    public WizardConfiguration Configuration => _store.Current;

    public bool CanApply => Configuration.CanApply && HasAcknowledged && !IsApplying;

    partial void OnHasAcknowledgedChanged(bool value) => OnPropertyChanged(nameof(CanApply));
    partial void OnIsApplyingChanged(bool value) => OnPropertyChanged(nameof(CanApply));

    [RelayCommand]
    private void RefreshDiff()
    {
        CurrentSnapshot = _policyReader.ReadSnapshot(Configuration.BrowserTargets.Count > 0
            ? Configuration.BrowserTargets
            : new[] { BrowserTarget.Edge, BrowserTarget.Chrome });
        OnPropertyChanged(nameof(Configuration));
        OnPropertyChanged(nameof(CanApply));
    }

    [RelayCommand(CanExecute = nameof(CanApply))]
    private async Task ApplyAsync()
    {
        ErrorMessage = null;
        IsApplying = true;

        try
        {
            var backupBaseDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WebsiteWhitelistManual", "Backups");

            var backupTask = Task.Run(() => _backupService.Backup(Configuration.BrowserTargets, backupBaseDirectory, DateTimeOffset.Now));
            var completed = await Task.WhenAny(backupTask, Task.Delay(BackupTimeout));

            if (completed != backupTask)
            {
                ErrorMessage = "備份逾時（超過 10 秒沒有回應），已中止套用。請重試一次；若持續逾時，請確認沒有其他程式鎖住登錄檔。";
                return;
            }

            var backupResult = await backupTask;
            if (!backupResult.Success)
            {
                ErrorMessage = $"備份失敗，已中止套用（未寫入任何變更）：{backupResult.ErrorMessage}";
                return;
            }

            LastBackupDirectory = backupResult.BackupDirectory;

            await Task.Run(() => _policyWriter.Apply(Configuration));
            RefreshDiff();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException)
        {
            ErrorMessage = $"寫入登錄檔時權限不足：{ex.Message}（理論上系統管理員權限已由 UAC 保證，若持續發生請重新以系統管理員身分啟動本工具）。";
        }
        finally
        {
            IsApplying = false;
        }
    }
}
```

- [ ] **Step 2: `Step4ConfirmPage.xaml`** — diff preview (target config vs. `CurrentSnapshot`), acknowledgement checkbox, `InfoBar` bound to `ErrorMessage`, apply button gated on `CanApply`:

```xml
<!-- src/WebsiteWhitelistManual.App/Pages/Step4ConfirmPage.xaml -->
<Page x:Class="WebsiteWhitelistManual.App.Pages.Step4ConfirmPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
      Title="步驟 4：套用前確認">
    <StackPanel>
        <TextBlock Text="步驟 4：套用前確認所有安全性設定" FontSize="20" FontWeight="Bold" Foreground="{StaticResource Brush.TextPrimary}"/>
        <TextBlock Text="請仔細檢視即將寫入 Windows 系統政策 (Group Policy / Registry) 的防護規則。確認無誤後即可進行系統授權套用。"
                   Foreground="{StaticResource Brush.TextBody}" Margin="0,8,0,16" TextWrapping="Wrap"/>

        <ui:InfoBar Title="這個步驟需要系統管理員權限"
                    Message="點擊下方「確認並套用」後，Windows 系統可能再次要求授權。這是正常且必要的流程，確保孩子的標準帳號無法自行修改此防護政策。"
                    IsOpen="True" Severity="Informational" Margin="0,0,0,16"/>

        <ui:InfoBar Title="套用失敗" Message="{Binding ErrorMessage}"
                    IsOpen="{Binding ErrorMessage, Converter={StaticResource NullToVisibilityConverter}, ConverterParameter=Bool}"
                    Severity="Danger" Margin="0,0,0,16"/>

        <TextBlock Text="{Binding Configuration.BrowserTargets.Count, StringFormat='目標瀏覽器政策 ({0} 套)'}" FontWeight="SemiBold" Margin="0,0,0,4"/>
        <TextBlock Text="{Binding Configuration.AllowlistSites.Count, StringFormat='允許訪問網站清單 ({0} 個網域)'}" Margin="0,0,0,16"/>

        <CheckBox Content="我已仔細檢閱上述政策項目，確認立刻套用上述防護標準" IsChecked="{Binding HasAcknowledged, Mode=TwoWay}" Margin="0,0,0,16"/>

        <TextBlock Text="{Binding LastBackupDirectory, StringFormat='上次備份已儲存至：{0}'}" Foreground="{StaticResource Brush.TextMuted}" Margin="0,0,0,16"
                   Visibility="{Binding LastBackupDirectory, Converter={StaticResource NullToVisibilityConverter}}"/>

        <ui:Button Content="確認並套用" Command="{Binding ApplyCommand}" Appearance="Primary" HorizontalAlignment="Left"/>
    </StackPanel>
</Page>
```

Note: `IsOpen="{Binding ErrorMessage, Converter=..., ConverterParameter=Bool}"` above assumes `NullToVisibilityConverter` also handles a `Bool` parameter path returning a `bool` instead of `Visibility` for `InfoBar.IsOpen` (which is `bool`, not `Visibility`). Update the converter from Task 7 to branch on `parameter`:

```csharp
// src/WebsiteWhitelistManual.App/Converters/NullToVisibilityConverter.cs — replace Convert with:
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hasValue = !string.IsNullOrEmpty(value as string);
        return Equals(parameter, "Bool") ? hasValue : (hasValue ? Visibility.Visible : Visibility.Collapsed);
    }
```

- [ ] **Step 3: `Step4ConfirmPage.xaml.cs`**

```csharp
// src/WebsiteWhitelistManual.App/Pages/Step4ConfirmPage.xaml.cs
using System.Windows.Controls;
using WebsiteWhitelistManual.App.ViewModels;

namespace WebsiteWhitelistManual.App.Pages;

public partial class Step4ConfirmPage : Page
{
    public Step4ConfirmPage(Step4ConfirmViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
```

- [ ] **Step 4: Register the ViewModel**

In `App.xaml.cs`, add `services.AddTransient<Step4ConfirmViewModel>();`.

- [ ] **Step 5: Build**

Run: `dotnet build`
Expected: FAILS only on missing `Step5CompletePage`.

- [ ] **Step 6: Commit**

```bash
git add src/WebsiteWhitelistManual.App/Pages/Step4ConfirmPage.xaml src/WebsiteWhitelistManual.App/Pages/Step4ConfirmPage.xaml.cs src/WebsiteWhitelistManual.App/ViewModels/Step4ConfirmViewModel.cs src/WebsiteWhitelistManual.App/Converters/NullToVisibilityConverter.cs src/WebsiteWhitelistManual.App/App.xaml.cs
git commit -m "feat: add Step4ConfirmPage with 10s-timeout backup+apply and InfoBar error handling"
```

---

### Task 10: `Step5CompletePage` + `Step5CompleteViewModel` — diff-based verification, manual browser-open button

**Files:**
- Create: `src/WebsiteWhitelistManual.App/Pages/Step5CompletePage.xaml`
- Create: `src/WebsiteWhitelistManual.App/Pages/Step5CompletePage.xaml.cs`
- Create: `src/WebsiteWhitelistManual.App/ViewModels/Step5CompleteViewModel.cs`
- Create: `src/WebsiteWhitelistManual.App/ViewModels/VerificationRow.cs`
- Modify: `src/WebsiteWhitelistManual.App/App.xaml.cs`

**Interfaces:**
- Consumes: `WizardConfigurationStore`, `IRegistryPolicyReader.ReadSnapshot`.
- Produces: `Step5CompletePage`, `Step5CompleteViewModel`, `VerificationRow`. This is the task implementing Global Constraints category 2 (registry-diff "written and confirmed" rows, never "site tested reachable") and category 3 (manual browser-open button with zero auto-verification tied to it).

- [ ] **Step 1: `VerificationRow`**

```csharp
// src/WebsiteWhitelistManual.App/ViewModels/VerificationRow.cs
namespace WebsiteWhitelistManual.App.ViewModels;

public sealed record VerificationRow(string Label, bool Passed, string Detail);
```

- [ ] **Step 2: `Step5CompleteViewModel`**

```csharp
// src/WebsiteWhitelistManual.App/ViewModels/Step5CompleteViewModel.cs
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WebsiteWhitelistManual.App.State;
using WebsiteWhitelistManual.Core.Models;
using WebsiteWhitelistManual.Core.Services;

namespace WebsiteWhitelistManual.App.ViewModels;

public sealed partial class Step5CompleteViewModel : ObservableObject
{
    private readonly WizardConfigurationStore _store;
    private readonly IRegistryPolicyReader _policyReader;

    public Step5CompleteViewModel(WizardConfigurationStore store, IRegistryPolicyReader policyReader)
    {
        _store = store;
        _policyReader = policyReader;
        RunVerification();
    }

    [ObservableProperty]
    private IReadOnlyList<VerificationRow> _rows = Array.Empty<VerificationRow>();

    public bool AllPassed => Rows.Count > 0 && Rows.All(r => r.Passed);

    public bool HasAllowlistSites => _store.Current.AllowlistSites.Count > 0;

    [RelayCommand]
    private void RunVerification()
    {
        var configuration = _store.Current;
        var snapshot = _policyReader.ReadSnapshot(configuration.BrowserTargets.Count > 0
            ? configuration.BrowserTargets
            : new[] { BrowserTarget.Edge, BrowserTarget.Chrome });

        var rows = new List<VerificationRow>();
        var expectedDomains = configuration.AllowlistSites.Select(s => s.Domain).OrderBy(d => d).ToList();

        foreach (var browser in snapshot.Browsers)
        {
            rows.Add(new VerificationRow(
                $"{browser.BrowserId}：政策機碼已寫入",
                browser.PolicyKeyExists,
                browser.PolicyKeyExists ? "登錄機碼存在並可讀取。" : "尚未偵測到此瀏覽器的政策機碼。"));

            var actualDomains = browser.AllowedUrls.OrderBy(d => d).ToList();
            var domainsMatch = expectedDomains.SequenceEqual(actualDomains);
            rows.Add(new VerificationRow(
                $"{browser.BrowserId}：允許清單機碼已寫入並核對",
                domainsMatch,
                domainsMatch
                    ? $"URLAllowlist 內容與設定精靈一致，共 {actualDomains.Count} 筆。"
                    : "URLAllowlist 內容與設定精靈不一致，請重新套用一次。"));

            rows.Add(new VerificationRow(
                $"{browser.BrowserId}：無痕模式機碼已核對",
                browser.IncognitoDisabled == configuration.AdvancedOptions.DisableIncognito,
                $"IncognitoModeAvailability/InPrivateModeAvailability 目前值：{browser.IncognitoDisabled}"));
        }

        Rows = rows;
        OnPropertyChanged(nameof(AllPassed));
        OnPropertyChanged(nameof(HasAllowlistSites));
    }

    [RelayCommand]
    private void OpenAllowedSiteManually()
    {
        var firstSite = _store.Current.AllowlistSites.FirstOrDefault();
        if (firstSite is null)
        {
            return;
        }

        // Fire-and-forget by design: this button's only job is to open the
        // browser for the parent to look at. The app cannot read what
        // happens inside the browser afterward (no block-count/log API
        // exists for Chromium's URLBlocklist policy), so no result is
        // captured, polled, or checked off — see spec 資料流 step 5.
        Process.Start(new ProcessStartInfo($"https://{firstSite.Domain}") { UseShellExecute = true });
    }
}
```

- [ ] **Step 3: `Step5CompletePage.xaml`**

```xml
<!-- src/WebsiteWhitelistManual.App/Pages/Step5CompletePage.xaml -->
<Page x:Class="WebsiteWhitelistManual.App.Pages.Step5CompletePage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
      Title="步驟 5：完成與驗證">
    <StackPanel>
        <ui:CardControl Background="{StaticResource Brush.SecondaryTint}" Margin="0,0,0,16">
            <StackPanel Orientation="Horizontal">
                <ui:SymbolIcon Symbol="CheckmarkCircle24" Foreground="{StaticResource Brush.Secondary}" FontSize="28" Margin="0,0,12,0"/>
                <TextBlock Text="保護已成功啟用！已將設定寫入 Windows 登錄檔原則。" FontSize="16" FontWeight="SemiBold" VerticalAlignment="Center"/>
            </StackPanel>
        </ui:CardControl>

        <TextBlock Text="系統防護自動驗證報告" FontSize="16" FontWeight="SemiBold" Margin="0,0,0,4"/>
        <TextBlock Text="以下逐項核對登錄機碼是否確實寫入並與設定精靈一致。本工具不會、也無法測試瀏覽器實際能否開啟或封鎖任何網站——Chromium 政策沒有可讀的攔截紀錄。"
                   Foreground="{StaticResource Brush.TextMuted}" TextWrapping="Wrap" Margin="0,0,0,12"/>

        <ItemsControl ItemsSource="{Binding Rows}">
            <ItemsControl.ItemTemplate>
                <DataTemplate>
                    <ui:CardControl Margin="0,0,0,8">
                        <Grid>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="Auto"/>
                            </Grid.ColumnDefinitions>
                            <StackPanel Grid.Column="0">
                                <TextBlock Text="{Binding Label}" FontWeight="SemiBold"/>
                                <TextBlock Text="{Binding Detail}" Foreground="{StaticResource Brush.TextMuted}" TextWrapping="Wrap"/>
                            </StackPanel>
                            <ui:Badge Grid.Column="1" Content="{Binding Passed}" Appearance="Success"/>
                        </Grid>
                    </ui:CardControl>
                </DataTemplate>
            </ItemsControl.ItemTemplate>
        </ItemsControl>

        <ui:Button Content="重新驗證" Command="{Binding RunVerificationCommand}" Margin="0,16,0,8" HorizontalAlignment="Left"/>

        <ui:InfoBar Title="想親眼確認？"
                    Message="按下方按鈕會用系統預設瀏覽器開啟一個已允許的網址，讓您親自檢查。本工具不會自動判讀開啟結果。"
                    IsOpen="True" Severity="Informational" Margin="0,8,0,8"/>
        <ui:Button Content="開啟已允許的網址 (手動檢查)" Command="{Binding OpenAllowedSiteManuallyCommand}"
                   HorizontalAlignment="Left" IsEnabled="{Binding HasAllowlistSites}"/>
    </StackPanel>
</Page>
```

- [ ] **Step 4: `Step5CompletePage.xaml.cs`**

```csharp
// src/WebsiteWhitelistManual.App/Pages/Step5CompletePage.xaml.cs
using System.Windows.Controls;
using WebsiteWhitelistManual.App.ViewModels;

namespace WebsiteWhitelistManual.App.Pages;

public partial class Step5CompletePage : Page
{
    public Step5CompletePage(Step5CompleteViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
    }
}
```

- [ ] **Step 5: Register the ViewModel**

In `App.xaml.cs`, add `services.AddTransient<Step5CompleteViewModel>();`.

- [ ] **Step 6: Build — this is the first build in this plan required to be clean**

Run: `dotnet build`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`. If not, work through remaining errors now — every page/ViewModel this plan references now exists.

- [ ] **Step 7: Run the Core test suite once more to confirm nothing there regressed**

Run: `dotnet test`
Expected: `Passed! ... Total: 59` (unchanged from Task 2 — nothing in Tasks 3–10 touches `Core`).

- [ ] **Step 8: Commit**

```bash
git add src/WebsiteWhitelistManual.App/Pages/Step5CompletePage.xaml src/WebsiteWhitelistManual.App/Pages/Step5CompletePage.xaml.cs src/WebsiteWhitelistManual.App/ViewModels/Step5CompleteViewModel.cs src/WebsiteWhitelistManual.App/ViewModels/VerificationRow.cs src/WebsiteWhitelistManual.App/App.xaml.cs
git commit -m "feat: add Step5CompletePage with registry-diff verification, no fabricated behavioral stats"
```

---

### Task 11: Windows manual-verification handoff

**Files:**
- Create: `WINDOWS_VERIFICATION.md`

**Interfaces:** none — documentation only, the task where the user takes over.

- [ ] **Step 1: Write the handoff doc**

```markdown
# 六個頁面實作完成 — Windows 手動驗證清單

`dotnet build`／`dotnet test` 都已經在開發端過關，但畫面實際長相、UAC、登錄檔真正讀寫，只能在你自己的 Windows 機器上驗證。請照下面順序點過一次：

## 1. 啟動與導覽

1. `dotnet run --project src/WebsiteWhitelistManual.App`（或雙擊發佈出的 .exe），確認 UAC/管理員權限如常運作。
2. 左側導覽應為兩段：「首頁」單獨一項在最上面，下面是「設定精靈導覽」群組標題（不可點擊），底下 5 個步驟項目，每項都有圖示。
3. 依序點開全部 6 個項目，確認每個都顯示對應內容（不是空白），且點擊不會當機。

## 2. 首頁 (Dashboard)

1. 確認畫面顯示「目前防護狀態」與「目前允許 N 個網站」——**這兩個數字在你尚未套用任何設定前應該反映登錄檔的真實現況（很可能是「尚未設定」「0 個網站」，除非這台機器之前已經手動照 `manual.html` 設定過）**。
2. 確認「本機使用者帳號」清單裡有出現這台機器上的所有帳號，包含 Administrator 等內建帳號。
3. 點「查看完整設定值 (進階)」，確認彈出視窗顯示可讀的機碼內容，格式跟 `manual.html` 的對照表看起來一致。

## 3. 步驟 1-3（選擇瀏覽器 / 允許的網站 / 進階選項）

1. 勾選 Edge 和/或 Chrome。
2. 到「允許的網站」輸入至少一個網域（例如 `classroom.google.com`），確認出現在清單裡；嘗試輸入空白或帶 `http://` 的網址，確認出現對應的錯誤訊息且不會被加入清單。
3. 到「進階選項」確認三個開關預設都是開啟狀態，且可以正常切換。
4. 來回切換左側導覽到不同步驟再切回來，確認剛剛輸入的資料還在（`WizardConfigurationStore` 有正確保留狀態）。

## 4. 步驟 4（套用前確認）——**這一步會真正寫入登錄檔，請只在你自己的機器或朋友的筆電上做，不要在公司電腦上測**

1. 勾選「我已仔細檢閱...」，確認「確認並套用」按鈕從停用變成可按（若步驟 1 或步驟 2 沒有任何資料，按鈕應保持停用）。
2. 按下「確認並套用」，等待完成（應該在幾秒內），確認沒有紅色錯誤訊息，且畫面顯示備份路徑（`%LOCALAPPDATA%\WebsiteWhitelistManual\Backups\<時間戳記>\`）。
3. 打開該資料夾，確認裡面有 `Edge.reg`／`Chrome.reg`（取決於你勾選了哪些瀏覽器）。
4. 用 `regedit` 手動核對 `HKLM\SOFTWARE\Policies\Microsoft\Edge`（或 `\Google\Chrome`）底下的 `URLBlocklist`、`URLAllowlist`、`InPrivateModeAvailability`/`IncognitoModeAvailability`、`BrowserSignin`、`DeveloperToolsAvailability` 是否跟你在精靈裡設定的一致。

## 5. 步驟 5（完成與驗證）

1. 確認驗證清單裡每一列都打勾（PASS），文字內容是「機碼已寫入並核對」，**不會**出現「已測試網站可開啟」「HTTP 200」這類字眼。
2. 按「開啟已允許的網址 (手動檢查)」，確認系統預設瀏覽器真的開啟了一個你剛才加入允許清單的網址。
3. 按「重新驗證」，確認清單會重新讀一次登錄檔並維持 PASS。

## 6. 還原（如果想測試備份檔案有效）

雙擊 Step 4 產生的 `.reg` 檔匯入，確認能把機碼還原成套用前的狀態（這是 v1 唯一的還原方式，工具本身沒有一鍵還原按鈕）。

## 回報方式

- **編譯期錯誤**：把完整錯誤訊息貼回來。
- **畫面跟預期不同、當機、按鈕沒反應**：截圖 + 描述在哪一步發生，我來對照修。
- **登錄檔實際值跟精靈設定不一致**：附上 `regedit` 截圖，這類問題通常代表 `RegistryPolicyWriter` 或 Step4/Step5 的 ViewModel 邏輯有誤。
```

- [ ] **Step 2: Commit**

```bash
git add WINDOWS_VERIFICATION.md
git commit -m "docs: add Windows manual-verification checklist for the 6 wizard pages"
```

---

## After this plan

`WebsiteWhitelistManual.App` has real content behind every `NavigationView` item: a Dashboard showing genuine registry-backed status and the full local-account list, and five wizard pages that accumulate state into a shared `WizardConfigurationStore`, diff against the real registry before writing, back up before applying, and verify by re-reading the registry afterward — never by fabricating block counts or claiming to have tested site reachability. The user takes over at `WINDOWS_VERIFICATION.md` to click through all six pages, trigger a real Apply, and confirm the written registry keys match `manual.html`'s canonical table on their own Windows machine.

Known follow-ups deliberately deferred past this plan (v1.1 candidates or out of v1 scope per spec, unchanged by this plan):
- No in-app one-click registry restore (parents import the `.reg` backup manually).
- No `.txt`/`.csv` bulk import for the allowlist.
- `IProcessRunner.Run` still has no true process-kill-on-timeout — Task 9's `Task.WhenAny` bounds the *caller's* wait but a genuinely hung `reg.exe` process is not killed. If this proves to be a real problem on the user's machine, a follow-up should add a `CancellationToken`-aware overload to `IProcessRunner` that kills the child process on timeout.
