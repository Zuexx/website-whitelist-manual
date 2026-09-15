import { useEffect, useState } from "react";
import { apiClient } from "../api/client";
import type { LocalAccount, PolicySnapshot } from "../api/types";
import { PolicyDetailDialog } from "../components/PolicyDetailDialog";
import "./DashboardPage.css";

export function DashboardPage() {
  const [snapshot, setSnapshot] = useState<PolicySnapshot | null>(null);
  const [accounts, setAccounts] = useState<LocalAccount[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [showDetail, setShowDetail] = useState(false);

  async function refresh() {
    setError(null);
    try {
      const [snapshotResult, accountsResult] = await Promise.all([
        apiClient.getSnapshot(),
        apiClient.getAccounts("all"),
      ]);
      setSnapshot(snapshotResult);
      setAccounts(accountsResult);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    }
  }

  useEffect(() => {
    refresh();
  }, []);

  const isProtectionActive = snapshot?.browsers.some(
    (browser) => browser.policyKeyExists && browser.allowedUrls.length > 0,
  ) ?? false;

  const allowedSiteCount = snapshot
    ? new Set(snapshot.browsers.flatMap((browser) => browser.allowedUrls)).size
    : 0;

  return (
    <div className="dashboard-page">
      <h1 className="page-title">首頁</h1>

      {error && <div className="card banner-warning">{error}</div>}

      <div className={`card banner-status ${isProtectionActive ? "banner-active" : ""}`}>
        <div>
          <div className="banner-status-title">
            目前防護狀態：{isProtectionActive ? "已啟用白名單管制" : "尚未設定"}
          </div>
          <div className="banner-status-subtitle">目前允許 {allowedSiteCount} 個網站</div>
        </div>
      </div>

      <button className="btn btn-secondary" onClick={refresh}>重新整理狀態</button>

      <h2 className="section-title">本機使用者帳號</h2>
      <div className="account-list">
        {accounts.map((account) => (
          <div key={account.accountName} className="card account-row">
            <span className="account-name">{account.accountName}</span>
            <span className="account-meta">系統管理員: {String(account.isAdministrator)}</span>
            <span className="account-meta">內建帳號: {String(account.isBuiltIn)}</span>
          </div>
        ))}
      </div>

      <button className="btn btn-secondary" onClick={() => setShowDetail(true)} disabled={!snapshot}>
        查看完整設定值 (進階)
      </button>

      {showDetail && snapshot && (
        <PolicyDetailDialog snapshot={snapshot} onClose={() => setShowDetail(false)} />
      )}
    </div>
  );
}
