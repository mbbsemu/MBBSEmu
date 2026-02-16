# ExportedModules — Emulated SDK Libraries

This directory contains the C# implementations of the MajorBBS/Worldgroup SDK libraries. When a loaded module's x86 code executes a `CALL FAR` to a virtual segment (>= 0xFF00), the CPU fires `_invokeExternalFunctionDelegate`, which routes to the appropriate exported module's `Invoke(ordinal)` method. These classes are the bridge between the emulated 16-bit world and modern C#.

## File Overview

| File | Lines | Purpose |
|---|---|---|
| `Majorbbs.cs` | ~8,400 | Core BBS API: user management, I/O, string/memory ops, file I/O, config, date/time, Btrieve, module registration |
| `Ordinals.cs` | ~1,245 | Dictionary mapping ordinal numbers to function names (1,300+ entries for MAJORBBS) |
| `ExportedModuleBase.cs` | ~1,230 | Abstract base class with parameter access, printf, output formatting, Btrieve helpers |
| `Galgsbl.cs` | ~1,030 | Galacticomm Global Software Breakout Library: screen/keyboard/ANSI, session I/O (BTUXMT, BTUCHI, etc.) |
| `Phapi.cs` | ~250 | Protected-mode hardware API: DosAllocRealSeg, DosRealIntr |
| `Doscalls.cs` | ~175 | OS/2-style DOS calls: DosAllocSeg, DosLoadModule, DosGetProcAddr, DosGetModHandle |
| `Galme.cs` | ~130 | Galacticomm Module Environment: messaging (simpsnd, oldsend), forum access (gforac) |
| `Galmsg.cs` | ~60 | Galacticomm Message database routines (stub — no ordinals implemented yet) |
| `IExportedModule.cs` | 13 | Interface: `Invoke()`, `SetState()`, `SetRegisters()`, `UpdateSession()` |
| `ExportAttribute.cs` | 17 | `[Export("NAME", ordinal)]` attribute (defined but not currently used in dispatch) |
| `EnumPrintfFlags.cs` | 20 | Flags enum for printf formatting: LeftJustify, Signed, Space, DecimalOrHex, LeftPadZero |

## Virtual Segment Mapping

Each exported module is assigned a virtual segment number used for relocation and dispatch:

| Segment | Class | SDK Header | Purpose |
|---|---|---|---|
| `0xFFFF` | `Majorbbs` | MAJORBBS.H | Core BBS API (~1,300 ordinals) |
| `0xFFFE` | `Galgsbl` | GALGSBL.H | Screen/keyboard/I/O breakout library |
| `0xFFFD` | `Phapi` | PHAPI.H | Protected-mode hardware API |
| `0xFFFC` | `Galme` | GALME.H | Module environment / messaging |
| `0xFFFB` | `Doscalls` | — | OS/2-compatible DOS calls |
| `0xFFFA` | `Galmsg` | GALMSG.H | Message database routines |

## IExportedModule Interface

Every exported module implements:

```csharp
public interface IExportedModule : IDisposable
{
    ReadOnlySpan<byte> Invoke(ushort ordinal, bool offsetsOnly = false);
    void SetState(ushort channelNumber);
    void SetRegisters(ICpuRegisters registers);
    void UpdateSession(ushort channelNumber);
}
```

- **`Invoke(ordinal, offsetsOnly)`** — Main entry point. If `offsetsOnly=true`, returns a `FarPtr.Data` for relocation without executing. Otherwise dispatches to the C# implementation.
- **`SetState(channelNumber)`** — Called before execution to set up the session context (loads VDA, user structs, status, input buffers for the given channel into module memory).
- **`SetRegisters(registers)`** — Passes the CPU register file so the exported function can read parameters and write return values.
- **`UpdateSession(channelNumber)`** — Called after execution to sync modified state (STATUS, USRPTR, VDA) back from module memory to the session object.

## ExportedModuleBase — Common Infrastructure

Abstract base class for all exported modules. Provides:

### Parameter Access (x86 Stack Convention)

Parameters are read from the x86 stack at `SS:[BP + 6 + 2*ordinal]`:

```csharp
GetParameter(0)              // ushort at [BP+6]
GetParameter(1)              // ushort at [BP+8]
GetParameterPointer(0)       // FarPtr from [BP+6] (offset) and [BP+8] (segment)
GetParameterString(0)        // null-terminated string at the pointer in param 0,1
GetParameterFilename(0)      // same as GetParameterString but uppercased
GetParameterLong(0)          // int32 from [BP+6] (low) and [BP+8] (high)
GetParameterULong(0)         // uint32 from params
GetParameterDouble(0)        // 64-bit float from 4 consecutive words
GetParameterBool(0)          // true if param != 0
SetParameter(0, value)       // write ushort back to stack
SetParameterPointer(0, ptr)  // write FarPtr back to stack
```

