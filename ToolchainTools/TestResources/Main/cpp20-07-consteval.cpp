// Test: C++20 consteval, constinit, template lambda, abbreviated function templates
#include <cassert>
#include <array>
#include <numeric>
#include <string>

// consteval — must be evaluated at compile time
consteval int factorial(int n) {
    return n <= 1 ? 1 : n * factorial(n - 1);
}

// constinit — guarantee constant initialization
constinit int global_value = factorial(5);

// Abbreviated function template
auto add(auto a, auto b) { return a + b; }

// Template lambda (C++20)
auto make_adder = []<typename T>(T x) {
    return [x](T y) { return x + y; };
};

// consteval string length
consteval std::size_t str_len(const char* s) {
    std::size_t len = 0;
    while (s[len]) ++len;
    return len;
}

int main() {
    // consteval
    static_assert(factorial(5) == 120);
    static_assert(factorial(10) == 3628800);

    // constinit
    assert(global_value == 120);

    // Abbreviated templates
    assert(add(1, 2) == 3);
    assert(add(1.5, 2.5) == 4.0);
    assert(add(std::string("hello "), std::string("world")) == "hello world");

    // Template lambda
    auto add5 = make_adder(5);
    assert(add5(3) == 8);
    auto add3_14 = make_adder(3.14);
    assert(add3_14(0.0) == 3.14);

    // consteval string
    static_assert(str_len("hello") == 5);
    static_assert(str_len("") == 0);

    // constexpr std::array operations
    constexpr std::array<int, 5> arr = {1, 2, 3, 4, 5};
    static_assert(arr[4] == 5);
    static_assert(std::size(arr) == 5);

    return 0;
}
