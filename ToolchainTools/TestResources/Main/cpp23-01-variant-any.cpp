// Test: std::variant, std::visit, std::any, std::optional::and_then (C++23)
#include <variant>
#include <any>
#include <optional>
#include <cassert>
#include <string>
#include <vector>

using Value = std::variant<int, double, std::string, bool>;

std::string describe(const Value& v) {
    return std::visit([](const auto& x) -> std::string {
        using T = std::decay_t<decltype(x)>;
        if constexpr (std::is_same_v<T, int>)
            return "int:" + std::to_string(x);
        else if constexpr (std::is_same_v<T, double>)
            return "double:" + std::to_string(x);
        else if constexpr (std::is_same_v<T, std::string>)
            return "string:" + x;
        else
            return std::string("bool:") + (x ? "true" : "false");
    }, v);
}

std::optional<int> safe_div(int a, int b) {
    if (b == 0) return std::nullopt;
    return a / b;
}

int main() {
    // std::variant + std::visit
    std::vector<Value> values = {42, 3.14, std::string("hello"), true};
    assert(describe(values[0]) == "int:42");
    assert(describe(values[2]) == "string:hello");
    assert(describe(values[3]) == "bool:true");

    // variant index and holds_alternative
    Value v = 99;
    assert(std::holds_alternative<int>(v));
    assert(std::get<int>(v) == 99);
    v = std::string("world");
    assert(std::holds_alternative<std::string>(v));

    // std::any
    std::any a = 42;
    assert(std::any_cast<int>(a) == 42);
    a = std::string("hello");
    assert(std::any_cast<std::string>(a) == "hello");
    assert(a.type() == typeid(std::string));

    // std::optional chaining
    assert(safe_div(10, 2).value() == 5);
    assert(!safe_div(10, 0).has_value());
    assert(safe_div(10, 0).value_or(-1) == -1);

    // optional with map-like transform (value_or chaining)
    auto result = safe_div(100, 4)
        .and_then([](int x) { return safe_div(x, 5); })
        .value_or(0);
    assert(result == 5);

    return 0;
}
