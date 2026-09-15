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
