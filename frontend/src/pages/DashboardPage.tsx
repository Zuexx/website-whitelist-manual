import { useEffect, useMemo, useState } from "react";
import {
  CheckCircle2,
  Circle,
  Globe2,
  ListChecks,
  RefreshCw,
  ShieldCheck,
  ShieldMinus,
  ShieldX,
  SlidersHorizontal,
  Users,
  XCircle,
} from "lucide-react";
import { apiClient } from "../api/client";
import type { BrowserPolicySnapshot, LocalAccount, PolicySnapshot } from "../api/types";
import { PolicyDetailDialog } from "../components/PolicyDetailDialog";
import "./DashboardPage.css";

const BROWSER_DISPLAY_NAME: Record<string, string> = {
  Edge: "Microsoft Edge",
  Chrome: "Google Chrome",
};

function buildHealthRows(browsers: BrowserPolicySnapshot[]) {
  const rows: { label: string; detail: string; state: "ok" | "fail" | "neutral" }[] = [];
  for (const browser of browsers) {
    const name = BROWSER_DISPLAY_NAME[browser.browserId] ?? browser.browserId;
    if (!browser.policyKeyExists) {
      rows.push({ label: `${name}：尚未套用任何原則`, detail: "尚未執行過設定精靈的套用步驟。", state: "neutral" });
      continue;
    }
    rows.push({
      label: `${name}：白名單機碼已建立`,
      detail: `URLAllowlist 共 ${browser.allowedUrls.length} 筆網址。`,
      state: "ok",
    });
    rows.push({
      label: `${name}：無痕視窗已停用`,
      detail: `IncognitoModeAvailability / InPrivateModeAvailability = ${String(browser.incognitoDisabled)}`,
      state: browser.incognitoDisabled ? "ok" : "fail",
    });
    rows.push({
      label: `${name}：帳號切換已停用`,
      detail: `BrowserSignin = ${String(browser.browserSigninDisabled)}`,
      state: browser.browserSigninDisabled ? "ok" : "fail",
    });
    rows.push({
      label: `${name}：開發人員工具已停用`,
      detail: `DeveloperToolsAvailability = ${String(browser.developerToolsDisabled)}`,
      state: browser.developerToolsDisabled ? "ok" : "fail",
    });
  }
  return rows;
}

