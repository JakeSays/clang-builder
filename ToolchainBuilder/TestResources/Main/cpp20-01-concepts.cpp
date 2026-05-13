// Test: C++20 concepts and constraints
#include <concepts>
#include <cassert>
#include <string>

template<typename T>
concept Numeric = std::integral<T> || std::floating_point<T>;

template<typename T>
concept Printable = requires(T t) {
    { std::to_string(t) } -> std::convertible_to<std::string>;
};

template<Numeric T>
T square(T x) { return x * x; }

template<typename T>
requires Numeric<T> && (sizeof(T) >= 4)
T safe_multiply(T a, T b) { return a * b; }

int main() {
    assert(square(5) == 25);
    assert(square(3.0) == 9.0);
    assert(safe_multiply(6, 7) == 42);
    assert(safe_multiply(2.5f, 4.0f) == 10.0f);

    static_assert(Numeric<int>);
    static_assert(Numeric<double>);
    static_assert(!Numeric<std::string>);
    static_assert(Printable<int>);

    return 0;
}
