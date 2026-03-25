#include <windows.h>
#include <wrl.h>
#include <shellapi.h>
#include <cstdlib>

#include <algorithm>
#include <cwchar>
#include <string_view>
#include <vector>
#include <string>

#include <WebView2.h>

#include "bigos/core/tab_manager.h"

using Microsoft::WRL::Callback;
using Microsoft::WRL::ComPtr;

namespace {

constexpr wchar_t kClassName[] = L"BigOsDesktopWindow";
constexpr LONG kChromeHeightPx = 74;

const wchar_t* kFreeBsdChromeScript = LR"JS(
(() => {
  if (window.__bigosChromeInjected) return;
  window.__bigosChromeInjected = true;

  const send = (msg) => {
    if (window.chrome && window.chrome.webview) {
      window.chrome.webview.postMessage(msg);
    }
  };

  const style = document.createElement('style');
  style.textContent = `
    #bigos-chrome {
      position: fixed;
      top: 0;
      left: 0;
      right: 0;
      height: 74px;
      z-index: 2147483647;
      background: #080808;
      color: #00d05a;
      font-family: 'Consolas', 'Courier New', monospace;
      border-bottom: 2px solid #12612a;
      box-shadow: 0 0 20px rgba(0, 220, 90, 0.16);
      display: flex;
      flex-direction: column;
      gap: 4px;
      padding: 6px 8px;
      box-sizing: border-box;
    }
    #bigos-top {
      width: 100%;
      display: flex;
      align-items: center;
      gap: 6px;
      min-height: 28px;
    }
    #bigos-tabs {
      display: flex;
      align-items: center;
      gap: 4px;
      overflow-x: auto;
      flex: 1;
      scrollbar-width: thin;
    }
    .bigos-tab {
      border: 1px solid #1e4e2d;
      background: #0d1510;
      color: #79e39c;
      font-family: 'Consolas', 'Courier New', monospace;
      font-size: 11px;
      padding: 3px 8px;
      max-width: 240px;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
      cursor: pointer;
    }
    .bigos-tab.active {
      border-color: #2daa55;
      color: #d2ffe2;
      background: #102417;
    }
    #bigos-new-tab {
      border: 1px solid #1e4e2d;
      background: #0f1f15;
      color: #7ded9f;
      font-family: 'Consolas', 'Courier New', monospace;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 26px;
      height: 24px;
      cursor: pointer;
    }
    #bigos-new-tab:hover { background: #173120; }
    #bigos-bottom {
      width: 100%;
      display: flex;
      align-items: center;
      gap: 8px;
      min-height: 30px;
    }
    .bigos-btn {
      border: 1px solid #1e4e2d;
      background: #0b1510;
      color: #7ded9f;
      font-family: 'Consolas', 'Courier New', monospace;
      font-size: 12px;
      line-height: 1;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      min-width: 34px;
      height: 26px;
      padding: 0 8px;
      cursor: pointer;
    }
    .bigos-btn.reload {
      min-width: 70px;
    }
    .bigos-btn:disabled {
      opacity: 0.45;
      cursor: default;
    }
    #bigos-prompt { color: #00ff7a; }
    #bigos-favorite {
      min-width: 34px;
      font-size: 15px;
      line-height: 1;
    }
    #bigos-favorites {
      border: 1px solid #1e4e2d;
      background: #020202;
      color: #00d05a;
      font-family: 'Consolas', 'Courier New', monospace;
      height: 26px;
      padding: 0 4px;
      max-width: 190px;
    }
    #bigos-address {
      flex: 1;
      background: #020202;
      color: #00d05a;
      border: 1px solid #12612a;
      padding: 5px 10px;
      outline: none;
    }
    #bigos-address:focus { border-color: #00ff7a; }
    #bigos-title {
      letter-spacing: 1px;
      color: #60ffa0;
      font-size: 12px;
      border: 1px solid #1e4e2d;
      padding: 3px 8px;
      background: #0d1510;
    }
    html, body {
      margin: 0 !important;
      padding: 0 !important;
      overflow: hidden !important;
    }
    body {
      margin-top: 74px !important;
      height: calc(100vh - 74px) !important;
      box-sizing: border-box !important;
    }
    /* PersonaForge specific 100vh overrides */
    .app-container, .p3app, .p3sel, .p3cr, .p3chat {
      height: calc(100vh - 74px) !important;
    }
  `;

  const bar = document.createElement('div');
  bar.id = 'bigos-chrome';
  bar.innerHTML = `
    <div id="bigos-top">
      <span id="bigos-title">BigOs C++</span>
      <div id="bigos-tabs"></div>
      <button id="bigos-new-tab" title="New tab">+</button>
    </div>
    <div id="bigos-bottom">
      <button class="bigos-btn" id="bigos-back" title="Back">&lt;</button>
      <button class="bigos-btn" id="bigos-forward" title="Forward">&gt;</button>
      <button class="bigos-btn reload" id="bigos-reload" title="Reload">Reload</button>
      <button class="bigos-btn" id="bigos-favorite" title="Add to favorites">&#9734;</button>
      <select id="bigos-favorites" title="Favorites">
        <option value="">favorites</option>
      </select>
      <span id="bigos-prompt">$&gt;</span>
      <input id="bigos-address" placeholder="url or search" />
    </div>
  `;

  document.head.appendChild(style);
  document.documentElement.appendChild(bar);

  const addr = document.getElementById('bigos-address');
  const tabsEl = document.getElementById('bigos-tabs');
  const backBtn = document.getElementById('bigos-back');
  const forwardBtn = document.getElementById('bigos-forward');
  const reloadBtn = document.getElementById('bigos-reload');
  const favoriteBtn = document.getElementById('bigos-favorite');
  const favoritesEl = document.getElementById('bigos-favorites');
  const newTabBtn = document.getElementById('bigos-new-tab');

  const navigateFromAddress = () => {
    const value = (addr.value || '').trim();
    if (value.length > 0) {
      send('navigate:' + value);
    }
  };

  addr.addEventListener('keydown', (e) => {
    if (e.key === 'Enter') {
      e.preventDefault();
      navigateFromAddress();
    }
  });

  backBtn.addEventListener('click', () => send('back'));
  forwardBtn.addEventListener('click', () => send('forward'));
  reloadBtn.addEventListener('click', () => send('reload'));
  favoriteBtn.addEventListener('click', () => send('toggle_favorite'));
  favoritesEl.addEventListener('change', () => {
    if (favoritesEl.value !== '') {
      send('open_favorite:' + favoritesEl.value);
      favoritesEl.value = '';
    }
  });
  newTabBtn.addEventListener('click', () => send('new_tab'));

  document.addEventListener('keydown', (e) => {
    if (e.ctrlKey && e.key.toLowerCase() === 't') {
      e.preventDefault();
      send('new_tab');
    }
    if (e.ctrlKey && e.key.toLowerCase() === 'w') {
      e.preventDefault();
      send('close_tab');
    }
    if (e.ctrlKey && e.key.toLowerCase() === 'l') {
      e.preventDefault();
      addr.focus();
      addr.select();
    }
  }, true);

  window.bigosSyncState = (state) => {
    if (!state || !Array.isArray(state.tabs)) {
      return;
    }

    tabsEl.innerHTML = '';
    for (const t of state.tabs) {
      const tab = document.createElement('button');
      tab.className = 'bigos-tab' + (t.active ? ' active' : '');
      tab.textContent = t.title || t.url || 'Tab';
      tab.title = t.url || '';
      tab.addEventListener('click', () => send('switch_tab:' + t.id));
      tab.addEventListener('auxclick', (e) => {
        if (e.button === 1) {
          e.preventDefault();
          send('close_tab:' + t.id);
        }
      });
      tabsEl.appendChild(tab);
    }

    backBtn.disabled = !state.canBack;
    forwardBtn.disabled = !state.canForward;
    favoriteBtn.textContent = state.isFavorite ? '\u2B50' : '\u2606';
    favoriteBtn.title = state.isFavorite ? 'Remove from favorites' : 'Add to favorites';

    favoritesEl.innerHTML = '';
    const placeholder = document.createElement('option');
    placeholder.value = '';
    placeholder.textContent = 'favorites';
    favoritesEl.appendChild(placeholder);

    if (Array.isArray(state.favorites)) {
      for (const f of state.favorites) {
        const opt = document.createElement('option');
        opt.value = String(f.id);
        opt.textContent = f.title || f.url || 'Favorite';
        opt.title = f.url || '';
        favoritesEl.appendChild(opt);
      }
    }

    if (typeof state.url === 'string' && document.activeElement !== addr) {
      addr.value = state.url;
    }
  };
})();
)JS";

