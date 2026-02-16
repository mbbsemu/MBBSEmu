# ExecutionUnits — CPU Execution Contexts

This directory contains `ExecutionUnit.cs`, a single but critical class that encapsulates everything needed to run a chunk of x86 code within a module: a CPU core, registers, memory reference, exported module dictionary, and interrupt handlers. ExecutionUnits are the mechanism that enables nested subroutine calls without corrupting CPU state.

## Why ExecutionUnits Exist

The original MajorBBS was single-threaded, but its API allows re-entrant calls. For example, while EU0 is executing a module's main handler, that handler might call `outprf()`, which calls `ProcessTextVariables()`, which needs to execute a module-registered text variable callback — a completely separate x86 subroutine. That callback needs its own CPU state (registers, instruction pointer) but must share the same module memory.

**The solution**: Each `MbbsModule` maintains a pool (`Queue<ExecutionUnit>`) of ExecutionUnits. They all share the same `IMemoryCore` (including stack segment), but each has its own `CpuCore` with independent registers. When a nested call is needed, a new EU is dequeued (or created on demand), executes with isolated register state, and is returned to the pool.

```
MbbsHost.Run()
  └─ MbbsModule.Execute()           ← dequeues EU0
       └─ EU0.Execute()             ← runs x86 code
            └─ CALL FAR 0xFFFF:326  ← hits ExternalFunctionDelegate
                 └─ Majorbbs.outprf()
                      └─ ProcessTextVariables()
                           └─ Module.Execute()  ← dequeues EU1 (nested!)
                                └─ EU1.Execute() ← runs text var callback
                                     └─ returns, EU1 re-enqueued
                      └─ outprf() continues
            └─ EU0 continues with its registers intact
       └─ EU0 re-enqueued
```

## ExecutionUnit.cs — Anatomy

### Fields

```csharp
public readonly ICpuCore ModuleCpu;                              // Own CpuCore instance
public readonly ICpuRegisters ModuleCpuRegisters;                // = ModuleCpu (CpuCore implements ICpuRegisters)
public readonly IMemoryCore ModuleMemory;                        // SHARED with module and other EUs
public readonly Dictionary<ushort, IExportedModule> ExportedModuleDictionary;  // SHARED
public string Path { get; init; }                                // Module file path
```

**Key design**: `ModuleCpu` is per-EU (isolated registers), but `ModuleMemory` and `ExportedModuleDictionary` are shared references from the parent `MbbsModule`.

### Constructor

Creates a new `CpuCore` and wires up:
- **Memory**: shared `IMemoryCore` from the module
- **ExternalFunctionDelegate**: routes `CALL FAR` to exported modules (see below)
- **Interrupt handlers**: `Int21h` (DOS file I/O), `Int3Eh` (MajorBBS-specific), `Int1Ah` (clock/date)
- **No I/O port handlers** (`ioPortHandlers: null`)

### ExternalFunctionDelegate

When the CPU executes a `CALL FAR` to a segment >= 0xFF00 (a virtual exported module segment), `CpuCore` fires the delegate instead of doing a normal far call:

```csharp
private ReadOnlySpan<byte> ExternalFunctionDelegate(ushort ordinal, ushort functionOrdinal)
{
    if (!ExportedModuleDictionary.TryGetValue(ordinal, out var exportedModule))
        throw new Exception($"Unknown or Unimplemented Imported Module: {ordinal:X4}");

    // Critical: re-associate this EU's registers with the exported module,
    // because nested EUs may have changed which registers it points to
    exportedModule.SetRegisters(ModuleCpuRegisters);

    return exportedModule.Invoke(functionOrdinal);
}
```

The `SetRegisters()` call is essential — because all EUs in a module share the same `ExportedModuleDictionary` (and thus the same `Majorbbs`/`Galgsbl` instances), a nested EU must re-bind its own registers before invoking. Without this, a nested call would read/write the parent EU's registers.

### Execute() — The Core Method

```csharp
public ICpuRegisters Execute(
    FarPtr entryPoint,              // CS:IP to start executing at
    ushort channelNumber,           // Which user session (or ushort.MaxValue for system calls)
    bool simulateCallFar = false,   // Push fake CS:IP return address for function pointer calls
    bool bypassState = false,       // Skip SetState() (for text variables, etc.)
    Queue<ushort> initialStackValues = null,  // Parameters to push before execution
    ushort initialStackPointer = CpuCore.STACK_BASE)  // SP start (shifted for nested calls)
```

**Step-by-step**:

1. **Reset CPU** — `ModuleCpu.Reset(initialStackPointer)` zeroes registers, sets SP/BP to the given stack pointer, pushes `uint.MaxValue` (0xFFFF:0xFFFF) as a halt sentinel onto the stack

2. **Set entry point** — `CS = entryPoint.Segment`, `IP = entryPoint.Offset`

3. **Push parameters** — dequeues `initialStackValues` onto the stack (if any)

4. **Simulate CALL FAR** (if requested) — sets `BP = SP`, then pushes `0xFFFF:0xFFFF` as a fake return address. This creates a proper stack frame so the called function can use `BP`-relative parameter access. Used when calling function pointers registered by the module (e.g., `sttrou`, polling routines)

