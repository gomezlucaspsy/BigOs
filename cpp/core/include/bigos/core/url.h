#pragma once

#include <string>

namespace bigos::core {

std::wstring normalize_url(const std::wstring& raw_input, const std::wstring& fallback_url);

}
