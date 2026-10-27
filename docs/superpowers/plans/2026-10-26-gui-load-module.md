# Load Module from the Admin Console Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Starting MBBSEmu with no arguments in a terminal opens the admin console from the executable's folder. It runs the Setup Wizard on first run, reloads saved modules, and offers File → Load Module (Ctrl+O), which loads a module's `.MDF` into the running BBS and remembers it in `modules.json`.

**Architecture:**
- **Host:** `MbbsHost` gains `LoadModule`. It queues a request that the worker thread runs between loop iterations (module INIT executes module code, and the worker loop enumerates `_modules` constantly). The task completes with a result.
- **UI side:** a UI-agnostic `ModuleLoader` turns a picked `.MDF` path into a host request, saves successes with `ModuleListFile.Append` (a `System.Text.Json.Nodes` edit plus an atomic replace), and writes the user-facing copy. The admin console adds a File menu, Ctrl+O, an MDF picker and result dialogs.
- **Startup:** `Program` decides "GUI mode" (zero arguments plus an interactive terminal) before parsing and changes to the executable's folder. In that mode only, it loads saved modules one by one through `ModuleLoader` so a broken one is skipped instead of aborting startup. Every other start path is untouched.

**Tech Stack:** C# / .NET 10, Terminal.Gui 2.5.0, System.Text.Json(.Nodes), xUnit + FluentAssertions.

**Spec:** `docs/superpowers/specs/2026-10-26-gui-load-module-design.md`

## Global Constraints

- **No change to command-line operation.**
  - Any argument (including `-CLI`), or no terminal, runs today's code path unchanged.
  - Command-line runs keep resolving `appsettings.json`, databases and relative paths against the current directory.
  - `MbbsHost.Start(...)` is unchanged.
  - Argument parsing is unchanged.
  - With no arguments and no terminal, MBBSEmu still prints help.
- **GUI mode** is exactly: `args.Length == 0` and `TerminalEnvironment.IsInteractive()`.
- **Home folder** (GUI mode only): `AppContext.BaseDirectory`, applied with `Directory.SetCurrentDirectory` before anything reads a file.
- **`modules.json` format:**
  - It stays loadable by `-C`.
  - Writes add no top-level properties. The `JsonModuleConfigurationFileConverter` rejects unknown top-level properties.
  - An appended entry is `{"Identifier","Path","MenuOptionKey"}` with an absolute `Path`.
- **Menu keys** for newly loaded modules come from `ModuleMenuKeys.Next`:
  - `A`–`Z` skipping `X` (log off), then `1`–`9`, then `10`–`99`.
  - Keys are compared case-insensitively.
- **Picker** shows `.MDF` files only, and accepts `.mdf` too.
- **Terminal.Gui tests** carry `[Collection("TerminalGui")]`.
- **Style:** match the surrounding code (`//Comment` with no space, `///` XML docs on public members, `Enum`-prefixed enums).
- **User-visible copy** lives in `ModuleLoadText` (Task 3), and those exact strings are used everywhere.
- **Never stage** the owner's modified files:
  - `MBBSEmu.Tests/Assets/IntegrationTest.msg`
  - `MBBSEmu/Assets/*.ans`
  - `MBBSEmu/appsettings.json`
  - the deletion of `MBBSEmu/Properties/launchSettings.json`

  The deletion of `MBBSEmu/modules.json` *is* part of this plan (Task 7).
- **Commits** end with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. **Same module, different case.** The sysop picks `mjwmut.mdf` while `MJWMUT` is loaded, and expects "already loaded", not a second copy. Pinned by Task 1 `LoadModule_SameModuleDifferentCase_ReportsAlreadyLoaded`.
2. **Shutdown while a load is in flight.** The load task must complete rather than hang the shutdown, and no result dialog should pop up once shutdown has begun. Pinned by Task 1 `LoadModule_AfterStop_FailsImmediately` and Task 4 `LoadFinishingDuringShutdown_ShowsNothing`.
3. **`modules.json` can't be written.** For example, MBBSEmu was unzipped into a read-only folder. The module stays loaded, and the sysop is told it wasn't saved. Pinned by Task 3 `LoadAsync_SaveFails_ReportsLoadedButNotSaved`.
4. **A hand-written `modules.json` next to the executable** (`BasePath "."`, relative paths, `Enabled: 0`, `Patches`). It loads at startup, and Load Module keeps every existing value. Pinned by Task 2 `Append_HandWrittenFile_KeepsEverythingAlreadyThere`.
5. **A saved module's folder was moved.** The console still opens, the skip message names the module and the missing path, and `modules.json` is unchanged. Pinned by Task 3 `LoadSavedAsync_NotFound_ReturnsSkipLine` and the Task 8 smoke check.

---

### Task 1: Load a module into the running host

**Files:**
- Create: `MBBSEmu/Module/ModuleMenuKeys.cs`, `MBBSEmu/Module/ModuleLoadResult.cs`
- Modify: `MBBSEmu/HostProcess/IMbbsHost.cs`, `MBBSEmu/HostProcess/MbbsHost.cs` (`Start`, `WorkerThread`, new private members)
- Modify: `MBBSEmu.Tests/Integration/MBBSEmuIntegrationTestBase.cs` (one protected helper)
- Test: `MBBSEmu.Tests/Module/ModuleMenuKeys_Tests.cs`, `MBBSEmu.Tests/Integration/RuntimeModuleLoad_Tests.cs`

**Interfaces:**
- Produces:
  - `public static class ModuleMenuKeys { public static string Next(IEnumerable<string> used); }` returns null when every key is taken.
  - `public enum EnumModuleLoadStatus { Loaded, AlreadyLoaded, NotFound, Failed }`
  - `public sealed record ModuleLoadResult(EnumModuleLoadStatus Status, string ModuleIdentifier, string ModuleName, string MenuOptionKey, string Detail);`
    - For `NotFound`, `Detail` is the missing path.
    - For `Failed`, it is the reason.
    - Otherwise it is null.
  - `IMbbsHost`: `Task<ModuleLoadResult> LoadModule(ModuleConfiguration moduleConfiguration);`

- [ ] **Step 1: Write the failing menu-key tests** in `ModuleMenuKeys_Tests`:
  - `Next_NothingUsed_ReturnsA`: `Next([])` is `"A"`.
  - `Next_SkipsX`: letters `A`–`W` used gives `"Y"`.
  - `Next_LettersUsed_ReturnsDigits`: all letters except `X` used gives `"1"`.
  - `Next_ComparesCaseInsensitively`: `Next(["a"])` is `"B"`.
  - `Next_SingleDigitsUsed_ReturnsTwoDigits`: letters and `1`–`9` used gives `"10"`.
  - `Next_EverythingUsed_ReturnsNull`: letters, `1`–`9` and `10`–`99` used gives null.
  - Null or empty entries in `used` are ignored.

