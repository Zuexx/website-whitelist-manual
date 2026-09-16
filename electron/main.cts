import { app, BrowserWindow, ipcMain } from "electron";
import { spawn, type ChildProcess } from "node:child_process";
import path from "node:path";
import fs from "node:fs/promises";
import os from "node:os";
import http from "node:http";

const isDev = !app.isPackaged;
const API_TOKEN_PATH = path.join(
  os.homedir(),
  "AppData", "Local", "WebsiteWhitelistManual", "api-token.txt",
);

const STATIC_MIME_TYPES: Record<string, string> = {
  ".html": "text/html; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".svg": "image/svg+xml",
  ".json": "application/json; charset=utf-8",
  ".png": "image/png",
  ".ico": "image/x-icon",
};

let apiProcess: ChildProcess | null = null;
let mainWindow: BrowserWindow | null = null;
let staticServer: http.Server | null = null;

function resolveApiExecutablePath(): string {
  // In development, the API is expected to already be running via a
  // separate `dotnet run` (see Task 9's dev-workflow doc) — Electron does
  // not spawn it itself in that mode, since `dotnet run` in dev rebuilds
  // faster than restarting a packaged exe on every change. In a packaged
  // build, electron-builder's extraResources config (Task 9) copies the
  // published self-contained API executable into
  // <installDir>/resources/api/WebsiteWhitelistManual.Api.exe.
  return path.join(process.resourcesPath, "api", "WebsiteWhitelistManual.Api.exe");
}

function startApiProcessIfPackaged(): void {
  if (isDev) {
    return;
  }
  const exePath = resolveApiExecutablePath();

  // node:child_process.spawn() launches the target via CreateProcess(),
  // which — unlike double-clicking the exe or Start-Process -Verb RunAs —
  // does NOT honor an embedded app.manifest's requireAdministrator
  // elevation: Windows returns ERROR_ELEVATION_REQUIRED and CreateProcess
  // simply fails (no UAC prompt, no running process, often no error
  // Node surfaces either). Only ShellExecuteEx-based launchers trigger
  // the UAC consent dialog for a manifest-elevated target. PowerShell's
  // Start-Process -Verb RunAs goes through ShellExecuteEx, so shelling
  // out to it here is what actually produces the UAC prompt — this was
  // verified failing silently with plain spawn() and working via this
  // PowerShell route on a real packaged build.
  apiProcess = spawn(
    "powershell.exe",
    [
      "-NoProfile",
      "-WindowStyle", "Hidden",
      "-Command",
      `Start-Process -FilePath '${exePath}' -Verb RunAs -WindowStyle Hidden`,
    ],
    { stdio: "ignore" },
  );
  apiProcess.on("error", (err) => {
    console.error("Failed to start WebsiteWhitelistManual.Api:", err);
  });
}

async function waitForApiToken(maxAttempts = 40, delayMs = 250): Promise<void> {
  for (let attempt = 0; attempt < maxAttempts; attempt++) {
    try {
      // Checking only that the token file exists isn't enough: Program.cs
      // writes it to disk BEFORE app.Run() actually starts Kestrel
      // listening, so there's a real window where the file is readable but
      // a fetch to the API still fails outright (connection refused) —
      // which the renderer has no automatic retry for, so a request fired
      // in that window fails and never recovers on its own. Probing
      // /api/health (which SharedSecretMiddleware always leaves open) is
      // what actually confirms the server is ready to accept requests.
      await fs.access(API_TOKEN_PATH);
      await fetch("http://127.0.0.1:5292/api/health", { signal: AbortSignal.timeout(1000) });
      return;
    } catch {
      await new Promise((resolve) => setTimeout(resolve, delayMs));
    }
  }
  throw new Error(
    `API token file did not appear at ${API_TOKEN_PATH} after ${maxAttempts * delayMs}ms — ` +
    "the API process may have failed to start, or the UAC prompt was dismissed.",
  );
}

