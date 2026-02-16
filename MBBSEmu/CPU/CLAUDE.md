# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

This directory implements the emulated 16-bit x86 CPU core. It is the innermost execution loop of MBBSEmu — every instruction from every loaded module passes through `CpuCore.Tick()`. The CPU emulates the integer instruction set, the x87 FPU, flags, and stack operations. It delegates external function calls (to MAJORBBS, GALGSBL, etc.) via a callback delegate and dispatches software interrupts to registered handlers.

## Files

| File | Lines | Purpose |
|---|---|---|
| `CPUCore.cs` | ~5,090 | Main execution engine: `Tick()` loop, instruction dispatch, all opcode handlers, operand extraction, flag evaluation |
| `CpuRegisters.cs` | 104 | Thin delegation wrapper: implements `ICpuRegisters` + `IFpuRegisters` by forwarding to `CpuRegistersStruct` |
| `RegistersStructs.cs` | 558 | Low-level register storage: `CpuRegistersStruct` (explicit layout struct) + `FpuRegistersStruct` |
| `ICpuCore.cs` | 47 | CPU interface: `Reset()`, `Tick()`, `Push()`, `Interrupt()` |
| `ICpuRegisters.cs` | 85 | Register interfaces: `ICpuRegisters` (GPRs, segments, flags) + `IFpuRegisters` (FPU stack/control) |
| `IIOPort.cs` | 9 | I/O port interface: `In(channel)` / `Out(channel, byte)` |
| `EnumFlags.cs` | 74 | `[Flags]` enum for x86 FLAGS register bits (CF, PF, AF, ZF, SF, TF, IF, DF, OF, IOPL, NT) |
| `EnumFpuStatusFlags.cs` | 25 | `[Flags]` enum for x87 FPU StatusWord bits (exception flags, condition codes, busy) |
| `EnumFpuControlWordFlags.cs` | 18 | `[Flags]` enum for x87 FPU ControlWord exception masks |
| `EnumArithmeticOperation.cs` | 18 | Enum used by flag evaluation: Addition, Subtraction, Multiplication, Division, shift variants |
| `EnumOperandType.cs` | 13 | Enum identifying operand role: Destination, Source, Count, None |

## Class Hierarchy

```
ICpuCore : ICpuRegisters, IDisposable
  └── CpuCore : CpuRegisters, ICpuCore
                   └── CpuRegisters : ICpuRegisters, IFpuRegisters
                          └── (contains) CpuRegistersStruct
                                             └── (contains) FpuRegistersStruct
```

`CpuCore` inherits from `CpuRegisters`, so all register properties (AX, BX, CS, IP, CarryFlag, etc.) are directly accessible as `this.AX`, `Registers.AX`, etc. throughout the opcode handlers. This flat inheritance was chosen for performance — it avoids indirection in the hottest code path.

## CpuRegistersStruct — Register Storage

Uses `[StructLayout(LayoutKind.Explicit)]` to create x86-style register aliasing. Overlapping fields at the same offset let writing to EAX also update AX, AH, and AL:

```
Offset  0: EAX (uint) / AX (ushort) / AL (byte at 0) / AH (byte at 1)
Offset  4: EBX / BX / BL / BH
Offset  8: ECX / CX / CL / CH
Offset 12: EDX / DX / DL / DH
Offset 16: ESP / SP
Offset 20: EBP / BP
Offset 24: ESI / SI
Offset 28: EDI / DI
Offset 32: DS
Offset 34: ES
Offset 36: SS
Offset 38: CS
Offset 40: IP
Offset 42-49: CarryFlag, SignFlag, OverflowFlag, ZeroFlag, DirectionFlag,
              AuxiliaryCarryFlag, InterruptFlag, Halt (individual bools)
Offset 50: FpuRegistersStruct
```

**FLAGS register:** Individual flags are stored as separate bools for fast per-flag access. `F()` assembles them into a packed ushort; `SetF(ushort)` disassembles back. This avoids bitfield manipulation in the hot path where most instructions only touch one or two flags.