- [ ] **Step 2: Run them to verify they fail**

  Run: `dotnet test MBBSEmu.Tests --filter "FullyQualifiedName~ModuleMenuKeys_Tests"`
  Expected: build error, `ModuleMenuKeys` does not exist.

- [ ] **Step 3: Implement `ModuleMenuKeys.Next`** in `MBBSEmu/Module/ModuleMenuKeys.cs`. Walk the fixed sequence and return the first key not in a case-insensitive `HashSet` of `used`.

- [ ] **Step 4: Run the tests to verify they pass.**
  Same command. Expected: 6 passed.

- [ ] **Step 5: Write the failing host tests.**
  - Add `protected IMbbsHost StartHostWithoutModules()` to `MBBSEmuIntegrationTestBase`. It:
    - copies the MBBSEMU test module into `_modulePath`, using the existing `CopyModuleToTempPath`;
    - writes `BBSGEN.DB` and `BBSUSR.DB` exactly as `RunHost` does;
    - calls `Start(new List<ModuleConfiguration>())` on the resolver's `IMbbsHost` and returns the host.

    Tests stop the host in `finally` (`Stop()`, then `WaitForShutdown()`).
  - In `RuntimeModuleLoad_Tests : MBBSEmuIntegrationTestBase`, every `LoadModule(...)` is awaited with `.Wait(TimeSpan.FromSeconds(30))`, which must return true:
    - `LoadModule_AfterStart_LoadsWithFirstFreeKey`
      - Config: `{ModuleIdentifier="MBBSEMU", ModulePath=_modulePath, ModuleEnabled=true}`.
      - Expect status `Loaded`, `MenuOptionKey` `"A"`, `ModuleName` `"MBBSEmu"` (the module's `ModuleDescription` if non-empty, else `Mdf.ModuleName`), and `host.GetModule("MBBSEMU").ModuleConfig.MenuOptionKey` `"A"`.
    - `LoadModule_SameModuleDifferentCase_ReportsAlreadyLoaded`: load `MBBSEMU`, then `mbbsemu` from the same folder. The second load gives `AlreadyLoaded` with key `"A"`.
    - `LoadModule_MissingFolder_ReportsNotFound`: path `Path.Combine(_modulePath, "missing")` gives `NotFound` with that path in `Detail`.
    - `LoadModule_MissingMdf_ReportsNotFound`: identifier `NOPE` in `_modulePath` gives `NotFound`, and `Detail` ends with `NOPE.MDF`.
    - `LoadModule_BrokenModule_ReportsFailedAndLeavesNoTrace`
      - Write `BROKEN.MDF`, a copy of `MBBSEMU.MDF` with `DLLs: BROKEN`, and `BROKEN.DLL` containing 64 bytes of `0x00`.
      - Loading `BROKEN` gives `Failed` with a non-empty `Detail`.
      - Loading `MBBSEMU` afterwards gives `Loaded` with key `"A"`, and `host.GetModule("MBBSEMU").MainModuleDll.StateCode` is `10`.
    - `LoadModule_TakenKey_IsReplacedWithNextFreeKey`: load `MBBSEMU` with `MenuOptionKey="X"`. The result key is `"A"`, because `X` is reserved and treated as taken.
    - `LoadModule_BeforeStart_FailsImmediately`: on the resolver's host without `Start`, the result is `Failed` with `Detail` `"MBBSEmu isn't running"`.
    - `LoadModule_AfterStop_FailsImmediately`: start, `Stop()`, `WaitForShutdown()`, then load. The task completes within 5 s with `Failed`.

- [ ] **Step 6: Run them to verify they fail**

  Run: `dotnet test MBBSEmu.Tests --filter "FullyQualifiedName~RuntimeModuleLoad_Tests"`
  Expected: build error, `IMbbsHost` has no `LoadModule`.

- [ ] **Step 7: Implement `MbbsHost.LoadModule`.**
  - **The queue:**
    - Use `Queue<(ModuleConfiguration, TaskCompletionSource<ModuleLoadResult>)>`, guarded by a lock that also guards `bool _acceptingModuleLoads`.
    - Create each `TaskCompletionSource` with `TaskCreationOptions.RunContinuationsAsynchronously`, so awaiting code never runs on the worker thread.
  - **`Start`:** sets `_acceptingModuleLoads = true` under the lock before starting the worker. This is the only change to `Start`.
  - **`LoadModule`:**
    - If not accepting, return a completed `Failed` result with `"MBBSEmu isn't running"`.
    - Otherwise, enqueue and call `TriggerProcessing()`.
  - **`WorkerThread`:**
    - Right after `WaitForNextTick()`, process every queued request.
    - After the `while` loop exits, set accepting to false under the lock, then complete every queued request with `Failed` and `"MBBSEmu is shutting down"`.
  - **Processing one request** (private `LoadModuleNow(ModuleConfiguration)`, on the worker):
    1. If a loaded module's key equals the identifier (`OrdinalIgnoreCase`), return `AlreadyLoaded` with that module's name and key.
    2. If the folder doesn't exist, return `NotFound(folder)`. If `_fileUtility.FindFile(folder, $"{id}.MDF")` doesn't resolve to an existing file, return `NotFound(Path.Combine(folder, $"{id}.MDF"))`.
    3. If `MenuOptionKey` is empty, `"X"`, or already used by a loaded module, replace it with `ModuleMenuKeys.Next(loaded keys + "X")`. If that returns null, return `Failed("Every main menu key is in use")`.
    4. Apply `ModuleEnabled ??= true`.
    5. Call `new MbbsModule(_fileUtility, Clock, Logger, config, null, _messagingCenter)`, then `AddModule(module)`. If either throws, call `RemoveModuleArtifacts(id)` and return `Failed(e.Message)`.
    6. Apply the same initialization check as `Start` (`MainModuleDll.EntryPoints.Count == 1 && (bool)ModuleEnabled`). If it fails, log `"{id} not properly initialized, removing"`, call `RemoveModuleArtifacts(id)` and return `Failed("The module didn't initialize")`.
    7. Return `Loaded(id, name, key)`.

- [ ] **Step 8: Run both test classes to verify they pass.**
  Expected: all `ModuleMenuKeys_Tests` and `RuntimeModuleLoad_Tests` pass.

- [ ] **Step 9: Commit**
  ```bash
  git add MBBSEmu/Module/ModuleMenuKeys.cs MBBSEmu/Module/ModuleLoadResult.cs MBBSEmu/HostProcess/IMbbsHost.cs MBBSEmu/HostProcess/MbbsHost.cs MBBSEmu.Tests/Module/ModuleMenuKeys_Tests.cs MBBSEmu.Tests/Integration/RuntimeModuleLoad_Tests.cs MBBSEmu.Tests/Integration/MBBSEmuIntegrationTestBase.cs
  git commit -m "Load modules into a running host on its worker thread"
  ```

### Task 2: Append to modules.json without disturbing it

**Files:**
- Create: `MBBSEmu/Module/ModuleListFile.cs`
- Test: `MBBSEmu.Tests/Module/ModuleListFile_Tests.cs`

**Interfaces:**
- Produces:
  - `public static class ModuleListFile { public const string DefaultFileName = "modules.json"; public static void Append(string path, string identifier, string modulePath, string menuOptionKey); }`
  - `Append` throws `InvalidDataException` when the existing file isn't a module list it can add to. IO errors propagate.

- [ ] **Step 1: Write the failing tests.** Use a temp directory per test, deleted in `Dispose`. Read results back with `JsonNode.Parse`:
  - `Append_NoFile_CreatesModuleList`
    - The root has only `Modules`, with one object `{"Identifier":"MJWMUT","Path":"/abs/MJWMUT","MenuOptionKey":"A"}`.
    - There is no `BasePath`.
  - `Append_WrittenFile_LoadsWithTheModuleConfigurationReader`
    - Deserialize as `ModuleConfigurationFile`, using options with `JsonModuleConfigurationFileConverter`, `JsonBooleanConverter`, `JsonStringEnumConverter` and `JsonFarPtrConverter` (the same set `Program` uses).
    - Expect identifier, path and key to match.
  - `Append_HandWrittenFile_KeepsEverythingAlreadyThere`
    - Existing file: `{"BasePath":".","Modules":[{"Identifier":"GWWARROW","Path":"GWWARROW","MenuOptionKey":"Q","Enabled":0,"Patches":[{"Name":"p","Description":"d","Segment":1,"Offset":2,"Bytes":"AQI="}]}]}`.
    - After appending: `BasePath` is still `"."`, `Modules[0]` is `JsonNode.DeepEquals` to the original, and `Modules[1]` is the new entry.
    - Read back with the reader, `Modules[1].ModulePath` equals the absolute path given, so it is not combined with `BasePath`.
  - `Append_InvalidJson_ThrowsAndLeavesFileUnchanged`: content `{ not json`. Expect `InvalidDataException`, and the file bytes are unchanged.
  - `Append_ModulesNotAnArray_Throws`: `{"Modules":{}}` gives `InvalidDataException`.
  - `Append_LeavesNoTemporaryFile`: after two appends, the directory contains only `modules.json`.

- [ ] **Step 2: Run them to verify they fail.**
  Run: `dotnet test MBBSEmu.Tests --filter "FullyQualifiedName~ModuleListFile_Tests"`
  Expected: build error, `ModuleListFile` does not exist.

- [ ] **Step 3: Implement `Append`.**
  - Parse with `JsonNode.Parse` using default document options (no comments or trailing commas, matching the `-C` reader). A `JsonException`, or a root that isn't an object, becomes `InvalidDataException` with the reason.
  - If `Modules` is missing, create it. If it isn't a `JsonArray`, throw `InvalidDataException`.
  - Add the entry and write it indented to `path + ".tmp"`, then `File.Move(tmp, path, overwrite: true)`.
  - On failure, delete the temporary file (best effort) and rethrow.

- [ ] **Step 4: Run the tests to verify they pass.** Expected: 6 passed.

- [ ] **Step 5: Commit**
  ```bash
  git add MBBSEmu/Module/ModuleListFile.cs MBBSEmu.Tests/Module/ModuleListFile_Tests.cs
  git commit -m "Add modules to modules.json without disturbing what is already there"
  ```

### Task 3: ModuleLoader: picked MDF to loaded, saved and explained

**Files:**
- Create: `MBBSEmu/UI/Modules/IModuleLoader.cs`, `MBBSEmu/UI/Modules/ModuleLoader.cs`, `MBBSEmu/UI/Modules/ModuleLoadText.cs`
- Test: `MBBSEmu.Tests/UI/Modules/ModuleLoader_Tests.cs`, `MBBSEmu.Tests/UI/Modules/ModuleLoadText_Tests.cs`

**Interfaces:**
- Consumes: `ModuleLoadResult`, `EnumModuleLoadStatus` (Task 1); `ModuleListFile.Append` (Task 2).
- Produces (namespace `MBBSEmu.UI.Modules`):
  - `public sealed record ModuleLoadReport(bool IsError, string Title, string Message);`
  - `public interface IModuleLoader { string StartFolder { get; } int LoadedCount { get; } Task<ModuleLoadReport> LoadAsync(string mdfPath); }`
  - `public sealed record ModuleSaving(string ModulesFile, string SessionOnlyReason) { public static ModuleSaving To(string modulesFile); public static ModuleSaving SessionOnly(string reason); }`
  - `public sealed class ModuleLoader : IModuleLoader`:
    - constructor `ModuleLoader(Func<ModuleConfiguration, Task<ModuleLoadResult>> loadIntoHost, ModuleSaving saving, string startFolder, string playHint)`
    - `public Task<string> LoadSavedAsync(ModuleConfiguration saved)` returns null when the module loaded (or was already loaded), and otherwise the skip line.
  - `ModuleLoadText` (exact copy):
    - Titles:
      - `LoadedTitle = "Module Loaded"`
      - `AlreadyLoadedTitle = "Already Loaded"`
      - `FailedTitle = "Couldn't Load Module"`
      - `SkippedTitle = "Some Modules Were Skipped"`
      - `PickerTitle = "Load Module"`
      - `MdfFiles = "Module definition (.MDF)"`
    - Fixed text:
      - `NoModulesHint = "No modules loaded yet — File → Load Module (Ctrl+O)"`. This is shorter than the spec's wording, so it fits on the menu row at 80 columns.
      - `StartedWithConfiguration = "MBBSEmu was started with a module configuration"`
      - `UnreadableModulesFileReason = "modules.json couldn't be read"`
    - `Loaded(name, key)` → `$"{name} is loaded. It's on the main menu as {key}."`
    - `LoadedForSession(name, key, reason)` → `$"{name} is loaded for this session only ({reason}). It's on the main menu as {key}."`
    - `LoadedNotSaved(name, key, reason)` → `$"{name} is loaded, but it couldn't be saved to modules.json ({reason}), so you'll need to load it again next time. It's on the main menu as {key}."`
    - `AlreadyLoaded(name)` → `$"{name} is already loaded."`
    - `NotFound(id, path)` → `$"Couldn't find {id} at {path}."`
    - `Failed(id, reason)` → `$"{id} couldn't be loaded:\n{reason}\n\nNothing was saved, and MBBSEmu is still running."`
    - `NotAnMdf(fileName)` → `$"{fileName} isn't a module definition. Choose the module's .MDF file."`
    - `SkippedNotFound(id, path)` → `$"Skipped {id}: not found at {path}"`. This names the identifier only, because the module's display name lives in the MDF, which is the thing that's missing. The spec's example `"Skipped Mutants! (MJWMUT): …"` can't be produced.
    - `SkippedFailed(id, reason)` → `$"Skipped {id}: {reason}"`
    - `ModulesFileUnreadable(path, reason)` → `$"{path} couldn't be read, so no saved modules were loaded and it won't be changed:\n{reason}"`
    - `PlayHint(bool telnetEnabled, int telnetPort, bool rloginEnabled, int rloginPort)`:
      - Telnet on: `$"Connect a telnet client to localhost, port {telnetPort}, to play."`
      - Only Rlogin on: `$"Telnet is turned off, so connect with Rlogin to localhost, port {rloginPort}, to play."`
      - Both off: `"Telnet and Rlogin are both turned off, so nobody can connect to play yet."`

- [ ] **Step 1: Write the failing tests.**
  - **`ModuleLoadText_Tests`:** the three `PlayHint` cases, with exact strings.
  - **`ModuleLoader_Tests`:**
    - Setup:
      - A fake host is a `Func` that records every `ModuleConfiguration` and returns a scripted `ModuleLoadResult`.
      - Use a temp directory, with `hint = "HINT"`.
      - The picked file is `<temp>/MJWMUT/MJWMUT.MDF`. The file need not exist, because the fake host decides.
    - Tests:
      - `LoadAsync_Loaded_SavesAndExplainsHowToPlay`
        - The host gets `{ModuleIdentifier="MJWMUT", ModulePath="<temp>/MJWMUT", MenuOptionKey=null, ModuleEnabled=true}` and returns `new ModuleLoadResult(EnumModuleLoadStatus.Loaded, "MJWMUT", "Mutants!", "A", null)`. The other tests script results the same way.
        - The report is `(false, "Module Loaded", Loaded("Mutants!","A") + "\n\nHINT")`.
        - `modules.json` has an entry with that absolute folder and key `"A"`.
        - `LoadedCount == 1`, and `StartFolder` is the module folder.
      - `LoadAsync_SessionOnly_WritesNothing`: with `ModuleSaving.SessionOnly(StartedWithConfiguration)`, the message is `LoadedForSession(...) + "\n\nHINT"` and no file exists in the temp directory.
      - `LoadAsync_SaveFails_ReportsLoadedButNotSaved`: `modules.json` contains `{ not json`. The report is not an error and its message starts with `"Mutants! is loaded, but it couldn't be saved"`. The file is unchanged and `LoadedCount == 1`.
      - `LoadAsync_NotAnMdf_DoesNotAskTheHost`: picking `MJWMUT.DLL` gives `(true, FailedTitle, NotAnMdf("MJWMUT.DLL"))`, and the host is never called.
      - `LoadAsync_LowercaseMdf_IsAccepted`: picking `mjwmut.mdf` calls the host with identifier `"mjwmut"`.
      - `LoadAsync_Failed_ReportsErrorAndSavesNothing`: `Failed("bad dll")` gives `(true, FailedTitle, Failed("MJWMUT","bad dll"))`, and no file is written.
      - `LoadAsync_AlreadyLoaded_SavesNothing`: the result is `(false, AlreadyLoadedTitle, AlreadyLoaded("Mutants!"))`.
      - `LoadAsync_HostThrows_ReportsError`: a thrown `InvalidOperationException("boom")` gives `(true, FailedTitle, Failed("MJWMUT","boom"))`.
      - `LoadSavedAsync_Loaded_ReturnsNullAndCounts`
      - `LoadSavedAsync_NotFound_ReturnsSkipLine`: `NotFound` with `Detail` `"/gone/MJWMUT"` gives `SkippedNotFound("MJWMUT","/gone/MJWMUT")`. No file is written in either `LoadSavedAsync` test.

- [ ] **Step 2: Run them to verify they fail.**
  Run: `dotnet test MBBSEmu.Tests --filter "FullyQualifiedName~MBBSEmu.Tests.UI.Modules"`
  Expected: build error.

- [ ] **Step 3: Implement the three files.**
  - **The picked file:** check the extension with `OrdinalIgnoreCase`. Then take `folder = Path.GetDirectoryName(Path.GetFullPath(mdfPath))` and `id = GetFileNameWithoutExtension`.
  - **What gets saved:** `ModuleListFile.Append` gets `folder`, not the result's path, because `MbbsModule` appends a separator to the config it is given.
  - **Save failures:** catch only `IOException`, `UnauthorizedAccessException` and `InvalidDataException`, and use the exception's `Message` as the reason.
  - **Thread safety:** `LoadedCount` uses `Interlocked`, and `StartFolder` is a `volatile` field (continuations run on pool threads).

- [ ] **Step 4: Run the tests to verify they pass.** Expected: all pass.

- [ ] **Step 5: Commit**
  ```bash
  git add MBBSEmu/UI/Modules MBBSEmu.Tests/UI/Modules
  git commit -m "Turn a picked module definition into a loaded, saved and explained module"
  ```

### Task 4: File → Load Module in the admin console

**Files:**
- Modify:
  - `MBBSEmu/UI/Main/AdminConsoleKeys.cs`
  - `MBBSEmu/UI/Main/StatusLine.cs`
  - `MBBSEmu/UI/Main/AdminConsoleView.cs`
  - `MBBSEmu/UI/Main/IAdminConsoleDialogs.cs`
  - `MBBSEmu/UI/Main/TerminalGuiAdminConsoleDialogs.cs`
  - `MBBSEmu/UI/Main/AdminConsole.cs`
- Test: `MBBSEmu.Tests/UI/Main/AdminConsoleKeys_Tests.cs`, `StatusLine_Tests.cs`, `AdminConsoleView_Tests.cs`, `TerminalGuiAdminConsoleDialogs_Tests.cs`, `AdminConsole_Tests.cs`

**Interfaces:**
- Consumes: `IModuleLoader`, `ModuleLoadReport`, `ModuleLoadText` (Task 3).
- Produces:
  - `EnumConsoleKeyAction.LoadModule`
  - `IAdminConsoleDialogs`:
    - `string PickModuleDefinition(string startFolder)` returns the full path, or null when cancelled.
    - `void ShowMessage(string title, string message)`
    - `void ShowError(string title, string message)`
  - `AdminConsoleView`:
    - `event EventHandler LoadModuleRequested`
    - `internal MenuItem LoadModuleMenuItem`
    - `public void SetNoModulesHint(bool show)`
  - `AdminConsoleOptions.ModuleLoader` (`IModuleLoader`). Null means Load Module does nothing.
  - `public void AdminConsole.ShowMessage(string title, string message)` is callable from any thread.

- [ ] **Step 1: Write the failing tests.**
  - **`AdminConsoleKeys_Tests`:**
    - `Classify(Key.O.WithCtrl)` is `LoadModule`.
    - `Decide(Key.O.WithCtrl, consoleOnTop: false)` is `None`.
  - **`StatusLine_Tests`:**
    - `AdminConsoleHints` keys are `["^Q","^O","Enter","Tab"]`, with `^O` labelled `"Load module"`.
    - `FitHints(80, AdminConsoleHints, RightText(true, true, "1234"))` keys are `["^Q","^O"]`.
    - Update any existing assertion that listed the old hints.
  - **`AdminConsoleView_Tests`** (harness at 80×24):
    - `Menu_ListsFileBeforeServerAndHelp`: on screen row 0, `"File"` appears before `"Server"`, which appears before `"Help"`.
    - `LoadModuleMenuItem_RaisesLoadModuleRequested`: invoke `LoadModuleMenuItem.Action!()` the way the existing Shutdown test does.
    - `NoModulesHint_ShownRightAlignedOnTheMenuRow`: after `SetNoModulesHint(true)`, row 0 `TrimEnd()` ends with `ModuleLoadText.NoModulesHint`.
    - `NoModulesHint_HiddenByDefaultAndAfterFalse`
    - `NoModulesHint_FollowsResize`: show the hint, resize to 100×30, and row 0 still ends with the hint.
  - **`TerminalGuiAdminConsoleDialogs_Tests`:**
    - `PickModuleDefinition_ShowsOnlyMdfFiles`
      - A temp folder holds `GAME.MDF`, `GAME.DLL` and `game2.mdf`.
      - Open the picker on it. In a 300 ms timeout, the screen contains `GAME.MDF` and `game2.mdf` and not `GAME.DLL`. Then call `RequestStop(TopRunnable)`, and the result is null.
    - `PickModuleDefinition_MissingStartFolder_OpensAnyway`: a nonexistent folder doesn't throw, and the result is null after `RequestStop`.
  - **`AdminConsole_Tests`:**
    - Extend `FakeDialogs` with:
      - `ConcurrentQueue<string> PickAnswers` (an empty queue answers null)
      - `int PickAsks`
      - `string LastStartFolder`
      - `ConcurrentQueue<(string, string)> Messages` and `Errors`
    - Add a `FakeModuleLoader : IModuleLoader` with:
      - `StartFolder` `"/start"`
      - a settable `LoadedCount`
      - `LoadAsync` that records paths and returns `NextResult`, a settable `Task<ModuleLoadReport>`
    - Pass the loader through a new optional `Run(..., IModuleLoader moduleLoader = null)` parameter.
    - Tests:
      - `CtrlO_PicksLoadsAndShowsTheResult`
        - Pick answer: `"/m/MJWMUT.MDF"`.
        - The result `(false, "Module Loaded", "msg")` appears in `Messages`.
        - `LastStartFolder` is `"/start"`, and the loader got that path.
        - Finish with a confirmed Ctrl+Q.
      - `CtrlO_PickerCancelled_LoadsNothing`
      - `CtrlO_WhileLoading_DoesNotPickAgain`: `NextResult` is an incomplete `TaskCompletionSource` task. Ctrl+O twice gives `PickAsks == 1`.
      - `LoadError_IsShownAsAnError`
      - `LoadFinishingDuringShutdown_ShowsNothing`: the load starts, shutdown begins, then the `TaskCompletionSource` completes. `Messages` and `Errors` stay empty.
      - `NoModuleLoader_CtrlODoesNothing`: `PickAsks == 0`.
      - `ShowMessage_FromStartServices_ShowsDialog`: `startServices` calls `_console.ShowMessage("t", "m")`, and `Messages` contains `("t", "m")`.
      - `NoModulesHint_ShownOnlyWhileNothingIsLoaded`
        - `LoadedCount` 0: after startup completes, `app.ToString()` contains `NoModulesHint`.
        - Set `LoadedCount` to 1: after 600 ms it doesn't.

- [ ] **Step 2: Run them to verify they fail.**
  Run: `dotnet test MBBSEmu.Tests --filter "FullyQualifiedName~MBBSEmu.Tests.UI.Main"`
  Expected: build errors for the new members.

- [ ] **Step 3: Implement.**
  - **Keys:** Ctrl+O gives `LoadModule` in `Classify`.
  - **Status line:** add the `^O` hint second.
  - **Menu:** a `_File` menu with `new MenuItem("_Load Module…", "Ctrl+O", …, Key.Empty)` goes before `_Server`.
  - **Hint:** a `Label` in the `ConsoleTheme.Menu` scheme, added right after `_menuBar` and hidden by default.
    - It sits at `Y = 0`, `X = width - text.Length - 1`, and is repositioned in `ApplyScreenSize`.
    - It is hidden while the too-small view shows. 80 columns always fit, so there's no overlap check.
  - **Picker:** `new OpenDialog` with:
    - `Title = PickerTitle`
    - `OpenMode = OpenMode.File`
    - `MustExist = true`
    - `AllowedTypes = [new AllowedType(MdfFiles, ".MDF", ".mdf")]`
    - `Path` set to `startFolder` plus a trailing separator when the folder exists, otherwise the user profile folder

    Run it with `_app.Run`, and return null when `Canceled` or the path is empty.
  - **Message boxes:** `ShowMessage` uses `MessageBox.Query(_app, title, message, AdminConsoleText.OkButton)`, and `ShowError` uses `MessageBox.ErrorQuery` with the same arguments.
  - **Controller:**
    - `LoadModule()` runs on the UI thread. It returns at once when:
      - there is no loader;
      - a load is already in progress;
      - the shutdown phase isn't `Running` (checked before and again after the picker);
      - the picker returned null.
    - Otherwise it sets the in-progress flag and starts `LoadAsync`.
    - The `ContinueWith` callback (on `TaskScheduler.Default`) marshals back with `_app.Invoke` wrapped in `try`/`catch (ObjectDisposedException)`, clears the flag, and shows the report if the phase is still `Running` and the console isn't ending. A faulted task shows `ShowError(FailedTitle, innermost message)`.
    - `ShowMessage` uses the same guarded `Invoke`.
  - **Refresh** calls `SetNoModulesHint(loader != null && _startTask?.IsCompleted == true && _startException == null && loader.LoadedCount == 0)`.

- [ ] **Step 4: Run the tests to verify they pass.**
  Run: the same filter, then `dotnet test MBBSEmu.Tests --filter "FullyQualifiedName~MBBSEmu.Tests.UI"`.
  Expected: all pass.

- [ ] **Step 5: Commit**
  ```bash
  git add MBBSEmu/UI/Main MBBSEmu.Tests/UI/Main
  git commit -m "Add File > Load Module (Ctrl+O) to the admin console"
  ```

### Task 5: Setup Wizard saves next to the program and keeps existing settings

**Files:**
- Modify:
  - `MBBSEmu/UI/Setup/SetupWizardModel.cs`
  - `SetupWizardView.cs`
  - `SetupWizard.cs`
  - `ISetupWizardDialogs.cs`
  - `TerminalGuiSetupWizardDialogs.cs`
  - `SetupWizardText.cs`
- Test: `MBBSEmu.Tests/UI/Setup/SetupWizardModel_Tests.cs`, `SetupWizardView_Tests.cs`

**Interfaces:**
- Produces:
  - `SetupWizardModel.LoadFrom(JsonObject settings)`
  - `SetupWizardView(SetupWizardModel model, ISetupWizardDialogs dialogs, string settingsPath, Action<string, string> writeFile)`
  - `SetupWizard.Run(string settingsPath)`
  - `ISetupWizardDialogs.AskSavePath` is removed.

- [ ] **Step 1: Write the failing tests.**
  - **`SetupWizardModel_Tests`:**
    - `LoadFrom_ReadsTheSettingsFile`
      - Input: `{"BBS.Title":"My BBS","BBS.Channels":"4","Cleanup.Time":"04:30","GSBL.BTURNO":"12345678","Module.DoLoginRoutine":"False","Telnet.Enabled":"False","Telnet.Port":"2323","Telnet.Heartbeat":"True","Rlogin.Enabled":"True","Rlogin.Port":"5513","Rlogin.RemoteIP":"10.0.0.1","Rlogin.PortPerModule":"True","Database.File":"bbs.db","Btrieve.CacheSize":"8","Account.DefaultKeys":["DEMO","SYSOP"]}`.
      - Every matching property is set, `CleanupTime` is `04:30`, and `Keys` is `["DEMO","SYSOP"]`.
    - `LoadFrom_AcceptsJsonBooleansAndNumbers`: `"Telnet.Enabled": true` and `"Btrieve.CacheSize": 8` are read.
    - `LoadFrom_IgnoresUnusableValues`: `"Cleanup.Time":"late"` keeps `03:00`, and `"GSBL.BTURNO":"123"` keeps the random number.
    - `ToJson_AfterLoadFrom_KeepsSettingsTheWizardDoesNotEdit`
      - Input includes `"Telnet.ConvertCP437ToUTF8":"True"`, `"ANSI.Login":"login.ans"` and `"Timer.Hertz":"18"`.
      - After `BbsName = "New"`, the output keeps those three exactly and has `"BBS.Title":"New"`.
  - **`SetupWizardView_Tests`:**
    - Remove `AskSavePath` from the fake.
    - Change `FinishSaved_WritesJsonAndReports` to construct the view with `"/home/appsettings.json"`, and assert the write and `ShowSaved` used that path.
    - Delete `SaveCancelled_ReturnsNotSaved`, since there is no save dialog any more.

- [ ] **Step 2: Run them to verify they fail.**
  Run: `dotnet test MBBSEmu.Tests --filter "FullyQualifiedName~MBBSEmu.Tests.UI.Setup"`
  Expected: build errors.

- [ ] **Step 3: Implement.**
  - **Edited keys:** keep one `static readonly string[] EditedKeys`, holding the 15 keys above. `LoadFrom` reads only these, and `ToJson` merges only these.
  - **`LoadFrom`:**
    - Read each JSON value's text with `JsonValue.ToString()`. Booleans go through `bool.TryParse`, and `CleanupTime` through `TimeSpan.TryParseExact(@"hh\:mm", InvariantCulture)`.
    - A registration number is used only if it matches `^\d{8}$`.
    - Keys: clear them, then call `AddKey` for each string, so the wizard's own rules apply.
    - Text fields are taken as written; the wizard validates them on Next.
    - Store `settings.DeepClone()` for merging.
  - **`ToJson`:**
    - With nothing loaded, it behaves as today.
    - Otherwise, clone the stored object, overwrite the `EditedKeys` with values from `JsonSerializer.SerializeToNode(ToAppSettings())`, and write it indented.
  - **`Finish`:** skip the save dialog and write to `settingsPath`.
  - **Remove** `AskSavePath`, plus the unused `SaveTitle`, `SaveExtension`, `JsonFiles` and `DefaultFileName`.
  - **`SetupWizard.Run(settingsPath)`:** if the file exists and `JsonNode.Parse` gives a `JsonObject`, call `LoadFrom` with it. On a `JsonException`, start from the defaults.

- [ ] **Step 4: Run the tests to verify they pass.** Expected: all `UI.Setup` tests pass.

- [ ] **Step 5: Commit**
  ```bash
  git add MBBSEmu/UI/Setup MBBSEmu.Tests/UI/Setup
  git commit -m "Setup Wizard saves next to MBBSEmu and keeps settings it doesn't edit"
  ```

### Task 6: Start into the admin console with no arguments

**Files:**
- Create: `MBBSEmu/UI/GuiStartup.cs`
- Modify: `MBBSEmu/UI/UserInterfaceModeSelector.cs`, `MBBSEmu/Program.cs`
- Test: `MBBSEmu.Tests/UI/GuiStartup_Tests.cs`, `MBBSEmu.Tests/UI/UserInterfaceModeSelector_Tests.cs`

**Interfaces:**
- Consumes:
  - `ModuleLoader`, `ModuleSaving`, `ModuleLoadText` (Task 3)
  - `AdminConsoleOptions.ModuleLoader` and `AdminConsole.ShowMessage` (Task 4)
  - `SetupWizard.Run(string)` (Task 5)
  - `ModuleListFile.DefaultFileName` (Task 2)
  - `IMbbsHost.LoadModule` (Task 1)
- Produces:
  - `public static class GuiStartup { public static string HomeFolder { get; } public static bool IsFirstRun(string homeFolder); }`
  - `UserInterfaceModeSelector.IsGuiMode(int argumentCount, bool interactiveTerminal)`

- [ ] **Step 1: Write the failing tests.**
  - **`UserInterfaceModeSelector_Tests`:** `IsGuiMode(0, true)` is true; `(0, false)` and `(1, true)` are false.
  - **`GuiStartup_Tests`** (temp folder):
    - `IsFirstRun_NoSettings_True`
    - `IsFirstRun_SettingsWithoutDatabase_True`: settings `{}` with no `mbbsemu.db`.
    - `IsFirstRun_SettingsAndDefaultDatabase_False`
    - `IsFirstRun_UsesDatabaseFileSetting`: `"Database.File":"data/custom.db"` with `custom.db` in the folder gives false, and true once it's deleted.
    - `IsFirstRun_InvalidSettingsJson_False`: the normal startup reports it.
    - `HomeFolder_IsTheExecutablesFolder`: equals `AppContext.BaseDirectory`.

- [ ] **Step 2: Run them to verify they fail.**
  Run: `dotnet test MBBSEmu.Tests --filter "FullyQualifiedName~GuiStartup_Tests|FullyQualifiedName~UserInterfaceModeSelector_Tests"`
  Expected: build errors.

- [ ] **Step 3: Implement `GuiStartup` and `IsGuiMode`.**
  - `IsFirstRun` reads `appsettings.json` with `JsonNode.Parse`.
  - It takes `Path.GetFileName` of `Database.File` (default `mbbsemu.db`) and checks that file in `homeFolder`.
  - A `JsonException` returns false.

- [ ] **Step 4: Run the tests to verify they pass.**

- [ ] **Step 5: Wire up `Program`.** Every edit below is reached only when `_isGuiMode` is set, or only touches the admin console:
  - **Start of `Run`:**
    - Replace the no-arguments block with:
      - `var interactive = TerminalEnvironment.IsInteractive();`
      - If `args.Length == 0 && !interactive`, call `PrintHelp(false)` and return (unchanged).
      - If `UserInterfaceModeSelector.IsGuiMode(args.Length, interactive) && !StartGuiMode()`, return.
    - Delete the `Y/n` prompt and the "Restart MBBSEmu" text.
  - **`StartGuiMode()`:**
    - `Directory.SetCurrentDirectory(GuiStartup.HomeFolder)`.
    - If `GuiStartup.IsFirstRun(...)`:
      - print `"Starting MBBSEmu Setup Wizard..."`;
      - call `SetupWizard.Run(Path.Combine(home, DefaultEmuSettingsFilename))`;
      - if it wasn't saved, print `SetupWizardText.CancelledMessage` and return false.
    - Set `_isGuiMode = true` and return true.
    - The existing missing-database check later calls `DatabaseReset`, which prompts for the sysop password in the terminal.
  - **Module file reading:**
    - Move the `-C` branch body verbatim into `private void LoadModuleConfigurationFile(string fileName)`, with `_moduleConfigFileName` replaced by `fileName`. The `-C` branch calls it.
    - Add `else if (_isGuiMode) LoadDefaultModuleList();` before the final `else`.
  - **`LoadDefaultModuleList()`:**
    - `_moduleSaving = ModuleSaving.To(Path.Combine(cwd, ModuleListFile.DefaultFileName))`.
    - If the file exists, call `LoadModuleConfigurationFile`.
    - On `JsonException`, `IOException` or `UnauthorizedAccessException`:
      - clear `_moduleConfigurations`;
      - set `_modulesFileProblem = ModuleLoadText.ModulesFileUnreadable(file, e.Message)` and log it as a warning;
      - set `_moduleSaving = ModuleSaving.SessionOnly(ModuleLoadText.UnreadableModulesFileReason)`.
  - **Creating the loader:**
    - After `var host = …`: `if (_isGuiMode) _moduleLoader = CreateModuleLoader(host, configuration, _moduleSaving);`
    - `CreateModuleLoader` uses:
      - `startFolder` = the last saved module's `ModulePath`, else the current directory;
      - `playHint = ModuleLoadText.PlayHint(TelnetEnabled, TelnetPort, RloginEnabled, RloginPort)`.
  - **`StartServices`:**
    - It returns `IReadOnlyList<string>`: the skip lines. The CLI caller ignores the result.
    - The first line becomes `var skipped = _isGuiMode ? StartGuiModules(host) : StartModules(host);`, where `StartModules` is exactly today's `host.Start(_moduleConfigurations)` and returns an empty list. The rest is unchanged, so Rlogin port-per-module still iterates `_moduleConfigurations`.
  - **`StartGuiModules(host)`:**
    - Call `host.Start(new List<ModuleConfiguration>())`.
    - Collect `_modulesFileProblem` (if any) as the first line.
    - Take the saved list, clear `_moduleConfigurations`, and for each saved module call `_moduleLoader.LoadSavedAsync(m).GetAwaiter().GetResult()`.
      - On null, re-add it.
      - Otherwise, log the line as a warning and collect it.
  - **`RunAdminConsole`:**
    - `_moduleLoader ??= CreateModuleLoader(host, configuration, ModuleSaving.SessionOnly(ModuleLoadText.StartedWithConfiguration));`
    - Build the console into a local, with `ModuleLoader = _moduleLoader`.
    - Run it with `() => { var skipped = StartServices(host, configuration); if (skipped.Count > 0) console.ShowMessage(ModuleLoadText.SkippedTitle, string.Join("\n", skipped)); }`.

- [ ] **Step 6: Build and run the touched test classes.**
  Run: `dotnet build && dotnet test MBBSEmu.Tests --filter "FullyQualifiedName~MBBSEmu.Tests.UI|FullyQualifiedName~RuntimeModuleLoad_Tests|FullyQualifiedName~ModuleListFile_Tests"`
  Expected: build succeeds with no new warnings, and all tests pass.

- [ ] **Step 7: Commit**
  ```bash
  git add MBBSEmu/UI/GuiStartup.cs MBBSEmu/UI/UserInterfaceModeSelector.cs MBBSEmu/Program.cs MBBSEmu.Tests/UI/GuiStartup_Tests.cs MBBSEmu.Tests/UI/UserInterfaceModeSelector_Tests.cs
  git commit -m "With no arguments, start into the admin console from MBBSEmu's own folder"
  ```

### Task 7: Ship no sample modules.json, keep saved settings, document it

**Files:**
- Modify: `MBBSEmu/MBBSEmu.csproj`, `MBBSEmu/Assets/commandLineHelp.txt`, `README.md`, `MBBSEmu/CLAUDE.md`
- Delete: `MBBSEmu/modules.json`

- [ ] **Step 1: Write the failing check.** Build into a fresh folder:
  ```bash
  dotnet build MBBSEmu/MBBSEmu.csproj -o $SCRATCH/buildcheck
  test ! -e $SCRATCH/buildcheck/modules.json
  ```
  Expected: fails, because `modules.json` is still produced. (Its source is deleted in the working copy, but the item still exists; if the build already skips it, note that in the ledger.)

- [ ] **Step 2: Implement.**
  - **`MBBSEmu.csproj`:**
    - Remove the `<None Update="modules.json">` item.
    - Change `appsettings.json` `CopyToOutputDirectory` from `Always` to `PreserveNewest`. Keep `CopyToPublishDirectory` and `ExcludeFromSingleFile`.
  - Run `git rm MBBSEmu/modules.json`.
  - **`commandLineHelp.txt`:** after the usage line, add:
    `Run MBBSEmu with no options in a terminal to open the admin console. It keeps appsettings.json, modules.json and the BBS databases next to the MBBSEmu program; use File > Load Module to add a module.`
  - **`README.md`:** before step 3, add:
    `Or skip the command line: run MBBSEmu with no options and choose File → Load Module to pick a module's .MDF file. MBBSEmu remembers it next time.`
  - **`MBBSEmu/CLAUDE.md`:** after the Boot sequence line, add:
    `With no arguments in a terminal (GUI mode), Program changes to the executable's folder, runs the Setup Wizard on first run, loads the default modules.json one module at a time (skipping broken ones), and the admin console's File → Load Module loads more into the running host.`

- [ ] **Step 3: Verify.**
  - Re-run Step 1. Expected: passes.
  - Then `echo '{"x":1}' > $SCRATCH/buildcheck/appsettings.json`, re-run the build into the same folder, and `grep -q '"x"' $SCRATCH/buildcheck/appsettings.json`. Expected: still there.

- [ ] **Step 4: Commit**
  ```bash
  git add MBBSEmu/MBBSEmu.csproj MBBSEmu/Assets/commandLineHelp.txt README.md MBBSEmu/CLAUDE.md
  git commit -m "Stop shipping a sample modules.json and stop overwriting saved settings on build"
  ```
  The deletion of `MBBSEmu/modules.json` is already staged by `git rm`.

### Task 8: End-to-end checks in a real terminal

Throwaway scripts live in the plan workspace and are not committed.

**Setup:**
- `dotnet publish MBBSEmu/MBBSEmu.csproj -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true -o $SCRATCH/gui-home`
- Drive it through a pty from a *different* working directory, `$SCRATCH/elsewhere`, the way `tui_smoke.py` does.

- [ ] **Step 1: First run.**
  - Start with no arguments. The wizard opens with no `Y/n` prompt.
  - Set the Telnet port to `2323` and finish. Expect `Enter New Password for sysop:` twice in the terminal, then the console.
  - Row 0 ends with `NoModulesHint`.
  - `appsettings.json`, `mbbsemu.db`, `BBSGEN.DB` and `BBSUSR.DB` exist in `gui-home`, and none exist in `elsewhere`.
- [ ] **Step 2: Load a module.**
  - Press Ctrl+O, type the full path to `modules/MJWMUT/MJWMUT.MDF` and press Enter.
  - The dialog reads `… is loaded. It's on the main menu as A.` plus the port-2323 hint.
  - `gui-home/modules.json` has `MJWMUT` with the absolute folder and key `A`, and the hint is gone.
  - Telnet to 2323 and log in as sysop: the main menu lists option `A`.
- [ ] **Step 3: Restart.** Ctrl+Q, then start again with no arguments: no wizard, `(MJWMUT) Module Added!` appears in the Application Log, and there is no hint.
- [ ] **Step 4: Moved module.** Rename the module folder and restart.
  - The "Some Modules Were Skipped" dialog shows `Skipped MJWMUT: not found at …`.
  - `modules.json` is byte-identical.
  - Restore the folder afterwards.
- [ ] **Step 5: Unreadable modules.json.** Write `{ not json` and restart.
  - The dialog explains the problem.
  - Ctrl+O loading MJWMUT reports `for this session only (modules.json couldn't be read)`, and the file is unchanged.
- [ ] **Step 6: Command line unchanged.**
  - From the repo root, `MBBSEmu -CLI -S <appsettings> -C modules/modules.json -DBRESET sysop` logs `(MJWMUT) Module Added!` and `Started MBBSEmu Build`, as before this branch.
  - `MBBSEmu < /dev/null` prints the help.
  - `MBBSEmu -S <appsettings> -C <file containing {"Modules": []}>` opens the console. Ctrl+O loading MJWMUT reports `for this session only (MBBSEmu was started with a module configuration)`, and that file is unchanged.
- [ ] **Step 7: Full suite.**
  - Run the full suite in the Linux container against a synced copy:
    `rsync -a --delete --exclude bin --exclude obj --exclude .git --exclude .superpowers <repo>/ $SCRATCH/repo-suite/ && docker run --rm -v $SCRATCH/repo-suite:/src -v mbbsemu-nuget:/root/.nuget -w /src mcr.microsoft.com/dotnet/sdk:10.0 dotnet test`
    Expected: every test passes.
  - Then run `dotnet test MBBSEmu.Tests --filter "FullyQualifiedName~MBBSEmu.Tests.UI"` natively. Expected: all pass.
  - Record both results in the ledger.
