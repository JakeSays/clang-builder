# Shared base for dynamic-linking Linux cross targets. An arch/libc leaf file sets
# the parameters below and then include()s this file:
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

# Language-agnostic link flags: lld and the compiler-rt builtins runtime are
# needed for C and C++ alike (compiler-rt replaces libgcc, which the compiler
# emits calls into for any language).
set(CMAKE_EXE_LINKER_FLAGS_INIT    "-fuse-ld=lld --rtlib=compiler-rt")
set(CMAKE_SHARED_LINKER_FLAGS_INIT "-fuse-ld=lld --rtlib=compiler-rt")
set(CMAKE_MODULE_LINKER_FLAGS_INIT "-fuse-ld=lld --rtlib=compiler-rt")

# C++-only runtime, gated by link language: only CXX-linked targets pull
# libc++/libunwind; pure-C targets never see these. CMake places them at the end
# of the link line, after the user's libraries -- the correct spot for the runtime.
set(CMAKE_CXX_STANDARD_LIBRARIES "--stdlib=libc++ -unwindlib=libunwind")

set(CMAKE_FIND_ROOT_PATH_MODE_PROGRAM NEVER)
set(CMAKE_FIND_ROOT_PATH_MODE_LIBRARY ONLY)
set(CMAKE_FIND_ROOT_PATH_MODE_INCLUDE ONLY)
set(CMAKE_FIND_ROOT_PATH_MODE_PACKAGE ONLY)