const wchar_t* kPwaCompatibilityScript = LR"JS(
(() => {
  if (window.__bigosPwaCompatInjected) return;
  window.__bigosPwaCompatInjected = true;

  // Make desktop shell appear like an installable standalone app where possible.
  if (!window.matchMedia) {
    window.matchMedia = function() {
      return {
        matches: false,
        media: '',
        onchange: null,
        addListener: function() {},
        removeListener: function() {},
        addEventListener: function() {},
        removeEventListener: function() {},
        dispatchEvent: function() { return false; }
      };
    };
  }

  const originalMatchMedia = window.matchMedia.bind(window);
  window.matchMedia = function(query) {
    try {
      const normalized = String(query || '').replace(/\s+/g, '').toLowerCase();
      if (normalized === '(display-mode:standalone)' || normalized === '(display-mode:window-controls-overlay)') {
        return {
          matches: true,
          media: query,
          onchange: null,
          addListener: function() {},
          removeListener: function() {},
          addEventListener: function() {},
          removeEventListener: function() {},
          dispatchEvent: function() { return true; }
        };
      }
    } catch (_) {}
    return originalMatchMedia(query);
  };

  // Provide an inert beforeinstallprompt so apps that gate UI on this event keep working.
  if (!('onbeforeinstallprompt' in window)) {
    Object.defineProperty(window, 'onbeforeinstallprompt', {
      configurable: true,
      writable: true,
      value: null
    });
  }

  if (!window.__bigosInstallPromptDispatched) {
    window.__bigosInstallPromptDispatched = true;
    setTimeout(() => {
      try {
        const event = new Event('beforeinstallprompt');
        event.prompt = async () => {
          if (window.chrome && window.chrome.webview) {
            window.chrome.webview.postMessage('install_app:' + (document.title || 'App') + '|' + window.location.href);
          }
        };
        event.userChoice = Promise.resolve({ outcome: 'accepted', platform: 'bigos' });
        event.preventDefault = Event.prototype.preventDefault.bind(event);
        window.dispatchEvent(event);
      } catch (_) {}
    }, 1200);
  }

  if (!navigator.standalone) {
    Object.defineProperty(navigator, 'standalone', {
      configurable: true,
      get: () => true
    });
  }
})();
)JS";