**Pointer convention:** `DX:AX` is used as a 32-bit return value / far pointer pair. `GetPointer()` returns `new FarPtr(DX, AX)`. `SetPointer(FarPtr)` sets DX=Segment, AX=Offset. `GetLong()` returns `(DX << 16) | AX`.

**DOS.H interop:** `ToRegs()` serializes AX/BX/CX/DX/SI/DI/CF/FLAGS into a 16-byte span matching the DOS REGS struct layout. `FromRegs()` deserializes.

**Initialization:** `Create()` / `Zero()` sets ControlWord to `0x37F` (all exceptions masked, round-to-nearest) and FPU stack top to 7 (empty stack).

## FpuRegistersStruct — FPU State

The x87 FPU uses a circular 8-slot stack stored in `CpuCore.FpuStack` (a `double[8]` array). The struct manages the stack pointer and control state:

- **Stack top** is bits 11-13 of `StatusWord`. `GetStackTop()` extracts it, `SetStackTop()` writes it.
- `GetStackPointer(Register.ST0..ST7)` maps a logical ST(n) register to a physical array index: `stackTop - n`. The result can be negative or wrap, which maps to the circular buffer.
- `PushStackTop()` increments (wraps 7→0). `PopStackTop()` decrements (wraps 0→7).
- **Rounding control** is bits 10-11 of `ControlWord`, mapped to `MidpointRounding` enum: 0=ToEven, 1=ToNegativeInfinity, 2=ToPositiveInfinity, 3=ToZero.
- `ClearExceptions()` clears lower 6 bits of both StatusWord and ControlWord.

## CpuCore.cs — The Execution Engine

### Constants

- `STACK_BASE = 0xFFFE` — initial SP value
- `STACK_SEGMENT = 0x0` — SS always 0
- `OpcodeCompilerOptimizations = AggressiveOptimization` — JIT hint on dispatch methods
- `OpcodeSubroutineCompilerOptimizations = AggressiveOptimization | AggressiveInlining` — JIT hint on per-size sub-handlers

The comment at line 105 explains the deliberate choice: aggressive inlining on the main dispatch (`Tick()`, `GetOperandValue*`) actually *slows* the code due to L1 instruction cache pressure. Only the size-specific sub-handlers (e.g., `Op_Add_8`, `Op_Add_16`) benefit from inlining into their parent.

### Key Members

- `Memory` (`IMemoryCore`) — the module's memory space
- `_currentInstruction` (`Iced.Intel.Instruction`) — decoded instruction being executed
- `_currentOperationSize` — 1, 2, or 4 (determined from Op0 register size or memory size)
- `_invokeExternalFunctionDelegate` — callback for `CALL FAR` to segments >= 0xFF00
- `_interruptHandlers` (`Dictionary<int, IInterruptHandler>`) — software interrupt dispatch table
- `_ioPortHandlers` (`Dictionary<int, IIOPort>`) — I/O port dispatch table
- `FpuStack` (`double[8]`) — x87 FPU data stack
- `InstructionCounter` — total instructions executed since last Reset

### Reset()

The full `Reset(memoryCore, delegate, interruptHandlers, ioPortHandlers)` initializes all state:
1. Stores the external function delegate and handler dictionaries
2. Zeros all registers
3. Sets BP=SP=`STACK_BASE`, SS=`STACK_SEGMENT`, ES=0xFFFF
4. Pushes `uint.MaxValue` (0xFFFFFFFF) as the end-of-execution sentinel — when `RETF` pops CS=0xFFFF, it sets `Halt=true`

The lightweight `Reset()` / `Reset(stackBase)` overloads just reinitialize registers and the sentinel without changing the memory core or delegates. Used when reusing an ExecutionUnit for a new subroutine call within the same module.

### Tick() — The Hot Loop

Called repeatedly by the execution loop (`while (!Halt) cpu.Tick()`). Each call executes exactly one instruction:

