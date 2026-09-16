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
  apiProcess = spawn(exePath, [], {
    // shell:false (the default) is required here — it spawns the target
    // executable directly so Windows honors its embedded app.manifest's
    // requireAdministrator elevation. Setting shell:true would run it
    // under cmd.exe instead, which complicates (though does not
    // necessarily break) manifest-based elevation — do not add it.
    stdio: "ignore",
  });
  apiProcess.on("error", (err) => {
    console.error("Failed to start WebsiteWhitelistManual.Api:", err);
  });
}

async function waitForApiToken(maxAttempts = 40, delayMs = 250): Promise<void> {
  for (let attempt = 0; attempt < maxAttempts; attempt++) {
    try {
      await fs.access(API_TOKEN_PATH);
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

  ipcMain.handle("get-api-token", async () => {
    return fs.readFile(API_TOKEN_PATH, "utf-8");
  });
});

app.on("window-all-closed", () => {
  if (process.platform !== "darwin") {
    app.quit();
  }
});

app.on("before-quit", () => {
  apiProcess?.kill();
  staticServer?.close();
});