struct AppState {
  struct Favorite {
    std::wstring title;
    std::wstring url;
  };

  bigos::core::TabManager tabs{L"https://duckduckgo.com"};
  std::vector<Favorite> favorites;
  ComPtr<ICoreWebView2Controller> controller;
  ComPtr<ICoreWebView2> webview;
  bool is_app_mode = false;
  std::wstring app_url;
};

AppState* GetState(HWND hwnd) {
  return reinterpret_cast<AppState*>(GetWindowLongPtr(hwnd, GWLP_USERDATA));
}

std::string Utf8FromWide(const std::wstring& value) {
  if (value.empty()) {
    return {};
  }

  const int size = WideCharToMultiByte(
      CP_UTF8,
      0,
      value.c_str(),
      static_cast<int>(value.size()),
      nullptr,
      0,
      nullptr,
      nullptr);

  if (size <= 0) {
    return {};
  }

  std::string out(static_cast<std::size_t>(size), '\0');
  WideCharToMultiByte(
      CP_UTF8,
      0,
      value.c_str(),
      static_cast<int>(value.size()),
      out.data(),
      size,
      nullptr,
      nullptr);
  return out;
}

std::wstring WideFromUtf8(const std::string_view value) {
  if (value.empty()) {
    return {};
  }

  const int size = MultiByteToWideChar(
      CP_UTF8,
      MB_ERR_INVALID_CHARS,
      value.data(),
      static_cast<int>(value.size()),
      nullptr,
      0);

  if (size <= 0) {
    return {};
  }

  std::wstring out(static_cast<std::size_t>(size), L'\0');
  MultiByteToWideChar(
      CP_UTF8,
      MB_ERR_INVALID_CHARS,
      value.data(),
      static_cast<int>(value.size()),
      out.data(),
      size);
  return out;
}

