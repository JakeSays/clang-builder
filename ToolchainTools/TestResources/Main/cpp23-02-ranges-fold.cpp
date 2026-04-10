// Test: C++23 std::ranges::fold_left
#include <algorithm>
#include <ranges>
#include <vector>
#include <string>
#include <cassert>
#include <functional>

int main() {
    auto iota = std::views::iota(1, 6);
    int sum = std::ranges::fold_left(iota, 0, std::plus<>{});
    assert(sum == 15);

    // fold_left with strings
    std::vector<std::string> words = {"hello", " ", "world"};
    std::string joined = std::ranges::fold_left(words, std::string{}, std::plus<>{});
    assert(joined == "hello world");

    return 0;
}
