const { app, BrowserWindow, ipcMain, globalShortcut, shell } = require('electron')
const fs = require('fs')
const fsP = fs.promises
const path = require('path')
const express = require('express')()
const iconv = require("iconv-lite")

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
            this.window.show()
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

function getBasePath() {
    if(process.platform === "win32")
    {
        return "c:/shortcut"
    }
    else if(process.platform === "linux")
    {
        return "~/shortcut"
    }
}

const basePath = getBasePath()
let candidates = {}

function getCandidates(startPath) {
    let ret = {}

    function core(dic, dir){
        let files = fs.readdirSync(dir)
        files.forEach(it => {
            let p = path.join(dir, it.toString())
            let stat = fs.statSync(p)
            if(stat.isDirectory())
            {
                core(ret, p)
            }
            if(stat.isSymbolicLink())
            {
                const link = fs.readlinkSync(p)
                ret[it] = link
            }
            else if(it.endsWith(".lnk"))
            {
                ret[it] = p
            }
        })
    }
    core(ret, startPath)
    return ret
}

ipcMain.handle("getCandidates", (events) => {
    candidates = getCandidates(basePath)
    return candidates
})

ipcMain.on("openCandidate", (event, key) => {
    console.log(`invoking... ${key}`)
    if(key in candidates)
    {
        const v = candidates[key]
        console.log(`${key} => ${v}`)
        shell.openPath(v)
        launcher.blur()
    }
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