# ============================================================================
# Multi-stage Dockerfile for MBBSEmu
# - Build and test with pinned .NET SDK version
# - Runtime uses LinuxServer.io base with s6-overlay
# ============================================================================

# ============================================================================
# Stage 1: BUILD - Restore and compile
# ============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

ARG BUILD_VERSION=dev
WORKDIR /src

# Copy solution and project files first for layer caching
COPY MBBSEmu.sln ./
COPY MBBSEmu/MBBSEmu.csproj MBBSEmu/
COPY MBBSEmu.Tests/MBBSEmu.Tests.csproj MBBSEmu.Tests/
COPY MBBSDatabase/MBBSDatabase.csproj MBBSDatabase/
COPY MBBSEmu.CPU.Benchmark/MBBSEmu.CPU.Benchmark.csproj MBBSEmu.CPU.Benchmark/

# Restore dependencies (cached if .csproj files unchanged)
RUN dotnet restore

# Copy remaining source code
COPY . .

# Write version file
RUN echo "${BUILD_VERSION}" > MBBSEmu/Assets/version.txt

# Build in Release mode
RUN dotnet build --no-restore --configuration Release


# ============================================================================
# Stage 2: TEST - Run tests (fails build if tests fail)
# ============================================================================
FROM build AS test

RUN dotnet test --no-build --configuration Release --verbosity normal


# ============================================================================
# Stage 3: PUBLISH - Create self-contained binary for target architecture
# ============================================================================
FROM build AS publish

# TARGETARCH is set by Docker BuildKit (amd64, arm64, etc.)
ARG TARGETARCH

# Map Docker architecture to .NET runtime identifier
RUN case "$TARGETARCH" in \
        amd64) DOTNET_RID="linux-x64" ;; \
        arm64) DOTNET_RID="linux-arm64" ;; \
        *) echo "ERROR: Unsupported architecture: $TARGETARCH" && exit 1 ;; \
    esac; \
    dotnet publish MBBSEmu/MBBSEmu.csproj \
        --output /app/publish \
        --configuration Release \
        --runtime $DOTNET_RID \
        --self-contained true \
        -p:PublishSingleFile=true \
        -p:IncludeNativeLibrariesForSelfExtract=true \
        -p:IncludeNativeLibrariesInSingleFile=true \
        -p:PublishTrimmed=false \
        -p:CopyOutputSymbolsToPublishDirectory=false


# ============================================================================
# Stage 4: RUNTIME - LinuxServer.io base with s6-overlay
# ============================================================================
FROM lsiobase/ubuntu:jammy AS runtime

ARG BUILD_DATE
ARG BUILD_VERSION
LABEL build_version="MBBSEmu version:- ${BUILD_VERSION} Build-date:- ${BUILD_DATE}"
LABEL maintainer="mbbsemu"
LABEL org.opencontainers.image.source="https://github.com/mbbsemu/MBBSEmu"

ENV CONFIG_PATH=/config
ENV DEBIAN_FRONTEND=noninteractive
ENV DOTNET_BUNDLE_EXTRACT_BASE_DIR=/tmp
ENV EMULATOR_PATH=/app

# Install runtime dependencies (libicu for .NET globalization)
RUN apt-get update && \
    apt-get install -qy --no-install-recommends libicu70 && \
    rm -rf /var/lib/apt/lists/*

# Copy s6-overlay service configurations
COPY docker/root/ /

# Copy published application from publish stage
COPY --from=publish /app/publish/MBBSEmu ${EMULATOR_PATH}/MBBSEmu

# Copy docker-specific appsettings with correct paths
COPY docker/appsettings.docker.json ${EMULATOR_PATH}/appsettings.json

# Ensure binary is executable
RUN chmod a+x ${EMULATOR_PATH}/MBBSEmu

# Expose default telnet port
EXPOSE 23

VOLUME ${CONFIG_PATH}
WORKDIR ${CONFIG_PATH}