```
1. Fetch:    _currentInstruction = Memory.GetInstruction(CS, IP)
2. Size:     _currentOperationSize = GetCurrentOperationSize()
3. Counter:  InstructionCounter++
4. Dispatch: switch (_currentInstruction.Mnemonic) → Op_XXX()
5. Advance:  IP += instruction.Length  (unless handler already set IP)
```

The dispatch switch at line 415 has ~100 cases organized into two groups:
- **Control-flow instructions** (jumps, calls, returns, loops, iret) — handler sets IP directly, then `return` from Tick() to skip the IP increment
- **Non-control-flow instructions** (arithmetic, logic, memory, FPU, string, I/O) — handler executes, then falls through to `IP += Length`

**Why a giant switch and not a dictionary/delegate table:** The large `switch` on `Mnemonic` is intentional. When the .NET JIT compiles this to native code, it emits an optimized jump table — a single indexed jump instruction rather than a chain of comparisons or dictionary lookups. This makes dispatch O(1) with minimal overhead (one bounds check + indirect jump), which matters critically here since `Tick()` is the innermost loop of the entire emulator and executes for every single instruction. A `Dictionary<Mnemonic, Action>` approach would add delegate allocation, dictionary hashing, and indirect call overhead on every tick.

Special case: `Mnemonic.INVALID` triggers `Memory.Recompile(CS, IP)` and re-dispatches via `goto Switch`. This handles memory that was modified after initial decompilation.

### Debug Infrastructure (DEBUG builds only)

Three debug features are configured as hardcoded lists in `Reset()`:

- **CPUBreakpoints** (`List<FarPtr>`) — if CS:IP matches, calls `Debugger.Break()`
- **CPUDebugRanges** (`List<List<FarPtr>>`) — if CS:IP falls within a start..end pair, logs disassembly + register dump + FPU stack to console each tick
- **WatchedVariables** (`Dictionary<(FarPtr, ushort), byte[]>`) — detects memory changes between ticks and logs them

To use these during development, uncomment the example entries in `Reset()` and run in Debug configuration.

### Instruction Handler Pattern

Every ALU instruction follows the same structure:

```csharp
// Top-level dispatcher (AggressiveOptimization, no inlining)
private void Op_Add(bool addCarry = false)
{
    var result = _currentOperationSize switch
    {
        1 => Op_Add_8(addCarry),
        2 => Op_Add_16(addCarry),
        4 => Op_Add_32(addCarry),
        _ => throw new Exception("Unsupported Operation Size")
    };
    WriteToDestination(result);
}

// Per-size sub-handler (AggressiveOptimization + AggressiveInlining)
private byte Op_Add_8(bool addCarry)
{
    var destination = GetOperandValueUInt8(Op0Kind, Destination);
    var source = GetOperandValueUInt8(Op1Kind, Source);
    unchecked
    {
        if (addCarry && Registers.CarryFlag) source++;
        var result = (byte)(source + destination);
        Flags_EvaluateCarry(Addition, result, destination, source);
        Flags_EvaluateOverflow(Addition, result, destination, source);
        Flags_EvaluateSignZero(result);
        return result;
    }
}
```

This three-tier pattern (dispatch → size select → compute+flags+write) is consistent across ADD, SUB, AND, OR, XOR, INC, DEC, NEG, CMP, TEST, SHL, SHR, SAR, RCR, RCL, ROR, SHLD, etc. When adding a new instruction, follow this same pattern.

ADC reuses `Op_Add(addCarry: true)`, and SBB follows the same pattern for subtraction with borrow.

### Operand Extraction

Size-aware getters read operand values from registers, immediates, or memory:

- `GetOperandValueUInt8(OpKind, EnumOperandType)` → byte
- `GetOperandValueUInt16(OpKind, EnumOperandType)` → ushort
- `GetOperandValueUInt32(OpKind, EnumOperandType)` → uint
- Signed variants: `GetOperandValueInt8/16/32()` — identical but return signed types
- FPU: `GetOperandValueFloat(OpKind, EnumOperandType)`, `GetOperandValueDouble(OpKind, EnumOperandType)`

