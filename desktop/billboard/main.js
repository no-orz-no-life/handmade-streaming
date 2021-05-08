const { app, BrowserWindow, ipcMain } = require('electron')
const fs = require('fs')
const fsP = fs.promises
const path = require('path')
const asciidoctor = require('asciidoctor')() 

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
        })
    }
}

let debug=true
let commonOption = {
    resizable: debug,
    alwaysOnTop: !debug,

    movable: debug,
    focusable: debug,
    frame: debug,
    transparent: !debug,

    fullscreenable: false,
    "skip-taskbar": !debug,
    webPreferences: {
        preload: path.join(__dirname, "preload.js")
    }
}
let skipWidth = 0
let carouselWidth = 192*2
let marqueeHeight = 108
let marqueeOption = Object.assign({}, commonOption, {
    x: skipWidth,
    y: debug ? 0 : (1080 - marqueeHeight),
    width: 1920 - carouselWidth,
    height: marqueeHeight,
    title: "Marquee",
})
let carouselOption = Object.assign({}, commonOption, {
    x: debug ? 0 : (skipWidth + (1920 - carouselWidth)),
    y: 0,
    width: carouselWidth,
    height: 1080,
    title: "Carousel",
})
let marquee = new Window(marqueeOption, `file://${__dirname}/public/marquee.html`)
let carousel = new Window(carouselOption, `file://${__dirname}/public/carousel.html`)

function ensureOpen() {
    marquee.ensureOpen()
    carousel.ensureOpen()
}
app.on("ready", ensureOpen)
app.on("window-all-closed", () => {
    if(process.platform !== "darwin") app.quit()
})
app.on("activate", ensureOpen)

// TODO: async
function watchFile(path) {
    var oldMtime = new Date()
    return () => {
        const stat = fs.statSync(path)
        if(stat.mtime.getTime() == oldMtime.getTime())
        {
            return false
        }
        oldMtime = stat.mtime
        return true
    }
}

var marqueeMessage = "uninitialized. something wrong..."
const marqueeFilePath = path.join(__dirname, "data", "marquee-message.txt")
const marqueeFileUpdated = watchFile(marqueeFilePath)
ipcMain.handle("getMarqueeMessage", async (event, ...args) => {
    if(marqueeFileUpdated)
    {
        let message = fs.readFileSync(marqueeFilePath)
        marqueeMessage = message.toString('utf-8')
    }
    return marqueeMessage
})

const carouselFilePath = path.join(__dirname, "data", "carousel.adoc")
const carouselFileUpdated = watchFile(carouselFilePath)
let carouselFileRenderedText = ""
ipcMain.handle("getCarouselPages", async (event, force) => {
    let needsUpdate = force
    if(carouselFileUpdated())
    {
        console.log(`updating: ${carouselFilePath}`)
        let text = fs.readFileSync(carouselFilePath).toString('utf-8')
        carouselFileRenderedText = asciidoctor.convert(text)
        needsUpdate = true
    }
    if(needsUpdate)
    {
        return carouselFileRenderedText
    }
    else
    {
        return "unchanged"
    }
})