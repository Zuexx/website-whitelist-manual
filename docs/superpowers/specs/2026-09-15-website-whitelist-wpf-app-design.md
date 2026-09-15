# 網站白名單設定工具 — WPF 桌面應用設計

**狀態**：待使用者審閱
**日期**：2026-09-15

## 背景與目的

`manual.html` 是一份已核對過的離線手冊，教家長手動用 `regedit` 把 Windows 11 Home 筆電鎖成「只能連白名單網站」，供家長把小孩的帳號鎖成標準使用者、並對 Edge / Chrome 寫入網址原則。本專案把手冊裡的手動操作做成一個有介面的桌面小工具，降低家長手動操作登錄編輯器的門檻與出錯機會。

UI 視覺設計已由 Google Stitch 產出五個畫面稿（`stitch_website_allowlist_desktop_app/`），本 spec 據此加上工程可行性收斂出的落地設計。

## 已定案的技術決策

1. **平台**：.NET 10（LTS）+ WPF 桌面應用，`TargetFramework` 為 `net10.0-windows`。使用者的 build 機器已安裝 .NET 10 SDK。
2. **UI 套件**：WPF-UI（Fluent Design NuGet 套件）打底的 `FluentWindow` + `NavigationView` 殼，不手刻樣式。
3. **MVVM**：`CommunityToolkit.Mvvm`（source generator 版 `ObservableObject` / `RelayCommand`），搭配 `Microsoft.Extensions.Hosting` 做建構子注入，讓 registry 服務可被抽換成測試替身。
4. **發佈方式**：`dotnet publish --self-contained`，單一 .exe，目標機器不需另裝 .NET Runtime。
5. **權限模式**：`app.manifest` 設 `requireAdministrator`，啟動即跳 UAC。不另做應用程式內密碼/PIN（UAC 本身已是足夠的守門機制，見下方「排除範圍」）。
6. **開發流程**：Claude 在 macOS 上寫完整 C#/XAML 原始碼；WPF 專案無法在 Mac 上編譯（`PresentationBuildTasks` 是 Windows-only），使用者在 Windows 上 `dotnet build`/`publish`，編譯期錯誤貼回來修，執行期/視覺問題需要截圖。UAC 授權與實際登錄檔寫入必須在使用者自己的機器或朋友的筆電上測，不在公司電腦上測。
7. **瀏覽器範圍（v1）**：只做 Edge + Chrome，不含 Firefox。兩者共用幾乎全部機碼結構，只有「停用無痕/隱私瀏覽」這一項政策名稱不同。
8. **標準使用者帳號建立**：維持手動（照 `manual.html` 操作），工具不自動建立/修改 Windows 帳號。
9. **導覽架構**：WPF-UI `NavigationView` 左側導覽，對齊 Stitch 稿子的佈局。
10. **首頁狀態資料來源**：每次進入 Dashboard 即時讀登錄檔實際值，不另存本地設定檔快取，避免跟真實登錄檔內容不同步。

## 資料模型（機碼對照表，以 `manual.html` 為準）

Stitch 畫面稿裡出現過 `BrowserGuestModeEnabled` 這個機碼名稱，但這是設計稿產生時腦補、從未在 `manual.html` 驗證過的名稱；本設計一律以 `manual.html` 已核對過的機碼為準，寫入邏輯**不使用** `BrowserGuestModeEnabled`。

| 機碼 | 型態 | Edge 路徑 | Chrome 路徑 | 值 | 作用 |
|---|---|---|---|---|---|
| `URLBlocklist\1` | REG_SZ | `HKLM\SOFTWARE\Policies\Microsoft\Edge\URLBlocklist` | `HKLM\SOFTWARE\Policies\Google\Chrome\URLBlocklist` | `*` | 封鎖所有網址 |
| `URLAllowlist\1..n` | REG_SZ | `...\Edge\URLAllowlist` | `...\Chrome\URLAllowlist` | 使用者輸入的網域 | 允許清單，優先權高於 Blocklist |
| 停用無痕模式 | DWORD | `InPrivateModeAvailability` = 1（`...\Edge` 下） | `IncognitoModeAvailability` = 1（`...\Chrome` 下） | 1 | 關閉無痕/隱私視窗 |
| `BrowserSignin` | DWORD | `...\Edge` 下 | `...\Chrome` 下 | 0 | 不能切換登入別的帳號 |
| `DeveloperToolsAvailability` | DWORD | `...\Edge` 下 | `...\Chrome` 下 | 2 | 關閉開發人員工具，避免被繞過 |

## 架構

.NET 10 WPF，`FluentWindow`（WPF-UI）作為外殼，內嵌 `NavigationView`。導覽分兩段：

- 頂部「首頁」（`DashboardPage`，預設畫面，顯示目前防護狀態）
- 「設定精靈導覽」區塊下的 5 個步驟頁，對應 Stitch 稿子的 `1/`～`5/`：選擇瀏覽器 → 允許的網站 → 進階選項 → 套用前確認 → 完成與驗證

各步驟頁可直接從左側導覽跳轉（不做強制線性鎖定），但「套用」動作要求 Step 1、Step 2 至少各有一筆有效資料（選了至少一個瀏覽器、至少一個允許網域），否則 Step 4 的套用按鈕停用並提示缺漏項目。

## 元件

**頁面 / ViewModel**：`DashboardPage`、`Step1BrowserPage`、`Step2SitesPage`、`Step3AdvancedPage`、`Step4ConfirmPage`、`Step5CompletePage`，各自配對一個 ViewModel。共用的 `WizardConfiguration`（跨步驟累積的選擇：browser targets、allowlist 網域、進階選項開關）以 singleton 形式透過 DI 容器在頁面間傳遞。

**服務層**（介面 + 實作，實作內部包一層 `IWindowsRegistry` 抽象）：