Each getter handles these `OpKind` cases:
- `OpKind.Register` — read from `Registers.GetValue(register)`, using Op0Register (Destination), Op1Register (Source), or Op2Register (Count). For implicit-accumulator instructions (like 1-operand IMUL), `Op1Register == Register.None` triggers reading from AL/AX.
- `OpKind.Immediate8/16/32/8to16/8to32` — read constant from instruction
- `OpKind.Memory` — compute offset via `GetOperandOffset()`, then read from `Memory.GetByte/Word/DWord(segment, offset)` where segment is `Registers.GetValue(_currentInstruction.MemorySegment)`
- `OpKind.MemorySegSI` / `OpKind.MemorySegDI` — special forms for string instructions

### GetOperandOffset — Address Computation

Computes the effective address offset for memory operands. Handles x86-16 addressing modes:

- `MemoryBase == DS/None` — just displacement
- `MemoryBase == BP` — `displacement + BP [+ MemoryIndex register]`
- `MemoryBase == BX` — `displacement + BX [+ MemoryIndex register]`
- `MemoryBase == SI` — `displacement + SI`
- `MemoryBase == DI` — `displacement + DI`
- `OpKind.NearBranch16` — direct branch target offset

### WriteToDestination

Writes the result back to wherever Op0 points:
- Register: `Registers.SetValue(Op0Register, result)` — dispatches to byte/ushort/uint setter based on `_currentOperationSize`
- Memory: `Memory.SetByte/Word/DWord(segment, offset, result)`
- Float/Double overloads: write to memory (as Float32 or Float64 bytes) or to `FpuStack[stackPointer]`
- `WriteToSource()` — rare variant that writes to Op1 (used by XCHG and a few others)

### GetCurrentOperationSize

Determines if the current instruction operates on 8, 16, or 32 bits by inspecting Op0:
- If Op0 is a Register: uses `register.GetSize()` (1 for AL/BL/etc, 2 for AX/BX/etc, 4 for EAX/EBX/etc)
- If Op0 is Memory: uses `MemorySize` (UInt8→1, UInt16→2, UInt32→4)
- Returns -1 for instructions where size doesn't apply (FPU, control flow, etc.)

### Flag Evaluation

Three method families, each overloaded for byte/ushort/uint:

**`Flags_EvaluateCarry(EnumArithmeticOperation, result, destination, source)`**
- Addition: `(source + destination) > TypeMax` (uses widened arithmetic to detect carry)
- Subtraction: `result > destination` (unsigned comparison)
- Shifts: `result != 0` (shifted-out bit)
- Also sets `AuxiliaryCarryFlag` for 8-bit additions (half-carry on lower nibble)

**`Flags_EvaluateOverflow(EnumArithmeticOperation, result, destination, source)`**
- Addition: pos+pos=neg or neg+neg=pos (sign mismatch detection via `IsNegative()`)
- Subtraction: neg-pos=pos or pos-neg=neg
- Shifts: only evaluated when count==1; checks MSB change via `IsFlagSet()` XOR CarryFlag
- ShiftArithmeticRight: always false (by definition)
- ShiftRight: checks if original MSB was set

**`Flags_EvaluateSignZero(result)`**
- ZeroFlag = `result == 0`
- SignFlag = `result.IsNegative()` (checks MSB for the given width)

### CALL Instruction — External Function Bridge

This is the most important instruction in the emulator. Every MajorBBS/Worldgroup SDK API call (MAJORBBS, GALGSBL, PHAPI, etc.) flows through CALL FAR. The `Op_Call()` method handles five distinct call types:

**1. `CALL FAR` to segment > 0xFF00 — Direct exported module call** (`OpKind.FarBranch16` with selector > 0xFF00)

This is the primary path for SDK API calls. When module code has been relocated, imported function references become far calls to virtual segments (0xFFFF=MAJORBBS, 0xFFFE=GALGSBL, etc.) with the ordinal as the offset:

```
Stack before:                    Stack after ENTER simulation:
                                 [SP]   → saved BP
                                 [SP+2] → return IP (next instruction)
                                 [SP+4] → return CS
                                 [SP+6] → parameter 0  ← BP+6
                                 [SP+8] → parameter 1  ← BP+8
                                 ...
```

