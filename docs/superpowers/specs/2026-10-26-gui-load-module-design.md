# Load Module from the Admin Console — Design

**Date:** 2026-10-26
**Status:** Approved
**Builds on:** `docs/superpowers/specs/2026-10-26-tui-terminal-gui-v2-design.md` (the Terminal.Gui v2 admin console and Setup Wizard, branch `feature/terminal-gui-v2`)
**Branch:** `feature/gui-load-module`

## Intent

MBBSEmu should be as easy to start as an NES emulator: download the program, download a module, start MBBSEmu, and "load the ROM." Many users aren't technical. They don't know what a JSON file is and shouldn't need to learn. Today, running a module means writing command-line arguments or a `modules.json` by hand.

This feature makes the full-screen admin console the friendly path. With no arguments, MBBSEmu opens the admin console, loads whatever modules the user loaded last time, and offers **File → Load Module** to add more. Technical users keep every command-line option exactly as it works today, including headless and Docker use.

## Goals

1. **No arguments opens the admin console.** Starting MBBSEmu with no arguments in a terminal (double-clicking the published executable, or `dotnet run`) opens it straight into the admin console.
2. **First run opens the Setup Wizard.** On first run (see Open Questions), the Setup Wizard opens directly with no `Y/n` prompt, then continues into the admin console once it's saved.
3. **File → Load Module.** The user picks a module's `.MDF` file, and the module loads immediately and becomes playable without restarting MBBSEmu.
4. **Loaded modules are remembered.** They're saved to a default `modules.json` and loaded automatically the next time MBBSEmu starts with no arguments.
5. **Files live next to the program.** The default `modules.json`, `appsettings.json` and the BBS databases are kept in the folder that holds the MBBSEmu executable, regardless of the folder it was started from.

## Non-goals

- **Removing or managing modules.** A future "Manage Modules" window will handle unloading, disabling, reordering and editing menu keys. Until then, a module is removed by editing `modules.json` or deleting the module's folder.
- **Loading straight from a `.zip`.** The user unzips the module somewhere first.
- **Changing any command-line behavior:** `-CLI`, `-C`, `-M`, `-P`, `-K`, `-S`, `-CONSOLE`, `-EXE`, `-APIREPORT`, the database options, Docker, and the no-terminal fallback all work as today.
- **Picking a module by its `.DLL`.** A module can have several DLLs but always has exactly one `.MDF`, so the picker shows `.MDF` files only.

## Anti-goals

- Any change in what a command-line or Docker user sees or gets.
- A module that is saved but now broken or missing stops MBBSEmu from starting, or crashes it.
- MBBSEmu rewrites a configuration file the user wrote and passed with `-C`.
- A non-technical user has to open, read or edit a JSON file to get a module running.
- Loading a module while people are connected disconnects them or corrupts the running BBS.

## Decided by the project owner

- The admin console is the experience for non-technical users. Technical users stay on the command-line options, and these are unchanged.
- With no arguments and no settings yet, the Setup Wizard opens. With settings present, MBBSEmu opens into the admin console, with no modules the first time.
- **File → Load Module** shows `.MDF` files only.
- Loaded modules are saved to a default `modules.json` that is reloaded on the next start.
- `appsettings.json` and the default `modules.json` live in the folder of the MBBSEmu executable. For example, a user who extracted MBBSEmu to `~/mbbsemu` gets `~/mbbsemu/modules.json`, wherever they started it from.
- This version only loads modules. Removal comes later in a "Manage Modules" window.
- **First run** means there is no BBS user database (the `Database.File` from settings, `mbbsemu.db` by default) next to the executable.
- `appsettings.json` keeps shipping next to the executable, because command-line users rely on it; it supplies the wizard's starting values. The sample `modules.json` stops shipping. The build copies `appsettings.json` into its output only when the source is newer, so `dotnet run` doesn't overwrite saved settings.
- The sysop password on first run is asked for in the terminal, after the wizard and before the admin console opens, using the existing password prompt.
- A saved module whose folder is missing is skipped with a "not found" message and stays in `modules.json`.

