const { app, BrowserWindow, ipcMain, globalShortcut, shell } = require('electron')
const fs = require('fs')
const fsP = fs.promises
const path = require('path')

const shortcutKey = "Alt+Space"
class Window {
    constructor(windowOption, initialURL) {
        this.windowOption = windowOption
        this.initialURL = initialURL
        this.window = null
    }
    ensureOpen() {
        if(this.window !== null) return
        this.window = new BrowserWindow(this.windowOption)
        this.window.loadURL(this.initialURL)
        this.window.on("closed", () => {
            this.window = null
            globalShortcut.unregister(shortcutKey)
        })
        globalShortcut.register(shortcutKey, () => {
            if(this.window.isFocused())
            {
                this.window.blur()
    
            } else {
                this.window.focus()
            }
        })
    }
    blur() {
        this.window.blur()
    }
}

let debug=true
let option = {
    resizable: debug,

    movable: true,
    focusable: true,
    frame: false,
    transparent: !debug,
    fullscreenable: false,
    "skip-taskbar": !debug,

    webPreferences: {
        preload: path.join(__dirname, "preload.js")
    },

    width: 600,
    height: 200,
    title: "Launcher",
}
let launcher = new Window(option, `file://${__dirname}/public/index.html`)

function ensureOpen() {
    launcher.ensureOpen()
}
app.on("ready", ensureOpen)
app.on("window-all-closed", () => {
    if(process.platform !== "darwin") app.quit()
})
app.on("activate", ensureOpen)

ipcMain.on("open", (event, url) => {
    shell.openExternal(url)
})

ipcMain.on("blur", (event, ...args) => {
    launcher.blur()
})

