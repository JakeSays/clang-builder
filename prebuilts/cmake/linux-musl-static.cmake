# Shared base for fully-static (100% static, no dynamic linker) musl Linux cross
# targets. An arch/libc leaf file sets the parameters below and then include()s
# this file:
#   TOOLCHAIN_PROCESSOR        value for CMAKE_SYSTEM_PROCESSOR (e.g. x86_64, aarch64)
#   TOOLCHAIN_TRIPLE           canonical clang triple, e.g. x86_64-unknown-linux-musl
#                              (must be the "unknown" form -- it keys the lib-musl
#                              and include subdirectory names)
#   TOOLCHAIN_SYSROOT_SUBDIR   subdirectory under sysroots/ (e.g. x64-musl)
#   TOOLCHAIN_ARCH_C_FLAGS     optional arch compile flags for C
#   TOOLCHAIN_ARCH_CXX_FLAGS   optional arch compile flags for C++
#
# See linux-musl-dynamic.cmake for the musl libc++ header/lib rationale. This file
# adds -static (fully static executables) and links the static libc++/libc++abi/
# libunwind archives as a group so their mutual references resolve order-
# independently. -unwindlib=libunwind is language-agnostic here (see the glibc
# linux-static.cmake comment: a static libc pulls the personality routine that
# needs _Unwind_*, so even pure-C static executables require the unwinder).
if(NOT DEFINED TOOLCHAIN_TRIPLE)
    message(FATAL_ERROR "Use an arch/libc toolchain file; do not pass this base file directly.")
endif()

get_filename_component(TOOLCHAIN_ROOT "${CMAKE_CURRENT_LIST_DIR}/.." ABSOLUTE)
set(TOOLCHAIN_MUSL_LIB "${TOOLCHAIN_ROOT}/lib-musl/lib/${TOOLCHAIN_TRIPLE}")
set(TOOLCHAIN_MUSL_INC "${TOOLCHAIN_ROOT}/lib-musl/include")

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

set(CMAKE_CXX_FLAGS_INIT "${TOOLCHAIN_ARCH_CXX_FLAGS} -nostdinc++ -isystem ${TOOLCHAIN_MUSL_INC}/c++/v1 -isystem ${TOOLCHAIN_MUSL_INC}/${TOOLCHAIN_TRIPLE}/c++/v1 -isystem ${TOOLCHAIN_MUSL_INC} -D_LIBCPP_PROVIDES_DEFAULT_RUNE_TABLE")
set(CMAKE_C_FLAGS_INIT   "${TOOLCHAIN_ARCH_C_FLAGS} -isystem ${TOOLCHAIN_MUSL_INC}")

set(CMAKE_EXE_LINKER_FLAGS_INIT    "-fuse-ld=lld --rtlib=compiler-rt -unwindlib=libunwind -static -L${TOOLCHAIN_MUSL_LIB}")
set(CMAKE_SHARED_LINKER_FLAGS_INIT "-fuse-ld=lld --rtlib=compiler-rt -unwindlib=libunwind -L${TOOLCHAIN_MUSL_LIB}")
set(CMAKE_MODULE_LINKER_FLAGS_INIT "-fuse-ld=lld --rtlib=compiler-rt -unwindlib=libunwind -L${TOOLCHAIN_MUSL_LIB}")

# C++-only runtime, gated by link language. Grouped so the static libc++/libc++abi/
# libunwind archives resolve order-independently. (-stdlib=libc++ stays so the
# driver sets up libc++; -L above lets these -l names resolve to lib-musl archives.)
set(CMAKE_CXX_STANDARD_LIBRARIES "-stdlib=libc++ -Wl,--start-group -lc++ -lc++abi -lunwind -Wl,--end-group")

set(CMAKE_FIND_ROOT_PATH_MODE_PROGRAM NEVER)
set(CMAKE_FIND_ROOT_PATH_MODE_LIBRARY ONLY)
set(CMAKE_FIND_ROOT_PATH_MODE_INCLUDE ONLY)
set(CMAKE_FIND_ROOT_PATH_MODE_PACKAGE ONLY)