**Important**: Pointer parameters consume 2 ordinal slots (offset + segment). So if param 0 is a pointer, the next scalar is at ordinal 2, not 1.

### Printf/Sprintf Implementation

`FormatPrintf(ReadOnlySpan<byte> stringToParse, ushort startingParameterOrdinal, bool isVsPrintf)` — Full C-style printf parsing supporting `%d`, `%u`, `%s`, `%c`, `%f`, `%ld`, `%lu`, with flags (`-`, `+`, `0`, `#`), width, and precision. Handles `%%` escaping and edge cases like trailing `%`.

### Output Formatting Pipeline

`FormatOutput()` chains three processors for text sent to clients:
1. `ProcessIfANSI()` — handles custom `ESC[[...` IF-ANSI conditional sequences
2. `FormatNewLineCarriageReturn()` — normalizes `\r`, `\n` to `\r\n`
3. `ProcessTextVariables()` — resolves registered text variables (`0x01` delimited)

### Other Helpers

- `ProcessEscapeCharacters()` — converts C escape sequences (`\n`, `\t`, `\xHH`, etc.) in byte spans
- `RealignStack(bytesToRealign)` — for stdcall functions that need manual stack cleanup
- `StringFromArray()` — extracts null-terminated string from a byte span
- `GetLocalVariableName()` / `GetLocalVariableOrdinal()` — generates unique memory variable names for return buffers so concurrent calls don't clobber each other
- Btrieve helpers: `BtrieveSaveProcessor()`, `BtrieveGetProcessor()`, `BtrieveDeleteProcessor()`, `BtrieveSetupGlobalPointer()`

### Shared State

- `ChannelDictionary` — maps channel numbers to `SessionBase` instances
- `ChannelNumber` — currently active channel
- `FilePointerDictionary` — tracks open file handles (from `fopen`)
- `McvPointerDictionary` — tracks open MCV (module config) files
- `Module` — the `MbbsModule` being served
- `Registers` — CPU register file for reading params / writing return values

## Invoke() Dispatch Pattern

Every exported module's `Invoke()` follows the same two-phase dispatch:

```csharp
public ReadOnlySpan<byte> Invoke(ushort ordinal, bool offsetsOnly = false)
{
    // Phase 1: Property/variable ordinals — return FarPtr.Data to memory-resident values
    switch (ordinal)
    {
        case 628: return usrnum;     // -> Module.Memory.GetVariablePointer("USRNUM").Data
        case 565: return status;     // -> Module.Memory.GetVariablePointer("STATUS").Data
        // ... ~60 more property cases in Majorbbs
    }

    // If only resolving addresses for relocation, return segment:ordinal as FarPtr
    if (offsetsOnly)
        return new FarPtr(Segment, ordinal).Data;

    // Phase 2: Method ordinals — execute the C# implementation
    switch (ordinal)
    {
        case 578: strlen(); break;
        case 574: strcpy(); break;
        case 560: sprintf(); break;
        // ... ~200+ method cases in Majorbbs
    }

    return null;
}
```

**Properties** (Phase 1) return a 4-byte `ReadOnlySpan<byte>` containing a `FarPtr` that points to the variable's location in module memory. These are used by the NE relocation process — the loader patches the module's code with the returned address.

**Methods** (Phase 2) execute a C# function, which reads parameters from the x86 stack via `GetParameter*()`, performs the operation, and writes the result to `Registers.AX` (16-bit) or `Registers.SetPointer()` / DX:AX (32-bit/pointer).

## Implementing a New Exported Function

1. **Find the ordinal** in `Ordinals.cs` (or the original SDK header) and note the function signature.

2. **Add the case** to the appropriate module's `Invoke()` method switch:
   ```csharp
   case 578:
       strlen();
       break;
   ```

3. **Implement the function** as a private method. Follow the parameter convention:
   ```csharp
   /// <summary>
   ///     Returns the length of a string
   ///     Signature: size_t strlen(const char *str)
   /// </summary>
   private void strlen()
   {
       // Read params from x86 stack — param 0 is a pointer (uses slots 0+1)
       var stringValue = GetParameterString(0, stripNull: true);

       // Return 16-bit result in AX
       Registers.AX = (ushort)stringValue.Length;
   }
   ```

