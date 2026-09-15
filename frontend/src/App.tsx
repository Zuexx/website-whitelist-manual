import { HashRouter, Route, Routes } from "react-router-dom";
import { AppShell } from "./components/AppShell";
import { WizardProvider } from "./state/WizardContext";
import { DashboardPage } from "./pages/DashboardPage";
import { Step1BrowserPage } from "./pages/Step1BrowserPage";
import { Step2SitesPage } from "./pages/Step2SitesPage";
import { Step3AdvancedPage } from "./pages/Step3AdvancedPage";
import { Step4ConfirmPage } from "./pages/Step4ConfirmPage";
import { Step5CompletePage } from "./pages/Step5CompletePage";

export function App() {
  return (
    <WizardProvider>
      <HashRouter>
        <Routes>
          <Route element={<AppShell />}>
            <Route index element={<DashboardPage />} />
            <Route path="step1" element={<Step1BrowserPage />} />
            <Route path="step2" element={<Step2SitesPage />} />
            <Route path="step3" element={<Step3AdvancedPage />} />
            <Route path="step4" element={<Step4ConfirmPage />} />
            <Route path="step5" element={<Step5CompletePage />} />
          </Route>
        </Routes>
      </HashRouter>
    </WizardProvider>
  );
}
