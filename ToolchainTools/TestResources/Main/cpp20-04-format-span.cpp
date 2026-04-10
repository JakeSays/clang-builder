// Test: C++20 std::format and std::span
#include <format>
#include <span>
#include <vector>
#include <array>
#include <cassert>
#include <string>
#include <numeric>

void sum_span(std::span<const int> s, int& out) {
    out = 0;
    for (int x : s) out += x;
}

int main() {
    // std::format
    assert(std::format("hello {}", "world") == "hello world");
    assert(std::format("{} + {} = {}", 1, 2, 3) == "1 + 2 = 3");
    assert(std::format("{:.2f}", 3.14159) == "3.14");
    assert(std::format("{:>10}", "right") == "     right");
    assert(std::format("{:05d}", 42) == "00042");

    // std::span from vector
    std::vector<int> v = {1, 2, 3, 4, 5};
    int total = 0;
    sum_span(v, total);
    assert(total == 15);

    // std::span from array
    std::array<int, 4> arr = {10, 20, 30, 40};
    sum_span(arr, total);
    assert(total == 100);

    // subspan
    std::span<int> s(v);
    auto sub = s.subspan(1, 3);
    assert(sub.size() == 3);
    assert(sub[0] == 2 && sub[2] == 4);

    return 0;
}