std::wstring EscapePersistField(const std::wstring& value) {
  std::wstring escaped;
  escaped.reserve(value.size() + 16);
  for (wchar_t ch : value) {
    switch (ch) {
      case L'\\':
        escaped += L"\\\\";
        break;
      case L'\t':
        escaped += L"\\t";
        break;
      case L'\r':
        escaped += L"\\r";
        break;
      case L'\n':
        escaped += L"\\n";
        break;
      default:
        escaped.push_back(ch);
        break;
    }
  }
  return escaped;
}

std::wstring UnescapePersistField(const std::wstring& value) {
  std::wstring out;
  out.reserve(value.size());

  for (std::size_t i = 0; i < value.size(); ++i) {
    const wchar_t ch = value[i];
    if (ch == L'\\' && i + 1 < value.size()) {
      const wchar_t code = value[i + 1];
      switch (code) {
        case L'\\':
          out.push_back(L'\\');
          ++i;
          continue;
        case L't':
          out.push_back(L'\t');
          ++i;
          continue;
        case L'r':
          out.push_back(L'\r');
          ++i;
          continue;
        case L'n':
          out.push_back(L'\n');
          ++i;
          continue;
        default:
          break;
      }
    }
    out.push_back(ch);
  }

  return out;
}

std::wstring FavoritesDirectoryPath() {
  wchar_t local_app_data[MAX_PATH]{};
  const DWORD length = GetEnvironmentVariableW(
      L"LOCALAPPDATA",
      local_app_data,
      static_cast<DWORD>(std::size(local_app_data)));
  if (length == 0 || length >= std::size(local_app_data)) {
    return L".";
  }

  return std::wstring(local_app_data, length) + L"\\BigOs";
}

std::wstring WebViewUserDataDirectoryPath() {
  return FavoritesDirectoryPath() + L"\\WebView2";
}

std::wstring FavoritesFilePath() {
  return FavoritesDirectoryPath() + L"\\favorites.txt";
}

bool EnsureFavoritesDirectoryExists() {
  const std::wstring dir = FavoritesDirectoryPath();
  if (CreateDirectoryW(dir.c_str(), nullptr)) {
    return true;
  }
  return GetLastError() == ERROR_ALREADY_EXISTS;
}

bool EnsureDirectoryExists(const std::wstring& path) {
  if (CreateDirectoryW(path.c_str(), nullptr)) {
    return true;
  }
  return GetLastError() == ERROR_ALREADY_EXISTS;
}

bool SaveFavorites(const AppState* state) {
  if (!state || !EnsureFavoritesDirectoryExists()) {
    return false;
  }

  std::wstring content;
  for (const auto& favorite : state->favorites) {
    content += EscapePersistField(favorite.title);
    content += L"\t";
    content += EscapePersistField(favorite.url);
    content += L"\n";
  }

  const std::string utf8 = Utf8FromWide(content);
  const std::wstring file_path = FavoritesFilePath();

  HANDLE file = CreateFileW(
      file_path.c_str(),
      GENERIC_WRITE,
      FILE_SHARE_READ,
      nullptr,
      CREATE_ALWAYS,
      FILE_ATTRIBUTE_NORMAL,
      nullptr);

  if (file == INVALID_HANDLE_VALUE) {
    return false;
  }

  DWORD written = 0;
  const BOOL ok = WriteFile(
      file,
      utf8.data(),
      static_cast<DWORD>(utf8.size()),
      &written,
      nullptr);

  CloseHandle(file);
  return ok == TRUE && written == utf8.size();
}

