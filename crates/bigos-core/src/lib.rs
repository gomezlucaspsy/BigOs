pub mod tab;
pub mod url_utils;

pub use tab::{Tab, TabManager};
pub use url_utils::normalize_url;

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn defaults_to_ddg() {
        let tabs = TabManager::new("https://duckduckgo.com");
        assert_eq!(tabs.active_tab().url, "https://duckduckgo.com/");
    }

    #[test]
    fn search_query_goes_to_ddg() {
        let url = normalize_url("rust async await", "https://duckduckgo.com");
        assert!(url.contains("duckduckgo.com"), "expected DDG search, got: {url}");
    }

    #[test]
    fn bare_hostname_gets_https() {
        let url = normalize_url("github.com", "https://duckduckgo.com");
        assert_eq!(url, "https://github.com/");
    }

    #[test]
    fn localhost_port_gets_http() {
        let url = normalize_url("localhost:3000", "https://duckduckgo.com");
        assert_eq!(url, "http://localhost:3000/");
    }
}

