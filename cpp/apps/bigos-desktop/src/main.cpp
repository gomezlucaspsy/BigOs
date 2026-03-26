#include <windows.h>
#include <wrl.h>
#include <shellapi.h>
#include <shlobj.h>
#include <commctrl.h>
#include <cstdlib>

#pragma comment(lib, "comctl32.lib")

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
constexpr LONG kChromeHeightPx = 38;

constexpr int IDC_BACK_BTN = 101;
constexpr int IDC_FWD_BTN = 102;
constexpr int IDC_RELOAD_BTN = 103;
constexpr int IDC_NEWTAB_BTN = 104;
constexpr int IDC_CLOSETAB_BTN = 105;
constexpr int IDC_FAV_BTN = 106;
constexpr int IDC_TABS_COMBO = 107;
constexpr int IDC_ADDRESS_EDIT = 108;

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

  HWND hwnd_content = nullptr;

  // Native chrome controls
  HWND hwnd_back = nullptr;
  HWND hwnd_forward = nullptr;
  HWND hwnd_reload = nullptr;
  HWND hwnd_newtab = nullptr;
  HWND hwnd_closetab = nullptr;
  HWND hwnd_fav = nullptr;
  HWND hwnd_tabs = nullptr;
  HWND hwnd_address = nullptr;
  HFONT chrome_font = nullptr;
  HBRUSH chrome_bg_brush = nullptr;
  bool syncing_combo = false; // guard against re-entrant CBN_SELCHANGE

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

LRESULT CALLBACK WndProc(HWND hwnd, UINT message, WPARAM wparam, LPARAM lparam);

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

void SyncUiState(AppState* state) {
  if (!state) {
    return;
  }

  // Update tab combo box (guard against re-entrant CBN_SELCHANGE)
  if (state->hwnd_tabs) {
    state->syncing_combo = true;
    SendMessageW(state->hwnd_tabs, CB_RESETCONTENT, 0, 0);
    const auto& tabs = state->tabs.tabs();
    const int active_id = state->tabs.active_tab().id;
    for (std::size_t i = 0; i < tabs.size(); ++i) {
      const auto& tab = tabs[i];
      std::wstring label = tab.title.empty() ? tab.url : tab.title;
      if (label.size() > 40) {
        label = label.substr(0, 37) + L"...";
      }
      int idx = static_cast<int>(SendMessageW(state->hwnd_tabs, CB_ADDSTRING, 0,
                                              reinterpret_cast<LPARAM>(label.c_str())));
      SendMessageW(state->hwnd_tabs, CB_SETITEMDATA, static_cast<WPARAM>(idx),
                   static_cast<LPARAM>(tab.id));
      if (tab.id == active_id) {
        SendMessageW(state->hwnd_tabs, CB_SETCURSEL, static_cast<WPARAM>(idx), 0);
      }
    }
    state->syncing_combo = false;
  }

  // Update URL bar (only if not focused, to not interfere with typing)
  if (state->hwnd_address && GetFocus() != state->hwnd_address) {
    SetWindowTextW(state->hwnd_address, state->tabs.active_tab().url.c_str());
  }

  // Update back/forward enabled state
  if (state->hwnd_back) {
    EnableWindow(state->hwnd_back, state->tabs.can_back() ? TRUE : FALSE);
  }
  if (state->hwnd_forward) {
    EnableWindow(state->hwnd_forward, state->tabs.can_forward() ? TRUE : FALSE);
  }

  // Update favorite button text
  if (state->hwnd_fav) {
    bool is_fav = false;
    const std::wstring& active_url = state->tabs.active_tab().url;
    for (const auto& f : state->favorites) {
      if (f.url == active_url) {
        is_fav = true;
        break;
      }
    }
    SetWindowTextW(state->hwnd_fav, is_fav ? L"\u2605" : L"\u2606");
  }
}

