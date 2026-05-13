// Test: std::thread, std::atomic, std::mutex
#include <thread>
#include <atomic>
#include <mutex>
#include <vector>
#include <cassert>

int main() {
    std::atomic<int> counter{0};
    std::mutex mtx;
    std::vector<int> results;

    auto worker = [&](int n) {
        for (int i = 0; i < n; ++i)
            counter.fetch_add(1, std::memory_order_relaxed);
        std::lock_guard<std::mutex> lock(mtx);
        results.push_back(n);
    };

    std::vector<std::thread> threads;
    for (int i = 0; i < 4; ++i)
        threads.emplace_back(worker, 1000);
    for (auto& t : threads)
        t.join();

    assert(counter.load() == 4000);
    assert(results.size() == 4);
    return 0;
}
