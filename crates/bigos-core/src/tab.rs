use crate::url_utils::normalize_url;

// ── Single tab ─────────────────────────────────────────────────────────────

#[derive(Debug, Clone)]
pub struct Tab {
    pub id: usize,
    pub title: String,
    pub url: String,
    history: Vec<String>,
    pos: usize,
}

impl Tab {
    pub fn new(id: usize, url: &str) -> Self {
        let resolved = normalize_url(url, "https://duckduckgo.com");
        Self {
            id,
            title: "New Tab".to_string(),
            url: resolved.clone(),
            history: vec![resolved],
            pos: 0,
        }
    }

    /// Push a new URL onto the forward-history-cleared stack.
    pub fn navigate(&mut self, raw: &str) {
        let url = normalize_url(raw, "https://duckduckgo.com");
        self.history.truncate(self.pos + 1);
        self.history.push(url.clone());
        self.pos = self.history.len() - 1;
        self.url = url;
    }

    /// Step back one history entry; returns the URL to load (if possible).
    pub fn back(&mut self) -> Option<String> {
        if self.pos == 0 {
            return None;
        }
        self.pos -= 1;
        self.url = self.history[self.pos].clone();
        Some(self.url.clone())
    }

    /// Step forward one history entry; returns the URL to load (if possible).
    pub fn forward(&mut self) -> Option<String> {
        if self.pos + 1 >= self.history.len() {
            return None;
        }
        self.pos += 1;
        self.url = self.history[self.pos].clone();
        Some(self.url.clone())
    }

    pub fn can_go_back(&self) -> bool {
        self.pos > 0
    }

    pub fn can_go_forward(&self) -> bool {
        self.pos + 1 < self.history.len()
    }
}

// ── Tab manager ────────────────────────────────────────────────────────────

pub struct TabManager {
    tabs: Vec<Tab>,
    active_idx: usize,
    next_id: usize,
    homepage: String,
}

impl TabManager {
    pub fn new(homepage: &str) -> Self {
        let first = Tab::new(0, homepage);
        Self {
            tabs: vec![first],
            active_idx: 0,
            next_id: 1,
            homepage: homepage.to_string(),
        }
    }

    pub fn active_tab(&self) -> &Tab {
        &self.tabs[self.active_idx]
    }

    pub fn active_tab_mut(&mut self) -> &mut Tab {
        &mut self.tabs[self.active_idx]
    }

    /// Open a new tab; returns its id.
    pub fn new_tab(&mut self) -> (usize, String) {
        let id = self.next_id;
        self.next_id += 1;
        let tab = Tab::new(id, &self.homepage);
        let url = tab.url.clone();
        self.tabs.push(tab);
        self.active_idx = self.tabs.len() - 1;
        (id, url)
    }

    /// Close a tab by id. Won't close the last tab.
    pub fn close_tab(&mut self, id: usize) {
        if self.tabs.len() == 1 {
            return;
        }
        if let Some(pos) = self.tabs.iter().position(|t| t.id == id) {
            self.tabs.remove(pos);
            if self.active_idx >= self.tabs.len() {
                self.active_idx = self.tabs.len() - 1;
            }
        }
    }

    /// Switch the active tab; returns the URL to display.
    pub fn switch_to(&mut self, id: usize) -> Option<String> {
        if let Some(pos) = self.tabs.iter().position(|t| t.id == id) {
            self.active_idx = pos;
            Some(self.tabs[pos].url.clone())
        } else {
            None
        }
    }

    pub fn tabs(&self) -> &[Tab] {
        &self.tabs
    }
}
