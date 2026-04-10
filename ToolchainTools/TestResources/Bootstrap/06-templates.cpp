// Test: templates, constexpr, if constexpr, fold expressions
#include <cassert>
#include <string_view>
#include <type_traits>

template<typename T>
constexpr T square(T x) { return x * x; }

template<typename... Args>
constexpr auto sum(Args... args) { return (args + ...); }

template<typename T>
constexpr auto describe() {
    if constexpr (std::is_integral_v<T>) return "integral";
    else if constexpr (std::is_floating_point_v<T>) return "float";
    else return "other";
}

int main() {
    static_assert(square(5) == 25);
    static_assert(sum(1, 2, 3, 4, 5) == 15);
    static_assert(square(3.0) == 9.0);

    assert(std::string_view(describe<int>()) == "integral");
    assert(std::string_view(describe<double>()) == "float");

    constexpr int arr[5] = {1, 2, 3, 4, 5};
    static_assert(arr[4] == 5);

    return 0;
}
