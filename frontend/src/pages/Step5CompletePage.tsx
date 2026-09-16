import { useEffect, useState } from "react";
import { AlertTriangle, CheckCircle2, Eye, ExternalLink, RefreshCw, RotateCcw, ShieldCheck, XCircle } from "lucide-react";
import { apiClient } from "../api/client";
import { useWizard } from "../state/WizardContext";
import type { PolicySnapshot } from "../api/types";
import "./Step5CompletePage.css";

interface VerificationRow {
  label: string;
  passed: boolean;
  detail: string;
}

function buildVerificationRows(
  snapshot: PolicySnapshot,
  expectedDomains: string[],
  expectIncognitoDisabled: boolean,
  expectYouTubeRestrict: boolean,
): VerificationRow[] {
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

    // Only shown when the parent actually turned this on — otherwise an
    // unchecked option would show a misleading "passed" row for a
    // protection nobody asked for.
    if (expectYouTubeRestrict) {
      rows.push({
        label: `${browser.browserId}：YouTube 限制模式機碼已核對`,
        passed: browser.youTubeRestrictEnabled === true,
        detail: `ForceYouTubeRestrict 目前值：${browser.youTubeRestrictEnabled ? "已啟用 (Strict)" : "未啟用"}`,
      });
    }
  }

  return rows;
}

export function Step5CompletePage() {
  const { allowlistSites, advancedOptions } = useWizard();
  const [rows, setRows] = useState<VerificationRow[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [isRefreshing, setIsRefreshing] = useState(false);

  const expectedDomains = allowlistSites.map((site) => site.domain);
  const allPassed = rows.length > 0 && rows.every((row) => row.passed);

  async function runVerification() {
    setError(null);
    setIsRefreshing(true);
    try {
      const snapshot = await apiClient.getSnapshot();
      setRows(buildVerificationRows(
        snapshot,
        expectedDomains,
        advancedOptions.disableIncognito,
        advancedOptions.forceYouTubeRestrict,
      ));
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setIsRefreshing(false);
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
      <div className={`card banner-row ${allPassed ? "banner-success" : "banner-pending"}`}>
        <span className={`icon-chip icon-chip-${allPassed ? "success" : "neutral"}`}>
          <ShieldCheck />
        </span>
        <div className="banner-row-text">
          <strong>{allPassed ? "保護已成功啟用！" : "驗證進行中"}</strong> 已將設定寫入 Windows 登錄檔原則。
        </div>
      </div>

      <div className="card info-banner banner-row">
        <span className="icon-chip icon-chip-sm icon-chip-warning"><RotateCcw /></span>
        <div className="banner-row-text">
          <strong>重要提醒：請重新啟動瀏覽器</strong>
          <p>
            Edge／Chrome 只會在啟動當下讀取一次防護規則。若孩子的瀏覽器目前已經開著，請先完全關閉所有 Edge／Chrome 視窗（工作管理員裡確認沒有殘留的背景行程）再重新打開，或直接重新啟動電腦，防護才會實際生效。這是瀏覽器本身的行為，不是本工具的問題。
          </p>
        </div>
      </div>

      <h2 className="section-title">系統防護自動驗證報告</h2>
      <p className="page-description">
        以下逐項核對登錄機碼是否確實寫入並與設定精靈一致。本工具不會、也無法測試瀏覽器實際能否開啟或封鎖任何網站——Chromium
        政策沒有可讀的攔截紀錄。
      </p>

      {error && (
        <div className="card banner-error banner-row">
          <AlertTriangle className="banner-icon" />
          <div className="banner-row-text">{error}</div>
        </div>
      )}

      <div className="verification-list">
        {rows.map((row) => (
          <div key={row.label} className="card verification-row">
            <span className={`health-row-icon ${row.passed ? "ok" : "fail"}`}>
              {row.passed ? <CheckCircle2 /> : <XCircle />}
            </span>
            <div className="verification-text">
              <div className="verification-label">{row.label}</div>
              <div className="verification-detail">{row.detail}</div>
            </div>
            <span className={`pill ${row.passed ? "pill-success" : "pill-warning"}`}>
              {row.passed ? "驗證通過" : "未通過"}
            </span>
          </div>
        ))}
      </div>

      <button className="btn btn-secondary" onClick={runVerification} disabled={isRefreshing}>
        <RefreshCw className={isRefreshing ? "spin" : ""} /> 重新驗證
      </button>

      <div className="card info-banner banner-row">
        <span className="icon-chip icon-chip-sm icon-chip-info"><Eye /></span>
        <div className="banner-row-text">
          <strong>想親眼確認？</strong>
          <p>按下方按鈕會用系統預設瀏覽器開啟一個已允許的網址，讓您親自檢查。本工具不會自動判讀開啟結果。</p>
        </div>
      </div>
      <button className="btn btn-secondary" onClick={openAllowedSiteManually} disabled={allowlistSites.length === 0}>
        <ExternalLink /> 開啟已允許的網址 (手動檢查)
      </button>
    </div>
  );
}
