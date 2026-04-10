// Test: C++20 ranges, views, and algorithms
#include <ranges>
#include <vector>
#include <numeric>
#include <cassert>
#include <algorithm>

int main() {
    std::vector<int> v = {1, 2, 3, 4, 5, 6, 7, 8, 9, 10};

    // Filter even numbers and square them
    auto result = v
        | std::views::filter([](int x) { return x % 2 == 0; })
        | std::views::transform([](int x) { return x * x; });

    std::vector<int> out(result.begin(), result.end());
    assert(out == std::vector<int>({4, 16, 36, 64, 100}));

    // Take first 3
    auto first3 = v | std::views::take(3);
    assert(std::ranges::distance(first3) == 3);

    // Iota view (fold_left is C++23; use accumulate here)
    auto iota = std::views::iota(1, 6);
    int sum = std::accumulate(iota.begin(), iota.end(), 0);
    assert(sum == 15);

    // Reverse view
    auto rev = v | std::views::reverse | std::views::take(3);
    std::vector<int> rev_out(rev.begin(), rev.end());
    assert(rev_out == std::vector<int>({10, 9, 8}));

    return 0;
}
