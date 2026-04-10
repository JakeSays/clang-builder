// Test: C++17 std::optional, std::variant, structured bindings
#include <optional>
#include <variant>
#include <string>
#include <cassert>

std::optional<int> parse(const std::string& s) {
    if (s == "42") return 42;
    return std::nullopt;
}

int main() {
    auto v = parse("42");
    assert(v.has_value() && *v == 42);
    assert(!parse("bad").has_value());

    std::variant<int, std::string> var = "hello";
    assert(std::holds_alternative<std::string>(var));
    var = 99;
    assert(std::get<int>(var) == 99);

    auto [a, b] = std::pair{1, 2};
    assert(a == 1 && b == 2);
    return 0;
}
