#!/usr/bin/env bash
# =============================================================================
# run-tests.sh
#
# Builds and runs bootstrap compiler tests to verify the bootstrap Clang
# install is working correctly. All tests target the host (x86_64) and
# are run natively.
#
# Usage:
#   ./run-tests.sh <bootstrap-install-dir> <build-dir> [musl-loader]
#
#   bootstrap-install-dir  Path to the bootstrap Clang install
#   build-dir              Directory to write compiled test binaries
#   musl-loader            Path to ld-musl-x86_64.so.1 (optional, for musl builds)
# =============================================================================
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

usage() {
    echo "Usage: $0 <bootstrap-install-dir> <build-dir> [musl-loader]"
    exit 1
}

[[ $# -lt 2 ]] && usage

INSTALL_DIR="$1"
BUILD_DIR="$2"
MUSL_LOADER="${3:-}"
SYSROOT="${4:-}"

CLANG="${INSTALL_DIR}/bin/clang++"
[[ -x "${CLANG}" ]] || { echo "ERROR: clang++ not found at ${CLANG}"; exit 1; }

SYSROOT_FLAG=""
[[ -n "${SYSROOT}" ]] && SYSROOT_FLAG="--sysroot=${SYSROOT}"

log()  { echo "[$(date '+%H:%M:%S')] $*"; }
pass() { echo "  ✓ PASS: $*"; }
fail() { echo "  ✗ FAIL: $*"; FAILURES=$((FAILURES + 1)); }

FAILURES=0
TOTAL=0
mkdir -p "${BUILD_DIR}"

log "Bootstrap compiler: $("${CLANG}" --version | head -1)"
log "Test directory:     ${SCRIPT_DIR}"
log "Build directory:    ${BUILD_DIR}"
[[ -n "${MUSL_LOADER}" ]] && log "Musl loader:        ${MUSL_LOADER}"
log ""

# Build --dynamic-linker flag if musl loader provided
DYNLINKER_FLAG=""
if [[ -n "${MUSL_LOADER}" && -f "${MUSL_LOADER}" ]]; then
    DYNLINKER_FLAG="-Wl,--dynamic-linker,${MUSL_LOADER}"
fi

for src in "${SCRIPT_DIR}"/*.cpp; do
    name="$(basename "${src}" .cpp)"
    TOTAL=$((TOTAL + 1))
    bin="${BUILD_DIR}/${name}"

    SYSROOT_LIB_FLAG=""
    [[ -n "${SYSROOT}" ]] && SYSROOT_LIB_FLAG="-L${SYSROOT}/usr/lib"

    # Compile
    if ! "${CLANG}" \
        -std=c++17 \
        -O2 \
        -stdlib=libc++ \
        -fuse-ld=lld \
        -rtlib=compiler-rt \
        -unwindlib=none \
        ${SYSROOT_FLAG} \
        -Wl,-Bstatic -lc++ -lc++abi -lunwind \
        -Wl,--strip-debug \
        -o "${bin}" \
        "${src}" \
        ${DYNLINKER_FLAG} \
        2>"${BUILD_DIR}/${name}.compile.log"; then
        fail "${name} — compile error"
        cat "${BUILD_DIR}/${name}.compile.log" | sed 's/^/    /'
        continue
    fi

    # Run
    if ! "${bin}" >"${BUILD_DIR}/${name}.run.log" 2>&1; then
        fail "${name} — runtime error (exit $?)"
        cat "${BUILD_DIR}/${name}.run.log" | sed 's/^/    /'
        continue
    fi

    pass "${name}"
done

log ""
log "Results: $((TOTAL - FAILURES))/${TOTAL} passed"

if [[ $FAILURES -gt 0 ]]; then
    log "FAILED: ${FAILURES} test(s) failed"
    exit 1
else
    log "All tests passed."
    exit 0
fi