export function DashboardPage() {
  const [snapshot, setSnapshot] = useState<PolicySnapshot | null>(null);
  const [accounts, setAccounts] = useState<LocalAccount[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [showDetail, setShowDetail] = useState(false);
  const [isRefreshing, setIsRefreshing] = useState(false);

  async function refresh() {
    setError(null);
    setIsRefreshing(true);
    try {
      const [snapshotResult, accountsResult] = await Promise.all([
        apiClient.getSnapshot(),
        apiClient.getAccounts("all"),
      ]);
      setSnapshot(snapshotResult);
      setAccounts(accountsResult);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setIsRefreshing(false);
    }
  }

  useEffect(() => {
    refresh();
  }, []);

  const configuredBrowsers = useMemo(
    () => (snapshot ? snapshot.browsers.filter((browser) => browser.policyKeyExists) : []),
    [snapshot],
  );
  const isProtectionActive = configuredBrowsers.some((browser) => browser.allowedUrls.length > 0);
  const allowedSiteCount = snapshot
    ? new Set(snapshot.browsers.flatMap((browser) => browser.allowedUrls)).size
    : 0;

  const advancedFlagsEnabled = useMemo(() => {
    if (configuredBrowsers.length === 0) return 0;
    const flags: (keyof BrowserPolicySnapshot)[] = [
      "incognitoDisabled",
      "browserSigninDisabled",
      "developerToolsDisabled",
    ];
    return flags.filter((flag) => configuredBrowsers.every((browser) => browser[flag] === true)).length;
  }, [configuredBrowsers]);

  const adminCount = accounts.filter((account) => account.isAdministrator).length;
  const healthRows = snapshot ? buildHealthRows(snapshot.browsers) : [];

  return (
    <div className="dashboard-page">
      <h1 className="page-title">首頁</h1>

      {error && (
        <div className="card banner-warning banner-row">
          <ShieldX className="banner-icon" />
          <div className="banner-row-text">{error}</div>
        </div>
      )}

      <div className={`card banner-status banner-row ${isProtectionActive ? "banner-active" : ""}`}>
        <span className={`icon-chip icon-chip-${isProtectionActive ? "success" : "neutral"}`}>
          {isProtectionActive ? <ShieldCheck /> : <ShieldMinus />}
        </span>
        <div className="banner-row-text">
          <div className="banner-status-title">
            目前防護狀態：{isProtectionActive ? "已啟用白名單管制" : "尚未設定"}
          </div>
          <div className="banner-status-subtitle">
            {configuredBrowsers.length > 0
              ? `已於 ${configuredBrowsers.map((b) => BROWSER_DISPLAY_NAME[b.browserId] ?? b.browserId).join("、")} 套用原則`
              : "尚未在任何瀏覽器套用原則"}
            ・目前允許 {allowedSiteCount} 個網站
          </div>
        </div>
        <button className="btn btn-secondary" onClick={refresh} disabled={isRefreshing}>
          <RefreshCw className={isRefreshing ? "spin" : ""} /> 重新整理狀態
        </button>
      </div>

      <div className="stat-grid">
        <div className="card stat-card">
          <div className="stat-card-header">
            <span className="stat-card-label">受管瀏覽器</span>
            <span className="icon-chip icon-chip-sm icon-chip-primary"><Globe2 /></span>
          </div>
          <div className="stat-card-value">
            {configuredBrowsers.length > 0
              ? configuredBrowsers.map((b) => BROWSER_DISPLAY_NAME[b.browserId] ?? b.browserId).join("、")
              : "尚未設定"}
          </div>
          <div className="stat-card-note">登錄檔即時讀取，非本地快取</div>
        </div>

        <div className="card stat-card">
          <div className="stat-card-header">
            <span className="stat-card-label">目前允許網站</span>
            <span className="icon-chip icon-chip-sm icon-chip-success"><ListChecks /></span>
          </div>
          <div className="stat-card-value">{allowedSiteCount} 個網址</div>
          <div className="stat-card-note">跨所有受管瀏覽器去重計算</div>
        </div>

        <div className="card stat-card">
          <div className="stat-card-header">
            <span className="stat-card-label">進階防繞過設定</span>
            <span className="icon-chip icon-chip-sm icon-chip-info"><SlidersHorizontal /></span>
          </div>
          <div className="stat-card-value">{advancedFlagsEnabled} / 3 項已啟用</div>
          <div className="stat-card-note">無痕視窗・帳號切換・開發人員工具</div>
        </div>

        <div className="card stat-card">
          <div className="stat-card-header">
            <span className="stat-card-label">本機使用者帳號</span>
            <span className="icon-chip icon-chip-sm icon-chip-neutral"><Users /></span>
          </div>
          <div className="stat-card-value">共 {accounts.length} 個帳號</div>
          <div className="stat-card-note">其中 {adminCount} 個為系統管理員</div>
        </div>
      </div>

      <div className="dashboard-columns">
        <div className="dashboard-column">
          <h2 className="section-title">本機使用者帳號</h2>
          <div className="account-list">
            {accounts.map((account) => (
              <div key={account.accountName} className="card account-row">
                <span className="icon-chip icon-chip-sm icon-chip-neutral"><Users /></span>
                <span className="account-name">{account.accountName}</span>
                <span className={`pill ${account.isAdministrator ? "pill-success" : "pill-neutral"}`}>
                  {account.isAdministrator ? "系統管理員" : "標準使用者"}
                </span>
                {account.isBuiltIn && <span className="pill pill-info">內建帳號</span>}
              </div>
            ))}
          </div>

          <button className="btn btn-secondary dashboard-detail-btn" onClick={() => setShowDetail(true)} disabled={!snapshot}>
            查看完整設定值 (進階)
          </button>
        </div>

        <div className="dashboard-column">
          <h2 className="section-title">防護狀態即時健康檢查</h2>
          <div className="card health-list">
            {healthRows.length === 0 && <div className="health-row-detail">尚無資料，請先重新整理狀態。</div>}
            {healthRows.map((row) => (
              <div key={row.label} className="health-row">
                <span className={`health-row-icon ${row.state}`}>
                  {row.state === "ok" ? <CheckCircle2 /> : row.state === "fail" ? <XCircle /> : <Circle />}
                </span>
                <div>
                  <div className="health-row-label">{row.label}</div>
                  <div className="health-row-detail">{row.detail}</div>
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>

      {showDetail && snapshot && (
        <PolicyDetailDialog snapshot={snapshot} onClose={() => setShowDetail(false)} />
      )}
    </div>
  );
}
