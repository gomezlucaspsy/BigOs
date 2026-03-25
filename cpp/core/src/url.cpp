#include "bigos/core/url.h"

#include <algorithm>

namespace {

std::wstring trim(std::wstring value) {
    auto not_space = [](wchar_t ch) { return !iswspace(ch); };

    value.erase(value.begin(), std::find_if(value.begin(), value.end(), not_space));
    value.erase(std::find_if(value.rbegin(), value.rend(), not_space).base(), value.end());
    return value;
}

bool contains_space(const std::wstring& value) {
    return std::any_of(value.begin(), value.end(), [](wchar_t ch) { return iswspace(ch); });
}

} // namespace

namespace bigos::core {

std::wstring normalize_url(const std::wstring& raw_input, const std::wstring& fallback_url) {
    std::wstring input = trim(raw_input);
    if (input.empty()) {
        return fallback_url;
    }

    if (input.rfind(L"http://", 0) == 0 || input.rfind(L"https://", 0) == 0) {
        return input;
    }

    if (input.rfind(L"localhost", 0) == 0) {
        return L"http://" + input;
    }

    if (!contains_space(input) && input.find(L'.') != std::wstring::npos) {
        return L"https://" + input;
    }

    std::wstring escaped;
    escaped.reserve(input.size());
    for (wchar_t ch : input) {
        if (iswalnum(ch) || ch == L'-' || ch == L'_' || ch == L'.' || ch == L'~') {
            escaped += ch;
        } else if (ch == L' ') {
            escaped += L'+';
        } else {
            wchar_t hex[4];
            swprintf(hex, 4, L"%%%02X", static_cast<unsigned int>(ch & 0xFF));
            escaped += hex;
        }
    }

    return L"https://duckduckgo.com/?q=" + escaped;
}

}