## Behavior

### When this applies ("GUI mode")

GUI mode means MBBSEmu was started with **no command-line arguments** in an interactive terminal (the existing `TerminalEnvironment.IsInteractive()` check). Every other way of starting MBBSEmu behaves as today.

### Where files live

In GUI mode, MBBSEmu uses the executable's folder (`AppContext.BaseDirectory`) as its home. That folder holds:
- `appsettings.json`
- the default `modules.json`
- the BBS databases (`BBSGEN.DB`, `BBSUSR.DB`, the SQLite user database)

For `dotnet run`, the home folder is the build output folder.

### Startup

1. **First run** (no user database in the home folder): the Setup Wizard opens directly, with no `Y/n` prompt. Saving writes `appsettings.json` into the home folder. The terminal then asks for the new sysop password using the existing prompt (`Enter New Password for sysop:` twice), the databases are created, and the admin console opens. Cancelling the wizard exits with "Setup cancelled", as today, and creates no database, so the next start is still a first run.
2. **Later runs:** the admin console opens and loads every module listed in the default `modules.json`. A missing file means no modules, which is not an error.
3. **A saved module that fails** is skipped, and the console still opens. A message lists each skipped module in plain language:
   - folder or `.MDF` missing: *"Skipped Mutants! (MJWMUT): not found at /path/to/folder"*
   - fails to load or initialize: *"Skipped <name>: <reason>"*

   Skipped modules stay in `modules.json`, so a temporarily missing drive doesn't erase them. The message appears on every start until the module is back or is removed from the file; removing it from inside MBBSEmu waits for the Manage Modules window.
4. **No modules loaded:** the console shows a short hint, *"No modules loaded yet — choose File → Load Module (Ctrl+O) to add one."* The hint disappears once a module is loaded.

### File → Load Module

- **Where:** a **File** menu (before **Server** and **Help**) with **Load Module…**, plus the shortcut **Ctrl+O**. The shortcut is also shown on the status bar.
- **The picker:**
  - It shows only `.MDF` files.
  - It starts in the folder the user last loaded from, or the home folder the first time.
  - The module identifier is the MDF file name without its extension, as everywhere else in MBBSEmu.
- **Already loaded:** choosing a module that is already loaded shows *"<Module name> is already loaded."* and changes nothing.
- **Loading:** the module loads into the running BBS without a restart and without disconnecting anyone.
  - It gets the next unused menu key: `A` to `Z` (skipping `X`, which the main menu uses for log off), then `1` to `9`, then two-character keys.
  - It appears in the BBS main menu the next time a user views it.
- **Success:** a confirmation names the module and tells the user how to play, for example *"Mutants! is loaded. Connect a telnet client to localhost, port 23, to play."* If Telnet is disabled, it says so instead.
- **Failure** (unreadable MDF, missing DLLs, the module fails to initialize): a plain-language error explains what went wrong. Nothing is saved, and the BBS keeps running.
- **Saving:** on success, the module is added to the default `modules.json`. The entry records the module's folder as an absolute path, so it works from any starting folder, plus its identifier and menu key. The file is written atomically (write a temporary file, then replace), so a crash mid-save can't corrupt it.
- **A `modules.json` the user wrote:** if the file next to the executable was written by hand, MBBSEmu only appends the new entry to its `Modules` array. It edits the JSON document directly rather than re-serializing it, so `BasePath`, relative paths, `Patches`, `Enabled` and the user's menu keys stay exactly as written. An absolute `Path` is never combined with `BasePath`, so the appended entry works alongside a `BasePath`.
- **Rlogin port-per-module:** if this option is on, a module loaded while running gets its own Rlogin port from the next start.

### Started with `-C`, `-M` or `-P` in a terminal

