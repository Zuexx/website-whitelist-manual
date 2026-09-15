import { NavLink } from "react-router-dom";
import "./NavRail.css";

const steps = [
  { to: "/step1", label: "1. 選擇瀏覽器" },
  { to: "/step2", label: "2. 允許的網站" },
  { to: "/step3", label: "3. 進階選項" },
  { to: "/step4", label: "4. 套用前確認" },
  { to: "/step5", label: "5. 完成與驗證" },
];

export function NavRail() {
  return (
    <nav className="nav-rail">
      <div className="nav-rail-brand">
        <span className="nav-rail-title">白名單防護</span>
        <span className="nav-rail-subtitle">家長防護管理模式</span>
      </div>
      <NavLink to="/" end className={({ isActive }) => `nav-item ${isActive ? "nav-item-active" : ""}`}>
        首頁
      </NavLink>
      <div className="nav-rail-section-label">設定精靈導覽</div>
      {steps.map((step) => (
        <NavLink
          key={step.to}
          to={step.to}
          className={({ isActive }) => `nav-item ${isActive ? "nav-item-active" : ""}`}
        >
          {step.label}
        </NavLink>
      ))}
    </nav>
  );
}
