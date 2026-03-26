import re

file_path = r"c:\Users\publi\OneDrive\Desktop\CODE\BigOs\cpp\apps\bigos-desktop\src\main.cpp"

with open(file_path, 'r', encoding='utf-8') as f:
    code = f.read()

# 1. Replace kFreeBsdChromeScript with kChromeHtml
chrome_js_pattern = re.compile(r'const wchar_t\* kFreeBsdChromeScript = LR"JS\(.*?\)JS";', re.DOTALL)

chrome_html = r'''const std::string kChromeHtml = R"HTML(
<!DOCTYPE html>
<html>
<head>
<style>
  body {
    margin: 0; padding: 0; overflow: hidden; background: #080808; display: flex; flex-direction: column; cursor: default;
    font-family: Consolas, 'Courier New', monospace; user-select: none; color: #40cc70;
  }
  #bigos-chrome {
    width: 100vw; height: 100vh;
    border-bottom: 2px solid #113311;
    display: flex; align-items: center; justify-content: space-between;
    padding: 0 16px; box-sizing: border-box;
    box-shadow: 0 4px 12px rgba(0, 0, 0, 0.4);
    background: #080808;
  }
  .bigos-group { display: flex; align-items: center; gap: 8px; }
  .bigos-btn {
    background: #0d1510; color: #40cc70; font-family: inherit; font-size: 14px;
    border: 1px solid #1e4e2d; padding: 6px 12px; cursor: pointer; border-radius: 2px;
  }
  .bigos-btn:hover { background: #1a3a25; color: #60ffa0; box-shadow: 0 0 6px #40cc7044; }
  .bigos-btn:active { background: #112a1a; transform: translateY(1px); }
  .bigos-btn:disabled { opacity: 0.5; cursor: not-allowed; border-color: #1e4e2d88; color: #40cc7088; }
  #bigos-address-wrap {
    flex: 1; margin: 0 20px; display: flex; align-items: center;
    background: #0d1510; border: 1px solid #1e4e2d; border-radius: 2px; position: relative;
  }
  #bigos-address-wrap::before {
    content: '$>'; color: #40cc70; font-weight: bold; margin-left: 12px; font-size: 14px;
  }
  #bigos-address {
    flex: 1; background: transparent; border: none; color: #60ffa0;
    font-family: inherit; font-size: 14px; padding: 8px 12px; outline: none; letter-spacing: 0.5px;
  }
  #bigos-address::placeholder { color: #1e4e2d; }
  #bigos-tabs {
    background: #0d1510; color: #40cc70; font-family: inherit; font-size: 13px;
    border: 1px solid #1e4e2d; padding: 6px; cursor: pointer; outline: none; max-width: 200px;
  }
  #bigos-tabs option { background: #080808; color: #40cc70; }
  #bigos-title {
    letter-spacing: 1px; color: #60ffa0; font-size: 12px; border: 1px solid #1e4e2d;
    padding: 3px 8px; background: #0d1510;
  }
</style>
</head>
<body>
  <div id="bigos-chrome">
    <div class="bigos-group">
      <span id="bigos-title">BigOs C++</span>
      <select id="bigos-tabs"></select>
      <button class="bigos-btn" id="bigos-newtab" title="New tab">+</button>
    </div>
    <div class="bigos-group">
      <button class="bigos-btn" id="bigos-back" title="Back">&lt;</button>
      <button class="bigos-btn" id="bigos-forward" title="Forward">&gt;</button>
      <button class="bigos-btn" id="bigos-reload" title="Reload">Reload</button>
      <button class="bigos-btn" id="bigos-favs" title="Add to favorites">&#x2606;</button>
    </div>
    <div id="bigos-address-wrap">
      <input type="text" id="bigos-address" autocomplete="off" spellcheck="false" placeholder="Enter URL or search..."/>
    </div>
  </div>

  <script>
    const addr = document.getElementById('bigos-address');
    const tabsEl = document.getElementById('bigos-tabs');
    const backBtn = document.getElementById('bigos-back');
    const fwdBtn = document.getElementById('bigos-forward');
    const reloadBtn = document.getElementById('bigos-reload');
    const newTabBtn = document.getElementById('bigos-newtab');
    const favsBtn = document.getElementById('bigos-favs');

    function sendCommand(cmd) {
      if (window.chrome && window.chrome.webview) {
        window.chrome.webview.postMessage(cmd);
      }
    }

    addr.addEventListener('keydown', (e) => {
      if (e.key === 'Enter') {
        let val = addr.value.trim();
        if (val) sendCommand('navigate:' + val);
      }
    });

    backBtn.addEventListener('click', () => sendCommand('back'));
    fwdBtn.addEventListener('click', () => sendCommand('forward'));
    reloadBtn.addEventListener('click', () => sendCommand('reload'));
    newTabBtn.addEventListener('click', () => sendCommand('new_tab'));
    favsBtn.addEventListener('click', () => sendCommand('toggle_favorite'));

    tabsEl.addEventListener('change', (e) => {
      sendCommand('switch_tab:' + e.target.value);
    });

    window.addEventListener('keydown', (e) => {
      if (e.ctrlKey && e.key.toLowerCase() === 't') { e.preventDefault(); sendCommand('new_tab'); }
      if (e.ctrlKey && e.key.toLowerCase() === 'w') { e.preventDefault(); sendCommand('close_tab'); }
      if (e.ctrlKey && e.key.toLowerCase() === 'l') { e.preventDefault(); addr.focus(); addr.select(); }
    }, true);

    window.bigosSyncState = (state) => {
      if (!state || !Array.isArray(state.tabs)) return;

      tabsEl.innerHTML = '';
      state.tabs.forEach((t) => {
        const opt = document.createElement('option');
        opt.value = t.id;
        opt.textContent = t.title || t.url || 'Tab';
        if (t.active) opt.selected = true;
        tabsEl.appendChild(opt);
      });

      backBtn.disabled = !state.canBack;
      fwdBtn.disabled = !state.canForward;
      favsBtn.innerHTML = state.isFavorite ? '&#x2B50;' : '&#x2606;';
      favsBtn.title = state.isFavorite ? 'Remove from favorites' : 'Add to favorites';

      if (typeof state.url === 'string' && document.activeElement !== addr) {
        addr.value = state.url;
      }
    };
  </script>
</body>
</html>
)HTML";'''
code = chrome_js_pattern.sub(chrome_html, code)

