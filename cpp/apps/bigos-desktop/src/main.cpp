#include <windows.h>
#include <wrl.h>

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

  const style = document.createElement('style');
  style.textContent = `
    html, body { margin-top: 64px !important; }
    #bigos-chrome {
      position: fixed;
      top: 0;
      left: 0;
      right: 0;
      height: 64px;
      z-index: 2147483647;
      background: #080808;
      color: #00d05a;
      font-family: 'Consolas', 'Courier New', monospace;
      border-bottom: 2px solid #12612a;
      box-shadow: 0 0 18px rgba(0, 220, 90, 0.14);
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 10px;
      box-sizing: border-box;
    }
    #bigos-prompt { color: #00ff7a; }
    #bigos-address {
      flex: 1;
      background: #020202;
      color: #00d05a;
      border: 1px solid #12612a;
      padding: 7px 10px;
      outline: none;
    }
    #bigos-address:focus { border-color: #00ff7a; }
    #bigos-title { letter-spacing: 1px; color: #60ffa0; }
  `;

  const bar = document.createElement('div');
  bar.id = 'bigos-chrome';
  bar.innerHTML = `
    <span id="bigos-title">BigOs C++</span>
    <span id="bigos-prompt">$&gt;</span>
    <input id="bigos-address" placeholder="url or search" />
  `;

  document.head.appendChild(style);
  document.documentElement.appendChild(bar);
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
}

void NavigateToActiveTab(AppState* state) {
  if (!state || !state->webview) {
    return;
  }

  const auto& url = state->tabs.active_tab().url;
  state->webview->Navigate(url.c_str());
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

                      state->webview->add_NavigationCompleted(
                          Callback<ICoreWebView2NavigationCompletedEventHandler>(
                              [hwnd](ICoreWebView2*, ICoreWebView2NavigationCompletedEventArgs*) -> HRESULT {
                                auto* current_state = GetState(hwnd);
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
