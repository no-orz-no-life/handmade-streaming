const { contextBridge, ipcRenderer} = require("electron");

contextBridge.exposeInMainWorld(
    "electron", {
        Open: (url) => ipcRenderer.send("open", url),
        Blur: () => ipcRenderer.send("blur")
    }
);