// Test: std::filesystem
#include <filesystem>
#include <fstream>
#include <cassert>
#include <string>

namespace fs = std::filesystem;

int main() {
    auto tmp = fs::temp_directory_path() / "bootstrap-test-XXXXXX";
    // Create a temp file
    auto path = fs::temp_directory_path() / "bootstrap_test.txt";
    {
        std::ofstream f(path);
        f << "bootstrap test";
    }
    assert(fs::exists(path));
    assert(fs::file_size(path) > 0);
    fs::remove(path);
    assert(!fs::exists(path));
    return 0;
}
