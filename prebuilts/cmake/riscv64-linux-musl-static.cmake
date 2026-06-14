set(TOOLCHAIN_PROCESSOR riscv64)
set(TOOLCHAIN_TRIPLE riscv64-unknown-linux-musl)
set(TOOLCHAIN_SYSROOT_SUBDIR riscv64-musl)
include("${CMAKE_CURRENT_LIST_DIR}/linux-musl-static.cmake")
