import { createContext, useContext, useMemo, useState, type ReactNode } from "react";
import type { AdvancedOptions, AllowlistSiteInput, BrowserId } from "../api/types";

interface WizardState {
  selectedBrowsers: BrowserId[];
  allowlistSites: AllowlistSiteInput[];
  advancedOptions: AdvancedOptions;
}

interface WizardContextValue extends WizardState {
  setSelectedBrowsers: (browsers: BrowserId[]) => void;
  setAllowlistSites: (sites: AllowlistSiteInput[]) => void;
  setAdvancedOptions: (options: AdvancedOptions) => void;
  canApply: boolean;
  hasApplied: boolean;
  setHasApplied: (applied: boolean) => void;
}

const defaultAdvancedOptions: AdvancedOptions = {
  disableIncognito: true,
  disableAccountSwitching: true,
  disableDeveloperTools: true,
  // Off by default, unlike the other three: this is an opt-in content
  // filter, not a security-critical default the parent should be forced
  // into enabling.
  forceYouTubeRestrict: false,
};

const WizardContext = createContext<WizardContextValue | null>(null);

export function WizardProvider({ children }: { children: ReactNode }) {
  const [selectedBrowsers, setSelectedBrowsers] = useState<BrowserId[]>([]);
  const [allowlistSites, setAllowlistSites] = useState<AllowlistSiteInput[]>([]);
  const [advancedOptions, setAdvancedOptions] = useState<AdvancedOptions>(defaultAdvancedOptions);
  const [hasApplied, setHasApplied] = useState(false);

  const value = useMemo<WizardContextValue>(() => ({
    selectedBrowsers,
    allowlistSites,
    advancedOptions,
    setSelectedBrowsers,
    setAllowlistSites,
    setAdvancedOptions,
    canApply: selectedBrowsers.length > 0 && allowlistSites.length > 0,
    hasApplied,
    setHasApplied,
  }), [selectedBrowsers, allowlistSites, advancedOptions, hasApplied]);

  return <WizardContext.Provider value={value}>{children}</WizardContext.Provider>;
}

export function useWizard(): WizardContextValue {
  const context = useContext(WizardContext);
  if (!context) {
    throw new Error("useWizard must be used within a WizardProvider");
  }
  return context;
}