void LoadFavorites(AppState* state) {
  if (!state) {
    return;
  }

  const std::wstring file_path = FavoritesFilePath();
  HANDLE file = CreateFileW(
      file_path.c_str(),
      GENERIC_READ,
      FILE_SHARE_READ,
      nullptr,
      OPEN_EXISTING,
      FILE_ATTRIBUTE_NORMAL,
      nullptr);

  if (file == INVALID_HANDLE_VALUE) {
    return;
  }

  LARGE_INTEGER size{};
  if (!GetFileSizeEx(file, &size) || size.QuadPart <= 0 || size.QuadPart > 1024 * 1024) {
    CloseHandle(file);
    return;
  }

  std::string bytes(static_cast<std::size_t>(size.QuadPart), '\0');
  DWORD read = 0;
  const BOOL ok = ReadFile(
      file,
      bytes.data(),
      static_cast<DWORD>(bytes.size()),
      &read,
      nullptr);
  CloseHandle(file);

  if (ok != TRUE) {
    return;
  }
  bytes.resize(read);

  const std::wstring decoded = WideFromUtf8(bytes);
  if (decoded.empty()) {
    return;
  }

  state->favorites.clear();

  std::size_t start = 0;
  while (start < decoded.size()) {
    std::size_t end = decoded.find(L'\n', start);
    if (end == std::wstring::npos) {
      end = decoded.size();
    }

    std::wstring line = decoded.substr(start, end - start);
    if (!line.empty() && line.back() == L'\r') {
      line.pop_back();
    }

    const std::size_t sep = line.find(L'\t');
    if (sep != std::wstring::npos) {
      AppState::Favorite favorite{};
      favorite.title = UnescapePersistField(line.substr(0, sep));
      favorite.url = UnescapePersistField(line.substr(sep + 1));
      if (!favorite.url.empty()) {
        if (favorite.title.empty()) {
          favorite.title = favorite.url;
        }
        state->favorites.push_back(std::move(favorite));
      }
    }

    start = end + 1;
  }
}

std::wstring EscapeJsString(const std::wstring& value) {
  std::wstring escaped;
  escaped.reserve(value.size() + 16);
  for (wchar_t ch : value) {
    switch (ch) {
      case L'\\':
        escaped += L"\\\\";
        break;
      case L'\"':
        escaped += L"\\\"";
        break;
      case L'\r':
        escaped += L"\\r";
        break;
      case L'\n':
        escaped += L"\\n";
        break;
      default:
        escaped.push_back(ch);
        break;
    }
  }
  return escaped;
}

std::wstring BuildStateSyncScript(const AppState* state) {
  if (!state) {
    return L"";
  }

  std::wstring script = L"window.bigosSyncState({tabs:[";
  const auto& tabs = state->tabs.tabs();
  const int active_id = state->tabs.active_tab().id;

  for (std::size_t i = 0; i < tabs.size(); ++i) {
    const auto& tab = tabs[i];
    if (i > 0) {
      script += L",";
    }
    script += L"{id:" + std::to_wstring(tab.id);
    script += L",title:\"" + EscapeJsString(tab.title) + L"\"";
    script += L",url:\"" + EscapeJsString(tab.url) + L"\"";
    script += L",active:";
    script += (tab.id == active_id ? L"true" : L"false");
    script += L"}";
  }

  script += L"],favorites:[";
  for (std::size_t i = 0; i < state->favorites.size(); ++i) {
    const auto& favorite = state->favorites[i];
    if (i > 0) {
      script += L",";
    }
    script += L"{id:" + std::to_wstring(i);
    script += L",title:\"" + EscapeJsString(favorite.title) + L"\"";
    script += L",url:\"" + EscapeJsString(favorite.url) + L"\"";
    script += L"}";
  }

  script += L"],isFavorite:";
  bool is_favorite = false;
  const std::wstring& active_url = state->tabs.active_tab().url;
  for (const auto& favorite : state->favorites) {
    if (favorite.url == active_url) {
      is_favorite = true;
      break;
    }
  }
  script += (is_favorite ? L"true" : L"false");
  script += L",canBack:";
  script += (state->tabs.can_back() ? L"true" : L"false");
  script += L",canForward:";
  script += (state->tabs.can_forward() ? L"true" : L"false");
  script += L",url:\"" + EscapeJsString(state->tabs.active_tab().url) + L"\"";
  script += L"});";
  return script;
}

void SyncUiState(AppState* state) {
  if (!state || !state->webview) {
    return;
  }
  const std::wstring script = BuildStateSyncScript(state);
  if (!script.empty()) {
    state->webview->ExecuteScript(script.c_str(), nullptr);
  }
}

void ResizeWebView(HWND hwnd) {
  auto* state = GetState(hwnd);
  if (!state || !state->controller) {
    return;
  }

  RECT bounds{};
  GetClientRect(hwnd, &bounds);
  state->controller->put_Bounds(bounds);
}

