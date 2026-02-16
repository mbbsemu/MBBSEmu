# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

This is the main MBBSEmu project — a cross-platform emulator for 16-bit MajorBBS/Worldgroup modules. It loads legacy NE-format DLLs, emulates an x86-16 CPU, and implements the Galacticomm SDK APIs in C# so that decades-old BBS modules can run on modern systems.

The project uses `AllowUnsafeBlocks` and `LangVersion=latest`. Unsafe code is expected and normal for low-level memory and CPU emulation.

## Directory Map

```
MBBSEmu/
├── CPU/                    # x86-16 processor emulation
├── Memory/                 # Segmented memory model (4GB per module)
├── HostProcess/            # Main event loop, exported SDK APIs, execution units
│   ├── ExportedModules/    #   C# implementations of MAJORBBS, GALGSBL, etc.
│   ├── ExecutionUnits/     #   CPU execution contexts
│   ├── HostRoutines/       #   Built-in menu & FSE (Full Screen Editor) routines
│   └── GlobalRoutines/     #   System-wide commands (page user, users online, sysop)
├── Module/                 # Module loading: MDF, MSG, MCV, DLL, patches
├── Session/                # User sessions: Telnet, Rlogin, local console, test
│   ├── Telnet/             #   Telnet protocol + IAC negotiation
│   ├── Rlogin/             #   Rlogin protocol
│   └── LocalConsole/       #   Local console session
├── Btrieve/                # Btrieve database emulation backed by SQLite
├── DOS/                    # DOS environment emulation
│   └── Interrupts/         #   INT 21h, 10h, 1Ah, 3Eh, 7Bh handlers
├── Disassembler/           # MZ and NE file format parsers
├── Server/                 # Async TCP socket server
├── Database/               # SQLite account/key repositories (Dapper)
├── DependencyInjection/    # ServiceResolver (wraps MS DI)
├── Logging/                # Multi-target logging (console, queue, CPU trace)
├── TextVariables/          # Dynamic text substitution system
├── Extensions/             # Utility extension methods
├── BIOS/                   # PIT (Programmable Interval Timer) emulation
├── IO/                     # Cross-platform file utilities, stream abstractions
├── Converters/             # JSON converters (bool, FarPtr, module config)
├── Resources/              # Embedded resource manager
├── UI/                     # Terminal.Gui interface (MainView, SetupView)
├── Util/                   # MessagingCenter, CRC32, LRU cache
├── Date/                   # IClock abstraction (SystemClock, FakeClock)
├── Reports/                # API usage report generation
├── Assets/                 # Embedded resources (DB templates, ANSI art, SQL, help text)
└── Program.cs              # Entry point and CLI argument handling
```

## CPU Emulation (`CPU/`)

The CPU is the innermost loop of the emulator. Understanding it is critical.

**Inheritance chain:** `CpuCore` → `CpuRegisters` → `ICpuRegisters, IFpuRegisters` → `ICpuCore`

### CpuCore.cs (~5,000 lines)

The main execution engine. The `Tick()` method is the hot loop:
1. Fetch instruction from memory via `Memory.GetInstruction(CS, IP)` (uses the Iced library for decoding)
2. Dispatch via a large `switch` on `Mnemonic` to `Op_XXX()` handler methods
3. Control-flow instructions (jumps, calls, returns) modify IP and return early
4. All other instructions fall through to `IP += instruction.Length`

**Instruction handler pattern:** Each `Op_XXX()` dispatches by operand size to `Op_XXX_8()`, `Op_XXX_16()`, or `Op_XXX_32()`. These extract operands via `GetOperandValueUInt8/16/32()`, compute the result, update flags via `Flags_EvaluateCarry/Overflow/SignZero()`, and write back via `WriteToDestination()`.

**Key members:**
- `_currentInstruction` — the Iced `Instruction` being executed
- `_currentOperationSize` — 1, 2, or 4 byte operation width
- `_invokeExternalFunctionDelegate` — callback fired when CPU hits a `CALL FAR` to segment >= 0xFF00 (an exported module call)
- `_interruptHandlers` — `Dictionary<int, IInterruptHandler>` for INT instructions
- `FpuStack` — `double[8]` array for x87 FPU, indexed via StatusWord bits 11-13

**Stack:** Segment 0x0000, SP starts at 0xFFFE, grows downward. `uint.MaxValue` is pushed as an end-of-execution sentinel.

**Performance:** Uses `MethodImplOptions.AggressiveOptimization` on hot paths. Comment notes that aggressive inlining is *slower* here due to L1 cache pressure.

### CpuRegistersStruct (RegistersStructs.cs)

