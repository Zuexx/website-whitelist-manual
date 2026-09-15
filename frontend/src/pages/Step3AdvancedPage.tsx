import { useWizard } from "../state/WizardContext";
import "./Step3AdvancedPage.css";

const options: { key: "disableIncognito" | "disableDeveloperTools" | "disableAccountSwitching"; title: string; description: string; registryNote: string }[] = [
  {
    key: "disableIncognito",
    title: "停用無痕視窗 (InPrivate / Incognito)",
    description: "防止孩子建立不會留下瀏覽紀錄的無痕分頁。",
    registryNote: "Windows 原則值：InPrivateModeAvailability / IncognitoModeAvailability = 1",
  },
  {
    key: "disableDeveloperTools",
    title: "停用開發人員工具 (Developer Tools / F12)",
    description: "避免具備技術好奇心的孩子透過控制台修改設定或繞過限制。",
    registryNote: "Windows 原則值：DeveloperToolsAvailability = 2",
  },
  {
    key: "disableAccountSwitching",
    title: "停用帳號切換 (BrowserSignin)",
    description: "禁止在瀏覽器中切換至未受管的其他帳號，確保白名單防護不會被多帳號登入繞過。",
    registryNote: "Windows 原則值：BrowserSignin = 0",
  },
];

export function Step3AdvancedPage() {
  const { advancedOptions, setAdvancedOptions } = useWizard();

  function toggle(key: typeof options[number]["key"]) {
    setAdvancedOptions({ ...advancedOptions, [key]: !advancedOptions[key] });
  }

  return (
    <div className="step3-page">
      <h1 className="page-title">步驟 3：設定防繞過與進階防護選項</h1>
      <p className="page-description">建議全數維持開啟以達成無漏洞防護。</p>

      {options.map((option) => (
        <div key={option.key} className="card option-row">
          <div className="option-text">
            <div className="option-title">{option.title}</div>
            <p className="option-description">{option.description}</p>
            <div className="option-registry-note">{option.registryNote}</div>
          </div>
          <label className="toggle">
            <input
              type="checkbox"
              checked={advancedOptions[option.key]}
              onChange={() => toggle(option.key)}
            />
            <span className="toggle-track" />
          </label>
        </div>
      ))}
    </div>
  );
}
