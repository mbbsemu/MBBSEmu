# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

MBBSEmu is a cross-platform emulator for running legacy MajorBBS and Worldgroup modules (16-bit NE format DLLs) on modern systems. It emulates both the Galacticomm host environment and an x86-16 processor. Written in C# targeting .NET 10.

## Build & Test Commands

```bash
# Build
dotnet build

# Run all tests
dotnet test

# Run a specific test class
dotnet test --filter "ClassName=ADD_Tests"

# Run a specific test method
dotnet test --filter "FullyQualifiedName~MBBSEmu.Tests.CPU.ADD_Tests.ADD_AL_IMM8"

# Publish for a specific platform (e.g., osx-arm64)
dotnet publish MBBSEmu/MBBSEmu.csproj --configuration Release --runtime osx-arm64 --self-contained true -p:PublishSingleFile=true
```

## Docker

Docker provides a consistent build/test/runtime environment without needing a local .NET SDK.

```bash
# Build and run the service
cd docker && docker compose up --build       # foreground (see logs)
cd docker && docker compose up --build -d    # detached
telnet localhost 2323                        # connect

# Stop the service
cd docker && docker compose down

# Fast iteration (no image rebuild needed)
docker/build.sh                              # build only
docker/test.sh                               # run all tests
docker/test.sh --filter "FullyQualifiedName~ADD_Tests"  # filtered tests

# Interactive shell in running container
docker/shell.sh
```

Config and databases are stored in `docker/config/` (bind-mounted to `/config` in the container). The Dockerfile uses a multi-stage build: build → test → publish → runtime.

## Solution Structure

- **MBBSEmu/** - Main executable: emulation engine, host process, exported modules
- **MBBSEmu.Tests/** - xUnit tests with FluentAssertions
- **MBBSEmu.CPU.Benchmark/** - CPU emulation performance benchmarks
- **MBBSDatabase/** - Database utility tool

## Architecture

### Core Emulation Pipeline

1. **Module Loading** (`Module/`): Parses NE-format DLLs, MDF (module definition), MSG (messages), MCV (config values). Each module gets a full 4GB virtual address space.
2. **CPU Emulation** (`CPU/`): `CpuCore` executes x86-16 instructions. Uses the `Iced` library for disassembly. Includes x87 FPU support.
3. **Memory** (`Memory/`): `ProtectedModeMemoryCore` provides per-module 4GB address space. `FarPtr` represents segment:offset pointers. Memory is lazily allocated.
4. **Host Process** (`HostProcess/`): `MbbsHost` manages module lifecycle, session scheduling, and execution units. `ExecutionUnit` wraps CPU contexts for concurrent module operations.
5. **Exported Modules** (`HostProcess/ExportedModules/`): C# implementations of MajorBBS/Worldgroup APIs that modules call via imported ordinals. Each mapped to a virtual segment:
   - `Majorbbs` (0xFFFF), `Galgsbl` (0xFFFE), `Phapi` (0xFFFD), `Galme` (0xFFFC), `Doscalls` (0xFFFB), `Galmsg` (0xFFFA)
6. **Sessions** (`Session/`): `SessionBase` subclasses handle different connection types (Telnet, Rlogin, local console). Sessions are assigned channel numbers and manage user I/O buffers.

### Key Subsystems

- **Btrieve** (`Btrieve/`): Database engine emulation backed by SQLite. `BtrieveFileProcessor` handles CRUD operations with key-based access.
- **DOS Interrupts** (`DOS/`): Handlers for INT 21h (file I/O), INT 10h (video), INT 1Ah (time), and MajorBBS-specific interrupts.
- **DI Container** (`DependencyInjection/ServiceResolver.cs`): Custom wrapper around `Microsoft.Extensions.DependencyInjection`. Most services are singletons.
- **Logging** (`Logging/`): `LogFactory` manages `MessageLogger`, `AuditLogger`, `CPULogger` with pluggable targets.

### Memory Map (per module)

| Range | Purpose |
|---|---|
| 0x0000:0x0000 - 0x0000:0xFFFF | CPU Stack (65KB) |
| 0x0001:0x0000 - 0x0FFF:0xFFFF | Code/Data Segments (~256MB) |
| 0x1000:0x0000 - 0x1FFF:0xFFFF | Heap Data (256MB) |
| 0x2000:0x0000 - 0x2FFF:0xFFFF | Real Mode Data (256MB) |
| 0x3000:0x0000 - 0x3001:0x00C0 | Btrieve Structs |
| 0x4000:0x0000 - 0x4000:0xFFFF | PSP for INT 21h |

## Testing Patterns

- **CPU tests** (`Tests/CPU/`): One file per instruction (e.g., `ADD_Tests.cs`). Extend `CpuTestBase` which provides `mbbsEmuCpuCore`, `mbbsEmuMemoryCore`, `mbbsEmuCpuRegisters`.
- **Exported module tests** (`Tests/ExportedModules/`): Test the C# API implementations. Extend `ExportedModuleTestBase`.
- **Integration tests** (`Tests/Integration/`): Full host process tests. Extend `MBBSEmuIntegrationTestBase`.
- **Test assets**: Embedded resources in `Tests/Assets/` (DLL, DAT, MDF, MSG, DB files).

## Important Notes

- `AllowUnsafeBlocks` is enabled — unsafe code is expected for low-level memory/CPU emulation.
- Entry point is `MBBSEmu/Program.cs` with CLI argument parsing (-M for module, -P for path, -C for config, etc.).
- CI runs on GitHub Actions (.github/workflows/dotnet.yml) building for 7 platform targets.