void InjectChrome(AppState* state) {
  if (!state || !state->webview) {
    return;
  }
  state->webview->ExecuteScript(kPwaCompatibilityScript, nullptr);
  if (!state->is_app_mode) {
    state->webview->ExecuteScript(kFreeBsdChromeScript, nullptr);
    SyncUiState(state);
  }
}

void NavigateToActiveTab(AppState* state) {
  if (!state || !state->webview) {
    return;
  }

  const auto& url = state->tabs.active_tab().url;
  state->webview->Navigate(url.c_str());
}

void UpdateActiveTabFromWebView(AppState* state) {
  if (!state || !state->webview) {
    return;
  }

  LPWSTR source = nullptr;
  if (SUCCEEDED(state->webview->get_Source(&source)) && source != nullptr) {
    state->tabs.navigate_active(source);
    CoTaskMemFree(source);
  }

  LPWSTR title = nullptr;
  if (SUCCEEDED(state->webview->get_DocumentTitle(&title)) && title != nullptr) {
    auto& active = state->tabs.active_tab();
    if (wcslen(title) > 0) {
      active.title = title;
    } else if (active.title.empty()) {
      active.title = active.url;
    }
    CoTaskMemFree(title);
  }
}

void HandleCommand(AppState* state, const std::wstring& command) {
  if (!state || !state->webview) {
    return;
  }

  if (command.rfind(L"install_app:", 0) == 0) {
    const std::wstring payload = command.substr(12);
    const std::size_t pipe = payload.find(L'|');
    if (pipe != std::wstring::npos) {
      std::wstring title = payload.substr(0, pipe);
      for (wchar_t& c : title) {
          if (c == L':' || c == L'\\' || c == L'/' || c == L'*' || c == L'?' || c == L'\"' || c == L'<' || c == L'>' || c == L'|') {
              c = L'_';
          }
      }
      std::wstring url = payload.substr(pipe + 1);
      
      wchar_t exe_path[MAX_PATH];
      GetModuleFileNameW(nullptr, exe_path, MAX_PATH);

      std::wstring ps_cmd = L"powershell -NoProfile -WindowStyle Hidden -Command \"$s=(New-Object -COM WScript.Shell).CreateShortcut([Environment]::GetFolderPath('Desktop')+'\\";
      ps_cmd += title + L".lnk');$s.TargetPath='";
      ps_cmd += exe_path;
      ps_cmd += L"';$s.Arguments='--app=";
      ps_cmd += url;
      ps_cmd += L"';$s.Save()\"";

      _wsystem(ps_cmd.c_str());
    }
    return;
  }

  if (command.rfind(L"navigate:", 0) == 0) {
    const std::wstring raw = command.substr(9);
    state->tabs.navigate_active(raw);
    NavigateToActiveTab(state);
    return;
  }

  if (command == L"back") {
    if (state->tabs.back_active()) {
      NavigateToActiveTab(state);
    }
    return;
  }

  if (command == L"forward") {
    if (state->tabs.forward_active()) {
      NavigateToActiveTab(state);
    }
    return;
  }

  if (command == L"reload") {
    state->webview->Reload();
    return;
  }

  if (command == L"new_tab") {
    state->tabs.new_tab();
    NavigateToActiveTab(state);
    return;
  }

  if (command == L"toggle_favorite") {
    const auto& tab = state->tabs.active_tab();
    auto it = std::find_if(
        state->favorites.begin(),
        state->favorites.end(),
        [&tab](const AppState::Favorite& favorite) { return favorite.url == tab.url; });

    if (it != state->favorites.end()) {
      state->favorites.erase(it);
    } else {
      AppState::Favorite favorite{};
      favorite.url = tab.url;
      favorite.title = tab.title.empty() ? tab.url : tab.title;
      state->favorites.push_back(std::move(favorite));
    }

    SaveFavorites(state);
    SyncUiState(state);
    return;
  }

  if (command.rfind(L"open_favorite:", 0) == 0) {
    const std::wstring value = command.substr(14);
    if (value.empty()) {
      return;
    }

    const int id = _wtoi(value.c_str());
    if (id < 0 || static_cast<std::size_t>(id) >= state->favorites.size()) {
      return;
    }

    state->tabs.navigate_active(state->favorites[static_cast<std::size_t>(id)].url);
    NavigateToActiveTab(state);
    return;
  }

  if (command == L"close_tab") {
    state->tabs.close_tab(state->tabs.active_tab().id);
    NavigateToActiveTab(state);
    return;
  }

  if (command.rfind(L"close_tab:", 0) == 0) {
    const std::wstring value = command.substr(10);
    const int id = _wtoi(value.c_str());
    state->tabs.close_tab(id);
    NavigateToActiveTab(state);
    return;
  }

  if (command.rfind(L"switch_tab:", 0) == 0) {
    const std::wstring value = command.substr(11);
    const int id = _wtoi(value.c_str());
    if (state->tabs.switch_to(id)) {
      NavigateToActiveTab(state);
    }
    return;
  }

  if (command == L"focus_address") {
    SyncUiState(state);
    return;
  }
}

