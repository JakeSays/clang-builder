// Test: C++17 structured bindings, std::apply, std::invoke
#include <tuple>
#include <map>
#include <cassert>
#include <functional>
#include <string>

std::tuple<int, double, std::string> make_record(int id) {
    return {id, id * 1.5, "item_" + std::to_string(id)};
}

int add3(int a, int b, int c) { return a + b + c; }

int main() {
    // Structured bindings with tuple
    auto [id, val, name] = make_record(4);
    assert(id == 4);
    assert(val == 6.0);
    assert(name == "item_4");

    // Structured bindings with map
    std::map<std::string, int> scores = {{"alice", 95}, {"bob", 87}};
    int total = 0;
    for (auto& [key, score] : scores)
        total += score;
    assert(total == 182);

    // std::apply
    auto t = std::make_tuple(1, 2, 3);
    int sum = std::apply(add3, t);
    assert(sum == 6);

    // std::apply with lambda
    auto product = std::apply([](int a, int b, int c) { return a * b * c; }, t);
    assert(product == 6);

    // std::invoke
    auto result = std::invoke(add3, 10, 20, 30);
    assert(result == 60);

    // std::invoke with member function
    std::string s = "hello";
    auto len = std::invoke(&std::string::size, s);
    assert(len == 5);

    return 0;
}
