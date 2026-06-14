# Shared base for fully-static (100% static, no dynamic linker) Linux cross
# targets. An arch/libc leaf file sets the parameters below and then include()s
# this file:
#   TOOLCHAIN_PROCESSOR        value for CMAKE_SYSTEM_PROCESSOR (e.g. x86_64, aarch64, armv7)
#   TOOLCHAIN_TRIPLE           clang target triple (e.g. x86_64-unknown-linux-gnu)
#   TOOLCHAIN_SYSROOT_SUBDIR   subdirectory under sysroots/ (e.g. x64, x64-musl)
#   TOOLCHAIN_ARCH_C_FLAGS     optional arch compile flags for C   (e.g. -march=armv7-a ...)
#   TOOLCHAIN_ARCH_CXX_FLAGS   optional arch compile flags for C++ (e.g. -march=armv7-a ...)
if(NOT DEFINED TOOLCHAIN_TRIPLE)
    message(FATAL_ERROR "Use an arch/libc toolchain file; do not pass this base file directly.")
endif()

get_filename_component(TOOLCHAIN_ROOT "${CMAKE_CURRENT_LIST_DIR}/.." ABSOLUTE)

set(CMAKE_SYSTEM_NAME Linux)
set(CMAKE_SYSTEM_PROCESSOR ${TOOLCHAIN_PROCESSOR})

set(CMAKE_C_COMPILER   "${TOOLCHAIN_ROOT}/bin/clang")
set(CMAKE_CXX_COMPILER "${TOOLCHAIN_ROOT}/bin/clang++")
set(CMAKE_ASM_COMPILER "${TOOLCHAIN_ROOT}/bin/clang")

set(CMAKE_C_COMPILER_TARGET   ${TOOLCHAIN_TRIPLE})
set(CMAKE_CXX_COMPILER_TARGET ${TOOLCHAIN_TRIPLE})
set(CMAKE_ASM_COMPILER_TARGET ${TOOLCHAIN_TRIPLE})

# This toolchain does not bundle a target sysroot; the caller supplies one with
# -DCMAKE_SYSROOT=... A sysroot dropped at the conventional sysroots/<subdir> path
# is used if present, otherwise configuration fails loudly rather than pointing at
# a nonexistent directory.
if(NOT DEFINED CMAKE_SYSROOT)
    if(EXISTS "${TOOLCHAIN_ROOT}/sysroots/${TOOLCHAIN_SYSROOT_SUBDIR}")
        set(CMAKE_SYSROOT "${TOOLCHAIN_ROOT}/sysroots/${TOOLCHAIN_SYSROOT_SUBDIR}")
    else()
        message(FATAL_ERROR "No target sysroot found. Pass -DCMAKE_SYSROOT=/path/to/${TOOLCHAIN_TRIPLE}-sysroot -- this toolchain does not bundle a sysroot.")
    endif()
endif()

# Arch compile flags (if any) plus libc++ headers on the C++ compile line.
set(CMAKE_C_FLAGS_INIT   "${TOOLCHAIN_ARCH_C_FLAGS}")
set(CMAKE_CXX_FLAGS_INIT "${TOOLCHAIN_ARCH_CXX_FLAGS} --stdlib=libc++")

# Fully static executables: -static links libc and every runtime statically, so
# the result has no dynamic linker and no shared-library dependencies. -static is
# applied to executables only; shared and module libraries are inherently dynamic.
# -fuse-ld=lld, --rtlib=compiler-rt and -unwindlib=libunwind are language-agnostic.
# The unwinder is base runtime here, not a C++-only dependency: glibc's own libc
# uses it -- libc's .cold error paths call _Unwind_Resume, and libc references
# __gcc_personality_v0, which pulls compiler-rt's personality routine that in turn
# needs _Unwind_*. A static link pulls those libc.a objects into the binary, so
# even a pure-C static executable needs libunwind. In a dynamic link those
# references live inside libc.so.6 (resolved when glibc itself was built) and never
# reach the user binary -- which is why the dynamic base keeps -unwindlib C++-only.
set(CMAKE_EXE_LINKER_FLAGS_INIT    "-fuse-ld=lld --rtlib=compiler-rt -unwindlib=libunwind -static")
set(CMAKE_SHARED_LINKER_FLAGS_INIT "-fuse-ld=lld --rtlib=compiler-rt -unwindlib=libunwind")
set(CMAKE_MODULE_LINKER_FLAGS_INIT "-fuse-ld=lld --rtlib=compiler-rt -unwindlib=libunwind")

# C++-only runtime, gated by link language. This toolchain's libc++ does not pull
# libc++abi automatically, and the static archives libc++/libc++abi/libunwind have
# mutual references, so they are linked as a group to make resolution order-
# independent. CMake appends this after the user's libraries, the correct position
# for the runtime. (--stdlib=libc++ stays so the driver sets up libc++ search paths.)
set(CMAKE_CXX_STANDARD_LIBRARIES "--stdlib=libc++ -Wl,--start-group -lc++ -lc++abi -lunwind -Wl,--end-group")

set(CMAKE_FIND_ROOT_PATH_MODE_PROGRAM NEVER)
set(CMAKE_FIND_ROOT_PATH_MODE_LIBRARY ONLY)
set(CMAKE_FIND_ROOT_PATH_MODE_INCLUDE ONLY)
set(CMAKE_FIND_ROOT_PATH_MODE_PACKAGE ONLY)