# 2. Update AppState
appstate_pattern = re.compile(r'struct AppState \{.*?std::wstring app_url;\n\};', re.DOTALL)
appstate_replacement = r'''struct AppState {
  struct Favorite {
    std::wstring title;
    std::wstring url;
  };

  bigos::core::TabManager tabs{L"https://duckduckgo.com"};
  std::vector<Favorite> favorites;
  ComPtr<ICoreWebView2Controller> ui_controller;
  ComPtr<ICoreWebView2> ui_webview;
  ComPtr<ICoreWebView2Controller> controller;
  ComPtr<ICoreWebView2> webview;
  bool is_app_mode = false;
  std::wstring app_url;
};'''
code = appstate_pattern.sub(appstate_replacement, code)

# 3. Update SyncUiState
syncuistate_pattern = re.compile(r'void SyncUiState\(AppState\* state\) \{.*?\}', re.DOTALL)
syncuistate_replacement = r'''void SyncUiState(AppState* state) {
  if (!state || !state->ui_webview) {
    return;
  }
  const std::wstring script = BuildStateSyncScript(state);
  if (!script.empty()) {
    state->ui_webview->ExecuteScript(script.c_str(), nullptr);
  }
}'''
code = syncuistate_pattern.sub(syncuistate_replacement, code)

