#include "bigos/core/tab_manager.h"

#include "bigos/core/url.h"

namespace bigos::core {

TabManager::TabManager(std::wstring homepage)
    : homepage_(std::move(homepage)), active_index_(0), next_tab_id_(1) {
    Tab first{};
    first.id = 0;
    first.title = L"New Tab";
    first.url = normalize_url(homepage_, L"https://duckduckgo.com");
    first.history = {first.url};
    first.history_index = 0;
    tabs_.push_back(std::move(first));
}

const Tab& TabManager::active_tab() const {
    return tabs_[active_index_];
}

Tab& TabManager::active_tab() {
    return tabs_[active_index_];
}

int TabManager::new_tab() {
    Tab tab{};
    tab.id = next_tab_id_++;
    tab.title = L"New Tab";
    tab.url = normalize_url(homepage_, L"https://duckduckgo.com");
    tab.history = {tab.url};
    tab.history_index = 0;
    tabs_.push_back(std::move(tab));
    active_index_ = tabs_.size() - 1;
    return tabs_[active_index_].id;
}

void TabManager::close_tab(int id) {
    if (tabs_.size() <= 1) {
        return;
    }

    for (std::size_t i = 0; i < tabs_.size(); ++i) {
        if (tabs_[i].id == id) {
            tabs_.erase(tabs_.begin() + static_cast<std::ptrdiff_t>(i));
            if (active_index_ >= tabs_.size()) {
                active_index_ = tabs_.size() - 1;
            }
            return;
        }
    }
}

bool TabManager::switch_to(int id) {
    for (std::size_t i = 0; i < tabs_.size(); ++i) {
        if (tabs_[i].id == id) {
            active_index_ = i;
            return true;
        }
    }
    return false;
}

bool TabManager::navigate_active(const std::wstring& raw_input) {
    Tab& tab = active_tab();
    tab.url = normalize_url(raw_input, homepage_);

    tab.history.resize(tab.history_index + 1);
    tab.history.push_back(tab.url);
    tab.history_index = tab.history.size() - 1;
    return true;
}

bool TabManager::back_active() {
    Tab& tab = active_tab();
    if (tab.history_index == 0) {
        return false;
    }
    --tab.history_index;
    tab.url = tab.history[tab.history_index];
    return true;
}

bool TabManager::forward_active() {
    Tab& tab = active_tab();
    if (tab.history_index + 1 >= tab.history.size()) {
        return false;
    }
    ++tab.history_index;
    tab.url = tab.history[tab.history_index];
    return true;
}

bool TabManager::can_back() const {
    return active_tab().history_index > 0;
}

bool TabManager::can_forward() const {
    const Tab& tab = active_tab();
    return tab.history_index + 1 < tab.history.size();
}

const std::vector<Tab>& TabManager::tabs() const {
    return tabs_;
}

}