5. **Set state on all exported modules** — calls `SetState(channelNumber)` on every exported module (Majorbbs, Galgsbl, etc.) to load the target session's data into module memory. Then calls `SetRegisters()` to bind this EU's registers. Skipped if `bypassState=true`

6. **Run CPU loop** — `while (!ModuleCpuRegisters.Halt) ModuleCpu.Tick()` — executes instructions until halt

7. **Halt detection** — when x86 code executes `RETF` and pops `CS = 0xFFFF` (the sentinel), `CpuCore` sets `Halt = true`, ending the loop

8. **Sync state back** — calls `UpdateSession(channelNumber)` on the Majorbbs exported module to copy STATUS, USER records, and VDA back from module memory to the session object. Skipped for system calls (`channelNumber == ushort.MaxValue`) or bypass-state calls

9. **Return registers** — the caller reads results from `AX`, `DX:AX`, etc.

## Pool Management in MbbsModule

`MbbsModule` owns the EU pool and provides the `Execute()` wrapper:

```csharp
// Pool: Queue<ExecutionUnit>, initialized with capacity 2
ExecutionUnits = new Queue<ExecutionUnit>(2);

public ICpuRegisters Execute(FarPtr entryPoint, ushort channelNumber, ...)
{
    // Determine which DLL is being called (for multi-DLL modules)
    // ...

    // Dequeue or create on demand
    if (!ExecutionUnits.TryDequeue(out executionUnit))
        executionUnit = new ExecutionUnit(Memory, _clock, _fileUtility,
            ExportedModuleDictionary, _logger, ModulePath);

    resultRegisters = executionUnit.Execute(entryPoint, channelNumber, ...);

    // Return to pool for reuse
    ExecutionUnits.Enqueue(executionUnit);
    return resultRegisters;
}
```

**Error handling**: If execution throws, the module is disabled, a `CrashReport` is saved, and a `DisableModule` message is sent to MbbsHost.

**Disposal**: `MbbsModule.Dispose()` disposes all pooled EUs, which in turn dispose their `CpuCore` instances.

## Call Sites — Where Execute() Is Invoked

### From MbbsHost (top-level, via MbbsModule.Execute)

`MbbsHost.Run()` is the bridge between the event loop and module execution:

```csharp
private ushort Run(string moduleName, FarPtr routine, ushort channelNumber,
    bool simulateCallFar = false, Queue<ushort> initialStackValues = null)
{
    var resultRegisters = _modules[moduleName].Execute(routine, channelNumber,
        simulateCallFar, false, initialStackValues);
    return resultRegisters.AX;
}
```

Called for:
- **User input processing** — executing the module's `sttrou` (state routine) entry point for a channel
- **Polling routines** — registered polling callbacks for active sessions
- **RTKICK** — real-time kick routines (channel = `ushort.MaxValue`)
- **RTIHDLR** — real-time interval handlers
- **SYSCYC** — system cycle callbacks
- **Global commands** — intercepted command handlers
- **Module _INIT_** — initial module startup

### From Exported Modules (nested calls)

Inside `Majorbbs.cs`, some API functions need to call back into module code:

- **Text variable resolution** (`ExportedModuleBase.cs:751`) — calls the module's registered `varrou` function pointer to get the current value of a text variable. Uses `initialStackPointer = 0xF100` to avoid stack overlap.

- **File tag-spec handlers** (`Majorbbs.cs:6476,6494`) — calls the module's `tshndl` callback for file transfer begin/finish events. Uses `initialStackPointer = Registers.SP - 0x800` to shift the nested stack 2KB below the current stack position.

- **Finished function callbacks** (`Majorbbs.cs:6554`) — calls completion handlers registered by modules.

All nested calls use `simulateCallFar=true` and `bypassState=true` (the parent EU already set the state).

## Stack Isolation for Nested Execution

Since all EUs share the same memory (including the stack segment at 0x0000), nested EUs must use a different region of the stack to avoid clobbering the parent's stack frame. This is controlled by the `initialStackPointer` parameter:

| Context | Stack Pointer | Purpose |
|---|---|---|
| Normal (top-level) | `0xFFFE` (`STACK_BASE`) | Default — full stack available |
| Text variable callback | `0xF100` | Fixed offset well below normal stack usage |
| File tag-spec handler | `Registers.SP - 0x800` | 2KB below parent's current SP |

The stack grows downward, so a lower initial SP means the nested EU uses memory below the parent's stack, preventing overlap.

## Halt Mechanism

The CPU execution loop (`while (!Halt) Tick()`) terminates when `Halt` becomes true. This happens through a sentinel value:

1. During `CpuCore.Reset()`, `uint.MaxValue` (0xFFFFFFFF) is pushed to the stack — this places `0xFFFF` as both the saved CS and saved IP
2. When `simulateCallFar=true`, another `0xFFFF:0xFFFF` pair is pushed as the fake return address
3. When the module code eventually executes `RETF`, it pops IP and CS from the stack
4. `Op_Retf()` checks: `Registers.Halt |= Registers.CS == 0xFFFF`
5. If CS is the sentinel value `0xFFFF`, halt is triggered and the execution loop ends

This is why segment `0xFFFF` (Majorbbs) works as both a virtual module segment and a halt sentinel — `CALL FAR 0xFFFF:ordinal` is intercepted by the delegate before a normal far call occurs, so the sentinel on the stack is only reached during a legitimate return.
