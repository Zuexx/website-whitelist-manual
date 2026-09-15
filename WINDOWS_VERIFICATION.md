# Electron + React + .NET API — Windows 手動驗證清單

`dotnet build`/`npm run build` 都已經在開發端過關，但 UAC 提權、Electron 生子行程、真正的登錄檔讀寫，只能在你自己的 Windows 機器上驗證。請照下面順序點過一次。

## 0. 最重要的一項：確認 UAC 真的會為 API 子行程跳出來

這是整個架構唯一沒辦法在非 Windows 環境驗證的假設：Electron（不需要提權）呼叫 `child_process.spawn()` 啟動帶有 `app.manifest`（`requireAdministrator`）的 `WebsiteWhitelistManual.Api.exe` 時，Windows 應該會對**這個子行程**單獨跳出 UAC 提示，就跟直接雙擊那個 .exe 一樣。

**已在開發環境驗證過的部分**：直接雙擊/`Start-Process -Verb RunAs` 啟動 `WebsiteWhitelistManual.Api.exe`（無論是 Debug build 還是 `dotnet publish` 產出的自帶執行檔）都會正確提權，`/api/health`、`/api/policy/snapshot`、`/api/accounts` 都回傳正確資料。`npm run electron:dev` 開發模式下 Electron 視窗也已確認能透過 IPC 拿到 token 並顯示真實資料。

**還沒驗證的部分**：正式安裝檔（`dist-installer/網站白名單設定工具-1.0.0-setup.exe`）安裝後，雙擊主程式時，Electron 主行程用 `child_process.spawn()` 生出來的 API 子行程是否一樣會正常跳 UAC。

**測試方式**：安裝完成後開啟主程式，確認：
1. 有跳出 UAC 提示（或這台機器已設定永遠允許，就直接以系統管理員身分啟動）。
2. 工作管理員裡看得到兩個行程：`網站白名單設定工具.exe`（一般權限）和 `WebsiteWhitelistManual.Api.exe`（詳細資料頁籤裡「提高的權限」欄位顯示「是」）。

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