# 4. Update ResizeWebView
resizewebview_pattern = re.compile(r'void ResizeWebView\(HWND hwnd\) \{.*?\}', re.DOTALL)
resizewebview_replacement = r'''constexpr int kChromeHeightPx = 74;

void ResizeWebView(HWND hwnd) {
  auto* state = GetState(hwnd);
  if (!state) {
    return;
  }

  RECT bounds{};
  GetClientRect(hwnd, &bounds);

  if (state->is_app_mode) {
    if (state->controller) {
      state->controller->put_Bounds(bounds);
    }
  } else {
    if (state->ui_controller) {
      RECT ui_bounds = bounds;
      ui_bounds.bottom = ui_bounds.top + kChromeHeightPx;
      state->ui_controller->put_Bounds(ui_bounds);
    }
    if (state->controller) {
      RECT content_bounds = bounds;
      content_bounds.top += kChromeHeightPx;
      state->controller->put_Bounds(content_bounds);
    }
  }
}'''
code = resizewebview_pattern.sub(resizewebview_replacement, code)

# 5. Update InjectChrome
injectchrome_pattern = re.compile(r'void InjectChrome\(AppState\* state\) \{.*?\}', re.DOTALL)
injectchrome_replacement = r'''void InjectChrome(AppState* state) {
  if (!state || !state->webview) {
    return;
  }
  state->webview->ExecuteScript(kPwaCompatibilityScript, nullptr);
  if (!state->is_app_mode) {
    SyncUiState(state);
  }
}'''
code = injectchrome_pattern.sub(injectchrome_replacement, code)

# 6. Update InitWebView
initwebview_pattern = re.compile(r'void InitWebView\(HWND hwnd\) \{.*?\}\n\}', re.DOTALL)
initwebview_replacement = r'''void InitWebView(HWND hwnd) {
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

            auto create_content = [hwnd, env]() -> HRESULT {
              return env->CreateCoreWebView2Controller(
                  hwnd,
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

                        NavigateToActiveTab(state);
                        return S_OK;
                      }).Get());
            };

            auto* current_state = GetState(hwnd);
            if (current_state && !current_state->is_app_mode) {
              return env->CreateCoreWebView2Controller(
                  hwnd,
                  Callback<ICoreWebView2CreateCoreWebView2ControllerCompletedHandler>(
                      [hwnd, create_content](HRESULT result, ICoreWebView2Controller* ui_controller) -> HRESULT {
                        if (FAILED(result) || !ui_controller) return E_FAIL;
                        auto* s = GetState(hwnd);
                        if (!s) return E_FAIL;

                        s->ui_controller = ui_controller;
                        s->ui_controller->get_CoreWebView2(&s->ui_webview);

                        ComPtr<ICoreWebView2Settings> settings;
                        if (SUCCEEDED(s->ui_webview->get_Settings(&settings)) && settings) {
                          settings->put_AreDevToolsEnabled(FALSE);
                          settings->put_AreDefaultContextMenusEnabled(FALSE);
                        }

                        s->ui_webview->NavigateToString(WideFromUtf8(kChromeHtml).c_str());

                        s->ui_webview->add_WebMessageReceived(
                            Callback<ICoreWebView2WebMessageReceivedEventHandler>(
                                [hwnd](ICoreWebView2*, ICoreWebView2WebMessageReceivedEventArgs* args) -> HRESULT {
                                  auto* curr = GetState(hwnd);
                                  if (curr) {
                                    LPWSTR message = nullptr;
                                    if (SUCCEEDED(args->TryGetWebMessageAsString(&message)) && message != nullptr) {
                                      HandleCommand(curr, message);
                                      CoTaskMemFree(message);
                                    }
                                  }
                                  return S_OK;
                                }).Get(), nullptr);

                        return create_content();
                      }).Get());
            } else {
              return create_content();
            }
          })
          .Get());
}
}'''
code = initwebview_pattern.sub(initwebview_replacement, code)

with open(file_path, 'w', encoding='utf-8') as f:
    f.write(code)

print("Patch applied successfully.")