The admin console still offers **File → Load Module**, and the module loads for that session. It is **not** saved, and the confirmation says *"…loaded for this session only (MBBSEmu was started with a module configuration)."* MBBSEmu never writes to a user's own configuration file.

## Command-line compatibility

The project owner restated this after approval: **none of these enhancements may change current command-line operation.** Concretely:

- GUI mode starts only with zero arguments and an interactive terminal. Any argument, including `-CLI`, or a missing terminal follows today's code path unchanged. The no-terminal case keeps printing help.
- The home-folder change (executable's folder), first-run detection, the terminal password prompt and the default `modules.json` all apply in GUI mode only. Command-line runs keep resolving `appsettings.json`, databases and relative paths against the current directory, as today.
- `MbbsHost` gains a way to load a module while running. Startup loading through `Start(...)`, the only path command-line runs use, is unchanged.
- The admin console started with arguments (no `-CLI`) gains **File → Load Module**, session-only. Nothing it does writes a user's files.
- Tests must pin this: the existing command-line and argument-parsing behavior stays green, and the mode decision for "no arguments + terminal" vs. "any argument" vs. "no terminal" is unit-tested.

## Constraints

- **Thread safety.** `MbbsHost.AddModule` must run on the host's worker thread. The `_modules` dictionary is a plain `Dictionary` that the worker loop enumerates constantly, and module initialization executes module code. Loading while running needs a request the worker picks up between iterations, and a result the UI waits for without blocking its own loop.
- **Format compatibility.** The saved `modules.json` uses the existing module configuration format, so `-C modules.json` loads it too. The existing JSON converter is read-only and rejects unknown top-level properties, so writes go through `System.Text.Json.Nodes` (append to `Modules`) and add no new top-level properties.
- **Failed loads leave no trace.** A module that fails to initialize while running must leave no state behind. In particular, `MbbsHost.AddModule` derives a state code from `_modules.Count`, which must not collide after a failed load.
- **Zero modules allowed.** Today `Program` exits with "You must specify a module…" when there are none. GUI mode must allow starting with zero modules.
- **No change to argument parsing.** The GUI-mode decision happens before parsing, purely on "no arguments + interactive terminal".
- **Housekeeping.** Follow the existing code style and test conventions, and use TDD. Every behavior above gets a test, except where it truly needs a real terminal; those go in the smoke scripts.

## Resolved questions

1. **First-run detection:** no user database in the home folder. `appsettings.json` keeps shipping. The sample `modules.json` stops shipping: remove its `CopyToOutputDirectory` item, and delete the file, matching the owner's working copy. `appsettings.json` switches to "copy if newer".
2. **Sysop password:** the existing terminal prompt, after the wizard and before the console.
3. **Missing modules:** skipped with a "not found" message and kept in the list.
4. **A hand-written `modules.json` next to the executable:** Load Module appends only the new entry and preserves everything else (see Saving).

## Calls made during design (not explicitly confirmed by the owner)

- No `Y/n` prompt before the wizard; saving the wizard continues straight into the console.
- A saved module that fails at startup is skipped with a message and kept in `modules.json`.
- Picker start folder; "already loaded" message; menu-key assignment order.
- Success message with connect instructions; the empty-console hint; Ctrl+O.
- With `-C`/`-M`/`-P`, Load Module works for the session only and never writes the user's file.
- Rlogin port-per-module for a newly loaded module takes effect at the next start.
- Atomic writes of `modules.json`.

## Left to the builder

- How the worker-thread hand-off is built: a queued request plus a completion signal, or similar.
- How the MDF picker is presented (Terminal.Gui `OpenDialog` with an `.MDF` filter, or similar).
- Class structure: for example a small `DefaultModuleList` that loads and saves the default file, and how GUI mode is threaded through `Program`.
- How the home folder is applied: changing the current directory at startup, or passing the folder explicitly. It must not affect any path given on the command line, and in GUI mode there are none.
