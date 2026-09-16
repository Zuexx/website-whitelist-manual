import { NavLink } from "react-router-dom";
import { Check, Globe2, Home, ListChecks, ShieldCheck, ShieldPlus, SlidersHorizontal } from "lucide-react";
import { useWizard } from "../state/WizardContext";
import "./NavRail.css";

const APP_VERSION = "1.3.0";

export function NavRail() {
  const { selectedBrowsers, allowlistSites, hasApplied } = useWizard();

  const step1Done = selectedBrowsers.length > 0;
  const step2Done = allowlistSites.length > 0;
  const step3Done = step1Done && step2Done;
  const step4Done = hasApplied;

  const steps: { to: string; label: string; icon: typeof Globe2; done: boolean }[] = [
    { to: "/step1", label: "1. 選擇瀏覽器", icon: Globe2, done: step1Done },
    { to: "/step2", label: "2. 允許的網站", icon: ListChecks, done: step2Done },
    { to: "/step3", label: "3. 進階選項", icon: SlidersHorizontal, done: step3Done },
    { to: "/step4", label: "4. 套用前確認", icon: ShieldPlus, done: step4Done },
    { to: "/step5", label: "5. 完成與驗證", icon: ShieldCheck, done: false },
  ];

  return (
    <nav className="nav-rail">
      <div className="nav-rail-brand">
        <span className="icon-chip icon-chip-primary">
          <ShieldCheck />
        </span>
        <div>
          <div className="nav-rail-title">白名單防護</div>
          <div className="nav-rail-subtitle">家長防護管理模式</div>
        </div>
      </div>

      <NavLink to="/" end className={({ isActive }) => `nav-item ${isActive ? "nav-item-active" : ""}`}>
        <span className="nav-item-icon"><Home /></span>
        首頁
      </NavLink>

      <div className="nav-rail-section-label">設定精靈導覽</div>
      {steps.map((step) => {
        const Icon = step.icon;
        return (
          <NavLink
            key={step.to}
            to={step.to}
            className={({ isActive }) => `nav-item ${isActive ? "nav-item-active" : ""}`}
          >
            {({ isActive }: { isActive: boolean }) => (
              <>
                <span className={`nav-step-dot ${step.done ? "nav-step-done" : ""} ${isActive ? "nav-step-active" : ""}`}>
                  {step.done ? <Check /> : <Icon />}
                </span>
                {step.label}
              </>
            )}
          </NavLink>
        );
      })}

      <div className="nav-rail-footer">網站白名單設定工具 v{APP_VERSION}</div>
    </nav>
  );
}