Steps:
1. Push CS (return segment) and IP+Length (return offset) to stack
2. Simulate `ENTER`: push BP, set BP=SP — this creates the stack frame that `ExportedModuleBase.GetParameter()` reads from at `[BP+6]`, `[BP+8]`, etc.
3. Save `ipBeforeCall` to detect control transfers
4. Invoke `_invokeExternalFunctionDelegate(segment, ordinal)` — this calls through to `ExportedModule.Invoke()` which dispatches to the C# implementation of the SDK function
5. **Normal return path** (IP unchanged): simulate `LEAVE` + `RETF` — SP=BP, pop BP, pop IP, pop CS. The C# function's return value is already in the AX register (or DX:AX for 32-bit returns).
6. **Control transfer path** (IP changed by delegate): return immediately without stack cleanup. This handles cases like `longjmp()` where the exported function redirects execution elsewhere.

**2. `CALL FAR` to normal segment** (`OpKind.FarBranch16` with selector <= 0xFF00)

Standard inter-segment call within module code. Pushes CS:IP, sets CS=selector, IP=target. Used for calls between DLL segments within the same module.

**3. `CALL NEAR`** (`OpKind.NearBranch16`)

Intra-segment call. Pushes only IP of next instruction, sets IP to target. CS unchanged.

**4. Indirect `CALL FAR` via memory** (`OpKind.Memory` with `IsCallFarIndirect`)

Handled by `Op_Call_CarFarIndirect_M16()`. This is the second-most common path for SDK calls — it occurs when module code calls through a function pointer stored in memory (e.g., callback tables, vtable-style dispatch):