4. **For pointer returns**, use `Registers.SetPointer()`:
   ```csharp
   private void strcpy()
   {
       var destinationPointer = GetParameterPointer(0);
       var sourcePointer = GetParameterPointer(2);  // slot 2, not 1!
       var inputBuffer = Module.Memory.GetString(sourcePointer);
       Module.Memory.SetArray(destinationPointer, inputBuffer);
       Registers.SetPointer(destinationPointer);  // return dest in DX:AX
   }
   ```

5. **For property ordinals**, add a property and a case in the first switch:
   ```csharp
   private ReadOnlySpan<byte> usrnum => Module.Memory.GetVariablePointer("USRNUM").Data;
   ```

6. **For stdcall functions** that manage their own stack cleanup, call `RealignStack(bytesToRealign)` at the end.

## Majorbbs.cs — The Largest Module

At ~8,400 lines, `Majorbbs.cs` implements the vast majority of the emulated SDK. Its constructor allocates dozens of well-known variables in module memory:

- `PRFBUF` (16KB output buffer), `PRFPTR`, `OUTBSZ`
- `INPUT` (255-byte user input), `INPLEN`, `NXTCMD`
- `USER` (per-channel user records), `*USRPTR`, `USRNUM`
- `USRACC` / `USAPTR` (account records), `EXTUSR` / `EXTPTR`
- `STATUS`, `CHANNEL`, `NTERMS`
- `MARGC`, `MARGN`, `MARGV` (parsed command arguments)
- `VDAPTR`, `VDATMP`, `VDASIZ` (volatile data area)
- `BBSTTL`, `COMPANY`, `ADDRES1`, `ADDRES2`, `DATAPH`, `LIVEPH`
- `VERSION`, `NMODS`, `MODULE`
- BBS config: `MMUCCR`, `PFNLVL`, `NUMBYTS`, `NUMFILS`
- `TXTVARS`, `NTVARS` (text variable system)

### SetState() — Session Context Switch

Called before each module execution to load the target channel's data into module memory:
1. Resets local variable ordinals
2. Copies VDA (Volatile Data Area) from session to `VDA-{channel}` memory
3. Sets `VDAPTR` to point to the channel's VDA
4. Sets `USRNUM` to the channel number
5. Sets `STATUS` to the session's current status
6. Copies `UsrPtr`, `UsrAcc`, `ExtUsrAcc` into the USER/USRACC/EXTUSR arrays at the channel's index
7. Updates `*USRPTR`, `USAPTR`, `EXTPTR` pointers

### UpdateSession() — Sync Back

Called after execution to propagate changes back:
1. Reads `STATUS` from memory — if changed, queues the new status
2. Copies USER record back to session's `UsrPtr`
3. Copies VDA back to session

## Galgsbl.cs — Screen/Keyboard I/O

Implements the Global Software Breakout Library (~1,030 lines). Key functions:

- **btuxmt** — transmit data to client (the primary output function)
- **btuchi** — set character interceptor callback for input handling
- **btuimp** — import data from session input buffer
- **chiinp** — inject character into session input
- **btuoba** — set output-before-anything callback
- **btuinj** — inject string into session
- **btupbc** — push-back character
- **btutrg** — set trigger length for input
- **btucli** — clear input buffer
- **btuxnf** — transmit without formatting

Properties: `BTURNO` (BBS registration number), `BTURS` (buffer size), `TICKER` (seconds counter, updated by a 1-second timer).

## Phapi.cs — Protected Mode Hardware API

Small module (~250 lines) for OS/2-style protected-mode operations:
- `DosRealIntr` — execute real-mode interrupt (bridges to INT handlers)
- `DosAllocRealSeg` — allocate a real-mode memory segment

## Doscalls.cs — DOS Call Wrappers

OS/2-compatible DOS API (~175 lines):
- `DosAllocSeg` — allocate memory segment
- `DosLoadModule` — load DLL (currently returns ERROR_FILE_NOT_FOUND)
- `DosGetProcAddr` — get function address from DLL
- `DosGetModHandle` / `DosGetModName` — module handle operations

Uses `RealignStack()` since these follow stdcall convention.

## Galme.cs — Module Environment

Messaging and forum access (~130 lines):
- `simpsnd` — simple send message (stub)
- `oldsend` — legacy send (stub)
- `gforac` — get forum access level (stub)
- `_txtlen` — property returning max text length (0x400)

## Galmsg.cs — Message Database

Placeholder module (~60 lines). No ordinals implemented — throws `ArgumentOutOfRangeException` for any call.

## Ordinals.cs — Function Name Registry

Contains `Ordinals.MAJORBBS`, a `Dictionary<int, string>` mapping all ~1,300 ordinal numbers to their SDK function names (e.g., `{578, "STRLEN"}`, `{574, "STRCPY"}`). Used for debug logging and reports. The ordinal numbers correspond directly to the NE DLL export table entries.
