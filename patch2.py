import re

file_path = r"c:\Users\publi\OneDrive\Desktop\CODE\BigOs\cpp\apps\bigos-desktop\src\main.cpp"

with open(file_path, 'r', encoding='utf-8') as f:
    code = f.read()

# 1. Update AppState to include HWNDs
appstate_pattern = re.compile(r'struct AppState \{.*?std::wstring app_url;\n\};', re.DOTALL)
appstate_replacement = r'''struct AppState {
  struct Favorite {
    std::wstring title;
    std::wstring url;
  };

  HWND hwnd_ui = nullptr;
  HWND hwnd_content = nullptr;

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

# 2. Update ResizeWebView
resizewebview_pattern = re.compile(r'constexpr int kChromeHeightPx = 74;\n\nvoid ResizeWebView\(HWND hwnd\) \{.*?\}', re.DOTALL)
resizewebview_replacement = r'''constexpr int kChromeHeightPx = 74;

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
    if (state->hwnd_ui) {
      MoveWindow(state->hwnd_ui, 0, 0, width, kChromeHeightPx, TRUE);
    }
    if (state->ui_controller) {
      RECT ui_bounds = {0, 0, width, kChromeHeightPx};
      state->ui_controller->put_Bounds(ui_bounds);
    }
    if (state->hwnd_content) {
      MoveWindow(state->hwnd_content, 0, kChromeHeightPx, width, height - kChromeHeightPx, TRUE);
    }
    if (state->controller) {
      RECT content_bounds = {0, 0, width, height - kChromeHeightPx};
      state->controller->put_Bounds(content_bounds);
    }
  }
}'''
code = resizewebview_pattern.sub(resizewebview_replacement, code)

# 3. Update InitWebView to use the child HWNDs
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
              auto* s = GetState(hwnd);
              HWND target_content = s && s->hwnd_content ? s->hwnd_content : hwnd;
              return env->CreateCoreWebView2Controller(
                  target_content,
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
              HWND target_ui = current_state->hwnd_ui ? current_state->hwnd_ui : hwnd;
              return env->CreateCoreWebView2Controller(
                  target_ui,
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


# 4. Update WndProc to initialize child HWNDs
wndproc_pattern = re.compile(r'    case WM_CREATE:\n      InitWebView\(hwnd\);\n      return 0;', re.DOTALL)
wndproc_replacement = r'''    case WM_CREATE: {
      auto* state = GetState(hwnd);
      if (state) {
        if (!state->is_app_mode) {
          state->hwnd_ui = CreateWindowExW(0, L"BigOsContainer", nullptr, WS_CHILD | WS_VISIBLE | WS_CLIPSIBLINGS | WS_CLIPCHILDREN, 0, 0, 0, 0, hwnd, nullptr, GetModuleHandle(nullptr), nullptr);
        }
        state->hwnd_content = CreateWindowExW(0, L"BigOsContainer", nullptr, WS_CHILD | WS_VISIBLE | WS_CLIPSIBLINGS | WS_CLIPCHILDREN, 0, 0, 0, 0, hwnd, nullptr, GetModuleHandle(nullptr), nullptr);
      }
      InitWebView(hwnd);
      return 0;
    }'''
code = wndproc_pattern.sub(wndproc_replacement, code)

# 5. Register the BigOsContainer window class inside wWinMain
wwinmain_pattern = re.compile(r'  LoadFavorites\(&state\);\n\n  WNDCLASSEX wc\{\};\n  wc\.cbSize = sizeof\(WNDCLASSEX\);\n  wc\.lpfnWndProc = WndProc;', re.DOTALL)
wwinmain_replacement = r'''  LoadFavorites(&state);

  WNDCLASSEX wc_container{};
  wc_container.cbSize = sizeof(WNDCLASSEX);
  wc_container.lpfnWndProc = DefWindowProc;
  wc_container.hInstance = instance;
  wc_container.lpszClassName = L"BigOsContainer";
  wc_container.hCursor = LoadCursor(nullptr, IDC_ARROW);
  RegisterClassEx(&wc_container);

  WNDCLASSEX wc{};
  wc.cbSize = sizeof(WNDCLASSEX);
  wc.lpfnWndProc = WndProc;'''
code = wwinmain_pattern.sub(wwinmain_replacement, code)

with open(file_path, 'w', encoding='utf-8') as f:
    f.write(code)

print("Patch 2 applied successfully.")
