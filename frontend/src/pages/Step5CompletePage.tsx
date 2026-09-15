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
