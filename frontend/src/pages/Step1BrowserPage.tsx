import { useWizard } from "../state/WizardContext";
import type { BrowserId } from "../api/types";
import "./Step1BrowserPage.css";

const browsers: { id: BrowserId; name: string; description: string }[] = [
  { id: "Edge", name: "Microsoft Edge", description: "Windows 內建瀏覽器・系統登錄原則 (Registry/GPO)" },
  { id: "Chrome", name: "Google Chrome", description: "全球主流瀏覽器・企業政策原則 (Policies\\Google\\Chrome)" },
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
          return (
            <label key={browser.id} className={`card browser-card ${checked ? "browser-card-selected" : ""}`}>
              <div className="browser-card-header">
                <input type="checkbox" checked={checked} onChange={() => toggle(browser.id)} />
                <span className="browser-card-name">{browser.name}</span>
              </div>
              <p className="browser-card-description">{browser.description}</p>
            </label>
          );
        })}
      </div>

      <div className="card info-banner">
        <strong>為什麼建議同時勾選 Edge 與 Chrome？</strong>
        <p>避免孩子在其中一個瀏覽器被封鎖時，自行切換至另一個未受管的瀏覽器瀏覽未核准網站。</p>
      </div>
    </div>
  );
}
