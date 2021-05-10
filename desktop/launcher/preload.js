const { contextBridge, ipcRenderer} = require("electron");

contextBridge.exposeInMainWorld(
    "electron", {
        Open: (url) => ipcRenderer.send("open", url),
        Blur: () => ipcRenderer.send("blur"),
        GetCandidates: () => ipcRenderer.invoke("getCandidates"),
        OpenCandidate: (key) => ipcRenderer.send("openCandidate", key),
    }
);