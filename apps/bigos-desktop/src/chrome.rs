/// Browser chrome overlay injected into every page.
///
/// Design language: FreeBSD / UNIX terminal — black background, phosphor-green
/// monospace type, box-drawing borders, no drop-shadows except a subtle glow.
///
/// Communication protocol:
///   JS → Rust  : `window.ipc.postMessage("<cmd>")` where <cmd> is one of:
///                   navigate:<url>
///                   back | forward | reload | new_tab
///                   close_tab | close_tab:<id>
///                   switch_tab:<id>
///                   win_min | win_max | win_close
///                   devtools
///
///   Rust → JS  : `window.bigosUpdateChrome({tabs, canBack, canFwd, url})`
///                called via WebView::evaluate_script after every state change.
pub const INIT_SCRIPT: &str = r#"
(function () {
  'use strict';
  if (document.getElementById('bigos-chrome')) return;

  /* ── styles ─────────────────────────────────────────────────────────── */
  var CHROME_H = 72;

  var style = document.createElement('style');
  style.id   = 'bigos-chrome-style';
  style.textContent = [
    'html { margin-top: 72px !important; }',
    '#bigos-chrome {',
    '  all: initial;',
    '  position: fixed; top: 0; left: 0; right: 0; height: 72px;',
    '  z-index: 2147483647;',
    '  background: #090909;',
    '  border-bottom: 2px solid #1a5f1a;',
    '  box-shadow: 0 0 20px rgba(0,200,64,.20);',
    '  font-family: "Courier New", Courier, monospace;',
    '  display: flex; flex-direction: column;',
    '  -webkit-app-region: no-drag;',
    '  box-sizing: border-box;',
    '}',
    /* title bar row */
    '#bigos-titlebar {',
    '  display: flex; align-items: center; height: 30px;',
    '  padding: 0 8px; gap: 5px;',
    '  background: #040404; border-bottom: 1px solid #143514;',
    '  -webkit-app-region: drag;',
    '}',
    '.bigos-logo {',
    '  color: #00ff66; font-weight: bold; font-size: 12px;',
    '  letter-spacing: 3px; padding: 1px 7px;',
    '  border: 1px solid #1a5f1a; background: #050505;',
    '  -webkit-app-region: no-drag; user-select: none;',
    '}',
    '#bigos-tabs {',
    '  display: flex; flex: 1; overflow: hidden; gap: 2px;',
    '  align-items: flex-end;',
    '  -webkit-app-region: no-drag;',
    '}',
    '.bigos-tab {',
    '  display: flex; align-items: center; gap: 4px;',
    '  padding: 2px 10px 2px 8px; font-size: 11px; color: #3a7a3a;',
    '  border: 1px solid #143514; border-bottom: none;',
    '  cursor: pointer; background: #0a0a0a;',
    '  max-width: 160px; white-space: nowrap; overflow: hidden;',
    '  text-overflow: ellipsis;',
    '  -webkit-app-region: no-drag;',
    '}',
    '.bigos-tab.active { color: #00ff66; background: #0e160e; border-color: #1a5f1a; }',
    '.bigos-tab-x {',
    '  color: #2a5a2a; margin-left: 4px; cursor: pointer;',
    '  font-size: 10px; flex-shrink: 0;',
    '}',
    '.bigos-tab-x:hover { color: #ff4040; }',
    '#bigos-newtab {',
    '  -webkit-app-region: no-drag;',
    '  background: none; border: 1px solid #1a5f1a; color: #00c040;',
    '  cursor: pointer; width: 24px; height: 20px; font-size: 15px;',
    '  padding: 0; line-height: 1; margin-left: 2px;',
    '}',
    '#bigos-newtab:hover { background: #0e1a0e; }',
    '#bigos-winctrl { display: flex; gap: 3px; margin-left: 8px; -webkit-app-region: no-drag; }',
    '.bigos-wb {',
    '  background: none; border: 1px solid #1a3a1a; color: #3a7a3a;',
    '  cursor: pointer; width: 22px; height: 20px; font-size: 11px; padding: 0;',
    '}',
    '.bigos-wb:hover { color: #00ff66; border-color: #1a5f1a; }',
    '#bigos-xbtn:hover { background: #6b0000; color: #fff; border-color: #8b0000; }',
    /* nav bar row */
    '#bigos-navbar {',
    '  display: flex; align-items: center;',
    '  height: 40px; padding: 0 8px; gap: 5px;',
    '}',
    '.bigos-nb {',
    '  background: none; border: 1px solid #143514; color: #3a7a3a;',
    '  cursor: pointer; width: 28px; height: 26px; font-size: 14px; padding: 0;',
    '}',
    '.bigos-nb:disabled { color: #1a2a1a; border-color: #0e150e; cursor: default; }',
    '.bigos-nb:not([disabled]):hover { color: #00ff66; border-color: #1a5f1a; }',
    '.bigos-prompt { color: #00aa40; font-size: 13px; user-select: none; }',
    '#bigos-addr {',
    '  flex: 1;',
    '  background: #030303; border: 1px solid #143514; color: #00c040;',
    '  font-family: "Courier New", Courier, monospace; font-size: 13px;',
    '  padding: 3px 8px; outline: none; caret-color: #00ff66;',
    '}',
    '#bigos-addr:focus { border-color: #00ff66; box-shadow: 0 0 8px rgba(0,255,102,.25); }',
    '#bigos-addr::placeholder { color: #1a4a1a; }',
    '#bigos-sec { font-size: 14px; min-width: 20px; text-align: center; cursor: default; }',
  ].join('\n');

  (document.head || document.documentElement).appendChild(style);

  /* ── markup ──────────────────────────────────────────────────────────── */
  var chrome = document.createElement('div');
  chrome.id = 'bigos-chrome';
  chrome.innerHTML =
    '<div id="bigos-titlebar">' +
      '<span class="bigos-logo">BO</span>' +
      '<div id="bigos-tabs"></div>' +
      '<button id="bigos-newtab" title="New Tab [Ctrl+T]">+</button>' +
      '<div id="bigos-winctrl">' +
        '<button class="bigos-wb" id="bigos-minbtn" title="Minimise">─</button>' +
        '<button class="bigos-wb" id="bigos-maxbtn" title="Maximise">□</button>' +
        '<button class="bigos-wb" id="bigos-xbtn"   title="Close">✕</button>' +
      '</div>' +
    '</div>' +
    '<div id="bigos-navbar">' +
      '<button class="bigos-nb" id="bigos-back"   title="Back [Alt+←]"    disabled>◂</button>' +
      '<button class="bigos-nb" id="bigos-fwd"    title="Forward [Alt+→]" disabled>▸</button>' +
      '<button class="bigos-nb" id="bigos-reload" title="Reload [F5]">↺</button>' +
      '<span class="bigos-prompt">$&gt;</span>' +
      '<input id="bigos-addr" type="text" placeholder="addr / search …"' +
             ' spellcheck="false" autocomplete="off" autocorrect="off">' +
      '<span id="bigos-sec" title=""></span>' +
    '</div>';

  var root = document.documentElement;
  root.insertBefore(chrome, root.firstChild);

  /* ── ipc helper ──────────────────────────────────────────────────────── */
  function ipc(msg) {
    if (window.ipc) { window.ipc.postMessage(msg); }
  }

  /* ── wiring ──────────────────────────────────────────────────────────── */
  var addr   = document.getElementById('bigos-addr');
  var back   = document.getElementById('bigos-back');
  var fwd    = document.getElementById('bigos-fwd');
  var secEl  = document.getElementById('bigos-sec');

  addr.addEventListener('keydown', function (e) {
    if (e.key === 'Enter')  { ipc('navigate:' + this.value.trim()); }
    if (e.key === 'Escape') { this.blur(); }
  });

  document.getElementById('bigos-back').onclick   = function () { ipc('back'); };
  document.getElementById('bigos-fwd').onclick    = function () { ipc('forward'); };
  document.getElementById('bigos-reload').onclick = function () { ipc('reload'); };
  document.getElementById('bigos-newtab').onclick = function () { ipc('new_tab'); };
  document.getElementById('bigos-minbtn').onclick = function () { ipc('win_min'); };
  document.getElementById('bigos-maxbtn').onclick = function () { ipc('win_max'); };
  document.getElementById('bigos-xbtn').onclick   = function () { ipc('win_close'); };

  /* keyboard shortcuts */
  document.addEventListener('keydown', function (e) {
    if (e.ctrlKey && e.key === 't') { e.preventDefault(); ipc('new_tab'); }
    if (e.ctrlKey && e.key === 'w') { e.preventDefault(); ipc('close_tab'); }
    if (e.ctrlKey && e.key === 'l') {
      e.preventDefault(); addr.focus(); addr.select();
    }
    if (e.altKey && e.key === 'ArrowLeft')  { e.preventDefault(); ipc('back'); }
    if (e.altKey && e.key === 'ArrowRight') { e.preventDefault(); ipc('forward'); }
    if (e.key === 'F5')  { ipc('reload'); }
    if (e.key === 'F12') { ipc('devtools'); }
  });

  /* ── live URL / security-icon sync ──────────────────────────────────── */
  function syncAddr() {
    var href = location.href;
    if (document.activeElement !== addr) { addr.value = href; }
    if (href.startsWith('https:')) {
      secEl.textContent = '🔒'; secEl.title = 'Secure connection (TLS)';
    } else if (href.startsWith('http:')) {
      secEl.textContent = '⚠️'; secEl.title = 'Not secure';
    } else {
      secEl.textContent = '·'; secEl.title = '';
    }
  }
  syncAddr();

  /* intercept SPA navigation */
  window.addEventListener('popstate', syncAddr);
  (function patchHistory(type) {
    var orig = history[type];
    history[type] = function () { orig.apply(this, arguments); syncAddr(); };
  })('pushState');
  (function patchHistory(type) {
    var orig = history[type];
    history[type] = function () { orig.apply(this, arguments); syncAddr(); };
  })('replaceState');

  /* ── state update hook called from Rust ─────────────────────────────── */
  window.bigosUpdateChrome = function (state) {
    /* tabs */
    var tabsEl = document.getElementById('bigos-tabs');
    if (tabsEl && Array.isArray(state.tabs)) {
      tabsEl.innerHTML = '';
      state.tabs.forEach(function (t) {
        var tab = document.createElement('div');
        tab.className = 'bigos-tab' + (t.active ? ' active' : '');

        var label = document.createElement('span');
        label.textContent = (t.title || 'New Tab').slice(0, 24);

        var xBtn = document.createElement('span');
        xBtn.className   = 'bigos-tab-x';
        xBtn.textContent = '×';
        xBtn.onclick = function (e) {
          e.stopPropagation();
          ipc('close_tab:' + t.id);
        };

        tab.onclick = function () { ipc('switch_tab:' + t.id); };
        tab.appendChild(label);
        tab.appendChild(xBtn);
        tabsEl.appendChild(tab);
      });
    }

    /* nav buttons */
    if (back) { back.disabled = !state.canBack; }
    if (fwd)  { fwd.disabled  = !state.canFwd; }

    /* address bar */
    if (state.url && document.activeElement !== addr) {
      addr.value = state.url;
      syncAddr();
    }
  };
})();
"#;

