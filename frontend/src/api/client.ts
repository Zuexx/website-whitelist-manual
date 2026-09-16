import type { ApplyPolicyRequest, ApplyPolicyResponse, LocalAccount, PolicySnapshot } from "./types";

const API_BASE_URL = "http://127.0.0.1:5292";

declare global {
  interface Window {
    electronApi?: {
      getApiToken: () => Promise<string>;
    };
  }
}

async function getToken(): Promise<string> {
  if (window.electronApi) {
    return window.electronApi.getApiToken();
  }
  // Dev-only fallback: `npm run dev` outside Electron has no IPC bridge, so
  // read the token file's content pasted manually into localStorage for
  // local frontend iteration against a `dotnet run` API instance. Never
  // reached in the packaged app, where window.electronApi always exists.
  const stored = window.localStorage.getItem("dev-api-token");
  if (!stored) {
    throw new Error(
      "No API token available. Running outside Electron: paste the contents of " +
      "%LOCALAPPDATA%\\WebsiteWhitelistManual\\api-token.txt into " +
      "localStorage.setItem('dev-api-token', '<token>') in the browser devtools console.",
    );
  }
  return stored;
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const token = await getToken();
  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      "X-Api-Token": token,
      ...init?.headers,
    },
  });

  if (!response.ok) {
    const body = await response.json().catch(() => ({ message: response.statusText }));
    throw new Error(body.message ?? `Request to ${path} failed with status ${response.status}`);
  }

  return response.json() as Promise<T>;
}

export const apiClient = {
  getSnapshot: () => request<PolicySnapshot>("/api/policy/snapshot"),
  getAccounts: (scope: "all" | "relevant") => request<LocalAccount[]>(`/api/accounts?scope=${scope}`),
  applyPolicy: (body: ApplyPolicyRequest) =>
    request<ApplyPolicyResponse>("/api/policy/apply", { method: "POST", body: JSON.stringify(body) }),
  removePolicy: () => request<ApplyPolicyResponse>("/api/policy", { method: "DELETE" }),
};