void ResizeWebView(HWND hwnd) {
  auto* state = GetState(hwnd);
  if (!state) {
    return;
  }

  RECT bounds{};
  GetClientRect(hwnd, &bounds);
  LONG width = bounds.right - bounds.left;
  LONG height = bounds.bottom - bounds.top;

  if (state->is_app_mode) {
    if (state->hwnd_content) {
      MoveWindow(state->hwnd_content, 0, 0, width, height, TRUE);
    }
    if (state->controller) {
      RECT content_bounds = {0, 0, width, height};
      state->controller->put_Bounds(content_bounds);
    }
  } else {
    // Layout native chrome controls in a single row
    // kChromeHeightPx is the strip height; controls are vertically centred
    constexpr int bh = 26; // button/edit height
    constexpr int bw = 28; // square nav buttons
    constexpr int gap = 3;
    int y = (kChromeHeightPx - bh) / 2;
    int x = gap;

    if (state->hwnd_back) {
      MoveWindow(state->hwnd_back,    x, y, bw, bh, TRUE); x += bw + gap;
    }
    if (state->hwnd_forward) {
      MoveWindow(state->hwnd_forward, x, y, bw, bh, TRUE); x += bw + gap;
    }
    if (state->hwnd_reload) {
      MoveWindow(state->hwnd_reload,  x, y, 46, bh, TRUE); x += 46 + gap;
    }
    // Tabs combo: pass large cy so dropdown can fit multiple items
    if (state->hwnd_tabs) {
      MoveWindow(state->hwnd_tabs,    x, y, 170, 200, TRUE); x += 170 + gap;
    }
    if (state->hwnd_newtab) {
      MoveWindow(state->hwnd_newtab,  x, y, bw, bh, TRUE); x += bw + gap;
    }
    if (state->hwnd_closetab) {
      MoveWindow(state->hwnd_closetab, x, y, bw, bh, TRUE); x += bw + gap;
    }
    if (state->hwnd_fav) {
      MoveWindow(state->hwnd_fav,     x, y, bw, bh, TRUE); x += bw + gap;
    }
    if (state->hwnd_address) {
      int addr_width = static_cast<int>(width) - x - gap;
      if (addr_width < 60) addr_width = 60;
      MoveWindow(state->hwnd_address, x, y, addr_width, bh, TRUE);
    }

    // Content area below chrome
    LONG content_height = height - kChromeHeightPx;
    if (content_height < 0) content_height = 0;

    if (state->hwnd_content) {
      MoveWindow(state->hwnd_content, 0, kChromeHeightPx, width, content_height, TRUE);
    }
    if (state->controller) {
      RECT content_bounds = {0, 0, width, content_height};
      state->controller->put_Bounds(content_bounds);
    }
  }
}

