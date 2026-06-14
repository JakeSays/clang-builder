# Clang Cross-Compilation Toolchain Builder

Build tooling that produces a complete **Clang + compiler-rt + libc++ + LLDB** toolchain for Linux targeting:

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

> **Looking for prebuilt toolchains?** This repository is the builder only. Download ready-to-use toolchains, and read usage and remote-debugging docs, at [clang-releases](https://github.com/JakeSays/clang-releases).

---

## Project Structure

The build tooling is split into three .NET 10 projects under the solution file `ClangBuilder.slnx`:

| Project | Binary | Description |
|---------|--------|-------------|
| `ToolchainBuilder/` | `clang-builder` | Builds and packages the LLVM toolchain |
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

A pre-built `clang-builder` binary is included at `prebuilts/clang-builder`.

```bash
./prebuilts/clang-builder build \
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
# Build clang-builder
dotnet publish -c Release -r linux-x64 -o ./out/clang-builder \
  ToolchainBuilder/ToolchainBuilder.csproj

# Build sysroot-builder
dotnet publish -c Release -r linux-x64 -o ./out/sysroot-builder \
  SysrootBuilder/SysrootBuilder.csproj
```
