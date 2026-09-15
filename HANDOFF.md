# 工作摘要 — 網站白名單設定工具（給接手 session 看）

給朋友的小孩鎖 Windows 11 Home 筆電，只能連指定網站。目前已經有一份離線手冊（手動 regedit 版），現在要把手冊裡的操作做成一個有介面的桌面小工具。這份文件是給接手的新對話 session 用的，讀完應該就能直接接著做決策/寫程式，不用重新問一輪。

## 目錄內容

- `manual.html` — 已完成並發佈的離線手冊（Artifact: https://claude.ai/artifact/Mt55LwPMmke3RoVb1ejSYX）。裡面有完整、已核對過的登錄檔機碼/數值表格，是工具要寫入的資料的「正確答案來源」。
- `stitch_website_allowlist_desktop_app/` — 用 Google Stitch 產出的 UI 設計稿（HTML + 截圖 + 設計 token）。畫面對應關係：
  | 資料夾 | 對應畫面 |
  |---|---|
  | 根目錄 `code.html`/`screen.png` | 首頁／目前保護狀態 |
  | `1/` | Step 1：選擇瀏覽器 |
  | `2/` | Step 2：允許的網站 |
  | `3/` | Step 3：進階選項 |
  | `4/` | Step 4：套用前確認 |
  | `5/` | Step 5：完成與驗證 |
  | `guardian_clear/DESIGN.md` | 完整設計系統（色票、字體、間距、元件規格）— teal 主色 `#0F766E`、emerald 輔色 `#10B981`、amber 警示色 `#D97706`，Windows 11 Fluent 風格 |

## 已經定案的技術決策

1. **平台**：.NET 8 + WPF，桌面應用（不是網頁）。
2. **UI 套件**：用現成的 **WPF-UI**（Fluent Design NuGet 套件）打底，不手刻 XAML 樣式——降低視覺 bug 出現機率，因為開發者（Claude）沒有 Windows 環境可以肉眼驗證畫面。
3. **發佈方式**：`dotnet publish --self-contained`，單一 .exe，朋友那台機器不需要先裝 .NET Runtime（代價是檔案較大，約 100–150MB）。
4. **權限模式**：`app.manifest` 設 `requireAdministrator`，程式一啟動就跳 UAC——因為工具的核心功能（寫 HKLM）本來就一定需要管理員權限，沒有「不需要權限」的使用情境。
5. **開發流程的限制（重要）**：Claude 目前跑在 macOS，**WPF 專案無法在 Mac 上編譯**（PresentationBuildTasks 是 Windows-only）。流程會是：Claude 寫完整的 C#/XAML 原始碼 → 使用者在 Windows 上 `dotnet build`/`dotnet publish` → 編譯期錯誤直接貼錯誤訊息回來由 Claude 修、執行期/視覺問題需要截圖。
6. **Build 環境**：使用者的公司電腦可以裝 .NET SDK、執行 dotnet CLI（不需要系統管理員權限就能 build/publish）。但**實際測試 UAC 授權跟登錄檔寫入**，必須換到使用者自己的機器或朋友的筆電上做，不要在公司電腦上測。
7. **瀏覽器範圍（v1）**：只做 **Edge + Chrome**，不做 Firefox（YAGNI，兩個 Chromium 系瀏覽器共用幾乎一樣的登錄機碼結構）。Edge 與 Chrome 的差異只有一項：停用無痕模式的機碼名稱不同——Edge 是 `InPrivateModeAvailability`，Chrome 是 `IncognitoModeAvailability`；其餘（`URLBlocklist`、`URLAllowlist`、`DeveloperToolsAvailability`、登入鎖定）機碼名稱相同，只是廠商路徑不同（`HKLM\SOFTWARE\Policies\Microsoft\Edge` vs `HKLM\SOFTWARE\Policies\Google\Chrome`）。
8. **標準使用者帳號建立維持手動**：工具不自動建立/修改 Windows 使用者帳號（風險較高、範圍外），這一步驟仍照 `manual.html` 手動做。工具最多只能「唯讀偵測並顯示」目前登入帳號是否為標準使用者，供家長確認。
9. **不做應用程式內的管理者密碼/PIN**：曾考慮在工具內加一道獨立的啟動密碼，防止小朋友自己開來改設定。結論是不需要——工具已經有 `app.manifest` 的 `requireAdministrator`，開啟就會跳 UAC 要求系統管理員帳密，小朋友（標準使用者）本來就過不了這關，跟另外做一層應用內密碼是重複保護。應用內密碼還會額外帶來「密碼存哪裡才安全」「忘記密碼怎麼復原」這些新問題，投入產出比不好，所以決定不做。

## 還沒決定、下一個 session 要先問完的 3 個問題

上一輪問到一半被中斷，這 3 題都還沒有答案：

1. **畫面導覽架構**：用 WPF-UI 的 `NavigationView` 當左側步驟導覽（跟 Stitch 設計稿的左側欄佈局最接近，也是套件最順手的用法），還是用簡化的 TabControl/自製 stepper？→ 傾向前者，因為 Stitch 稿子已經證實這個左側欄佈局是可行且好看的。
2. **首頁「目前保護狀態」的資料來源**：每次開程式即時讀登錄檔實際值（推薦，永遠跟真實狀態一致），還是另外存一份本地 JSON 設定檔（讀寫快一點，但有跟登錄檔內容不一致的風險）？
3. （已在對話中確認）**瀏覽器範圍**：Edge + Chrome，不含 Firefox——見上方第 7 點，這題其實已經有答案了，只是還沒正式寫進設計文件。

## Stitch 稿子裡發現、原本 prompt 沒要求、值得評估要不要做進 v1 的細節

- **套用前自動備份 `.reg` 還原點**（Step 4 畫面文字：「套用後將自動產生還原備份點...存放於安全本機目錄」）。這個很值得做——套用前用 `reg export` 把即將覆蓋的機碼匯出成 `.reg` 檔，給家長一個「一鍵復原」的退路，技術上不難，風險可控。
- **「查看完整設定值（進階）」/「檢視原則登錄檔原始碼」連結**：給懂技術的家長看實際寫入的機碼路徑，方便對照 `manual.html`。值得做，UI 上只是一個唯讀文字檢視。
- **「今日攔截嘗試 14 次」/「過去 7 天防護成效」統計卡片**：這個**做不到**——Chromium 的 `URLBlocklist` 政策純粹是瀏覽器內部擋下顯示錯誤頁，並不會把「被擋了幾次」寫到任何工具讀得到的地方（沒有 log API）。這是 Stitch 生成時腦補的裝飾性數據，下一個 session 設計時要記得從範圍裡拿掉，避免做出一個造假數據的功能。
- **「批量匯入名單（.txt/.csv）」**：可以做但屬於加分項，不影響核心功能，先列為 v1.1 candidate，不放進第一版範圍。

## 下一步

接手的 session 應該：
1. 先把上面「還沒決定的 3 個問題」問完（第 3 題其實只需要使用者確認一下上面的結論即可）。
2. 用 brainstorming skill 的 architectural path 繼續往下走：提出 2–3 個方案（主要是導覽架構那題）、呈現完整分區設計（architecture / components / data flow / error handling / testing）、寫成正式 spec。
3. Spec 確認後用 writing-plans skill 產出實作計畫。
4. 開始寫 WPF 專案的實際原始碼（C#/XAML），交給使用者在 Windows 上 build。