void InjectChrome(AppState* state) {
  if (!state || !state->webview) {
    return;
  }
  state->webview->ExecuteScript(kPwaCompatibilityScript, nullptr);
  if (!state->is_app_mode) {
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

#include <shobjidl.h>

void CreateDesktopShortcut(const std::wstring& title, const std::wstring& target, const std::wstring& args) {
    ComPtr<IShellLinkW> link;
    if (SUCCEEDED(CoCreateInstance(CLSID_ShellLink, nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&link)))) {
        link->SetPath(target.c_str());
        link->SetArguments(args.c_str());

    PWSTR desktop = nullptr;
    if (SUCCEEDED(SHGetKnownFolderPath(FOLDERID_Desktop, 0, nullptr, &desktop)) && desktop) {
      std::wstring lnk_path = std::wstring(desktop) + L"\\" + title + L".lnk";
            ComPtr<IPersistFile> persist;
            if (SUCCEEDED(link.As(&persist))) {
                persist->Save(lnk_path.c_str(), TRUE);
            }
      CoTaskMemFree(desktop);
        }
    }
}

LRESULT CALLBACK AddressEditProc(HWND hwnd, UINT msg, WPARAM wp, LPARAM lp,
                                 UINT_PTR /*id*/, DWORD_PTR ref) {
  if (msg == WM_KEYDOWN && wp == VK_RETURN) {
    auto* state = reinterpret_cast<AppState*>(ref);
    if (state && state->webview) {
      wchar_t buf[4096]{};
      GetWindowTextW(hwnd, buf, static_cast<int>(std::size(buf)));
      std::wstring val(buf);
      if (!val.empty()) {
        state->tabs.navigate_active(val);
        NavigateToActiveTab(state);
        SyncUiState(state);
        SetFocus(state->hwnd_content);
      }
    }
    return 0;
  }
  return DefSubclassProc(hwnd, msg, wp, lp);
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

      std::wstring args = L"--app=" + url;
      CreateDesktopShortcut(title, exe_path, args);
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

            auto* s = GetState(hwnd);
            HWND target = s && s->hwnd_content ? s->hwnd_content : hwnd;
            return env->CreateCoreWebView2Controller(
                target,
                Callback<ICoreWebView2CreateCoreWebView2ControllerCompletedHandler>(
                    [hwnd](HRESULT result, ICoreWebView2Controller* controller) -> HRESULT {
                      if (FAILED(result) || !controller) return E_FAIL;
                      auto* state = GetState(hwnd);
                      if (!state) return E_FAIL;

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
                                if (!args) return S_OK;
                                COREWEBVIEW2_PERMISSION_KIND kind;
                                args->get_PermissionKind(&kind);
                                args->put_State(COREWEBVIEW2_PERMISSION_STATE_ALLOW);
                                return S_OK;
                              }).Get(), nullptr);

                      ResizeWebView(hwnd);

                      state->webview->add_WebMessageReceived(
                          Callback<ICoreWebView2WebMessageReceivedEventHandler>(
                              [hwnd](ICoreWebView2*, ICoreWebView2WebMessageReceivedEventArgs* args) -> HRESULT {
                                auto* current_state = GetState(hwnd);
                                if (current_state) {
                                  LPWSTR message = nullptr;
                                  if (SUCCEEDED(args->TryGetWebMessageAsString(&message)) && message != nullptr) {
                                    HandleCommand(current_state, message);
                                    CoTaskMemFree(message);
                                  }
                                }
                                return S_OK;
                              }).Get(), nullptr);

                      state->webview->add_NavigationCompleted(
                          Callback<ICoreWebView2NavigationCompletedEventHandler>(
                              [hwnd](ICoreWebView2*, ICoreWebView2NavigationCompletedEventArgs* args) -> HRESULT {
                                auto* current_state = GetState(hwnd);
                                if (current_state && current_state->webview) {
                                  BOOL success = FALSE;
                                  if (args) args->get_IsSuccess(&success);
                                  if (success) UpdateActiveTabFromWebView(current_state);
                                }
                                InjectChrome(current_state);
                                return S_OK;
                              }).Get(), nullptr);

                      state->webview->add_DocumentTitleChanged(
                          Callback<ICoreWebView2DocumentTitleChangedEventHandler>(
                              [hwnd](ICoreWebView2*, IUnknown*) -> HRESULT {
                                auto* current_state = GetState(hwnd);
                                if (current_state && current_state->webview) {
                                  UpdateActiveTabFromWebView(current_state);
                                  SyncUiState(current_state);
                                }
                                return S_OK;
                              }).Get(), nullptr);

                      // Catch keyboard shortcuts when WebView has focus
                      state->controller->add_AcceleratorKeyPressed(
                          Callback<ICoreWebView2AcceleratorKeyPressedEventHandler>(
                              [hwnd](ICoreWebView2Controller*, ICoreWebView2AcceleratorKeyPressedEventArgs* args) -> HRESULT {
                                COREWEBVIEW2_KEY_EVENT_KIND kind;
                                args->get_KeyEventKind(&kind);
                                if (kind != COREWEBVIEW2_KEY_EVENT_KIND_KEY_DOWN) return S_OK;

                                UINT key = 0;
                                args->get_VirtualKey(&key);
                                bool ctrl = (GetKeyState(VK_CONTROL) & 0x8000) != 0;
                                if (!ctrl) return S_OK;

                                auto* st = GetState(hwnd);
                                if (!st) return S_OK;

                                if (key == 'L' && st->hwnd_address) {
                                  args->put_Handled(TRUE);
                                  SetFocus(st->hwnd_address);
                                  SendMessageW(st->hwnd_address, EM_SETSEL, 0, -1);
                                } else if (key == 'T') {
                                  args->put_Handled(TRUE);
                                  st->tabs.new_tab();
                                  SyncUiState(st);
                                  NavigateToActiveTab(st);
                                } else if (key == 'W') {
                                  args->put_Handled(TRUE);
                                  st->tabs.close_tab(st->tabs.active_tab().id);
                                  SyncUiState(st);
                                  NavigateToActiveTab(st);
                                }
                                return S_OK;
                              }).Get(), nullptr);

                      NavigateToActiveTab(state);
                      return S_OK;
                    }).Get());
          })
          .Get());
}

