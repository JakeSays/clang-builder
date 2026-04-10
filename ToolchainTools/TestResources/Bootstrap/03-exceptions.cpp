// Test: exception handling, RTTI
#include <stdexcept>
#include <typeinfo>
#include <cassert>
#include <string>

struct Base { virtual ~Base() = default; };
struct Derived : Base { int x = 42; };

int main() {
    // Exception handling
    bool caught = false;
    try {
        throw std::runtime_error("test error");
    } catch (const std::exception& e) {
        caught = true;
        assert(std::string(e.what()) == "test error");
    }
    assert(caught);

    // RTTI / dynamic_cast
    Base* b = new Derived();
    auto* d = dynamic_cast<Derived*>(b);
    assert(d != nullptr && d->x == 42);
    assert(typeid(*b) == typeid(Derived));
    delete b;

    return 0;
}
