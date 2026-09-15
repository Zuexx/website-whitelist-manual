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
