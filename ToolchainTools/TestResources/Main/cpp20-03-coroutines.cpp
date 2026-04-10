// Test: C++20 coroutines
#include <coroutine>
#include <cassert>
#include <optional>
#include <vector>

// Simple generator using coroutines
template<typename T>
struct Generator {
    struct promise_type {
        T current_value;
        auto get_return_object() { return Generator{std::coroutine_handle<promise_type>::from_promise(*this)}; }
        auto initial_suspend() { return std::suspend_always{}; }
        auto final_suspend() noexcept { return std::suspend_always{}; }
        auto yield_value(T value) {
            current_value = value;
            return std::suspend_always{};
        }
        void return_void() {}
        void unhandled_exception() { std::terminate(); }
    };

    std::coroutine_handle<promise_type> handle;
    Generator(std::coroutine_handle<promise_type> h) : handle(h) {}
    ~Generator() { if (handle) handle.destroy(); }

    bool next() {
        handle.resume();
        return !handle.done();
    }
    T value() { return handle.promise().current_value; }
};

Generator<int> range(int from, int to) {
    for (int i = from; i < to; ++i)
        co_yield i;
}

Generator<int> fibonacci() {
    int a = 0, b = 1;
    while (true) {
        co_yield a;
        auto c = a + b;
        a = b;
        b = c;
    }
}

int main() {
    // Test range generator
    std::vector<int> vals;
    auto gen = range(1, 6);
    while (gen.next())
        vals.push_back(gen.value());
    assert((vals == std::vector<int>{1, 2, 3, 4, 5}));

    // Test fibonacci generator
    std::vector<int> fibs;
    auto fib = fibonacci();
    for (int i = 0; i < 8 && fib.next(); ++i)
        fibs.push_back(fib.value());
    assert((fibs == std::vector<int>{0, 1, 1, 2, 3, 5, 8, 13}));

    return 0;
}
