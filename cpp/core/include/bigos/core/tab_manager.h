#pragma once

#include <string>
#include <vector>

namespace bigos::core {

struct Tab {
    int id;
    std::wstring title;
    std::wstring url;
    std::vector<std::wstring> history;
    std::size_t history_index;
};

class TabManager {
public:
    explicit TabManager(std::wstring homepage);

    const Tab& active_tab() const;
    Tab& active_tab();

    int new_tab();
    void close_tab(int id);
    bool switch_to(int id);

    bool navigate_active(const std::wstring& raw_input);
    bool back_active();
    bool forward_active();

    bool can_back() const;
    bool can_forward() const;

    const std::vector<Tab>& tabs() const;

private:
    std::wstring homepage_;
    std::vector<Tab> tabs_;
    std::size_t active_index_;
    int next_tab_id_;
};

}
