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
2. **不要用 `dotnet run` 來測 UAC**——`dotnet run`/`dotnet build` 底層用的是 `CreateProcess`，沒有經過 `ShellExecute`，對 `requireAdministrator` manifest **無法提權**，只會直接失敗（`ERROR_ELEVATION_REQUIRED`），不會跳出 UAC 對話框。正確的測法是用會走 `ShellExecute` 的方式啟動：
   - 在檔案總管裡直接雙擊發佈出來的 `WebsiteWhitelistManual.App.exe`，或
   - 在 PowerShell 跑 `Start-Process -Verb RunAs path\to\WebsiteWhitelistManual.App.exe`
   這樣才會跳 UAC 要求系統管理員權限——這是 `app.manifest` 裡 `requireAdministrator` 的效果，是預期行為。
3. 同意 UAC 後，應該看到一個標題「網站白名單設定工具」的視窗，左側有 NavigationView，選單依序是：首頁、選擇瀏覽器、允許的網站、進階選項、套用前確認、完成與驗證。**這一版選單項目點了還不會切換畫面內容**——實際頁面內容跟導覽邏輯是下一份計畫的範圍，這裡只驗證外殼、DI 組裝、UAC 提權能不能正常運作。
4. 如果啟動時 DI 組裝或服務解析失敗，現在不會直接沒反應或當掉——會跳出一個「網站白名單設定工具」標題的錯誤訊息框，顯示例外訊息，按下確定後程式結束。如果看到這個訊息框，把完整錯誤文字貼回來。

## 不要在公司電腦上測

照專案既有的規則，UAC 授權跟登錄檔讀寫操作只在你自己的機器或朋友的筆電上測，不要在公司電腦上跑。

## 回報方式

- **`dotnet build`/`dotnet publish` 編譯期錯誤**：把完整錯誤訊息貼回來，我來修。
- **跑不起來、UAC 沒跳、視窗長得不對、當掉**：截圖 + 描述症狀，我來對照修。
