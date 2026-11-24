# Build environment for the Build Clang Toolchain workflow. Every host tool its jobs run comes from
# this image, so a change to GitHub's runner images cannot change them.

# The snapshot archive is served only over HTTPS and the base image has no CA bundle. This stage
# installs ca-certificates only to copy its bundle into the final image, where installing
# ca-certificates from the snapshot rewrites that bundle.
FROM ubuntu@sha256:562111b88caa79c71391c0c4d3c2114a7d5c35915c20561fd893cb1e07929e6b AS certificates

RUN apt-get update \
    && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends ca-certificates

FROM ubuntu@sha256:562111b88caa79c71391c0c4d3c2114a7d5c35915c20561fd893cb1e07929e6b

LABEL org.opencontainers.image.source=https://github.com/JakeSays/clang-builder

COPY --from=certificates /etc/ssl/certs/ca-certificates.crt /etc/ssl/certs/ca-certificates.crt

# --snapshot reads the archive as it was at one moment, so rebuilding the image installs the same
# package versions. python3 is 3.12, the version of the host sysroot's libpython. build-essential,
# python3-dev, libxml2-dev, zlib1g-dev and libncurses-dev are for the native tablegen sub-build,
# which stage1 configures with the host compiler.
RUN apt-get update --snapshot 20261124T000000Z \
    && DEBIAN_FRONTEND=noninteractive apt-get install --snapshot 20261124T000000Z -y --no-install-recommends \
        build-essential \
        ca-certificates \
        cmake \
        curl \
        gh \
        git \
        git-lfs \
        libncurses-dev \
        libxml2-dev \
        ninja-build \
        patchelf \
        python3 \
        python3-dev \
        qemu-user \
        rsync \
        swig \
        xz-utils \
        zlib1g-dev \
        zstd \
    && rm -rf /var/lib/apt/lists/*

# Jobs run as root in the container, while the workspace on the host belongs to the runner user.
RUN git config --system --add safe.directory '*'
