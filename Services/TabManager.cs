// ============================================================
// TabManager.cs — Browser Tab Lifecycle Manager
// Purpose : Creates, closes, activates, and updates tabs.
//           Each tab is a lightweight model \u2014 the actual
//           WebView2 is shared and navigated on activation.
//           Invariant: there is always at least one tab open.
// ============================================================
using UnixBrowser.Models;

namespace UnixBrowser.Services
{
    /// <summary>
    /// Manages the collection of open browser tabs.
    /// Enforces the invariant that at least one tab always exists.
    /// Notifies the UI via events \u2014 does not touch UI directly.
    /// </summary>
    public class TabManager
    {
        private readonly List<Tab> _tabs = new(); // All open tabs in order
        private string? _activeTabId;             // ID of the currently visible tab

        public IReadOnlyList<Tab> Tabs      => _tabs.AsReadOnly();
        public Tab? ActiveTab => _activeTabId != null
            ? _tabs.FirstOrDefault(t => t.Id == _activeTabId)
            : null;

        /// <summary>Fired when the tab list changes (add/close/update).</summary>
        public event Action?      OnTabsChanged;
        /// <summary>Fired when a different tab becomes active.</summary>
        public event Action<Tab>? OnTabActivated;

        /// <summary>
        /// Creates a new tab, makes it active, and notifies subscribers.
        /// Returns the created Tab so callers can navigate it immediately.
        /// </summary>
        public Tab CreateNewTab(string url = "about:blank", string title = "New Tab")
        {
            var tab = new Tab { Url = url, Title = title };
            _tabs.Add(tab);
            _activeTabId = tab.Id;
            OnTabsChanged?.Invoke();
            OnTabActivated?.Invoke(tab);
            return tab;
        }

        /// <summary>
        /// Closes a tab by ID. Refuses to close the last remaining tab
        /// \u2014 a browser with zero tabs is a closed browser.
        /// Activates the first remaining tab if the active tab was closed.
        /// </summary>
        public void CloseTab(string tabId)
        {
            if (_tabs.Count == 1) return; // Invariant: always keep one tab

            _tabs.RemoveAll(t => t.Id == tabId);

            // If we just closed the active tab, activate the first available one
            if (_activeTabId == tabId)
            {
                _activeTabId = _tabs.FirstOrDefault()?.Id;
                if (ActiveTab != null) OnTabActivated?.Invoke(ActiveTab);
            }

            OnTabsChanged?.Invoke();
        }

        /// <summary>Makes the tab with the given ID the active (visible) tab.</summary>
        public void ActivateTab(string tabId)
        {
            var tab = _tabs.FirstOrDefault(t => t.Id == tabId);
            if (tab == null) return;

            _activeTabId = tabId;
            OnTabActivated?.Invoke(tab);
        }

        /// <summary>
        /// Updates the title and/or URL of a tab.
        /// Passing null for either field leaves it unchanged.
        /// </summary>
        public void UpdateTab(string tabId, string? title = null, string? url = null)
        {
            var tab = _tabs.FirstOrDefault(t => t.Id == tabId);
            if (tab == null) return;

            if (title != null) tab.Title = title;
            if (url   != null) tab.Url   = url;
            OnTabsChanged?.Invoke();
        }
    }
}

