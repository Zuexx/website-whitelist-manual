import { contextBridge, ipcRenderer } from "electron";

contextBridge.exposeInMainWorld("electronApi", {
  getApiToken: () => ipcRenderer.invoke("get-api-token"),
});
