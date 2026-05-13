# Clang Cross-Compilation Toolchain

Builds a complete **Clang + compiler-rt + libc++ + LLDB** toolchain targeting:

| Target | Triple |
|--------|--------|
| x86_64 glibc | `x86_64-linux-gnu` |
| x86_64 musl | `x86_64-linux-musl` |
| x86 (i686) glibc | `i686-linux-gnu` |
| x86 (i686) musl | `i686-linux-musl` |
| armv7 glibc | `armv7-linux-gnueabihf` |
| armv7 musl | `armv7-linux-musleabihf` |
| aarch64 glibc | `aarch64-linux-gnu` |
| aarch64 musl | `aarch64-linux-musl` |
| riscv64 glibc | `riscv64-linux-gnu` |
| riscv64 musl | `riscv64-linux-musl` |

The host toolchain (clang, lld, lldb, llvm-\*) is fully statically linked against musl libc and requires no runtime dependencies on the build machine.

---

## Project Structure

The build tooling is split into three .NET 10 projects under the solution file `ClangBuilder.slnx`:

| Project | Binary | Description |
|---------|--------|-------------|
| `ToolchainBuilder/` | `toolchain-builder` | Builds and packages the LLVM toolchain |
| `SysrootBuilder/` | `sysroot-builder` | Builds glibc (Debian) and musl (Alpine) sysroots |
| `Common/` | *(library)* | Shared utilities used by both tools |

---

## Building

### Prerequisites

```bash
sudo apt-get install -y cmake ninja-build git python3 curl xz-utils patchelf \
  qemu-user qemu-user-static
```

QEMU is used to run cross-compiled test binaries on the build machine.

### Run the toolchain build

A pre-built `toolchain-builder` binary is included at `prebuilts/toolchain-builder`.

```bash
./prebuilts/toolchain-builder build \
  --llvm-version 22.1.2 \
  --all \
  --prebuilts-dir ./prebuilts \
  --work-dir      /tmp/toolchain-work \
  --output-dir    . \
  --jobs          $(nproc) \
  --run-tests \
  --package
```

All prebuilts (bootstrap compiler and cross sysroots) are read from `--prebuilts-dir` and stored in Git LFS.

### Build options

| Option | Description |
|--------|-------------|
| `--llvm-version <ver>` | LLVM version to build |
| `--all` | Build all targets |
| `--x64`, `--armv7`, `--aarch64`, `--riscv64`, `--x32` | Build individual targets |
| `--prebuilts-dir <dir>` | Directory containing prebuilt archives |
| `--work-dir <dir>` | Working directory for build artifacts |
| `--output-dir <dir>` | Where to write the final package |
| `--jobs <N>` | Parallel build jobs |
| `--run-tests` | Run toolchain tests before packaging |
| `--package` | Package the toolchain after a successful build |
| `--keep-work-dir` | Do not delete the work directory after the build |
| `--force-reconfigure` | Re-run CMake configure even if already configured |

### Build sysroots

A pre-built `sysroot-builder` binary is included at `prebuilts/sysroot-builder`.

```bash
# Build an Alpine musl host sysroot
./prebuilts/sysroot-builder make-sysroot \
  --host \
  --output-dir ./prebuilts \
  --work-dir   /tmp/sysroot-work

# Build Debian glibc cross sysroots for all architectures
./prebuilts/sysroot-builder make-sysroot \
  --glibc --all \
  --output-dir ./prebuilts \
  --work-dir   /tmp/sysroot-work
```

A `--package-list <file>` option overrides the default package set. The file contains one package per line (`#` comments and blank lines are ignored) and may include `release:` and `repo-url:` directives:

```
# my custom sysroot
release: bookworm
repo-url: http://my-mirror.internal/debian

libc6-dev
linux-libc-dev
zlib1g-dev
```

Use `--packages` alongside `--package-list` to add or remove individual packages (`+pkg` or plain `pkg` to add, `-pkg` to remove).

### Building from source

Install the [.NET 10 SDK](https://dotnet.microsoft.com/download), then:

```bash
# Build toolchain-builder
dotnet publish -c Release -r linux-x64 -o ./out/toolchain-builder \
  ToolchainBuilder/ToolchainBuilder.csproj

# Build sysroot-builder
dotnet publish -c Release -r linux-x64 -o ./out/sysroot-builder \
  SysrootBuilder/SysrootBuilder.csproj
```

---

## CI

A GitHub Actions workflow (`.github/workflows/build-clang-toolchain.yml`) builds, tests, and packages the toolchain and publishes it as a GitHub Release.

A companion workflow (`.github/workflows/check-clang-version.yml`) runs daily, checks for new LLVM releases, and triggers the build automatically when a new version is found.

---

## Using the Toolchain

The default target triple is `x86_64-linux-gnu`. Pass `--target` to cross-compile:

```bash
TOOLCHAIN=/path/to/clang-22.1.2-linux-x86_64

# Cross-compile for aarch64 glibc
$TOOLCHAIN/bin/clang++ \
  --target=aarch64-linux-gnu \
  --sysroot=$TOOLCHAIN/sysroots/aarch64 \
  -stdlib=libc++ -rtlib=compiler-rt \
  hello.cpp -o hello

# Cross-compile for aarch64 musl (static)
$TOOLCHAIN/bin/clang++ \
  --target=aarch64-linux-musl \
  --sysroot=$TOOLCHAIN/sysroots/aarch64-musl \
  -stdlib=libc++ -rtlib=compiler-rt -static \
  hello.cpp -o hello
```

Clang finds compiler-rt and libc++ automatically via its resource directory — no manual `-L` or `-I` flags needed.

---

## Remote Debugging with LLDB

Copy the appropriate `lldb-server` binary from `bin/<arch>-linux/` to the target device and start it:

```bash
# Platform mode — attach to any process or launch new ones
lldb-server platform --listen "*:9999" --server

# gdbserver mode — debug a single process
lldb-server gdbserver 0.0.0.0:9999 -- /path/to/binary
```

Then connect from the host using `lldb`. Both binaries are built from the same LLVM version for protocol compatibility. The `lldb-server` binary is statically linked against musl and has no runtime dependencies.