1. Push CS:IP to stack
2. Read the target far pointer from memory at `[MemorySegment:offset]`
3. **Key detail:** If the memory segment is < 0xFF00 (normal memory), read a 4-byte far pointer from that location. If the segment is >= 0xFF00 (already an exported module segment), construct the pointer directly from segment:offset — this handles cases where the imported name table segment is used as the base for the indirect call.
4. Set CS:IP to the loaded pointer
5. If CS > 0xFF00 (it's an exported module): simulate ENTER, invoke delegate, simulate LEAVE+RETF (same pattern as case 1)
6. If CS <= 0xFF00: just continue execution at the new CS:IP (intra-module far call through pointer)

**5. Indirect `CALL NEAR` via memory** (`OpKind.Memory` with `IsCallNearIndirect`)

Reads a 16-bit offset from memory, pushes return IP, sets IP to the loaded offset. Used for function pointer calls within the same segment.

### RETF — Execution Termination

`Op_Retf()` pops IP then CS from the stack. The critical line: `Registers.Halt |= Registers.CS == 0xFFFF`. When the sentinel value pushed during `Reset()` is reached (CS=0xFFFF), execution stops. `Op_Ret()` (near return) pops only IP but also checks the halt condition. Both handle optional `RETF imm16` by advancing SP.

### INT — Software Interrupts

`Op_Int()` simply looks up the vector in `_interruptHandlers` and calls `Handle()`. The handlers are injected via `Reset()`. Common vectors: INT 21h (DOS API), INT 10h (video), INT 1Ah (time), INT 3Eh (MajorBBS), INT 7Bh (Btrieve).

### String Instructions and REP Prefix

String operations (MOVSB, MOVSW, STOSB, STOSW, LODSB, LODSW, SCASB, CMPSB) use the `Repeat()` helper:

```csharp
private void Repeat(Action action, bool isRepeCompatible = false)
{
    if (IsRepInstruction())
        Op_Rep(action, isRepeCompatible);  // loops CX times, checks REPE/REPNE conditions
    else
        action.Invoke();  // single execution
}
```

Each string op lambda increments/decrements SI and/or DI based on `DirectionFlag` (CLD=forward, STD=backward).

### FPU Instructions

FPU operations work with `FpuStack[8]` indexed via `Registers.Fpu.GetStackPointer(ST_register)`:
- Load instructions (`FLD`, `FILD`, `FLD1`, `FLDPI`, `FLDZ`, `FLDCW`): push a value onto the FPU stack
- Store instructions (`FST`, `FSTP`, `FSTCW`, `FSTSW`, `FISTP`): pop/read from stack to memory or register
- Arithmetic (`FADD`, `FSUB`, `FMUL`, `FDIV`, `FADDP`, `FSUBP`, `FMULP`, `FDIVP`, `FSUBR`, `FSUBRP`, `FDIVR`, `FDIVRP`): operate on stack top and operand, optionally pop
- Comparison (`FCOM`, `FCOMP`, `FCOMPP`, `FTST`): set FPU condition codes in StatusWord
- Math (`FSIN`, `FCOS`, `FSQRT`, `FPATAN`, `FSCALE`, `FRNDINT`): use C# `Math` functions
- Control (`FXCH`, `FCHS`, `FCLEX`): swap, negate, clear exceptions

The `-P` suffix variants (FADDP, FMULP, etc.) pop the stack after operation. `FCOMPP` pops twice. `SAHF` transfers FPU condition codes to CPU flags (AH→FLAGS), enabling conditional jumps after FPU comparisons.

### Stack Operations

- `Push(ushort)`: `SP -= 2; Memory.SetWord(SS, SP, value)`
- `Push(uint)`: `SP -= 4; Memory.SetDWord(SS, SP, value)`
- `Pop()`: reads word at SS:SP, then `SP += 2`
- `PopDWord()`: reads dword at SS:SP, then `SP += 4`
- All marked `[AggressiveInlining]` — these are called extremely frequently

### I/O Port Instructions

`Op_In()` and `Op_Out()` dispatch to `_ioPortHandlers[port].In(channel)` / `.Out(channel, byte)`. The PIT (Programmable Interval Timer) is the primary I/O device registered on ports 0x40-0x43.

## Relationship to Other Components

- **Memory** (`IMemoryCore`): Passed to `Reset()`. Every `GetOperandValue*()` and `WriteToDestination()` reads/writes through it. `GetInstruction()` fetches decoded Iced instructions.
- **Iced.Intel**: External library providing x86 instruction decoding. `Instruction`, `Mnemonic`, `OpKind`, `Register`, `MemorySize` are all Iced types. The CPU doesn't encode instructions — it only decodes and executes.
- **ExecutionUnit** (`HostProcess/ExecutionUnits/`): Creates and owns `CpuCore` instances. Provides the `InvokeExternalFunctionDelegate` closure that bridges to exported modules.
- **IInterruptHandler** (`DOS/Interrupts/`): Implements software interrupt logic. Registered during `Reset()`.
- **IIOPort** (`BIOS/ProgrammableIntervalTimer.cs`): Implements port-mapped I/O. Registered during `Reset()`.

## Testing

CPU instruction tests live in `MBBSEmu.Tests/CPU/`. Each file tests one instruction (e.g., `ADD_Tests.cs`, `FMUL_Tests.cs`). Tests extend `CpuTestBase` which provides:
- `mbbsEmuCpuCore` — a configured CpuCore instance
- `mbbsEmuMemoryCore` — memory for loading test code
- `mbbsEmuCpuRegisters` — register access for assertions

Tests assemble small instruction sequences, execute them, and verify register/memory/flag state.

## Adding a New Instruction

1. Add a new `case Mnemonic.XXX:` entry in the `Tick()` switch (control-flow → `return` section, or non-control-flow → `break` section)
2. Create `Op_XXX()` method with `[MethodImpl(OpcodeCompilerOptimizations)]`
3. If the instruction has size variants, create `Op_XXX_8()`, `Op_XXX_16()`, `Op_XXX_32()` with `[MethodImpl(OpcodeSubroutineCompilerOptimizations)]`
4. Use `GetOperandValue*()` to read operands, compute result, call `Flags_Evaluate*()` as appropriate, call `WriteToDestination()` to store result
5. Add tests in `MBBSEmu.Tests/CPU/XXX_Tests.cs` extending `CpuTestBase`
