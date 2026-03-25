'use strict'

// BigOs — Node Web Explorer
const { app, BrowserWindow, ipcMain, shell } = require('electron')
const path = require('path')

// ── Performance flags applied before app is ready ─────────────────────────────
app.commandLine.appendSwitch('disable-background-networking')
app.commandLine.appendSwitch('disable-client-side-phishing-detection')
app.commandLine.appendSwitch('disable-crash-reporter')
app.commandLine.appendSwitch('disable-default-apps')
app.commandLine.appendSwitch('disable-hang-monitor')
app.commandLine.appendSwitch('disable-metrics-reporting')
app.commandLine.appendSwitch('disable-sync')
app.commandLine.appendSwitch('disable-translate')
app.commandLine.appendSwitch('enable-gpu-rasterization')
app.commandLine.appendSwitch('enable-zero-copy')
app.commandLine.appendSwitch('ignore-gpu-blocklist')
app.commandLine.appendSwitch('no-report-upload')
app.commandLine.appendSwitch('safebrowsing-disable-auto-update')

let mainWindow

function createWindow () {
  mainWindow = new BrowserWindow({
    width: 1280,
    height: 820,
    minWidth: 800,
    minHeight: 500,
    backgroundColor: '#000000',
    show: false,
    frame: false,       // custom titlebar
    thickFrame: true,   // allow edge-drag resize on Windows
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      nodeIntegration: false,
      contextIsolation: true,
      webviewTag: true,   // enable <webview> for page rendering
      sandbox: false,     // required for webviewTag in Electron 28+
      spellcheck: false,
      devTools: true
    }
  })

  mainWindow.loadFile(path.join(__dirname, 'src', 'renderer', 'index.html'))

  mainWindow.on('ready-to-show', () => mainWindow.show())
  mainWindow.on('closed', () => { mainWindow = null })
}

app.whenReady().then(() => {
  createWindow()
  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) createWindow()
  })
})

app.on('window-all-closed', () => app.quit())

// ── Window control IPC ─────────────────────────────────────────────────────────
ipcMain.on('window-minimize', () => mainWindow?.minimize())
ipcMain.on('window-maximize', () => {
  if (!mainWindow) return
  mainWindow.isMaximized() ? mainWindow.unmaximize() : mainWindow.maximize()
})
ipcMain.on('window-close', () => mainWindow?.close())

// ── Open external URLs safely ──────────────────────────────────────────────────
ipcMain.on('open-external', (_, url) => {
  try {
    const parsed = new URL(url)
    if (parsed.protocol === 'https:' || parsed.protocol === 'http:') {
      shell.openExternal(url)
    }
  } catch { /* invalid URL, ignore */ }
})

// ── Version info ───────────────────────────────────────────────────────────────
ipcMain.handle('get-version', () => ({
  electron: process.versions.electron,
  chrome: process.versions.chrome,
  node: process.versions.node
}))
