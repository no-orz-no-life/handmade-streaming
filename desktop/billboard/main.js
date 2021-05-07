const { app, BrowserWindow } = require('electron')
const url  = require('url')
const path = require('path')

let mainWindow

app.on("ready", createWindow)
app.on("window-all-closed", () => {
    if(process.platform !== "darwin") app.quit()
})
app.on("activate", () => {
    if(mainWindow === null) createWindow()
})

function createWindow() {
    mainWindow = new BrowserWindow({
        webPreferences: {
            nodeIntegration: false
        }
    })

    mainWindow.loadURL(url.format({
        pathname: path.join(__dirname, 'public', 'marquee.html'),
        protocol: 'file:',
        slashes: true,
    }))
    mainWindow.on("closed", () => {
        mainWindow = null
    })
}