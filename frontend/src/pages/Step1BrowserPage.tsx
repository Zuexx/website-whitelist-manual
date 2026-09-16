import { CheckCircle2, Chrome, Info, MonitorSmartphone } from "lucide-react";
import { useWizard } from "../state/WizardContext";
import type { BrowserId } from "../api/types";
import "./Step1BrowserPage.css";

const browsers: { id: BrowserId; name: string; description: string; icon: typeof Chrome; tint: string }[] = [
  {
    id: "Edge",
    name: "Microsoft Edge",
    description: "Windows 內建瀏覽器・系統登錄原則 (Registry/GPO)",
    icon: MonitorSmartphone,
    tint: "info",
  },
  {
    id: "Chrome",
    name: "Google Chrome",
    description: "全球主流瀏覽器・企業政策原則 (Policies\\Google\\Chrome)",
    icon: Chrome,
    tint: "success",
  },
];

export function Step1BrowserPage() {
  const { selectedBrowsers, setSelectedBrowsers } = useWizard();

  function toggle(id: BrowserId) {
    setSelectedBrowsers(
      selectedBrowsers.includes(id)
        ? selectedBrowsers.filter((existing) => existing !== id)
        : [...selectedBrowsers, id],
    );
  }

  return (
    <div className="step1-page">
      <h1 className="page-title">步驟 1：選擇要鎖定的瀏覽器</h1>
      <p className="page-description">
        選擇這台筆電上要鎖定的瀏覽器，可以複選。其餘瀏覽器不受本工具管理。
      </p>

      <div className="browser-cards">
        {browsers.map((browser) => {
          const checked = selectedBrowsers.includes(browser.id);
          const Icon = browser.icon;
          return (
            <label key={browser.id} className={`card browser-card ${checked ? "browser-card-selected" : ""}`}>
              <input
                type="checkbox"
                className="browser-card-checkbox"
                checked={checked}
                onChange={() => toggle(browser.id)}
              />
              <div className="browser-card-header">
                <span className={`icon-chip icon-chip-${browser.tint}`}><Icon /></span>
                <span className="browser-card-name">{browser.name}</span>
                {checked && <CheckCircle2 className="browser-card-check" />}
              </div>
              <p className="browser-card-description">{browser.description}</p>
            </label>
          );
        })}
      </div>

      <div className="card info-banner banner-row">
        <span className="icon-chip icon-chip-sm icon-chip-info"><Info /></span>
        <div className="banner-row-text">
          <strong>為什麼建議同時勾選 Edge 與 Chrome？</strong>
          <p>避免孩子在其中一個瀏覽器被封鎖時，自行切換至另一個未受管的瀏覽器瀏覽未核准網站。</p>
        </div>
      </div>
    </div>
  );
}
