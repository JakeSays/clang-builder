// Test: C++17 parallel algorithms, string_view, std::clamp
#include <algorithm>
#include <numeric>
#include <vector>
#include <string_view>
#include <cassert>
#include <string>
#include <charconv>
#include <array>

bool starts_with(std::string_view s, std::string_view prefix) {
    return s.substr(0, prefix.size()) == prefix;
}

int main() {
    // string_view — zero-copy string operations
    std::string str = "hello world";
    std::string_view sv = str;
    assert(sv.substr(6) == "world");
    assert(starts_with(sv, "hello"));
    assert(sv.find("world") == 6);

    // std::clamp
    assert(std::clamp(5,  0, 10) == 5);
    assert(std::clamp(-1, 0, 10) == 0);
    assert(std::clamp(15, 0, 10) == 10);

    // std::gcd and std::lcm
    assert(std::gcd(12, 8) == 4);
    assert(std::lcm(4,  6) == 12);

    // std::reduce and std::transform_reduce
    std::vector<int> v = {1, 2, 3, 4, 5};
    assert(std::reduce(v.begin(), v.end()) == 15);
    assert(std::reduce(v.begin(), v.end(), 10) == 25);

    int dot = std::transform_reduce(
        v.begin(), v.end(), v.begin(), 0);
    assert(dot == 55);  // 1+4+9+16+25

    // std::exclusive_scan and std::inclusive_scan
    std::vector<int> prefix(v.size());
    std::inclusive_scan(v.begin(), v.end(), prefix.begin());
    assert((prefix == std::vector<int>{1, 3, 6, 10, 15}));

    std::exclusive_scan(v.begin(), v.end(), prefix.begin(), 0);
    assert((prefix == std::vector<int>{0, 1, 3, 6, 10}));

    // std::from_chars (no locale, no allocation)
    std::array<char, 16> buf{"12345"};
    int parsed = 0;
    auto [ptr, ec] = std::from_chars(buf.data(), buf.data() + 5, parsed);
    assert(ec == std::errc{});
    assert(parsed == 12345);

    return 0;
}
