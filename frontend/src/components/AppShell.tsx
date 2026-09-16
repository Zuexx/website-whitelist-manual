import { Outlet } from "react-router-dom";
import { NavRail } from "./NavRail";
import { StatusBar } from "./StatusBar";
import "./AppShell.css";

export function AppShell() {
  return (
    <div className="app-shell">
      <NavRail />
      <div className="app-shell-main">
        <StatusBar />
        <main className="app-shell-content">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
