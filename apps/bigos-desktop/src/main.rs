//! BigOs desktop — native browser shell built on wry + tao.
//!
//! Architecture:
//!   - One `tao` OS window per session.
//!   - One `wry` WebView covers the full client area.
//!   - The browser chrome (tabs, nav, address bar) is a fixed-position
//!     DOM overlay injected via an initialisation script (`chrome::INIT_SCRIPT`).
//!   - IPC: JS → Rust via `window.ipc.postMessage(...)`, parsed in `parse_ipc`.
//!   - State changes flow back as `window.bigosUpdateChrome({...})` calls.

#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

mod chrome;

use std::sync::{Arc, Mutex};

use bigos_core::TabManager;
use tao::{
    dpi::LogicalSize,
    event::{Event, WindowEvent},
    event_loop::{ControlFlow, EventLoop, EventLoopProxy},
    window::WindowBuilder,
};
use wry::{http::Request, WebViewBuilder};

// ── App-level events ──────────────────────────────────────────────────────────

#[derive(Debug, Clone)]
enum AppEvent {
    Navigate(String),
    Back,
    Forward,
    Reload,
    NewTab,
    CloseTab(Option<usize>), // None = active tab
    SwitchTab(usize),
    WinMin,
    WinMax,
    WinClose,
    DevTools,
}

// ── IPC message parser ────────────────────────────────────────────────────────

fn parse_ipc(body: &str) -> Option<AppEvent> {
    let s = body.trim();
    if let Some(url) = s.strip_prefix("navigate:") {
        return Some(AppEvent::Navigate(url.to_string()));
    }
    if let Some(id) = s.strip_prefix("close_tab:") {
        return id.parse::<usize>().ok().map(|n| AppEvent::CloseTab(Some(n)));
    }
    if let Some(id) = s.strip_prefix("switch_tab:") {
        return id.parse::<usize>().ok().map(AppEvent::SwitchTab);
    }
    match s {
        "back"      => Some(AppEvent::Back),
        "forward"   => Some(AppEvent::Forward),
        "reload"    => Some(AppEvent::Reload),
        "new_tab"   => Some(AppEvent::NewTab),
        "close_tab" => Some(AppEvent::CloseTab(None)),
        "win_min"   => Some(AppEvent::WinMin),
        "win_max"   => Some(AppEvent::WinMax),
        "win_close" => Some(AppEvent::WinClose),
        "devtools"  => Some(AppEvent::DevTools),
        _           => None,
    }
}

// ── Build JS call that refreshes the chrome overlay ──────────────────────────

fn json_escape(s: &str) -> String {
    s.replace('\\', "\\\\")
     .replace('"', "\\\"")
     .replace('\n', "\\n")
     .replace('\r', "\\r")
}

fn chrome_update_js(tabs: &TabManager) -> String {
    let active_id = tabs.active_tab().id;
    let tab_arr: Vec<String> = tabs
        .tabs()
        .iter()
        .map(|t| {
            format!(
                r#"{{"id":{},"title":"{}","active":{}}}"#,
                t.id,
                json_escape(&t.title),
                t.id == active_id,
            )
        })
        .collect();

    format!(
        r#"window.bigosUpdateChrome({{"tabs":[{}],"canBack":{},"canFwd":{},"url":"{}"}});"#,
        tab_arr.join(","),
        tabs.active_tab().can_go_back(),
        tabs.active_tab().can_go_forward(),
        json_escape(&tabs.active_tab().url),
    )
}

// ── Entry point ───────────────────────────────────────────────────────────────

fn main() -> wry::Result<()> {
    const HOMEPAGE: &str = "https://duckduckgo.com";

    let tabs = Arc::new(Mutex::new(TabManager::new(HOMEPAGE)));

    let event_loop = EventLoop::<AppEvent>::with_user_event();
    let proxy: EventLoopProxy<AppEvent> = event_loop.create_proxy();

    let window = WindowBuilder::new()
        .with_title("BigOs")
        .with_inner_size(LogicalSize::new(1280.0_f64, 820.0_f64))
        .with_min_inner_size(LogicalSize::new(800.0_f64, 500.0_f64))
        .with_visible(false)
        .build(&event_loop)
        .expect("Failed to create BigOs window");

    let startup_url = tabs.lock().unwrap().active_tab().url.clone();
    let proxy_ipc = proxy.clone();

    let webview = WebViewBuilder::new()
        .with_url(&startup_url)
        .with_initialization_script(chrome::INIT_SCRIPT)
        .with_devtools(cfg!(debug_assertions))
        .with_ipc_handler(move |req: Request<String>| {
            let body = req.body().clone();
            if let Some(event) = parse_ipc(&body) {
                let _ = proxy_ipc.send_event(event);
            }
        })
        .build(&window)
        .expect("Failed to create WebView");

    // Push initial chrome state.
    let init_js = chrome_update_js(&tabs.lock().unwrap());
    let _ = webview.evaluate_script(&init_js);

    // ── Event loop ────────────────────────────────────────────────────────────
    event_loop.run(move |event, _, control_flow| {
        *control_flow = ControlFlow::Wait;

        match event {
            Event::WindowEvent {
                event: WindowEvent::Focused(_),
                ..
            } => {
                window.set_visible(true);
            }

            Event::WindowEvent {
                event: WindowEvent::CloseRequested,
                ..
            } => {
                *control_flow = ControlFlow::Exit;
            }

            Event::UserEvent(app_event) => {
                let mut tabs_guard = tabs.lock().unwrap();

                match app_event {
                    AppEvent::Navigate(raw) => {
                        let url = bigos_core::normalize_url(&raw, HOMEPAGE);
                        tabs_guard.active_tab_mut().navigate(&url);
                        let _ = webview.load_url(&url);
                    }

                    AppEvent::Back => {
                        if let Some(url) = tabs_guard.active_tab_mut().back() {
                            let _ = webview.load_url(&url);
                        }
                    }

                    AppEvent::Forward => {
                        if let Some(url) = tabs_guard.active_tab_mut().forward() {
                            let _ = webview.load_url(&url);
                        }
                    }

                    AppEvent::Reload => {
                        let _ = webview.evaluate_script("location.reload()");
                    }

                    AppEvent::NewTab => {
                        let (_id, url) = tabs_guard.new_tab();
                        let _ = webview.load_url(&url);
                    }

                    AppEvent::CloseTab(maybe_id) => {
                        let id = maybe_id.unwrap_or(tabs_guard.active_tab().id);
                        tabs_guard.close_tab(id);
                        let url = tabs_guard.active_tab().url.clone();
                        let _ = webview.load_url(&url);
                    }

                    AppEvent::SwitchTab(id) => {
                        if let Some(url) = tabs_guard.switch_to(id) {
                            let _ = webview.load_url(&url);
                        }
                    }

                    AppEvent::WinMin => window.set_minimized(true),
                    AppEvent::WinMax => {
                        window.set_maximized(!window.is_maximized());
                    }
                    AppEvent::WinClose => *control_flow = ControlFlow::Exit,

                    AppEvent::DevTools => {
                        #[cfg(debug_assertions)]
                        webview.open_devtools();
                    }
                }

                let js = chrome_update_js(&tabs_guard);
                let _ = webview.evaluate_script(&js);
            }

            _ => {}
        }
    });
}

