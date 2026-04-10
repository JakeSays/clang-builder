namespace Std.BuildTools.Clang;

internal class StaticPythonBuilder
{
    public async Task<bool> Build(FilePath outputDir, string pyVersion)
    {
        var dockerfile = BuildDockerfile(pyVersion);

        var dockerfilePath = Path.Combine(Path.GetTempPath(), $"Dockerfile.staticpy.{Guid.NewGuid():N}");
        await File.WriteAllTextAsync(dockerfilePath, dockerfile);

        try
        {
            Log.Info($"Building Alpine container to compile static Python {pyVersion}...");
            var buildArgs = $"build -t alpine-static-python -f \"{dockerfilePath}\" .";
            if (await ProcessRunner.Run("docker", buildArgs) != 0)
            {
                Log.Error("ERROR: docker build failed");
                return false;
            }

            Log.Info("Creating container to extract artifacts...");
            var (createExit, containerId) = await ProcessRunner.GetOutput("docker", "create alpine-static-python");
            if (createExit != 0 || string.IsNullOrWhiteSpace(containerId))
            {
                Log.Error("ERROR: docker create failed");
                return false;
            }

            containerId = containerId.Trim();

            if (outputDir.Exists)
            {
                FileUtils.DeleteDirectory(outputDir);
            }
            Directory.CreateDirectory(outputDir);

            Log.Info("Extracting libpython and headers from container...");
            if (await ProcessRunner.Run("docker", $"cp \"{containerId}:/staging/usr/local/include\" \"{outputDir}\"") != 0)
            {
                await RemoveContainer(containerId);
                return false;
            }
            if (await ProcessRunner.Run("docker", $"cp \"{containerId}:/staging/usr/local/lib\" \"{outputDir}\"") != 0)
            {
                await RemoveContainer(containerId);
                return false;
            }

            await RemoveContainer(containerId);

            Log.Info(LogColor.Green, $"Static Python {pyVersion} ready at: {outputDir}");
            return true;
        }
        finally
        {
            File.Delete(dockerfilePath);
        }
    }

    private static async Task RemoveContainer(string containerId)
    {
        await ProcessRunner.Run("docker", $"rm -v {containerId}");
    }

    private static string BuildDockerfile(string pyVersion) => $"""
        FROM alpine:latest

        RUN apk add --no-cache \
            build-base \
            linux-headers \
            wget \
            tar \
            xz \
            xz-dev \
            xz-static \
            zlib-dev \
            zlib-static \
            openssl-dev \
            openssl-libs-static \
            bzip2-dev \
            bzip2-static \
            sqlite-dev \
            sqlite-static \
            libffi-dev \
            expat-dev \
            expat-static \
            readline-dev \
            readline-static \
            ncurses-dev \
            ncurses-static \
            gdbm-dev \
            util-linux-dev

        WORKDIR /build
        RUN wget https://www.python.org/ftp/python/{pyVersion}/Python-{pyVersion}.tar.xz
        RUN tar -xf Python-{pyVersion}.tar.xz

        WORKDIR /build/Python-{pyVersion}

        RUN ./configure \
            --disable-shared \
            --with-system-ffi \
            --with-system-expat \
            --without-ensurepip \
            --disable-test-modules

        RUN sed -i 's/\*shared\*/\*static\*/g' Modules/Setup.stdlib

        RUN make -j$(nproc)

        RUN make install DESTDIR=/staging

        RUN set -e; \
            LIBPY=$(find /staging -name "libpython*.a" | head -1); \
            BUILD_DIR="/build/Python-{pyVersion}"; \
            for obj in $(find "$BUILD_DIR/Modules/_hacl" -name "Hacl_Hash_*.o" 2>/dev/null); do \
                name=$(basename "$obj"); \
                if ! ar t "$LIBPY" | grep -qx "$name"; then \
                    echo "[*] Adding missing HACL object to archive: $name"; \
                    ar r "$LIBPY" "$obj"; \
                fi; \
            done
        """;
}
