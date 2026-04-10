// Test: C++20 designated initializers, three-way comparison, aggregate init
#include <compare>
#include <cassert>
#include <string>
#include <vector>
#include <algorithm>

struct Point {
    int x, y;
    auto operator<=>(const Point&) const = default;
};

struct Config {
    int width  = 800;
    int height = 600;
    bool fullscreen = false;
    std::string title = "default";
};

int main() {
    // Designated initializers
    Config cfg = {
        .width  = 1920,
        .height = 1080,
        .fullscreen = true,
        .title  = "test"
    };
    assert(cfg.width == 1920);
    assert(cfg.fullscreen == true);
    assert(cfg.title == "test");

    // Default designated init
    Config def = { .title = "only title" };
    assert(def.width == 800);
    assert(def.height == 600);
    assert(def.title == "only title");

    // Three-way comparison
    Point p1{1, 2}, p2{1, 3}, p3{1, 2};
    assert((p1 <=> p2) < 0);
    assert((p2 <=> p1) > 0);
    assert((p1 <=> p3) == 0);
    assert(p1 < p2);
    assert(p2 > p1);
    assert(p1 == p3);

    // Sorting with three-way comparison
    std::vector<Point> pts = {{3,4},{1,2},{2,1},{1,1}};
    std::sort(pts.begin(), pts.end());
    assert(pts[0].x == 1 && pts[0].y == 1);
    assert(pts[3].x == 3 && pts[3].y == 4);

    return 0;
}
