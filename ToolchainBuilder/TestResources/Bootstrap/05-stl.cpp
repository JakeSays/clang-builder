// Test: STL containers, algorithms, lambdas, ranges
#include <vector>
#include <map>
#include <algorithm>
#include <numeric>
#include <functional>
#include <cassert>
#include <string>

int main() {
    // Vector + algorithm
    std::vector<int> v = {5, 3, 1, 4, 2};
    std::sort(v.begin(), v.end());
    assert(v == std::vector<int>({1, 2, 3, 4, 5}));

    int sum = std::accumulate(v.begin(), v.end(), 0);
    assert(sum == 15);

    // Lambda capture
    int factor = 3;
    std::transform(v.begin(), v.end(), v.begin(),
        [factor](int x) { return x * factor; });
    assert(v[0] == 3 && v[4] == 15);

    // Map
    std::map<std::string, int> m;
    m["a"] = 1; m["b"] = 2; m["c"] = 3;
    assert(m.size() == 3);
    assert(m.count("b") == 1);

    // std::function
    std::function<int(int, int)> add = [](int a, int b) { return a + b; };
    assert(add(3, 4) == 7);

    return 0;
}