// Vite's build emits `<script type="module">`, and Chromium refuses to run
// module scripts loaded from a `file://` origin (it's treated as the null
// origin, which module-script CORS rejects outright) — so `loadFile()`
// against the built index.html renders a blank window with no visible
// error, only a console CORS message. Serving the same static files over
// loopback HTTP instead gives the renderer a real origin Chromium accepts,
// without needing any change to the Vite output.
function startStaticServer(rootDir: string): Promise<number> {
  return new Promise((resolve, reject) => {
    const server = http.createServer((req, res) => {
      void (async () => {
        const requestPath = decodeURIComponent((req.url ?? "/").split("?")[0] ?? "/");
        const relativePath = requestPath === "/" ? "index.html" : requestPath.replace(/^\/+/, "");
        const resolvedPath = path.join(rootDir, relativePath);

        try {
          const data = await fs.readFile(resolvedPath);
          const contentType = STATIC_MIME_TYPES[path.extname(resolvedPath)] ?? "application/octet-stream";
          res.writeHead(200, { "Content-Type": contentType });
          res.end(data);
        } catch {
          // HashRouter keeps every client-side route in the URL fragment,
          // which never reaches the server, so this fallback only matters
          // for a stray or malformed request — not normal navigation.
          const fallback = await fs.readFile(path.join(rootDir, "index.html"));
          res.writeHead(200, { "Content-Type": STATIC_MIME_TYPES[".html"] });
          res.end(fallback);
        }
      })();
    });

    server.on("error", reject);
    server.listen(0, "127.0.0.1", () => {
      const address = server.address();
      if (address && typeof address === "object") {
        staticServer = server;
        resolve(address.port);
      } else {
        reject(new Error("Static server did not report a listening port."));
      }
    });
  });
}

async function createMainWindow(): Promise<void> {
  mainWindow = new BrowserWindow({
    width: 1024,
    height: 720,
    webPreferences: {
      preload: path.join(__dirname, "preload.cjs"),
      contextIsolation: true,
      nodeIntegration: false,
    },
  });

  mainWindow.webContents.on("did-fail-load", (_event, errorCode, errorDescription) => {
    console.error(`Renderer failed to load: ${errorCode} ${errorDescription}`);
  });

  if (isDev) {
    await mainWindow.loadURL("http://localhost:5173");
  } else {
    const port = await startStaticServer(path.join(__dirname, "..", "dist"));
    await mainWindow.loadURL(`http://127.0.0.1:${port}/index.html`);
  }
}

app.whenReady().then(async () => {
  // Registered before the window loads (and before the API/token file are
  // even guaranteed to exist yet): the renderer's Dashboard fires its
  // getApiToken() IPC call the moment it mounts, so the handler must
  // already be listening or that first invoke can resolve to `undefined`
  // instead of erroring — which client.ts would then happily send as a
  // literal "X-Api-Token: undefined" header, failing the server's
  // comparison instead of surfacing a clear "not ready yet" error.
  ipcMain.handle("get-api-token", async () => {
    await waitForApiToken();
    return fs.readFile(API_TOKEN_PATH, "utf-8");
  });

  startApiProcessIfPackaged();
  try {
    await waitForApiToken();
  } catch (err) {
    console.error(err);
    // The renderer's own error handling (client.ts) surfaces a clear
    // message per-request rather than this main-process code blocking
    // window creation entirely — the window still opens so the user can
    // at least see the app and any in-page error state.
  }
  await createMainWindow();
});

app.on("window-all-closed", () => {
  if (process.platform !== "darwin") {
    app.quit();
  }
});

let hasShutDownApi = false;

app.on("before-quit", (event) => {
  // apiProcess here is the PowerShell launcher (see startApiProcessIfPackaged),
  // not the elevated API itself — killing it does nothing to the actual
  // WebsiteWhitelistManual.Api.exe, and this unelevated process has no
  // permission to terminate that higher-integrity process directly (Windows
  // Mandatory Integrity Control blocks it even for the same user). Asking
  // the API to shut itself down over its own HTTP endpoint is the only
  // reliable way to avoid leaving it running as an orphan. before-quit
  // handlers run synchronously by default, so quitting is deferred with
  // preventDefault() until the shutdown request actually completes —
  // otherwise Electron can tear the process down mid-fetch.
  apiProcess?.kill();
  staticServer?.close();

  if (hasShutDownApi) {
    return;
  }
  event.preventDefault();
  void shutdownApiProcess().finally(() => {
    hasShutDownApi = true;
    app.quit();
  });
});

async function shutdownApiProcess(): Promise<void> {
  if (isDev) {
    return;
  }
  try {
    const token = await fs.readFile(API_TOKEN_PATH, "utf-8");
    await fetch("http://127.0.0.1:5292/api/shutdown", {
      method: "POST",
      headers: { "X-Api-Token": token },
      signal: AbortSignal.timeout(2000),
    });
  } catch {
    // Best-effort: if the API never started, isn't reachable, or already
    // shut down, there's nothing left to signal — quitting proceeds
    // regardless (see the .finally() at the call site).
  }
}
