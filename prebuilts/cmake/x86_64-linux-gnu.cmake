get_filename_component(TOOLCHAIN_ROOT "${CMAKE_CURRENT_LIST_DIR}/.." ABSOLUTE)

set(CMAKE_SYSTEM_NAME Linux)
set(CMAKE_SYSTEM_PROCESSOR x86_64)

set(CMAKE_C_COMPILER   "${TOOLCHAIN_ROOT}/bin/clang")
set(CMAKE_CXX_COMPILER "${TOOLCHAIN_ROOT}/bin/clang++")
set(CMAKE_ASM_COMPILER "${TOOLCHAIN_ROOT}/bin/clang")

set(CMAKE_C_COMPILER_TARGET   x86_64-unknown-linux-gnu)
set(CMAKE_CXX_COMPILER_TARGET x86_64-unknown-linux-gnu)
set(CMAKE_ASM_COMPILER_TARGET x86_64-unknown-linux-gnu)

if(NOT DEFINED CMAKE_SYSROOT)
    set(CMAKE_SYSROOT "${TOOLCHAIN_ROOT}/sysroots/x64")
endif()

set(CMAKE_CXX_FLAGS_INIT "--stdlib=libc++")

set(CMAKE_EXE_LINKER_FLAGS_INIT    "-fuse-ld=lld --rtlib=compiler-rt --stdlib=libc++ -unwindlib=libunwind")
set(CMAKE_SHARED_LINKER_FLAGS_INIT "-fuse-ld=lld --rtlib=compiler-rt --stdlib=libc++ -unwindlib=libunwind")
set(CMAKE_MODULE_LINKER_FLAGS_INIT "-fuse-ld=lld --rtlib=compiler-rt --stdlib=libc++ -unwindlib=libunwind")

set(CMAKE_FIND_ROOT_PATH_MODE_PROGRAM NEVER)
set(CMAKE_FIND_ROOT_PATH_MODE_LIBRARY ONLY)
set(CMAKE_FIND_ROOT_PATH_MODE_INCLUDE ONLY)
set(CMAKE_FIND_ROOT_PATH_MODE_PACKAGE ONLY)
