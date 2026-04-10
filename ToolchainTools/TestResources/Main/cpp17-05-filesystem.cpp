// Test: C++17 std::filesystem
#include <filesystem>
#include <fstream>
#include <cassert>
#include <string>
#include <vector>
#include <algorithm>

namespace fs = std::filesystem;

int main() {
    auto tmp = fs::temp_directory_path() / "cpp17-fs-test";
    fs::create_directories(tmp);

    // Create some files
    for (auto name : {"a.txt", "b.txt", "c.cpp"}) {
        std::ofstream f(tmp / name);
        f << "content of " << name;
    }

    // Iterate directory
    std::vector<std::string> names;
    for (auto& entry : fs::directory_iterator(tmp))
        names.push_back(entry.path().filename().string());
    std::sort(names.begin(), names.end());
    assert(names.size() == 3);
    assert(names[0] == "a.txt");

    // path operations
    fs::path p = tmp / "a.txt";
    assert(p.extension() == ".txt");
    assert(p.stem() == "a");
    assert(p.filename() == "a.txt");
    assert(fs::exists(p));
    assert(fs::file_size(p) > 0);

    // copy and rename
    fs::copy(tmp / "a.txt", tmp / "a_copy.txt");
    assert(fs::exists(tmp / "a_copy.txt"));
    fs::rename(tmp / "a_copy.txt", tmp / "a_renamed.txt");
    assert(!fs::exists(tmp / "a_copy.txt"));
    assert(fs::exists(tmp / "a_renamed.txt"));

    // relative path
    fs::path rel = fs::relative(tmp / "b.txt", tmp);
    assert(rel == "b.txt");

    // Cleanup
    fs::remove_all(tmp);
    assert(!fs::exists(tmp));

    return 0;
}
