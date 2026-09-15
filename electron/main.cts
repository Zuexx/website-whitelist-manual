import { app, BrowserWindow, ipcMain } from "electron";
import { spawn, type ChildProcess } from "node:child_process";
import path from "node:path";
import fs from "node:fs/promises";
import os from "node:os";

const isDev = !app.isPackaged;
const API_TOKEN_PATH = path.join(
  os.homedir(),
  "AppData", "Local", "WebsiteWhitelistManual", "api-token.txt",
);

let apiProcess: ChildProcess | null = null;
let mainWindow: BrowserWindow | null = null;

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

function createMainWindow(): void {
  mainWindow = new BrowserWindow({
    width: 1024,
    height: 720,
    webPreferences: {
      preload: path.join(__dirname, "preload.cjs"),
      contextIsolation: true,
      nodeIntegration: false,
    },
  });

  if (isDev) {
    mainWindow.loadURL("http://localhost:5173");
  } else {
    mainWindow.loadFile(path.join(process.resourcesPath, "app", "index.html"));
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
  createMainWindow();

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
});