void InitWebView(HWND hwnd) {
  auto* state = GetState(hwnd);
  if (!state) {
    return;
  }

    const std::wstring user_data_dir = WebViewUserDataDirectoryPath();
    EnsureFavoritesDirectoryExists();
    EnsureDirectoryExists(user_data_dir);

    CreateCoreWebView2EnvironmentWithOptions(
      nullptr,
      user_data_dir.c_str(),
      nullptr,
      Callback<ICoreWebView2CreateCoreWebView2EnvironmentCompletedHandler>(
          [hwnd](HRESULT result, ICoreWebView2Environment* env) -> HRESULT {
            if (FAILED(result) || !env) {
              return E_FAIL;
            }

            return env->CreateCoreWebView2Controller(
                hwnd,
                Callback<ICoreWebView2CreateCoreWebView2ControllerCompletedHandler>(
                    [hwnd](HRESULT result, ICoreWebView2Controller* controller) -> HRESULT {
                      if (FAILED(result) || !controller) {
                        return E_FAIL;
                      }

                      auto* state = GetState(hwnd);
                      if (!state) {
                        return E_FAIL;
                      }

                      state->controller = controller;
                      state->controller->get_CoreWebView2(&state->webview);

                      ComPtr<ICoreWebView2Settings> settings;
                      if (SUCCEEDED(state->webview->get_Settings(&settings)) && settings) {
                        settings->put_IsScriptEnabled(TRUE);
                        settings->put_AreDefaultScriptDialogsEnabled(TRUE);
                        settings->put_IsWebMessageEnabled(TRUE);
                        settings->put_AreDevToolsEnabled(TRUE);
                        settings->put_IsStatusBarEnabled(TRUE);
                        settings->put_AreDefaultContextMenusEnabled(TRUE);
                      }

                      state->webview->add_PermissionRequested(
                          Callback<ICoreWebView2PermissionRequestedEventHandler>(
                              [](ICoreWebView2*, ICoreWebView2PermissionRequestedEventArgs* args) -> HRESULT {
                                if (!args) {
                                  return S_OK;
                                }

                                COREWEBVIEW2_PERMISSION_KIND kind = COREWEBVIEW2_PERMISSION_KIND_UNKNOWN_PERMISSION;
                                args->get_PermissionKind(&kind);

                                switch (kind) {
                                  case COREWEBVIEW2_PERMISSION_KIND_NOTIFICATIONS:
                                  case COREWEBVIEW2_PERMISSION_KIND_GEOLOCATION:
                                  case COREWEBVIEW2_PERMISSION_KIND_CLIPBOARD_READ:
                                  case COREWEBVIEW2_PERMISSION_KIND_MICROPHONE:
                                  case COREWEBVIEW2_PERMISSION_KIND_CAMERA:
                                    args->put_State(COREWEBVIEW2_PERMISSION_STATE_ALLOW);
                                    return S_OK;
                                  default:
                                    return S_OK;
                                }
                              })
                              .Get(),
                          nullptr);

                      ResizeWebView(hwnd);

                      state->webview->add_WebMessageReceived(
                          Callback<ICoreWebView2WebMessageReceivedEventHandler>(
                              [hwnd](ICoreWebView2*, ICoreWebView2WebMessageReceivedEventArgs* args) -> HRESULT {
                                auto* current_state = GetState(hwnd);
                                if (!current_state) {
                                  return S_OK;
                                }

                                LPWSTR message = nullptr;
                                if (SUCCEEDED(args->TryGetWebMessageAsString(&message)) && message != nullptr) {
                                  HandleCommand(current_state, message);
                                  CoTaskMemFree(message);
                                }

                                return S_OK;
                              })
                              .Get(),
                          nullptr);

                      state->webview->add_NavigationCompleted(
                          Callback<ICoreWebView2NavigationCompletedEventHandler>(
                              [hwnd](ICoreWebView2*, ICoreWebView2NavigationCompletedEventArgs* args) -> HRESULT {
                                auto* current_state = GetState(hwnd);
                                if (current_state && current_state->webview) {
                                  BOOL success = FALSE;
                                  if (args) {
                                    args->get_IsSuccess(&success);
                                  }
                                  if (success) {
                                    UpdateActiveTabFromWebView(current_state);
                                  }
                                }
                                InjectChrome(current_state);
                                return S_OK;
                              })
                              .Get(),
                          nullptr);

                      state->webview->add_DocumentTitleChanged(
                          Callback<ICoreWebView2DocumentTitleChangedEventHandler>(
                              [hwnd](ICoreWebView2*, IUnknown*) -> HRESULT {
                                auto* current_state = GetState(hwnd);
                                if (!current_state || !current_state->webview) {
                                  return S_OK;
                                }

                                UpdateActiveTabFromWebView(current_state);
                                SyncUiState(current_state);
                                return S_OK;
                              })
                              .Get(),
                          nullptr);

                      NavigateToActiveTab(state);
                      return S_OK;
                    })
                    .Get());
          })
          .Get());
}