Uses `[StructLayout(LayoutKind.Explicit)]` to create x86-style register aliasing — EAX, AX, AH, AL all overlap the same memory at different offsets. Individual flag bools (CarryFlag, ZeroFlag, etc.) are stored separately and assembled/disassembled to/from the FLAGSƒ register via `F()` / `SetF()`.

The FPU registers struct maintains a circular stack via `GetStackTop()`/`PushStackTop()`/`PopStackTop()` with 3-bit stack pointer in StatusWord.

### Key types
- `FarPtr` — Segment:Offset pointer (4 bytes). Used everywhere. Constructable from bytes, `"SSSS:OOOO"` strings, or segment+offset ushorts.
- `EnumOperandType` — Destination, Source, Count
- `IIOPort` — Interface for `IN`/`OUT` instruction handlers

## Memory System (`Memory/`)

Each loaded module gets its own full 16-bit address space (up to 4GB, lazily allocated).

### ProtectedModeMemoryCore

The primary memory implementation. Stores segments as `byte[0x10000][]` — an array of 65,536 possible segments, each up to 65,535 bytes. Also maintains parallel arrays for segment metadata and decompiled instructions (cached Iced `Instruction[]`).

**Memory regions:**
| Segment Range | Purpose |
|---|---|
| 0x0000 | CPU stack (always allocated) |
| 0x0001–0x0FFF | Code/data segments (loaded from DLL) |
| 0x1000–0x1FFF | Heap (malloc/free via MemoryAllocator) |
| 0x2000–0x2FFF | Real mode data |
| 0x3000–0x3001 | Btrieve structs (GENBB, ACCBB) |
| 0x4000 | PSP for INT 21h |

### IMemoryCore interface

Three access patterns, all overloaded for segment:offset, FarPtr, and variable-name:
- **Get:** `GetByte`, `GetWord`, `GetDWord`, `GetArray`, `GetString`, `GetPointer`, `GetInstruction`
- **Set:** `SetByte`, `SetWord`, `SetDWord`, `SetArray`, `SetPointer`, `FillArray`
- **Allocate:** `AllocateVariable` (named, tracked by name), `Malloc`/`Free` (heap), `AllocateRealModeSegment`, `AllocateBigMemoryBlock`

### MemoryAllocator

A malloc/free implementation using a linked list of free blocks and a ConcurrentDictionary of allocated blocks. Each heap segment gets its own allocator. When no segment has room, a new heap segment is created.

### Named Variables

`AllocateVariable("USRPTR", size, declarePointer: true)` allocates memory and tracks it by name. The `declarePointer` flag also creates a `"*USRPTR"` variable holding a pointer to the data. Variables are retrieved by name via `GetVariablePointer("USRPTR")`.

## Host Process (`HostProcess/`)

### MbbsHost — The Main Event Loop

`WorkerThread()` is the core loop, processing in classic DOS BBS serial fashion:
1. Accept incoming sessions → assign channel numbers
2. Handle disconnects
3. For each active session:
   - `ProcessDataFromClient()` — read one byte from input
   - `ProcessGSBLInputEvents()` — handle BTUCHI character interceptors
   - `ProcessIncomingCharacter()` — echo handling, buffer management
   - `ProcessSessionState()` — route to appropriate handler based on `EnumSessionState`
4. Fire timed events: `ProcessRTKICK()`, `ProcessRTIHDLR()`, `ProcessSYSCYC()`
5. Sync session status from module memory

**Module registration** (`AddModule`): Creates exported module instances, applies relocation records, loads DLL segments into memory, runs the module's `_INIT_` routine.

**Module execution** (`Run`): Delegates to `MbbsModule.Execute()` which dequeues an `ExecutionUnit`, runs the CPU loop, and returns registers.

### Exported Modules — The SDK Implementation

These are C# implementations of the MajorBBS/Worldgroup SDK libraries. When module code does a `CALL FAR` to a virtual segment, the CPU fires `_invokeExternalFunctionDelegate`, which routes to the appropriate exported module's `Invoke(ordinal)` method.

**Virtual segment mapping:**
| Segment | Module | SDK Library |
|---|---|---|
| 0xFFFF | `Majorbbs.cs` | MAJORBBS — core BBS API (user management, I/O, config, string ops) |
| 0xFFFE | `Galgsbl.cs` | GALGSBL — screen/keyboard/ANSI routines |
| 0xFFFD | `Phapi.cs` | PHAPI — phone/modem APIs |
| 0xFFFC | `Galme.cs` | GALME — module environment |
| 0xFFFB | `Doscalls.cs` | DOSCALLS — DOS interrupt wrappers |
| 0xFFFA | `Galmsg.cs` | GALMSG — message database routines |

