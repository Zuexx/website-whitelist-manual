export type BrowserId = "Edge" | "Chrome";

export interface BrowserPolicySnapshot {
  browserId: string;
  policyKeyExists: boolean;
  blockedUrls: string[];
  allowedUrls: string[];
  incognitoDisabled: boolean | null;
  browserSigninDisabled: boolean | null;
  developerToolsDisabled: boolean | null;
}

export interface PolicySnapshot {
  browsers: BrowserPolicySnapshot[];
}

export interface LocalAccount {
  accountName: string;
  isAdministrator: boolean;
  isBuiltIn: boolean;
}

export interface AllowlistSiteInput {
  domain: string;
  categoryLabel: string | null;
}

export interface AdvancedOptions {
  disableIncognito: boolean;
  disableAccountSwitching: boolean;
  disableDeveloperTools: boolean;
}

export interface ApplyPolicyRequest {
  browserIds: BrowserId[];
  allowlistSites: AllowlistSiteInput[];
  advancedOptions: AdvancedOptions;
}

export interface ApplyPolicyResponse {
  success: boolean;
  backupDirectory: string | null;
  backupFilePaths: string[];
  errorMessage: string | null;
  resultingSnapshot: PolicySnapshot | null;
}

export interface ApiError {
  message: string;
}
