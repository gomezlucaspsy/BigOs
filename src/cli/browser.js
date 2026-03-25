'use strict'

/* ════════════════════════════════════════════════════════════════
   BigOs CLI
   Terminal-based web browser using blessed TUI.
   ─ Fetches pages via node-fetch
   ─ Parses HTML with cheerio
   ─ Navigates links with Tab / Enter
   Note: JavaScript / SPAs require the GUI mode (bigos-gui).
   ════════════════════════════════════════════════════════════════ */

const blessed = require('blessed')
const fetch   = require('node-fetch')
const cheerio = require('cheerio')
const { URL }  = require('url')

class BigOsCLI {
  constructor () {
    this.history      = []
    this.historyIdx   = -1
    this.currentUrl   = ''
    this.links        = []
    this.selectedLink = -1

    this.screen = blessed.screen({
      smartCSR: true,
      title: 'BigOs CLI',
      fullUnicode: true,
      cursor: { artificial: true, shape: 'line', blink: true, color: 'green' }
    })

    this._buildUI()
    this._bindKeys()
  }

  // ── UI layout ──────────────────────────────────────────────────
  _buildUI () {
    const S = this.screen

    // Header bar
    this.header = blessed.box({
      parent: S, top: 0, left: 0, width: '100%', height: 1,
      content: ' BigOs CLI v1.0  [g] goto  [b] back  [f] fwd  [Tab] links  [Enter] follow  [?] help  [q] quit',
      style: { fg: 'black', bg: 'white', bold: true }
    })

    // URL input box
    this.urlBox = blessed.box({
      parent: S, top: 1, left: 0, width: '100%', height: 3,
      border: { type: 'line' },
      style: { border: { fg: 'white' }, fg: 'white', bg: 'black' }
    })
    blessed.text({
      parent: this.urlBox, left: 1, top: 0,
      content: '$>',
      style: { fg: 'brightwhite', bold: true, bg: 'black' }
    })
    this.urlDisplay = blessed.text({
      parent: this.urlBox, left: 4, top: 0, right: 1, height: 1,
      style: { fg: 'white', bg: 'black' }
    })

    // Content scrollbox
    this.content = blessed.box({
      parent: S, top: 4, left: 0, width: '100%', bottom: 3,
      border: { type: 'line' },
      scrollable: true,
      alwaysScroll: true,
      keys: true,
      vi: true,
      mouse: true,
      scrollbar: { ch: '█', style: { bg: 'white' } },
      style: {
        border: { fg: 'white' },
        fg: 'white', bg: 'black',
        scrollbar: { bg: 'white', fg: 'black' }
      },
      tags: true
    })

    // Status bar
    this.statusBox = blessed.box({
      parent: S, bottom: 0, left: 0, width: '100%', height: 3,
      border: { type: 'line' },
      style: { border: { fg: 'white' }, fg: 'white', bg: 'black' }
    })
    this.statusMsg = blessed.text({
      parent: this.statusBox, left: 1, top: 0, right: 1,
      style: { fg: 'white', bg: 'black' }
    })
  }

  // ── Key bindings ───────────────────────────────────────────────
  _bindKeys () {
    const S = this.screen

    S.key(['q', 'C-c'], () => { S.destroy(); process.exit(0) })

    S.key(['g', ':'], ()      => this._promptUrl())
    S.key(['b'],      ()      => this.goBack())
    S.key(['f'],      ()      => this.goForward())
    S.key(['tab'],    ()      => this._nextLink())
    S.key(['enter'],  ()      => this._followLink())
    S.key(['?', 'h'], ()      => this._showHelp())
    S.key(['r'],      ()      => this.currentUrl && this.navigate(this.currentUrl))

    this.content.key(['j', 'down'],  () => this.content.scroll(1))
    this.content.key(['k', 'up'],    () => this.content.scroll(-1))
    this.content.key(['G'],          () => this.content.setScrollPerc(100))
    this.content.key(['g'],          () => this.content.setScrollPerc(0))
    this.content.key(['C-d'],        () => this.content.scroll(12))
    this.content.key(['C-u'],        () => this.content.scroll(-12))
    this.content.key(['space'],      () => this.content.scroll(15))

    this.content.focus()
  }

  // ── URL prompt ─────────────────────────────────────────────────
  _promptUrl () {
    const prompt = blessed.prompt({
      parent: this.screen,
      top: 'center', left: 'center',
      width: '80%', height: 5,
      border: { type: 'line' },
      label: ' ∷ Go to URL ',
      style: { border: { fg: 'white' }, fg: 'white', bg: 'black', label: { fg: 'brightwhite' } }
    })
    prompt.input('$> ', this.currentUrl, (err, value) => {
      prompt.destroy()
      this.screen.render()
      this.content.focus()
      if (!err && value && value.trim()) {
        this.navigate(this._normalize(value.trim()))
      }
    })
    this.screen.render()
  }