**Ordinal dispatch pattern** (in each exported module's `Invoke()` method):
- First switch block: **Property/variable returns** — returns a `FarPtr.Data` pointing to a memory-resident value
- Second switch block: **Method invocations** — calls C# implementations that extract parameters from the x86 stack and manipulate module memory

**ExportedModuleBase** provides common infrastructure:
- `GetParameter(ordinal)` — reads from `SS:[BP + 6 + 2*ordinal]` (x86 stack calling convention)
- `GetParameterPointer(ordinal)` — reads 32-bit far pointer from stack
- `GetParameterString(ordinal)` — reads null-terminated string from memory at pointer
- `FormatPrintf()` — full printf/vsprintf implementation with all format specifiers
- `SetState(channelNumber)` / `UpdateSession(channelNumber)` — context-switch between user sessions

### ExecutionUnit

Encapsulates one CPU execution context. Modules maintain a `Queue<ExecutionUnit>` as an object pool. Each execution:
1. Sets CS:IP to entry point, pushes parameters to stack
2. Calls `SetState()` on all exported modules for the target channel
3. Runs `while (!Halt) Tick()` until RETF/HLT
4. Calls `UpdateSession()` to sync state back

The `ExternalFunctionDelegate` closure bridges CPU → exported modules: looks up module by segment, calls `SetRegisters()` then `Invoke(ordinal)`.

### Host & Global Routines

- **HostRoutines** (`MenuRoutines.cs`, `FsdRoutines.cs`): Built-in functionality for the BBS menu system and Full Screen Editor
- **GlobalRoutines** (`UsersOnlineGlobal.cs`, `PageUserGlobal.cs`, `SysopGlobal.cs`): System-wide commands interceptable from any module

## Module Loading (`Module/`)

### MbbsModule

Container for a loaded module. Holds:
- `ModuleDlls` — list of `MbbsDll` (NE-format parsed DLLs)
- `ProtectedModeMemoryCore` — dedicated memory space
- `ExecutionUnits` — pooled CPU contexts
- `ExportedModuleDictionary` — per-module exported module instances
- `RtkickRoutines`, `RtihdlrRoutines`, `TaskRoutines` — registered real-time callbacks
- `GlobalCommandHandlers` — interceptable command registrations

### MbbsDll / NEFile / MZFile (`Disassembler/`)

NE (New Executable) format parser. Extracts segment table, imported name table, entry points, relocation records. During module loading, relocation records are processed: imported function references are patched with far pointers to virtual segments (e.g., `CALL MAJORBBS.ATOL` becomes `CALL FAR 0xFFFF:0x004D`).

### Supporting parsers
- `MdfFile` — Module Definition File (.MDF): module name, developer, DLL list, MSG files, dependencies
- `MsgFile` — Message file (.MSG): string resources accessed by index
- `McvFile` — Module Configuration Values (.MCV)
- `ModuleConfiguration` — JSON config: module identifier, path, menu key, patches, enabled flag
- `ModulePatch` — Runtime code patches applied to loaded DLL segments

## Session System (`Session/`)

### SessionBase

Abstract base class managing user state and I/O. Key elements:
- **State machine:** `EnumSessionState` (~30 states covering login → module → logoff lifecycle)
- **I/O buffers:** `InputBuffer`, `InputCommand`, `DataToClient` (BlockingCollection output queue), `DataFromClient` (BlockingCollection input queue), `EchoBuffer` (GSBL bypass)
- **User structs:** `UsrPtr`, `UsrAcc`, `ExtUsrAcc` — in-memory representations of MajorBBS user data structures
- **Session variables:** Dictionary of delegates for dynamic text substitution
- **LineBreaker:** Word-wrapping with ANSI escape sequence awareness (state machine parser that avoids breaking mid-escape)

### Protocol implementations
- **TelnetSession** — RFC 854 Telnet with IAC negotiation, option tracking, window size detection, 0xFF escaping
- **RloginSession** — Rlogin protocol with username extraction and module-specific entry routing
- **SocketSession** (abstract) — Base for socket sessions. Async receive via `BeginReceive`/`EndReceive`, dedicated sender thread processing `DataToClient` queue, optional heartbeat

### Threading model
Each socket session has: (1) async receive callback, (2) dedicated send worker thread. The `MbbsHost.WorkerThread()` processes sessions synchronously in a loop, matching the original single-threaded DOS BBS model.

## Btrieve Database (`Btrieve/`)

Emulates the Pervasive Btrieve database engine using SQLite as the backend.

### BtrieveFileProcessor (~48KB, largest file)

Converts legacy `.DAT` files to SQLite `.DB` on first access (or when `.DAT` is newer). Implements all Btrieve operation codes: OPEN, CLOSE, INSERT, UPDATE, DELETE, GET (by key, by position, first, last, next, previous).

Key features:
- LRU cache for record lookups (configurable via `Btrieve.CacheSize`)
- ConcurrentDictionary of prepared SqliteCommand objects
- Auto-increment key tracking
- Database versioning (v1→v2 upgrade path)
- ACS (Alternative Character Set) support for case-insensitive key comparisons

### BtrieveKey / BtrieveKeyDefinition

Composite key support with multiple segments. Handles data type conversions (INTEGER, STRING, BINARY, FLOAT, etc.) and byte-order reversal for SQLite compatibility.

### BtrieveQuery

Cursor-based query interface with directional traversal (forward/reverse) and mid-query direction changes.

## DOS Emulation (`DOS/`)

### Interrupt Handlers (`DOS/Interrupts/`)

- **Int21h** (~1,100 lines): Primary DOS API — file operations (open/close/read/write/seek/create/delete/find), memory management (allocate/free with allocation strategies), DTA, FCB, current drive/directory. Handles STDIN/STDOUT/STDERR + custom file handles.
- **Int10h**: Video/display services
- **Int1Ah**: Clock/date services
- **Int3Eh**: MajorBBS-specific services
- **Int7Bh**: Btrieve API entry point

### ExeRuntime

Loads and runs standalone DOS .EXE files. Sets up PSP, environment segment, applies MZ relocation records, registers interrupt handlers, and configures segment registers for execution.

### PIT (`BIOS/ProgrammableIntervalTimer.cs`)

Emulates the Intel 8253/8254 PIT. Port-mapped I/O on 0x40-0x43, supports all 6 timer modes. Fires INT 8 every ~55ms.

## Configuration & Startup

### Program.cs (~850 lines)

CLI argument parsing: `-M` (module), `-P` (path), `-K` (menu key), `-C` (config file), `-S` (settings), `-EXE` (run DOS executable), `-DBRESET`, `-DBREBUILD`, `-PWRESET`, `-APIREPORT`, `-CONSOLE`, `-CLI`, `-V` (version).

Boot sequence: parse args → load AppSettings → build ServiceResolver → load modules (from CLI, directory scan, or modules.json) → create MbbsHost → start Telnet/Rlogin servers → launch UI → wait for shutdown.

### AppSettingsManager

Loads `appsettings.json` via Microsoft.Extensions.Configuration. Key settings: `BBS.Title`, `BBS.Channels`, `Telnet.Enabled/Port`, `Rlogin.Enabled/Port/RemoteIP`, `Database.File`, `Btrieve.CacheSize`, `Cleanup.Time`, `Module.DoLoginRoutine`.

### ServiceResolver (`DependencyInjection/`)

Custom DI wrapper around `Microsoft.Extensions.DependencyInjection`. Registers singletons for nearly everything (MbbsHost, LogFactory, AppSettingsManager, repositories, clock, etc.) and transients for per-request items (FsdUtility, SocketServer). Supports constructor overrides for testing.

## Other Subsystems

### TextVariables (`TextVariables/`)
Binary format: `0x01 <justification> <padding> <name> 0x01`. Variables are registered as delegates and resolved at output time. Supports Left/Center/Right justification with padding. Session-level variables (CHANNEL, USERID, BAUD, TIME_ONLINE) and global variables.

### Logging (`Logging/`)
`LogFactory` creates typed loggers: `MessageLogger` (general), `AuditLogger` (security), `CPULogger` (instruction trace). Loggers have pluggable `ILoggingTarget` implementations (`ConsoleTarget`, `QueueTarget` for UI).

### Extensions (`Extensions/`)
Performance-critical utility methods with aggressive inlining: `ByteExtensions` (bit operations, parity), `UshortExtensions`/`UintExtensions` (sign extension, bit tests), `ReadOnlySpanExtensions`, `ANSIStringExtensions`, `EnumSessionStateExtensions`.

### IO (`IO/`)
`FileUtility` provides case-insensitive file finding (essential for DOS compatibility on Linux/macOS), DOS path stripping (`\BBSV6\`, `C:\BBSV6`), and path separator normalization. Stream abstractions (`IStream`) wrap System.IO.Stream, TextReader/Writer, and BlockingCollections.

### Util (`Util/`)
`MessagingCenter` — pub/sub event system with weak references (ported from Xamarin.Forms). `LRUCache<K,V>` — generic least-recently-used cache. `CRC32` — hash algorithm.
