using UnixBrowser.Models;

namespace UnixBrowser.Services
{
    public class TabManager
    {
        private List<Tab> _tabs = new();
        private string? _activeTabId;

        public IReadOnlyList<Tab> Tabs => _tabs.AsReadOnly();
        public Tab? ActiveTab => _activeTabId != null ? _tabs.FirstOrDefault(t => t.Id == _activeTabId) : null;

        public event Action? OnTabsChanged;
        public event Action<Tab>? OnTabActivated;

        public Tab CreateNewTab(string url = "about:blank", string title = "New Tab")
        {
            var tab = new Tab { Url = url, Title = title };
            _tabs.Add(tab);
            _activeTabId = tab.Id;
            OnTabsChanged?.Invoke();
            OnTabActivated?.Invoke(tab);
            return tab;
        }

        public void CloseTab(string tabId)
        {
            if (_tabs.Count == 1) return; // Always keep at least 1 tab

            _tabs.RemoveAll(t => t.Id == tabId);

            if (_activeTabId == tabId)
            {
                _activeTabId = _tabs.FirstOrDefault()?.Id;
                if (ActiveTab != null)
                    OnTabActivated?.Invoke(ActiveTab);
            }

            OnTabsChanged?.Invoke();
        }

        public void ActivateTab(string tabId)
        {
            var tab = _tabs.FirstOrDefault(t => t.Id == tabId);
            if (tab != null)
            {
                _activeTabId = tabId;
                OnTabActivated?.Invoke(tab);
            }
        }

        public void UpdateTab(string tabId, string? title = null, string? url = null)
        {
            var tab = _tabs.FirstOrDefault(t => t.Id == tabId);
            if (tab != null)
            {
                if (title != null) tab.Title = title;
                if (url != null) tab.Url = url;
                OnTabsChanged?.Invoke();
            }
        }
    }
}
