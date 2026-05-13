// Test: C++17 if constexpr, nested namespaces, inline variables
#include <cassert>
#include <string>
#include <type_traits>

namespace app::util::math {
    inline constexpr double pi = 3.14159265358979;

    template<typename T>
    constexpr T abs(T x) {
        if constexpr (std::is_unsigned_v<T>)
            return x;
        else
            return x < 0 ? -x : x;
    }

    template<typename T>
    std::string type_name() {
        if constexpr (std::is_integral_v<T>)
            return "integral";
        else if constexpr (std::is_floating_point_v<T>)
            return "floating";
        else
            return "other";
    }
}

int main() {
    namespace math = app::util::math;

    static_assert(math::abs(-5) == 5);
    static_assert(math::abs(5u) == 5u);
    static_assert(math::abs(-3.14) == 3.14);
    assert(math::pi > 3.14 && math::pi < 3.15);

    assert(math::type_name<int>()    == "integral");
    assert(math::type_name<double>() == "floating");
    assert(math::type_name<char*>()  == "other");

    return 0;
}
