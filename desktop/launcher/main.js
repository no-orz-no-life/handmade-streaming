const { app, BrowserWindow, ipcMain, globalShortcut, shell } = require('electron')
const fs = require('fs')
const fsP = fs.promises
const path = require('path')
const express = require('express')()

const shortcutKey = "Alt+Space"
function toggleFocus(){
    launcher.toggleFocus()
}
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
        globalShortcut.register(shortcutKey, toggleFocus)
    }
    blur() {
        this.window.blur()
    }
    toggleFocus()
    {
        if(this.window.isFocused())
        {
            this.window.blur()

        } else {
            this.window.focus()
        }
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


if(process.platform === "linux") 
{
    // TODO: Wayland?
    const httpPort = 4126
    express.get("/toggleFocus", (req, res) => {
        launcher.toggleFocus()
        res.send("OK")
    })
    express.listen(httpPort, "127.0.0.1", () => {
        console.log(`listening http://localhost:${httpPort}/`)
    })
   
}