/// Minimal new-tab splash page (about:blank replacement).
pub const NEW_TAB_HTML: &str = r#"<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <title>New Tab — BigOs</title>
  <style>
    *, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
    body {
      background: #090909; color: #00c040;
      font-family: 'Courier New', Courier, monospace;
      display: flex; flex-direction: column;
      align-items: center; justify-content: center;
      min-height: 100vh; padding-top: 80px;
    }
    pre.logo {
      color: #00ff66; font-size: 13px; line-height: 1.4;
      text-shadow: 0 0 8px rgba(0,255,102,.4);
    }
    .sub {
      color: #3a7a3a; font-size: 12px; margin-top: 12px;
      letter-spacing: 2px;
    }
    .shortcuts {
      margin-top: 40px; display: grid;
      grid-template-columns: repeat(2, 1fr); gap: 8px 32px;
      font-size: 11px; color: #2a5a2a;
    }
    .shortcuts span { color: #00aa40; }
  </style>
</head>
<body>
<pre class="logo">
  ╔══════════════════════════════════════════════╗
  ║   ____  _        ___          _             ║
  ║  | __ )(_) __ _ / _ \___     | |            ║
  ║  |  _ \| |/ _` | | | / __|   | |            ║
  ║  | |_) | | (_| | |_| \__ \_  |_|            ║
  ║  |____/|_|\__, |\___/|___(_) (_)            ║
  ║           |___/                             ║
  ╠══════════════════════════════════════════════╣
  ║  Rust Web Explorer  //  Unix Edition  v0.1  ║
  ╚══════════════════════════════════════════════╝
</pre>
<p class="sub">React-Native-Web compatible  ·  Chromium / WebKit engine</p>
<div class="shortcuts">
  <div><span>Ctrl+T</span>  new tab</div>
  <div><span>Ctrl+W</span>  close tab</div>
  <div><span>Ctrl+L</span>  focus address</div>
  <div><span>Alt+←/→</span> back / forward</div>
  <div><span>F5</span>      reload</div>
  <div><span>F12</span>     dev tools</div>
</div>
</body>
</html>
"#;