LRESULT CALLBACK WndProc(HWND hwnd, UINT message, WPARAM wparam, LPARAM lparam) {
  switch (message) {
    case WM_NCCREATE: {
      auto* create = reinterpret_cast<CREATESTRUCT*>(lparam);
      if (create) {
        auto* state = reinterpret_cast<AppState*>(create->lpCreateParams);
        SetWindowLongPtr(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(state));
      }
      return DefWindowProc(hwnd, message, wparam, lparam);
    }
    case WM_CREATE: {
      auto* state = GetState(hwnd);
      if (!state) {
        return -1;
      }

      HINSTANCE inst = reinterpret_cast<HINSTANCE>(GetWindowLongPtr(hwnd, GWLP_HINSTANCE));

      // Create a font for the chrome controls
      state->chrome_font = CreateFontW(
          -13, 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE,
          DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
          CLEARTYPE_QUALITY, DEFAULT_PITCH | FF_MODERN, L"Consolas");

      state->chrome_bg_brush = CreateSolidBrush(RGB(8, 8, 8));

      if (!state->is_app_mode) {
        // Use BS_OWNERDRAW so we can paint green-on-black (WM_CTLCOLORBTN doesn't
        // work for standard push buttons — owner-draw is the reliable path).
        auto mkbtn = [&](const wchar_t* label, int id) -> HWND {
          HWND h = CreateWindowExW(0, L"BUTTON", label,
              WS_CHILD | WS_VISIBLE | BS_OWNERDRAW,
              0, 0, 0, 0, hwnd,
              reinterpret_cast<HMENU>(static_cast<INT_PTR>(id)), inst, nullptr);
          return h;
        };
        state->hwnd_back      = mkbtn(L"\x25C0", IDC_BACK_BTN);
        state->hwnd_forward   = mkbtn(L"\x25B6", IDC_FWD_BTN);
        state->hwnd_reload    = mkbtn(L"[R]",    IDC_RELOAD_BTN);
        state->hwnd_newtab    = mkbtn(L"[+]",    IDC_NEWTAB_BTN);
        state->hwnd_closetab  = mkbtn(L"[-]",    IDC_CLOSETAB_BTN);
        state->hwnd_fav       = mkbtn(L"\u2606", IDC_FAV_BTN);

        // Combobox for tabs — themed edit bg via WM_CTLCOLORLISTBOX/WM_CTLCOLOREDIT
        state->hwnd_tabs = CreateWindowExW(0, L"COMBOBOX", nullptr,
            WS_CHILD | WS_VISIBLE | CBS_DROPDOWNLIST | WS_VSCROLL,
            0, 0, 0, 200,
            hwnd, reinterpret_cast<HMENU>(static_cast<INT_PTR>(IDC_TABS_COMBO)), inst, nullptr);

        // Address edit
        state->hwnd_address = CreateWindowExW(0, L"EDIT", L"",
            WS_CHILD | WS_VISIBLE | ES_AUTOHSCROLL,
            0, 0, 0, 0,
            hwnd, reinterpret_cast<HMENU>(static_cast<INT_PTR>(IDC_ADDRESS_EDIT)), inst, nullptr);

        // Apply font to all chrome controls
        HWND controls[] = {
          state->hwnd_back, state->hwnd_forward, state->hwnd_reload,
          state->hwnd_tabs, state->hwnd_newtab, state->hwnd_closetab,
          state->hwnd_fav, state->hwnd_address
        };
        for (HWND ctrl : controls) {
          if (ctrl) SendMessageW(ctrl, WM_SETFONT, reinterpret_cast<WPARAM>(state->chrome_font), TRUE);
        }

        // Subclass the address bar to catch Enter key
        SetWindowSubclass(state->hwnd_address, AddressEditProc, 1,
                          reinterpret_cast<DWORD_PTR>(state));
      }

      // Content container for the WebView
      state->hwnd_content = CreateWindowExW(
          0, L"BigOsContainer", nullptr,
          WS_CHILD | WS_VISIBLE | WS_CLIPCHILDREN,
          0, 0, 0, 0, hwnd, nullptr, inst, nullptr);

      InitWebView(hwnd);
      return 0;
    }
    case WM_SIZE:
      ResizeWebView(hwnd);
      return 0;
    case WM_COMMAND: {
      auto* state = GetState(hwnd);
      if (!state) break;
      int id = LOWORD(wparam);
      int code = HIWORD(wparam);

      if (id == IDC_BACK_BTN && code == BN_CLICKED) {
        if (state->tabs.back_active()) {
          NavigateToActiveTab(state);
          SyncUiState(state);
        }
      } else if (id == IDC_FWD_BTN && code == BN_CLICKED) {
        if (state->tabs.forward_active()) {
          NavigateToActiveTab(state);
          SyncUiState(state);
        }
      } else if (id == IDC_RELOAD_BTN && code == BN_CLICKED) {
        if (state->webview) state->webview->Reload();
      } else if (id == IDC_NEWTAB_BTN && code == BN_CLICKED) {
        state->tabs.new_tab();
        SyncUiState(state);        // update combo first so user sees new tab
        NavigateToActiveTab(state); // then navigate
      } else if (id == IDC_CLOSETAB_BTN && code == BN_CLICKED) {
        state->tabs.close_tab(state->tabs.active_tab().id);
        SyncUiState(state);
        NavigateToActiveTab(state);
      } else if (id == IDC_FAV_BTN && code == BN_CLICKED) {
        HandleCommand(state, L"toggle_favorite");
        // Force repaint of fav button
        if (state->hwnd_fav) InvalidateRect(state->hwnd_fav, nullptr, TRUE);
      } else if (id == IDC_TABS_COMBO && code == CBN_SELCHANGE) {
        // Guard: SyncUiState repopulates the combo which triggers CBN_SELCHANGE
        if (!state->syncing_combo) {
          int sel = static_cast<int>(SendMessageW(state->hwnd_tabs, CB_GETCURSEL, 0, 0));
          if (sel != CB_ERR) {
            int tab_id = static_cast<int>(
                SendMessageW(state->hwnd_tabs, CB_GETITEMDATA, static_cast<WPARAM>(sel), 0));
            if (state->tabs.switch_to(tab_id)) {
              SyncUiState(state);
              NavigateToActiveTab(state);
            }
          }
        }
      }
      return 0;
    }
    case WM_DRAWITEM: {
      // Owner-draw for all chrome buttons — green-on-black terminal aesthetic
      auto* dis = reinterpret_cast<LPDRAWITEMSTRUCT>(lparam);
      if (!dis) break;

      bool pressed   = (dis->itemState & ODS_SELECTED) != 0;
      bool disabled  = (dis->itemState & ODS_DISABLED) != 0;
      bool focused   = (dis->itemState & ODS_FOCUS) != 0;

      COLORREF bg_normal   = RGB(8,  8,  8);
      COLORREF bg_pressed  = RGB(28, 56, 36);
      COLORREF border_col  = RGB(30, 80, 46);
      COLORREF text_col    = disabled ? RGB(40, 80, 55) : RGB(64, 204, 112);

      HBRUSH bg_brush = CreateSolidBrush(pressed ? bg_pressed : bg_normal);
      FillRect(dis->hDC, &dis->rcItem, bg_brush);
      DeleteObject(bg_brush);

      // Single-pixel border
      HPEN pen = CreatePen(PS_SOLID, 1, border_col);
      HPEN old_pen = reinterpret_cast<HPEN>(SelectObject(dis->hDC, pen));
      HBRUSH null_brush = reinterpret_cast<HBRUSH>(GetStockObject(NULL_BRUSH));
      HBRUSH old_brush = reinterpret_cast<HBRUSH>(SelectObject(dis->hDC, null_brush));
      Rectangle(dis->hDC, dis->rcItem.left, dis->rcItem.top,
                dis->rcItem.right, dis->rcItem.bottom);
      SelectObject(dis->hDC, old_pen);
      SelectObject(dis->hDC, old_brush);
      DeleteObject(pen);

      // Focus dotted rect (subtle)
      if (focused) {
        RECT fr = dis->rcItem;
        InflateRect(&fr, -2, -2);
        DrawFocusRect(dis->hDC, &fr);
      }

      // Button text
      wchar_t label[64]{};
      GetWindowTextW(dis->hwndItem, label, 63);

      auto* state = GetState(hwnd);
      HFONT fnt = state ? state->chrome_font : nullptr;
      HFONT old_fnt = fnt ? reinterpret_cast<HFONT>(SelectObject(dis->hDC, fnt)) : nullptr;
      SetTextColor(dis->hDC, text_col);
      SetBkMode(dis->hDC, TRANSPARENT);
      DrawTextW(dis->hDC, label, -1, &dis->rcItem,
                DT_CENTER | DT_VCENTER | DT_SINGLELINE | DT_NOPREFIX);
      if (old_fnt) SelectObject(dis->hDC, old_fnt);
      return TRUE;
    }
    case WM_SETFOCUS: {
      auto* state = GetState(hwnd);
      if (state && state->controller) {
        state->controller->MoveFocus(COREWEBVIEW2_MOVE_FOCUS_REASON_PROGRAMMATIC);
      }
      return 0;
    }
    case WM_CTLCOLOREDIT:
    case WM_CTLCOLORLISTBOX: {
      // Color the address edit and combobox list green-on-black
      auto* state = GetState(hwnd);
      HDC hdc = reinterpret_cast<HDC>(wparam);
      SetTextColor(hdc, RGB(96, 255, 160));
      SetBkColor(hdc, RGB(12, 20, 14));
      if (state && state->chrome_bg_brush) {
        return reinterpret_cast<LRESULT>(state->chrome_bg_brush);
      }
      break;
    }
    case WM_CTLCOLORSTATIC: {
      auto* state = GetState(hwnd);
      HDC hdc = reinterpret_cast<HDC>(wparam);
      SetTextColor(hdc, RGB(64, 204, 112));
      SetBkColor(hdc, RGB(8, 8, 8));
      if (state && state->chrome_bg_brush) {
        return reinterpret_cast<LRESULT>(state->chrome_bg_brush);
      }
      break;
    }
    case WM_ERASEBKGND: {
      auto* state = GetState(hwnd);
      if (state && !state->is_app_mode) {
        HDC hdc = reinterpret_cast<HDC>(wparam);
        RECT rc{};
        GetClientRect(hwnd, &rc);
        // Chrome strip
        RECT chrome_rc = rc;
        chrome_rc.bottom = kChromeHeightPx;
        HBRUSH bg = CreateSolidBrush(RGB(8, 8, 8));
        FillRect(hdc, &chrome_rc, bg);
        // 1px separator line at bottom of chrome
        RECT sep_rc = {rc.left, kChromeHeightPx - 1, rc.right, kChromeHeightPx};
        HBRUSH sep = CreateSolidBrush(RGB(30, 80, 46));
        FillRect(hdc, &sep_rc, sep);
        DeleteObject(bg);
        DeleteObject(sep);
        return 1;
      }
      break;
    }
    case WM_DESTROY: {
      auto* state = GetState(hwnd);
      if (state) {
        SaveFavorites(state);
        if (state->hwnd_address) {
          RemoveWindowSubclass(state->hwnd_address, AddressEditProc, 1);
        }
        state->webview.Reset();
        state->controller.Reset();
        if (state->chrome_font) {
          DeleteObject(state->chrome_font);
          state->chrome_font = nullptr;
        }
        if (state->chrome_bg_brush) {
          DeleteObject(state->chrome_bg_brush);
          state->chrome_bg_brush = nullptr;
        }
      }
      PostQuitMessage(0);
      return 0;
    }
    default:
      return DefWindowProc(hwnd, message, wparam, lparam);
  }
  return DefWindowProc(hwnd, message, wparam, lparam);
}

}  // namespace

int WINAPI wWinMain(HINSTANCE instance, HINSTANCE, PWSTR, int) {
  HRESULT hr = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
  if (FAILED(hr)) {
    return 1;
  }

  INITCOMMONCONTROLSEX icc{};
  icc.dwSize = sizeof(icc);
  icc.dwICC = ICC_STANDARD_CLASSES | ICC_WIN95_CLASSES;
  InitCommonControlsEx(&icc);

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

  WNDCLASSEX wc_container{};
  wc_container.cbSize = sizeof(WNDCLASSEX);
  wc_container.lpfnWndProc = DefWindowProc;
  wc_container.hInstance = instance;
  wc_container.lpszClassName = L"BigOsContainer";
  wc_container.hCursor = LoadCursor(nullptr, IDC_ARROW);
  RegisterClassEx(&wc_container);

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