LRESULT CALLBACK WndProc(HWND hwnd, UINT message, WPARAM wparam, LPARAM lparam) {
  switch (message) {
    case WM_NCCREATE: {
      auto* create = reinterpret_cast<LPCREATESTRUCT>(lparam);
      auto* state = reinterpret_cast<AppState*>(create->lpCreateParams);
      SetWindowLongPtr(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(state));
      return TRUE;
    }

    case WM_CREATE:
      InitWebView(hwnd);
      return 0;

    case WM_SIZE:
      ResizeWebView(hwnd);
      return 0;

    case WM_DESTROY:
      PostQuitMessage(0);
      return 0;

    default:
      return DefWindowProc(hwnd, message, wparam, lparam);
  }
}

}  // namespace

int WINAPI wWinMain(HINSTANCE instance, HINSTANCE, PWSTR, int) {
  HRESULT hr = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
  if (FAILED(hr)) {
    return 1;
  }

  AppState state{};

  int argc;
  LPWSTR* argv = CommandLineToArgvW(GetCommandLineW(), &argc);
  if (argv && argc > 1) {
      std::wstring arg = argv[1];
      if (arg.rfind(L"--app=", 0) == 0) {
          state.is_app_mode = true;
          state.app_url = arg.substr(6);
          state.tabs = bigos::core::TabManager{state.app_url};
      }
  }
  if (argv) {
      LocalFree(argv);
  }

  LoadFavorites(&state);

  WNDCLASSEX wc{};
  wc.cbSize = sizeof(WNDCLASSEX);
  wc.lpfnWndProc = WndProc;
  wc.hInstance = instance;
  wc.lpszClassName = kClassName;
  wc.hCursor = LoadCursor(nullptr, IDC_ARROW);

  RegisterClassEx(&wc);

  HWND hwnd = CreateWindowEx(
      0,
      kClassName,
      L"BigOs",
      WS_OVERLAPPEDWINDOW,
      CW_USEDEFAULT,
      CW_USEDEFAULT,
      1320,
      860,
      nullptr,
      nullptr,
      instance,
      &state);

  if (!hwnd) {
    CoUninitialize();
    return 1;
  }

  ShowWindow(hwnd, SW_SHOWNORMAL);
  UpdateWindow(hwnd);

  MSG msg{};
  while (GetMessage(&msg, nullptr, 0, 0)) {
    TranslateMessage(&msg);
    DispatchMessage(&msg);
  }

  CoUninitialize();
  return static_cast<int>(msg.wParam);
}