- `IRegistryPolicyReader` — 讀出 Edge/Chrome 目前 HKLM 底下的實際值，組成 `PolicySnapshot`。供 Dashboard 顯示、Step 4 diff 預覽、Step 5 套用後驗證共用。
- `IRegistryPolicyWriter` — 把 `WizardConfiguration` 套用成登錄機碼寫入（依上方資料模型表）。
- `IRegistryBackupService` — 套用前用 `reg export` 把即將覆蓋的機碼匯出成 `%LOCALAPPDATA%\WebsiteWhitelistManual\Backups\Restore_Backup_yyyyMMdd_HHmmss.reg`。**v1 範圍**：只自動產生備份檔並在畫面上顯示檔名/路徑，不做應用程式內一鍵還原按鈕；還原方式是家長之後手動雙擊該 `.reg` 檔匯入即可。應用程式內還原按鈕列為 v1.1 candidate。
- `ILocalAccountInspector` — 唯讀列出本機所有使用者帳號，標出哪些是標準使用者、哪些是系統管理員群組成員。刻意不判斷「目前登入的是誰」——因為本程式透過 UAC 提權執行，讀到的 token 必為管理員，沒有參考價值；改為列出本機帳號清單讓家長自己核對哪個是小孩的帳號。

**模型**：`BrowserTarget`（Edge/Chrome enum + 對應機碼路徑）、`AllowlistSite`（網域字串 + 可選顯示用分類標籤，標籤純粹是 UI 呈現、不寫入登錄檔）、`AdvancedOptionsState`（無痕/DevTools/帳號切換三個 bool）、`PolicySnapshot`（目前登錄檔實際狀態的唯讀快照）。

**「查看完整設定值」頁面/對話框**：唯讀文字檢視，把 `PolicySnapshot` 轉成跟 `manual.html` 對照表一致的格式，讓懂技術的家長核對。Dashboard 與 Step 4 都有進入點。

## 資料流

1. 啟動 → manifest 觸發 UAC → `MainWindow` 載入。
2. `DashboardPage` 用 `IRegistryPolicyReader` 即時讀登錄檔，顯示目前防護狀態；同時用 `ILocalAccountInspector` 顯示本機帳號清單供核對。
3. Step 1–3：使用者操作只累積在記憶體中的 `WizardConfiguration`，不觸碰登錄檔。
4. Step 4（套用前確認）：重新讀一次目前登錄檔，跟 `WizardConfiguration` 做 diff，顯示「即將變更什麼」。使用者按下確認後：先 `IRegistryBackupService.Backup()`，成功才呼叫 `IRegistryPolicyWriter.Apply()`。
5. Step 5（完成與驗證）：套用後再讀一次登錄檔，逐項核對機碼是否確實落地，打勾顯示。**明確不做**「網站是否真的能打開」的自動化驗證（Chromium 政策沒有任何工具可讀的攔截次數/存取紀錄 API），文案只承諾「機碼已正確寫入並核對」，不承諾瀏覽器實際行為。

## 錯誤處理

- 備份失敗（`reg export` 出錯、目錄無法建立等）→ 整個中止，不寫入（fail closed），提示家長重試。
- 寫入時攔截 `UnauthorizedAccessException` / `SecurityException`（防禦性處理，理論上 manifest 已保證提權），用 WPF-UI `InfoBar` 顯示錯誤、擋住套用按鈕。
- Step 2 網域輸入只做基本格式檢查（非空、無空白、粗略網域格式），不做 DNS 實際解析（離線工具無此必要）。
- Step 1 若偵測不到 Chrome 安裝路徑，不擋選取，顯示提示「尚未偵測到安裝，之後安裝仍會套用政策」。

## 測試

Registry 存取全部包一層 `IWindowsRegistry` 介面（薄封裝 `Microsoft.Win32.Registry`），單元測試用記憶體假實作跑，不需要真的 Windows 環境，可在 macOS 上以 `dotnet test`（xUnit）執行。ViewModel 的驗證邏輯、diff 計算、步驟間狀態流轉同樣可在 Mac 上單元測試覆蓋。實際 UAC 提權、真正寫入登錄檔、視覺畫面驗證，維持人工流程：使用者在 Windows 上 build/publish、實測並截圖回饋。

## 明確排除的範圍（v1 not-doing）

- **今日攔截嘗試次數 / 過去 7 天防護成效統計卡片**：Chromium 的 `URLBlocklist` 政策純粹是瀏覽器內部擋下顯示錯誤頁，沒有任何工具讀得到「被擋了幾次」的 log API。Stitch 稿子裡的「已阻絕 14 次」是生成時腦補的裝飾性數據，不實作，避免做出造假數據的功能。
- **Firefox 支援**：YAGNI，v1 只做 Edge + Chrome。
- **應用程式內批量匯入名單（.txt/.csv）**：加分項，列為 v1.1 candidate。
- **應用程式內一鍵還原按鈕**：v1 只自動產生 `.reg` 備份檔，還原靠家長手動匯入；列為 v1.1 candidate。
- **應用程式內管理者密碼/PIN**：`app.manifest` 的 `requireAdministrator` 已經是足夠的守門機制（小孩的標準使用者帳號本來就過不了 UAC），另外疊一層應用內密碼是重複保護，還會額外帶來「密碼存哪裡才安全」「忘記密碼怎麼復原」的新問題，投入產出比不好。
- **自動建立/修改 Windows 使用者帳號**：風險較高、範圍外，維持手動照 `manual.html` 操作；工具只唯讀偵測並列出本機帳號供家長核對。

## 下一步

Spec 經使用者審閱後，交給 `writing-plans` skill 產出實作計畫，再開始寫 WPF 專案的實際 C#/XAML 原始碼。
