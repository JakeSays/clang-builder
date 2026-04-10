// Test: C++20 std::jthread and std::stop_token
#include <thread>
#include <stop_token>
#include <atomic>
#include <chrono>
#include <cassert>
#include <vector>
#include <mutex>

int main() {
    using namespace std::chrono_literals;

    std::atomic<int> counter{0};
    std::atomic<bool> stopped{false};

    // jthread auto-joins and sends stop request on destruction
    {
        std::jthread worker([&](std::stop_token st) {
            while (!st.stop_requested()) {
                counter.fetch_add(1, std::memory_order_relaxed);
                std::this_thread::sleep_for(1ms);
            }
            stopped = true;
        });
        std::this_thread::sleep_for(20ms);
        // worker.request_stop() called automatically on destruction
    }

    assert(stopped.load());
    assert(counter.load() > 0);

    // stop_callback
    std::atomic<bool> cb_called{false};
    {
        std::stop_source ss;
        std::stop_callback cb(ss.get_token(), [&]{ cb_called = true; });
        ss.request_stop();
        assert(cb_called.load());
    }

    // Multiple jthreads
    std::mutex mtx;
    std::vector<int> results;
    {
        std::vector<std::jthread> threads;
        for (int i = 0; i < 4; ++i) {
            threads.emplace_back([&, i](std::stop_token) {
                std::lock_guard lock(mtx);
                results.push_back(i);
            });
        }
    }
    assert(results.size() == 4);

    return 0;
}
