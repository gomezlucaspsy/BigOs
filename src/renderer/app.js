'use strict'

/* ════════════════════════════════════════════════════════════════
   BigOs — Renderer / Browser Chrome
   ════════════════════════════════════════════════════════════════ */

// ── DOM refs ──────────────────────────────────────────────────────
const tabsContainer = document.getElementById('tabs-container')
const newTabBtn     = document.getElementById('new-tab-btn')
const webviewCont   = document.getElementById('webview-container')
const startPage     = document.getElementById('start-page')
const addressBar    = document.getElementById('address-bar')
const backBtn       = document.getElementById('back-btn')
const fwdBtn        = document.getElementById('fwd-btn')
const reloadBtn     = document.getElementById('reload-btn')
const statusText    = document.getElementById('status-text')
const tabCountEl    = document.getElementById('tab-count')
const engineInfo    = document.getElementById('engine-info')
const secIcon       = document.getElementById('sec-icon')

// ── State ─────────────────────────────────────────────────────────
let tabs        = []
let activeTabId = null
let tabIdSeq    = 0

// ── Helpers ───────────────────────────────────────────────────────
function normalizeUrl (raw) {
  const s = (raw || '').trim()
  if (!s) return null
  if (/^https?:\/\//i.test(s))                                   return s
  if (/^file:\/\//i.test(s))                                     return s
  if (/^localhost(:\d+)?(\/|$)/i.test(s))                        return 'http://' + s
  if (/^\d{1,3}(\.\d{1,3}){3}/.test(s))                         return 'http://' + s
  if (/^[\w-]+(\.[\w.-]+)+(\/.*)?$/.test(s) && !s.includes(' ')) return 'https://' + s
  return 'https://search.brave.com/search?q=' + encodeURIComponent(s)
}

function setStatus (msg) { statusText.textContent = msg }

function updateTabCount () {
  tabCountEl.textContent = 'TABS:' + tabs.length
}

function updateSecIcon (url) {
  if (!url || url === 'about:blank' || url === '') {
    secIcon.textContent = ''; secIcon.className = ''; return
  }
  if (url.startsWith('https://')) {
    secIcon.textContent = '🔒'; secIcon.className = 'secure'
  } else if (url.startsWith('http://')) {
    secIcon.textContent = '⚠';  secIcon.className = 'insecure'
  } else {
    secIcon.textContent = ''; secIcon.className = ''
  }
}

function updateNavBtns () {
  const t = getTab()
  backBtn.disabled = !(t && t.canGoBack)
  fwdBtn.disabled  = !(t && t.canGoForward)
}

function getTab (id) {
  const target = id !== undefined ? id : activeTabId
  return tabs.find(t => t.id === target) || null
}

// ── Tab creation ──────────────────────────────────────────────────
function createTab (url) {
  const id = ++tabIdSeq

  // Tab element
  const el = document.createElement('div')
  el.className = 'tab'
  el.dataset.tabId = id
  el.innerHTML = '<span class="tab-title">New Tab</span><button class="tab-close" title="Close [Ctrl+W]">×</button>'
  el.querySelector('.tab-close').addEventListener('click', e => { e.stopPropagation(); closeTab(id) })
  el.addEventListener('click', () => switchTab(id))
  tabsContainer.appendChild(el)

  // Webview element — use src only for initial load to avoid same-URL no-op
  const wv = document.createElement('webview')
  wv.setAttribute('allowpopups', '')
  wv.setAttribute('webpreferences', 'contextIsolation=yes,nodeIntegration=no,spellcheck=no')
  if (url && url !== 'about:blank') wv.setAttribute('src', url)
  wv.style.cssText = 'position:absolute;top:0;left:0;width:100%;height:100%;border:none;display:none;'
  webviewCont.appendChild(wv)

  const tab = {
    id,
    url:          url || '',
    title:        'New Tab',
    loading:      false,
    canGoBack:    false,
    canGoForward: false,
    el,
    wv
  }
  tabs.push(tab)

  // ── Webview events ────────────────────────────────────────────
  wv.addEventListener('did-start-loading', () => {
    tab.loading = true
    el.classList.add('loading')
    if (id === activeTabId) {
      reloadBtn.textContent = '✕'
      reloadBtn.title = 'Stop loading [Esc]'
      setStatus('LOADING...')
    }
  })

  wv.addEventListener('did-stop-loading', () => {
    tab.loading      = false
    tab.canGoBack    = wv.canGoBack()
    tab.canGoForward = wv.canGoForward()
    el.classList.remove('loading')
    if (id === activeTabId) {
      reloadBtn.textContent = '↺'
      reloadBtn.title = 'Reload [F5]'
      updateNavBtns()
      setStatus('LOADED  //  ' + (wv.getURL() || ''))
    }
  })

  wv.addEventListener('did-navigate', e => {
    tab.url          = e.url
    tab.canGoBack    = wv.canGoBack()
    tab.canGoForward = wv.canGoForward()
    if (id === activeTabId) {
      addressBar.value = e.url
      updateNavBtns()
      updateSecIcon(e.url)
    }
  })

  wv.addEventListener('did-navigate-in-page', e => {
    if (!e.isMainFrame) return
    tab.url = e.url
    if (id === activeTabId) {
      addressBar.value = e.url
      updateSecIcon(e.url)
    }
  })

  wv.addEventListener('page-title-updated', e => {
    tab.title = e.title || 'Untitled'
    el.querySelector('.tab-title').textContent = tab.title
    el.title = tab.title
  })

  wv.addEventListener('did-fail-load', e => {
    if (e.errorCode === -3) return  // aborted — user navigated away
    tab.title = 'Error'
    el.querySelector('.tab-title').textContent = 'Error'
    if (id === activeTabId) setStatus('ERR  //  ' + e.errorDescription)
  })

  // Open target=_blank / window.open() in a new tab
  wv.addEventListener('new-window', e => createTab(e.url))

  // Update address bar while typing a URL in the page
  wv.addEventListener('will-navigate', e => {
    if (id === activeTabId) addressBar.value = e.url
  })

  switchTab(id)
  return tab
}

// ── Tab switching ─────────────────────────────────────────────────
function switchTab (id) {
  tabs.forEach(t => { t.wv.style.display = 'none'; t.el.classList.remove('active') })
  startPage.style.display = 'none'

  const t = getTab(id)
  if (!t) return

  activeTabId = id
  t.el.classList.add('active')

  if (!t.url || t.url === 'about:blank') {
    startPage.style.display = 'flex'
    addressBar.value = ''
    setStatus('READY')
  } else {
    t.wv.style.display = 'block'
    addressBar.value = t.url
    setStatus('LOADED  //  ' + t.url)
  }

  updateNavBtns()
  updateSecIcon(t.url)
  updateTabCount()
}

// ── Tab closing ───────────────────────────────────────────────────
function closeTab (id) {
  const idx = tabs.findIndex(t => t.id === id)
  if (idx === -1) return
  const { el, wv } = tabs[idx]
  el.remove(); wv.remove()
  tabs.splice(idx, 1)

  if (tabs.length === 0) { createTab(); return }

  if (activeTabId === id) {
    const next = tabs[Math.min(idx, tabs.length - 1)]
    switchTab(next.id)
  }
  updateTabCount()
}

// ── Navigation ────────────────────────────────────────────────────
function navigate (raw) {
  const url = normalizeUrl(raw)
  if (!url) return
  const t = getTab()
  if (!t) return

  t.url = url
  addressBar.value = url
  updateSecIcon(url)
  startPage.style.display = 'none'
  t.wv.style.display = 'block'

  t.wv.loadURL(url).catch(() => t.wv.setAttribute('src', url))
}

// ── Address bar ───────────────────────────────────────────────────
addressBar.addEventListener('keydown', e => {
  if (e.key === 'Enter') { navigate(addressBar.value); addressBar.blur() }
  if (e.key === 'Escape') {
    const t = getTab()
    addressBar.value = t ? t.url : ''
    addressBar.blur()
    if (t && t.loading) t.wv.stop()
  }
})
addressBar.addEventListener('focus', () => addressBar.select())

// ── Nav buttons ───────────────────────────────────────────────────
backBtn.addEventListener('click',   () => { const t = getTab(); if (t && t.canGoBack)    t.wv.goBack() })
fwdBtn.addEventListener('click',    () => { const t = getTab(); if (t && t.canGoForward) t.wv.goForward() })
reloadBtn.addEventListener('click', () => {
  const t = getTab(); if (!t) return
  t.loading ? t.wv.stop() : t.wv.reload()
})

// ── New-tab button ────────────────────────────────────────────────
newTabBtn.addEventListener('click', () => createTab())

// ── Window controls ───────────────────────────────────────────────
document.getElementById('btn-min').addEventListener('click',   () => window.api.minimize())
document.getElementById('btn-max').addEventListener('click',   () => window.api.maximize())
document.getElementById('btn-close').addEventListener('click', () => window.api.close())

// ── Double-click titlebar — toggle maximize ───────────────────────
document.getElementById('titlebar').addEventListener('dblclick', () => window.api.maximize())

// ── Global keyboard shortcuts ─────────────────────────────────────
document.addEventListener('keydown', e => {
  const t = getTab()

  if (e.ctrlKey && e.key === 't') {
    e.preventDefault(); createTab()
  } else if (e.ctrlKey && e.key === 'w') {
    e.preventDefault(); if (activeTabId != null) closeTab(activeTabId)
  } else if (e.ctrlKey && e.key === 'l') {
    e.preventDefault(); addressBar.focus()
  } else if (e.key === 'F5' || (e.ctrlKey && e.key === 'r')) {
    e.preventDefault(); if (t) t.wv.reload()
  } else if (e.ctrlKey && e.shiftKey && e.key === 'R') {
    e.preventDefault(); if (t) t.wv.reloadIgnoringCache()
  } else if (e.altKey && e.key === 'ArrowLeft') {
    e.preventDefault(); if (t && t.canGoBack)    t.wv.goBack()
  } else if (e.altKey && e.key === 'ArrowRight') {
    e.preventDefault(); if (t && t.canGoForward) t.wv.goForward()
  } else if (e.key === 'F12') {
    if (t) t.wv.isDevToolsOpened() ? t.wv.closeDevTools() : t.wv.openDevTools()
  } else if (e.ctrlKey && e.shiftKey && e.key === 'I') {
    if (t) t.wv.openDevTools()
  } else if (e.ctrlKey && e.key >= '1' && e.key <= '9') {
    const idx = parseInt(e.key, 10) - 1
    if (idx < tabs.length) { e.preventDefault(); switchTab(tabs[idx].id) }
  }
})

// ── Populate version info ─────────────────────────────────────────
window.api.getVersion().then(v => {
  engineInfo.textContent = 'CHROMIUM/' + v.chrome.split('.')[0]
  document.title = 'BigOs'
}).catch(() => {})

// ── Initial tab ───────────────────────────────────────────────────
createTab()
