//! BigOs Android bridge.
//!
//! This crate compiles to a shared library (`libbigos_android.so`) that the
//! Android WebView shell (Kotlin/Java) loads via JNI.  All browser logic lives
//! in `bigos-core`; this crate is purely the FFI glue layer.
//!
//! # How it fits together
//!
//! ```text
//! Android app (Kotlin)
//!   └─ WebView (android.webkit.WebView)
//!        └─ JNI calls  ──►  libbigos_android.so  (this crate)
//!                                └─ bigos-core (pure Rust)
//! ```
//!
//! The Kotlin shell lives under `apps/bigos-android/` (a standard Android
//! Gradle project).  The same FreeBSD-style HTML chrome overlay used on
//! desktop is injected via `WebView.addJavascriptInterface` + `evaluateJavascript`.

use bigos_core::{normalize_url, TabManager};

// ── Pure-Rust API (usable in tests without JNI) ───────────────────────────────

/// Normalise a URL or search query for display in the address bar.
/// Re-exported so the Android Kotlin layer can call it through JNI.
pub fn resolve_url(raw: &str) -> String {
    normalize_url(raw, "https://duckduckgo.com")
}

/// Create a default tab manager seeded with DuckDuckGo.
pub fn default_tab_manager() -> TabManager {
    TabManager::new("https://duckduckgo.com")
}

// ── JNI bindings (Android only) ───────────────────────────────────────────────

#[cfg(target_os = "android")]
mod android {
    use super::resolve_url;
    use jni::objects::{JClass, JString};
    use jni::sys::jstring;
    use jni::JNIEnv;

    /// Called from Kotlin:
    /// ```kotlin
    /// external fun resolveUrl(raw: String): String
    /// ```
    #[no_mangle]
    pub extern "C" fn Java_com_bigos_browser_NativeBridge_resolveUrl(
        mut env: JNIEnv,
        _class: JClass,
        raw: JString,
    ) -> jstring {
        let input: String = env
            .get_string(&raw)
            .expect("Invalid JString from Android")
            .into();

        let resolved = resolve_url(&input);

        env.new_string(resolved)
            .expect("Failed to create JString")
            .into_raw()
    }
}

#[cfg(test)]
mod tests {
    use super::resolve_url;

    #[test]
    fn bare_host_resolves() {
        assert_eq!(resolve_url("rust-lang.org"), "https://rust-lang.org/");
    }

    #[test]
    fn search_resolves_to_ddg() {
        let url = resolve_url("hello world");
        assert!(url.contains("duckduckgo.com"));
    }

    #[test]
    fn localhost_resolves_to_http() {
        assert_eq!(resolve_url("localhost:8081"), "http://localhost:8081/");
    }
}

