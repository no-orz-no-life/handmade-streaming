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
    opacity: 0.8,
    transparent: !debug,

    fullscreenable: false,

    webPreferences: {
        preload: path.join(__dirname, "preload.js")
    }
}

let skipWidth = 1366
let carouselWidth = 192*2
let marqueeHeight = 108
let marqueeOption = Object.assign({}, commonOption, {
    x: skipWidth,
    y: 1080 - marqueeHeight,
    width: 1920 - carouselWidth,
    height: marqueeHeight,
    title: "Marquee",
})
let carouselOption = Object.assign({}, commonOption, {
    x: skipWidth + (1920 - carouselWidth),
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
var oldMtime = new Date()
var marqueeMessage = "uninitialized. something wrong..."
const marqueeFilePath = path.join(__dirname, "data", "marquee-message.txt")
ipcMain.handle("getMarqueeMessage", async (event, ...args) => {
    let stat = fs.statSync(marqueeFilePath)
    if(oldMtime.getTime() !== stat.mtime.getTime()) {
        console.log("modified.")
        let message = fs.readFileSync(marqueeFilePath)
        oldMtime = stat.mtime
        marqueeMessage = message.toString('utf-8')
    }
    return marqueeMessage
})

var carouselPages = {}
// when file vanishes?
const carouselFileDir = path.join(__dirname, "data")
const carouselFilePattern = new RegExp("carousel\.[0-9]+\.adoc")
ipcMain.handle("getCarouselPages", async (event, force) => {
    let files = fs.readdirSync(carouselFileDir)
    files.filter(it => it.match(carouselFilePattern)).forEach(it => {
        let p = path.join(carouselFileDir, it)
        let stat = fs.statSync(p)
        if(!(p in carouselPages) || carouselPages[p].mtime.getTime() !== stat.mtime.getTime())
        {
            // needs update
            console.log(`updating: ${p}`)
            let text = fs.readFileSync(p).toString('utf-8')
            const html = asciidoctor.convert(text)

            carouselPages[p] = {
                mtime: stat.mtime,
                renderedText: html
            }
        }
    })
    return Object.keys(carouselPages).map(key => carouselPages[key].renderedText)
})