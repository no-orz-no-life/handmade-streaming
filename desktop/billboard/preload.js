const { contextBridge, ipcRenderer} = require("electron");

contextBridge.exposeInMainWorld(
    "electron", {
        GetMarqueeMessage: () => ipcRenderer.invoke("getMarqueeMessage"),
        GetCarouselPages: (force) => ipcRenderer.invoke("getCarouselPages", force)
    }
);
