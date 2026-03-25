use percent_encoding::{utf8_percent_encode, NON_ALPHANUMERIC};
use url::Url;

/// Normalise raw address-bar input:
///   - Full URLs (http/https/file/about) → returned as-is (parsed).
///   - `localhost[:port][/path]`          → wrapped with `http://`.
///   - Bare hostnames (`foo.com`)         → wrapped with `https://`.
///   - Anything else                      → DuckDuckGo search query.
pub fn normalize_url(raw: &str, fallback: &str) -> String {
    let input = raw.trim();
    if input.is_empty() {
        return fallback.to_string();
    }

    // Already a known scheme?
    if let Ok(parsed) = Url::parse(input) {
        match parsed.scheme() {
            "http" | "https" | "file" | "about" | "data" => return parsed.to_string(),
            _ => {}
        }
    }

    // localhost[:port] — use plain http so the browser doesn't block it
    if input.starts_with("localhost") {
        let candidate = format!("http://{input}");
        if let Ok(parsed) = Url::parse(&candidate) {
            return parsed.to_string();
        }
    }

    // Looks like a bare hostname (has a dot, no spaces, no path-only slash)?
    if !input.contains(' ') && input.contains('.') {
        let candidate = format!("https://{input}");
        if let Ok(parsed) = Url::parse(&candidate) {
            return parsed.to_string();
        }
    }

    // Fall back: DuckDuckGo search
    let query = utf8_percent_encode(input, NON_ALPHANUMERIC).to_string();
    format!("https://duckduckgo.com/?q={query}")
}
