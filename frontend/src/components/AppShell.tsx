import { Outlet } from "react-router-dom";
import { NavRail } from "./NavRail";
import "./AppShell.css";

export function AppShell() {
  return (
    <div className="app-shell">
      <NavRail />
      <main className="app-shell-content">
        <Outlet />
      </main>
    </div>
  );
}