  // ── URL normalizer ─────────────────────────────────────────────
  _normalize (s) {
    if (/^https?:\/\//i.test(s))                                   return s
    if (/^localhost(:\d+)?(\/|$)/i.test(s))                        return 'http://' + s
    if (/^\d{1,3}(\.\d{1,3}){3}/.test(s))                         return 'http://' + s
    if (/^[\w-]+(\.[\w.-]+)+(\/.*)?$/.test(s) && !s.includes(' ')) return 'https://' + s
    return 'https://search.brave.com/search?q=' + encodeURIComponent(s)
  }

  // ── Navigation ─────────────────────────────────────────────────
  async navigate (targetUrl) {
    this._setStatus('FETCHING  //  ' + targetUrl)
    this.urlDisplay.setContent(targetUrl)
    this.screen.render()

    try {
      const res = await fetch(targetUrl, {
        headers: {
          'User-Agent': 'BigOs/1.0 (CLI; Node.js)',
          'Accept': 'text/html,application/xhtml+xml,text/plain,*/*;q=0.9',
          'Accept-Language': 'en-US,en;q=0.9'
        },
        redirect: 'follow',
        timeout: 15000
      })

      const finalUrl  = res.url
      const ctype     = res.headers.get('content-type') || ''
      const statusStr = `${res.status} ${res.statusText}`

      // Push previous URL into history
      if (this.currentUrl && this.currentUrl !== finalUrl) {
        this.history = this.history.slice(0, this.historyIdx + 1)
        this.history.push(this.currentUrl)
        this.historyIdx = this.history.length - 1
      }
      this.currentUrl = finalUrl
      this.urlDisplay.setContent(finalUrl)

      if (ctype.includes('text/html') || ctype.includes('application/xhtml')) {
        const html = await res.text()
        this._renderHtml(html, finalUrl)
      } else if (ctype.includes('text/plain') || ctype.includes('application/json')) {
        const text = await res.text()
        this._renderText(text)
      } else {
        this._renderText(
          `[Binary / unsupported content: ${ctype}]\n\n` +
          `Use bigos-gui for full rendering of this content.\n` +
          `URL: ${finalUrl}`
        )
      }

      this._setStatus(`${statusStr}  //  ${finalUrl}`)
      this.screen.render()
      this.content.focus()

    } catch (err) {
      this._setStatus(`ERROR  //  ${err.message}`)
      this.content.setContent(
        `{red-fg}ERROR LOADING PAGE{/red-fg}\n\n` +
        `URL  : ${targetUrl}\n` +
        `Cause: ${err.message}\n\n` +
        `Press {white-fg}g{/white-fg} to try a different URL.`
      )
      this.screen.render()
    }
  }

  // ── HTML renderer ──────────────────────────────────────────────
  _renderHtml (html, baseUrl) {
    const $ = cheerio.load(html)
    $('script,style,noscript,iframe,svg,canvas,video,audio,object').remove()

    const title = $('title').text().trim()

    // Collect links before transforming
    this.links = []
    this.selectedLink = -1
    $('a[href]').each((_, el) => {
      const href = $(el).attr('href') || ''
      const text = $(el).text().trim().replace(/\s+/g, ' ')
      if (!href || !text) return
      try {
        const abs = new URL(href, baseUrl).href
        if (!/^https?:\/\//i.test(abs)) return
        const idx = this.links.length
        this.links.push({ url: abs, text: text.slice(0, 70) })
        $(el).replaceWith(`{white-fg}[${idx + 1}]${text}{/white-fg}`)
      } catch { /* skip malformed */ }
    })

    // Headings
    $('h1').each((_, el) => {
      const t = $(el).text().trim()
      $(el).replaceWith(`\n\n{bold}{bright-white-fg}══ ${t.toUpperCase()} ══{/bright-white-fg}{/bold}\n`)
    })
    $('h2,h3').each((_, el) => {
      const t = $(el).text().trim()
      $(el).replaceWith(`\n\n{white-fg}── ${t} ──{/white-fg}\n`)
    })
    $('h4,h5,h6').each((_, el) => {
      const t = $(el).text().trim()
      $(el).replaceWith(`\n  ${t}\n`)
    })

    // Paragraphs & blocks
    $('p,div,section,article,main,aside').each((_, el) => {
      const t = $(el).text().trim()
      if (t) $(el).replaceWith(`\n${t}\n`)
    })

    // Lists
    $('li').each((_, el) => {
      const t = $(el).text().trim()
      if (t) $(el).replaceWith(`\n  · ${t}`)
    })

    // Code blocks
    $('pre,code').each((_, el) => {
      const t = $(el).text()
      $(el).replaceWith(`\n{dim}${t}{/dim}\n`)
    })

    // Horizontal rules
    $('hr').each((_, el) => $(el).replaceWith('\n' + '─'.repeat(72) + '\n'))

    const body = ($('body').text() || $('*').text())
      .replace(/\r\n/g, '\n')
      .replace(/\r/g, '\n')
      .replace(/\t/g, '  ')
      .replace(/\n{4,}/g, '\n\n\n')
      .trim()

    const header = title
      ? `{bold}{bright-white-fg}${title}{/bright-white-fg}{/bold}\n` +
        '{white-fg}' + '═'.repeat(Math.min(title.length + 4, 80)) + '{/white-fg}\n\n'
      : ''

    const linkList = this.links.length
      ? '\n\n{white-fg}─── LINKS (' + this.links.length + ') ' + '─'.repeat(40) + '{/white-fg}\n' +
        this.links.map((l, i) =>
          `{white-fg}[${i + 1}]{/white-fg} ${l.text}\n    {dim}${l.url}{/dim}`
        ).join('\n')
      : ''

    this.content.setContent(header + body + linkList)
    this.content.scrollTo(0)
  }

  // ── Plain-text renderer ────────────────────────────────────────
  _renderText (text) {
    this.links = []
    this.selectedLink = -1
    this.content.setContent(text)
    this.content.scrollTo(0)
  }

  // ── Link navigation ────────────────────────────────────────────
  _nextLink () {
    if (!this.links.length) return
    this.selectedLink = (this.selectedLink + 1) % this.links.length
    const l = this.links[this.selectedLink]
    this._setStatus(`LINK [${this.selectedLink + 1}/${this.links.length}]  //  ${l.url}`)
    this.screen.render()
  }

  _followLink () {
    if (this.selectedLink >= 0 && this.selectedLink < this.links.length) {
      this.navigate(this.links[this.selectedLink].url)
    }
  }

  // ── History ────────────────────────────────────────────────────
  goBack () {
    if (this.historyIdx >= 0) {
      const prev = this.history[this.historyIdx]
      this.historyIdx--
      this.navigate(prev)
    } else {
      this._setStatus('AT BEGINNING OF HISTORY'); this.screen.render()
    }
  }

  goForward () {
    this._setStatus('FORWARD HISTORY NOT AVAILABLE'); this.screen.render()
  }

  // ── Status ─────────────────────────────────────────────────────
  _setStatus (msg) {
    this.statusMsg.setContent(msg)
  }

  // ── Help overlay ───────────────────────────────────────────────
  _showHelp () {
    const box = blessed.message({
      parent: this.screen,
      top: 'center', left: 'center',
      width: '60%', height: '70%',
      border: { type: 'line' },
      label: ' BigOs CLI — Help ',
      style: { border: { fg: 'white' }, fg: 'white', bg: 'black', label: { fg: 'brightwhite' } }
    })
    box.display(
      [
        'BigOs CLI v1.0 — KEYBOARD SHORTCUTS',
        '──────────────────',
        'g / :       Open URL prompt',
        'r           Reload current page',
        'b           Go back in history',
        'f           Go forward (when available)',
        '',
        'Tab         Cycle through page links',
        'Enter       Follow currently selected link',
        '',
        'j / ↓       Scroll down one line',
        'k / ↑       Scroll up one line',
        'Space       Scroll down half page',
        'Ctrl+D      Scroll down 12 lines',
        'Ctrl+U      Scroll up 12 lines',
        'G           Scroll to bottom',
        '',
        '?           Show this help',
        'q / Ctrl+C  Quit',
        '',
        '─────────────────────────────────────',
        'Note: JavaScript / React SPAs require',
        'the GUI mode: bigos-gui',
        '─────────────────────────────────────',
        '',
        'Press Enter or Escape to close'
      ].join('\n'),
      0,
      () => { this.screen.render(); this.content.focus() }
    )
  }

  // ── Entry point ────────────────────────────────────────────────
  run (initialUrl) {
    this.screen.render()
    if (initialUrl) {
      this.navigate(this._normalize(initialUrl))
    } else {
      this.content.setContent(
        [
          '{bold}{bright-white-fg}BigOs CLI  v1.0{/bright-white-fg}{/bold}',
          '{white-fg}' + '═'.repeat(40) + '{/white-fg}',
          '',
          'Welcome to BigOs CLI',
          'A Unix-styled terminal web browser built on Node.js.',
          '',
          'Press {bright-white-fg}g{/bright-white-fg} to enter a URL.',
          'Press {bright-white-fg}?{/bright-white-fg} for full help.',
          'Press {bright-white-fg}q{/bright-white-fg} to quit.',
          '',
          '{dim}For full JavaScript / React / SPA support use:{/dim}',
          '{dim}  bigos-gui{/dim}'
        ].join('\n')
      )
      this._setStatus('READY  //  BigOs CLI v1.0')
      this.screen.render()
    }
  }
}

module.exports = BigOsCLI
