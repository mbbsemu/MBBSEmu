#!/bin/bash
# Build in Docker SDK container (faster than rebuilding the full image)
cd "$(dirname "$0")/.."
docker run --rm -v "$(pwd)":/src -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet build "$@"
