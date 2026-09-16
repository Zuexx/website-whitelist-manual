import { Braces, X } from "lucide-react";
import type { PolicySnapshot } from "../api/types";
import "./PolicyDetailDialog.css";

function formatSnapshot(snapshot: PolicySnapshot): string {
  return snapshot.browsers
    .map((browser) => [
      `=== ${browser.browserId} ===`,
      `政策機碼存在: ${browser.policyKeyExists}`,
      `URLBlocklist: ${browser.blockedUrls.join(", ")}`,
      `URLAllowlist: ${browser.allowedUrls.join(", ")}`,
      `停用無痕模式: ${browser.incognitoDisabled}`,
      `停用帳號切換 (BrowserSignin=0): ${browser.browserSigninDisabled}`,
      `停用開發人員工具: ${browser.developerToolsDisabled}`,
      `YouTube 限制模式 (ForceYouTubeRestrict=2): ${browser.youTubeRestrictEnabled}`,
      "",
    ].join("\n"))
    .join("\n");
}

export function PolicyDetailDialog({ snapshot, onClose }: { snapshot: PolicySnapshot; onClose: () => void }) {
  return (
    <div className="dialog-overlay" onClick={onClose}>
      <div className="dialog-panel" onClick={(event) => event.stopPropagation()}>
        <div className="dialog-header">
          <h2 className="dialog-title"><Braces /> 完整設定值</h2>
          <button className="dialog-close" onClick={onClose} title="關閉">
            <X />
          </button>
        </div>
        <pre className="dialog-content">{formatSnapshot(snapshot)}</pre>
      </div>
    </div>
  );
}
