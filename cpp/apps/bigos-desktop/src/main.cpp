#include <windows.h>
#include <wrl.h>

#include <cwchar>
#include <vector>
#include <string>

#include <WebView2.h>

#include "bigos/core/tab_manager.h"

using Microsoft::WRL::Callback;
using Microsoft::WRL::ComPtr;

namespace {

constexpr wchar_t kClassName[] = L"BigOsDesktopWindow";

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
    html, body { margin-top: 74px !important; }
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
      width: 30px;
      height: 26px;
      cursor: pointer;
    }
    .bigos-btn:disabled {
      opacity: 0.45;
      cursor: default;
    }
    #bigos-prompt { color: #00ff7a; }
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
      <button class="bigos-btn" id="bigos-back" title="Back">◀</button>
      <button class="bigos-btn" id="bigos-forward" title="Forward">▶</button>
      <button class="bigos-btn" id="bigos-reload" title="Reload">↻</button>
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
    if (typeof state.url === 'string' && document.activeElement !== addr) {
      addr.value = state.url;
    }
  };
})();
)JS";

struct AppState {
  bigos::core::TabManager tabs{L"https://duckduckgo.com"};
  ComPtr<ICoreWebView2Controller> controller;
  ComPtr<ICoreWebView2> webview;
};

AppState* GetState(HWND hwnd) {
  return reinterpret_cast<AppState*>(GetWindowLongPtr(hwnd, GWLP_USERDATA));
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

  script += L"],canBack:";
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
  state->webview->ExecuteScript(kFreeBsdChromeScript, nullptr);
  SyncUiState(state);
}

void NavigateToActiveTab(AppState* state) {
  if (!state || !state->webview) {
    return;
  }

  const auto& url = state->tabs.active_tab().url;
  state->webview->Navigate(url.c_str());
}

void HandleCommand(AppState* state, const std::wstring& command) {
  if (!state || !state->webview) {
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

  CreateCoreWebView2EnvironmentWithOptions(
      nullptr,
      nullptr,
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
                              [hwnd](ICoreWebView2*, ICoreWebView2NavigationCompletedEventArgs*) -> HRESULT {
                                auto* current_state = GetState(hwnd);
                                if (current_state && current_state->webview) {
                                  LPWSTR source = nullptr;
                                  if (SUCCEEDED(current_state->webview->get_Source(&source)) && source != nullptr) {
                                    current_state->tabs.navigate_active(source);
                                    CoTaskMemFree(source);
                                  }
                                }
                                InjectChrome(current_state);
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
