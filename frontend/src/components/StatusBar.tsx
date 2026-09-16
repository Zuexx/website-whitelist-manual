import { useCallback, useEffect, useState } from "react";
import { ShieldCheck, ShieldAlert, RefreshCw } from "lucide-react";
import { apiClient } from "../api/client";
import "./StatusBar.css";

export function StatusBar() {
  const [isActive, setIsActive] = useState<boolean | null>(null);
  const [lastChecked, setLastChecked] = useState<Date | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isChecking, setIsChecking] = useState(false);

  const check = useCallback(async () => {
    setIsChecking(true);
    try {
      const snapshot = await apiClient.getSnapshot();
      const active = snapshot.browsers.some(
        (browser) => browser.policyKeyExists && browser.allowedUrls.length > 0,
      );
      setIsActive(active);
      setLastChecked(new Date());
      setError(null);
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      setIsChecking(false);
    }
  }, []);

  useEffect(() => {
    check();
  }, [check]);

  const statusLabel = error
    ? "無法連線至本機防護服務"
    : isActive === null
      ? "正在檢查防護狀態…"
      : isActive
        ? "白名單守護程序運行中"
        : "尚未啟用白名單防護";

  const tone = error ? "warning" : isActive ? "success" : "neutral";

  return (
    <div className={`status-bar status-bar-${tone}`}>
      <div className="status-bar-row">
        <span className={`icon-chip icon-chip-sm icon-chip-${tone === "warning" ? "warning" : tone === "success" ? "success" : "neutral"}`}>
          {tone === "warning" ? <ShieldAlert /> : <ShieldCheck />}
        </span>
        <span className="status-bar-label">安全狀態：{statusLabel}</span>
        {lastChecked && (
          <span className="status-bar-time">
            (最後檢查：{lastChecked.toLocaleTimeString("zh-TW", { hour: "2-digit", minute: "2-digit" })})
          </span>
        )}
      </div>
      <button className="status-bar-refresh" onClick={check} disabled={isChecking} title="重新檢查">
        <RefreshCw className={isChecking ? "spin" : ""} />
      </button>
    </div>
  );
}
