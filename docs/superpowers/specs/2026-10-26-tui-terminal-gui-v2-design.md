# Admin Console & Setup Wizard on Terminal.Gui v2 — Design

**Date:** 2026-10-26
**Status:** Draft for review
**Scope:** `MBBSEmu/UI/` (admin console `MainView`, `SetupView` wizard), the startup/shutdown code in `MBBSEmu/Program.cs` that launches them, and the logging hookup that feeds them.

## Intent

MBBSEmu's text UI is the screen a sysop watches while the BBS runs: the "admin console" with the audit log, the channel list and the application log, plus the first-run Setup Wizard. The project owner considers the current implementation clunky and unstable. The goal is to move it to the current major version of its library, Terminal.Gui v2, and rebuild it to be stable and usable on every terminal sysops actually use. That includes remote servers reached over SSH, where terminal sizes vary and change often. Features stay the same.

## Goals

1. Upgrade from Terminal.Gui 1.17.1 to **Terminal.Gui 2.5.0**, the latest stable release (published 2026-09-11).
2. **Keep feature parity** with today's admin console and Setup Wizard (inventory below).
3. **Adapt to the terminal size:** both screens lay out correctly at any size from 80×24 up and redraw cleanly when the terminal is resized.
4. **Be stable:** no garbled screens, no stray output drawn over the UI, no hangs, and the terminal is always left in a usable state on exit.
5. **Work on** Linux, macOS and Windows terminals, and over SSH, including inside tmux/screen with detach and reattach.
6. **Fall back to `-CLI` behavior automatically** when there is no terminal, for example when running as a systemd service.
7. **Fix the known bugs** listed below.

## Non-goals

- New admin features: kicking users, viewing module state, configuration editing, and so on.
- Changes to `-CONSOLE` mode (`LocalConsoleSession`, the local play session) or to `-CLI` log output.
- User-selectable themes or color configuration.
- Changes to the logging system beyond what the UI needs. Specifically, the per-entry `StackTrace` lookup in `QueueTarget`/`ConsoleTarget` stays.
- Docker changes. Docker already runs `-CLI`.
- A plain-ASCII rendering mode for terminals that aren't UTF-8 (see Constraints).

## Anti-goals (it fails if any of these are true, even if it technically works)

- Any text (log lines, exception output, auxiliary program output) is written to the raw console while Terminal.Gui owns the screen, which corrupts the display.
- Resizing, or a tmux reattach at a different size, leaves leftover characters, overlapping panels or a frozen layout.
- After exit (normal, startup failure or wizard cancel), the user's terminal is left in raw mode, on the alternate screen, with a hidden cursor, or with mouse reporting still on.
- A single stray keypress (for example Esc, Terminal.Gui v2's default quit key) shuts the BBS down and disconnects every user.
- On a headless server MBBSEmu crashes or hangs because it tried to start the UI.
- The UI uses more and more memory the longer the BBS runs.

## Constraints

- **Target framework:** `net10.0`. Terminal.Gui 2.5.0 ships only a `net10.0` build, so this matches.
- **Package versions:** Terminal.Gui 2.5.0 depends on `Microsoft.Extensions.Configuration`, `.Configuration.Json` and `.Configuration.Binder` 10.0.11, `Microsoft.Extensions.Logging.Abstractions` 10.0.7 and `Microsoft.Extensions.Options` 10.0.11. `MBBSEmu.csproj` currently pins its `Microsoft.Extensions.*` packages at 9.0.0. Those must move to compatible 10.x versions, or restore fails with a package-downgrade error (NU1605).
- **NStack/`ustring`** is gone in v2 and every use becomes `string`.
- **Platforms:** CI (`.github/workflows/dotnet.yml`) publishes 8 runtime identifiers (`win-x64`, `win-x86`, `win-arm64`, `linux-x64`, `linux-arm`, `linux-arm64`, `osx-x64`, `osx-arm64`) as self-contained, untrimmed single-file builds. All of them must still build and publish.
- **Code style:** follow the existing code style and the xUnit + FluentAssertions test conventions (see `CLAUDE.md`).
- **UTF-8 terminal required.** The console uses box-drawing and other non-ASCII characters (double-line borders, `…`, `▼`, `×`). The requirement is documented in the command-line help text (`MBBSEmu.Assets.commandLineHelp.txt`). When MBBSEmu is about to start the UI on Linux or macOS and the locale (`LC_ALL`, then `LC_CTYPE`, then `LANG`) doesn't name UTF-8, it prints a single warning line suggesting `LANG=C.UTF-8` (and `tmux -u` inside tmux), repeats it in the Application Log, and starts anyway. There is no ASCII fallback.
- **Static API:** Terminal.Gui 2.5 recommends the instance-based model (`Application.Create()` returning an `IApplication`) and calls the static `Application.*` model legacy. New code should use the instance model.

## Decided by the project owner

- **Scope:** feature parity plus cleanup and stability. No new features.
- **Terminals:** Linux, macOS and Windows terminals, plus SSH into remote servers.
- **Minimum size 80×24.** Below that, show a "terminal too small" message, not a broken layout.
- **No terminal:** automatically behave as if `-CLI` were passed.
- **Admin console layout "C":** channels in a full-height sidebar on the right, with the Audit Log above the Application Log on the left (mockups below).
- **Setup Wizard:** port with the same steps, fields and help, and fix its bugs (list below).
- **UI failure shuts the BBS down.** If the UI fails while the BBS is running, MBBSEmu shuts down and does not keep serving without the UI: a UI failure is a bug to fix, not something to ride out (see "UI failure while running").
- **UTF-8 terminal required,** and scope kept minimal: no ASCII fallback.
- **Manual testing:** the project owner runs the manual verification matrix. The builder runs every automated and local check it can.

## Current behavior to preserve (parity inventory)

**Admin console (`MainView`):**
- Menu: **Server → Shutdown** (Ctrl+Q) and **Help → About**. About shows "The MajorBBS Emulation Project", the build number from `MBBSEmu.Assets.version.txt`, "Open Source & Community Driven!", "Distributed under MIT License", and the GitHub and website URLs.
- **Audit Log:** a Summary/Detail table fed by `AuditLogger` (module `shocst()` calls), newest at the bottom.
- **Channels:** one row per configured channel (`AppSettingsManager.BBSChannels`), `N: USERNAME` (upper-cased), `N: CONNECTING` (session without a username) or `N: DISCONNECTED`, refreshed about every 250 ms from `IMbbsHost.GetUserSessions()`.
- **Application Log:** a Time/Class/Level/Message table fed by `MessageLogger`. ERROR rows are colored. Activating a row (Enter or double-click) opens a dialog with the full Time, Class, Level and Message.
- **Level labels:** TRACE, DEBUG, INFO, WARN, ERROR, CRIT, NONE.
- **Status bar:** Telnet enabled state, Rlogin enabled state, project name and build.
- **Startup:** the UI starts before modules load, so module-loading log lines appear in it.

**Setup Wizard (`SetupView`):** runs when MBBSEmu is started with no arguments and the user answers Y (or presses Enter) at `Run the MBBSEmu Setup Wizard (Y/n):`. Steps:
1. **Welcome:** text as today.
2. **General Settings:** BBS Name (default "Emulated MajorBBS System"), Channels (default 8, valid range 1–255), Registration Number (random 8 digits, must be exactly 8 digits), Cleanup Time (default 03:00, 24-hour), Run Login Routines (Yes/No, default Yes).
3. **Telnet Settings:** Telnet Server (Enabled/Disabled, default Enabled), Telnet Port (valid range 1–65535), Telnet Heartbeat (On/Off, default Off).
4. **Rlogin Settings:** Rlogin Server (default Disabled), Rlogin Port (default 513, valid range 1–65535), Rlogin Remote IP (default 127.0.0.1, must be a valid IP), Port Per Module (default Yes).
5. **Advanced Settings:** Database Filename (default `mbbsemu.db`, required), Btrieve Cache Size (default 4, must be at least 1).
6. **Security Settings:** Default User Keys, an editable list (default DEMO, NORMAL, USER) with Add (+), Remove (−) and Defaults buttons. No empty or duplicate keys, and at least one key is required.
7. **Review Settings:** a preview of the JSON that will be written.

Every field shows its existing help text in the step's help pane when it receives focus. The help text is kept verbatim from `SetupView.cs`. Each step is validated when leaving it, with the same messages as today. Finishing asks "Are you sure you want to write these settings to appsettings.json?", then opens a Save dialog that defaults to `./appsettings.json` with only `.json` allowed, writes the file and confirms. Afterwards the normal terminal shows "Setup Wizard Complete!" plus the restart hint.

## Known bugs to fix

1. **Wizard saves defaults, not user input.** The `AppSettings` object (and the Review JSON) is built once, when the wizard is constructed, so edits are ignored. It must be built from the current field values when the Review step is shown and again at save time.
2. **"Run Login Routines" is never saved.** `ModuleDoLoginRoutine` isn't set, so the file always gets `false`.
3. **The wizard's default Telnet port is 21.** It should be 23, matching `MBBSEmu/appsettings.json` and the Docker config.
4. **Numeric fields accept non-digits.** For example, the port check `int.TryParse(...) && port is < 1 or > 65535` silently passes `23a`, and Btrieve cache size uses `int.Parse` at construction time. Channels, Registration Number, both ports and Btrieve Cache Size must accept only digits and must be validated.
5. **UI threading.** The UI runs on a `Task.Run` thread, and a second background loop mutates the `DataTable`s and the channel list and calls `Application.DoEvents()` from outside the UI thread. All UI mutation must happen on the UI thread.
6. **Stray console writes corrupt the screen.** `MainView` *replaces* the loggers in `LogFactory`, but loggers fetched earlier keep their `ConsoleTarget` and write straight to the terminal underneath the UI. Examples are `Program._logger` (fetched at `Program.cs:309`, before the UI starts) and the `static readonly` loggers in `MsgFile`/`McvFile`. Lines like "Telnet listening on…" and "Started MBBSEmu Build…" are written this way.
7. **The terminal isn't restored on exit.** Shutdown calls `RequestStop()` but never `Application.Shutdown()`.
8. **Fixed column widths** (12/36/5/60 and 20/50) don't fit 80 columns and waste space on wide terminals.
9. **Unbounded log tables.** Rows are never trimmed, so memory grows for as long as the BBS runs.
10. **Forced auto-scroll.** Every new entry jumps the selection to the end, even while the sysop is scrolled up reading.
11. **Low-contrast errors.** ERROR rows are dark red on blue and hard to read.
12. **Busy-wait at startup.** `Program.cs` prints "Waiting for MBBSEmu GUI to start..." and polls `isRunning`.
13. **The UI freezes during shutdown.** `ShutdownServer` stops the host on the UI thread, so the screen freezes and shutdown log lines never appear.

## Admin console design

### Layout (option "C")

At 80×24, the floor size:

```
 Server   Help
╔═ Audit Log ══════════════════════════════════════════════╗╔═ Channels (5/32) ╗
║Summary   │Detail                                         ║║0: SYSOP          ║
║──────────────────────────────────────────────────────────║║1: BOBSMITH       ║
║CLEANUP   │Nightly cleanup routines complete              ║║2: MOOSE          ║
║LOGOFF    │GUEST disconnected (carrier lost)              ║║3: CONNECTING     ║
║LOGON     │MOOSE logged on from 10.0.0.12                 ║║4: DARKSTAR_THE_D…║
║GWWARROW  │Arrow game started on channel 2                ║║5: KERMIT         ║
╚══════════════════════════════════════════════════════════╝║6: DISCONNECTED   ║
╔═ Application Log ════════════════════════════════════════╗║7: DISCONNECTED   ║
║Time    │Level│Message                                    ║║8: DISCONNECTED   ║
║──────────────────────────────────────────────────────────║║9: DISCONNECTED   ║
║21:04:12│INFO │Loading GWWARROW                           ║║10: DISCONNECTED  ║
║21:04:12│INFO │Started MBBSEmu Build #2026.10.26.1        ║║11: DISCONNECTED  ║
║21:06:40│INFO │Accepted connection from 192.168.1.44:51522║║12: DISCONNECTED  ║
║21:06:40│INFO │Client window size reported as 132x50      ║║13: DISCONNECTED  ║
║21:07:03│WARN │Unsupported ordinal 1092 called by GWWARRO…║║14: DISCONNECTED  ║
║21:07:09│ERROR│Unable to open TWDATA.DAT: file is locked …║║15: DISCONNECTED  ║
║21:08:15│INFO │Accepted connection from 10.0.0.12:40118   ║║16: DISCONNECTED  ║
║21:08:15│INFO │Channel 2 entered module GWWARROW          ║║17: DISCONNECTED  ║
║21:09:02│INFO │Channel 1 logged off                       ║║18: DISCONNECTED  ║
║21:09:44│INFO │Accepted connection from 172.16.0.9:60001  ║║19: DISCONNECTED  ▼
╚══════════════════════════════════════════════════════════╝╚══════════════════╝
 ^Q Shutdown  Enter View entry      Telnet: ON  Rlogin: OFF  Build 2026.10.26.1
```

At 132×40 the same structure grows. The sidebar widens to fit the longest name, the Application Log gains a `Source` column and millisecond timestamps, and Message takes the rest:

```
║Time        │Source              │Level│Message                                                        ║║4: DARKSTAR_THE_DESTROYER║
║21:07:09.204│BtrieveFileProcessor│ERROR│Unable to open TWDATA.DAT: file is locked by another process   ║║5: KERMIT                ║
```

### Sizing rules

These numbers match the approved mockups. The builder may tune them by a column or two if real rendering needs it.

| Element | Rule |
|---|---|
| Menu bar | Row 0. |
| Status bar | Last row. |
| Channels sidebar | Full body height on the right. Width = longest channel line + 2 (borders), clamped to a minimum of 20 and a maximum of ¼ of the screen width. Recomputed when the channel text or the screen size changes. |
| Left column | Screen width minus the sidebar. |
| Audit Log | Top of the left column, about 35% of the body height (body = screen height − 2), at least 8 rows including borders. |
| Application Log | Remainder of the left column. |
| Audit columns | Summary is 10 wide when the left column is under 80 columns, otherwise 14. Detail takes the rest. |
| Application Log columns, log interior < 70 columns | `Time` (HH:mm:ss, 8) │ `Level` (5) │ `Message` (rest). No Source column. |
| Application Log columns, log interior ≥ 70 columns | `Time` (HH:mm:ss.fff, 12) │ `Source` (14, or 20 when the interior is ≥ 100) │ `Level` (5) │ `Message` (rest). |
| Truncation | A cell that doesn't fit ends with `…`. Nothing scrolls horizontally. |

`Source` is the last segment of the calling type's name (`HostProcess.MbbsHost` becomes `MbbsHost`). The full type name stays available in the entry dialog. The column is labeled `Source`, not `Class`.

### Panel behavior

- **Follow mode:** each log follows new entries while its view is at the bottom. Once the sysop scrolls up or selects an older row, new entries are added without moving the view. Returning to the last row (End, or scrolling down) resumes following.
- **Retention:** the Application Log keeps the most recent 5,000 entries and the Audit Log the most recent 1,000. Older entries are dropped.
- **Entry dialog:** activating a row (Enter or double-click) in either log opens a dialog with all fields untruncated: for the Application Log the full timestamp, full type name, level and wrapped message; for the Audit Log the summary and the wrapped detail. Long content scrolls inside the dialog, and the dialog fits within 80×24.
- **Channels title:** `Channels (online/total)`, where "online" counts channels with a logged-in username.
- **Channel rows:** DISCONNECTED rows are dimmed. The list scrolls when there are more channels than rows, with a visible indicator that more exist.
- **Focus:** Tab and Shift+Tab move focus between the three panels, and arrow keys, PgUp/PgDn and Home/End scroll the focused panel. The mouse can select and scroll.
- **Refresh rate:** about every 250 ms, as today.

### Colors (fixed in code)

The classic look is kept, defined in code so v2 themes and user config files can't change it:
- Blue background with white text.
- Bright cyan double-line borders and titles.
- Menu and status bar in white on dark gray, with hotkeys in bright yellow.
- Selected or focused row in black on gray.
- ERROR and CRIT rows in **bright** red, WARN rows in yellow, and dimmed DISCONNECTED channels.

### Keys, menu, status bar

- **Shutdown:** Ctrl+Q and Server → Shutdown both open a confirmation: "Shut down MBBSEmu? All connected users will be disconnected." with Yes/No, defaulting to No. Nothing else quits the console. Terminal.Gui's default application quit key (Esc) must be unbound or rebound so it never stops the console, and Esc only closes dialogs.
- **Ctrl+C** in the admin console opens the same shutdown confirmation.
- **Ctrl+Z** is swallowed. On Linux and macOS, Terminal.Gui v2 binds it by default to suspend the process, and a suspended MBBSEmu freezes every caller.
- **Status bar, left:** shortcut hints `^Q Shutdown`, `Enter View entry` and `Tab Next panel`. Hints are dropped from the right when they don't fit.
- **Status bar, right:** `Telnet: ON|OFF  Rlogin: ON|OFF  Build <version>`. It never truncates; at 80 columns it fits by dropping hints.

### UI failure while running

If the UI fails after startup (an unhandled exception from the Terminal.Gui loop, a timer or an event handler, or the terminal becoming unusable), MBBSEmu shuts down:
1. The error is sent to the logger as CRIT.
2. Services stop through the same path as a confirmed shutdown, with no confirmation and the same 15-second bound as the signal path.
3. The terminal is restored.
4. The exception is printed to the normal terminal in the same format as today's "Critical Exception has occurred:" output.

There is no headless continuation.

### Terminal too small

When the screen is narrower than 80 or shorter than 24, the console replaces the whole screen with a centered message:

```
                          Terminal too small

                 Currently 70×20, need at least 80×24

                    Resize the window to continue.
                   Ctrl+Q still shuts down MBBSEmu.
```

The normal layout comes back as soon as the terminal is 80×24 or larger. Logs keep being collected while the message is shown, so nothing is lost. Ctrl+Q still opens the shutdown confirmation.

### Shutdown flow

1. Ctrl+Q (or the menu, or Ctrl+C) opens the confirmation, and the sysop chooses Yes.
2. The status bar shows "Shutting down…". Services stop on a background thread, the same `IStoppable` services `Program` tracks today, so the UI keeps drawing and shutdown log lines appear.
3. When the services have stopped, the UI exits and Terminal.Gui is disposed, which restores the terminal.
4. Logging switches back to the console, and a final "MBBSEmu shut down" line is printed to the normal terminal.

**External signals:** if SIGTERM or SIGHUP arrives while the console is up (for example an SSH session dropping without tmux), MBBSEmu takes the same path from step 2, without confirmation, and does its best to restore the terminal.

**Stuck shutdown:** `MbbsHost.WaitForShutdown()` can block forever if a module never returns.
- Pressing Ctrl+Q or Ctrl+C again while "Shutting down…" is shown asks "Force quit now? Shutdown has not finished." Yes restores the terminal and exits the process immediately. This mirrors the CLI's "second Ctrl+C kills it".
- On the signal path there is no one at the keyboard, so MBBSEmu waits up to 15 seconds (matching Docker's `stop_grace_period`), then restores the terminal and exits.
- A forced exit may skip `IGlobalCache` disposal. SQLite's transactional writes make that acceptable.

## Setup Wizard design

- **Steps:** the same seven steps, fields, defaults, help text and validation messages as listed above, with the bug fixes applied.
- **Size:** a dialog sized to the terminal, with about 90% width and 80% height as today but never larger than the screen, that redraws on resize. The 80×24 floor and the "too small" message apply here too.
- **Review:** the JSON preview is built from the current field values each time the Review step is entered, and it scrolls.
- **Numeric fields:** Channels, Registration Number, ports and Btrieve Cache Size accept only digits. Registration Number keeps leading zeros, so it is stored as a string, as today. The existing range and length checks still apply.
- **Cancel:** cancelling the wizard (Esc or the Cancel button), or cancelling the Save dialog, exits the UI cleanly and prints "Setup cancelled" to the normal terminal. Nothing is written.
- **Confirm declined:** answering No to "Are you sure…?" returns to the Review step.
- **Finish:** after saving, the UI exits and the normal terminal shows the same "Setup Wizard Complete!" and restart lines as today.

## Startup, terminal detection, and lifecycle

- **Unchanged argument handling:** the `Program.cs` argument parsing is not touched.
- **Prompts first:** interactive console prompts (the `Y/n` wizard prompt, and the `-DBRESET`/`-PWRESET` password prompts) finish before any Terminal.Gui UI starts. Today's ordering already does this.
- **Mode decision:**
  - The **admin console** starts only for a normal server run: not `-CLI`, not `-CONSOLE`, not `-EXE`, not `-APIREPORT`, and not the one-shot `-?`/`-V`. A terminal must also be available.
  - **"Terminal available"** means stdin and stdout are both interactive (not redirected) and Terminal.Gui can initialize. If initialization throws, behave as headless.
  - **Headless without `-CLI`:** log one line, "No interactive terminal detected; running in CLI mode", through the normal console logger, then continue exactly as `-CLI` does.
  - **Headless at the no-argument wizard prompt:** skip the prompt and the wizard, print the `-?` help text as if the user had answered "n", and don't wait for "(Press Any Key to Exit)".
- **Early start:** the admin console starts before module loading (`host.Start`) so module-loading logs appear in it, as today, without the busy-wait. Output written before the UI starts stays in the normal terminal's scrollback.
- **Startup failure:** if startup throws while the UI is up, the UI is torn down first and the exception is then printed to the normal terminal, the same text `Program.cs` prints today.
- **Raw console audit:** while the UI owns the terminal, nothing may write to `Console` directly. The call sites found so far:
  - The `ConsoleTarget`-backed loggers (bug 6).
  - `MbbsHost`'s auxiliary-program runner (`MbbsHost.cs:1028`), which hands `Console.In`/`Console.Out`/`Console.Error` to `ExeRuntime`.
  - The startup-exception handler.

  While the UI is active, auxiliary-program stdout and stderr go to the Application Log, and its stdin reads as empty.

### Logging hookup

Loggers are not replaced in `LogFactory`. Instead the UI changes the targets on the existing `MessageLogger` and `AuditLogger` instances: it removes `ConsoleTarget` and adds a queue target when the UI starts, and reverses that when the UI stops. `LoggerBase.AddTarget`/`RemoveTarget<T>` already support this (`LocalConsoleSession` uses the same pattern). Every logger reference, including ones fetched earlier or cached statically, therefore stops writing to the console while the UI is up.

### Threading model

- Terminal.Gui runs on the main thread and owns the terminal for its whole lifetime.
- Host startup and shutdown run on background threads.
- The UI picks up new data with a UI-loop timer (`IApplication.AddTimeout`, about 250 ms). The timer drains the log queues, applies retention, and snapshots channel state. Any other cross-thread UI update goes through `IApplication.Invoke`.
- No view, table source or list source is touched from another thread, and no message pumping happens from outside the UI loop.

## Testing and verification

**Automated (xUnit + FluentAssertions, in `MBBSEmu.Tests`):**
- Pure logic pulled out of the views, so it can be tested without a terminal:
  - Layout and column calculations, including sidebar width and the column sets at 80×24, 100×30 and 132×40.
  - The too-small check.
  - Channel line formatting and the online count.
  - Log entry formatting: level labels and `Source` shortening.
  - Bounded log buffers and the follow-mode rules.
  - The mode decision (flags plus redirection lead to the TUI, CLI fallback or wizard skip).
  - The wizard's settings builder (field values to `AppSettings`, including `ModuleDoLoginRoutine`, the Telnet default 23 and key lists) and its validation rules.
- View-level tests using Terminal.Gui v2's test support (an `IApplication` with a test driver and input injection):
  - The admin console lays out at 80×24 and 132×40.
  - It shows the too-small screen at 79×24 and 80×23 and recovers on resize.
  - Ctrl+Q opens the confirmation, and Esc does not quit.
  - The wizard Review step reflects edited fields.

**Local checks run by the builder:** the builder runs everything that is possible on its own machine and records the results in the PR:
- The automated tests.
- MBBSEmu driven inside a pseudo-terminal at fixed sizes, with scripted resizes and keystrokes, with the screen captured and checked (on macOS, and on Linux inside a Docker container).
- tmux detach and reattach at a different size, if tmux can be installed locally.
- The headless and redirected-output runs.

**Manual verification matrix** (run by the project owner; results recorded in the PR):
- macOS Terminal.app and iTerm2.
- A Linux desktop terminal.
- Windows Terminal, plus the legacy Windows console host (conhost), best-effort.
- SSH from macOS or Windows into a Linux host, run inside tmux: detach, reattach from a different-size window, and resize live.
- Headless runs: `MBBSEmu -S … -C … > out.log` with stdin redirected, and a `systemd-run`/`nohup` launch, should both run in CLI mode.
- Each run checks:
  - The 80×24 layout.
  - A large-window layout.
  - Live resizing in both directions across the 80×24 floor.
  - A log burst (for example several connects).
  - Ctrl+Q with No, then with Yes.
  - The terminal is fully restored afterwards (cursor visible, echo on, no mouse escape codes when clicking).
  - A full wizard run that saves edited values.

## Left to the builder

- Class structure inside `MBBSEmu/UI/`: whether `UIBase` survives, view decomposition, and where the extracted logic lives. `UIMetadata` is only read by `UIBase.GetName`/`GetDescription`, which nothing calls, so it may be removed.
- Exact Terminal.Gui v2 controls and APIs (TableView or a custom list for the logs, NumericUpDown or a validated TextField for digits, and so on), as long as the behavior above holds.
- Exact new versions of the `Microsoft.Extensions.*` packages, as long as restore succeeds without downgrade warnings and nothing else regresses.
- How the "terminal available" check is implemented, and how auxiliary-program output is redirected while the UI is up.
- Tuning the sizing numbers by a column or two to match real rendering.
- Making the target swap on `LoggerBase` thread-safe against concurrent `Write`s, and raising `QueueTarget`'s 1,000-pending-entry drop limit if bursts between UI ticks can exceed it.
- Rendering log messages that contain newlines, tabs or escape codes on a single row: control characters are replaced in the table, and the entry dialog shows the original text.

## Calls made during design (not explicitly confirmed by the project owner)

These were played back and the owner raised no objections, but they are design-time calls, not explicit requests:
- Classic blue/cyan look kept and fixed in code.
- Shutdown confirmation dialog; Esc never quits; Ctrl+C opens the same confirmation.
- `Source` column (short type name), millisecond timestamps only at wider sizes, and the column thresholds above.
- Follow-mode auto-scroll; retention of 5,000 application and 1,000 audit entries.
- Entry dialog added to the Audit Log too (today it exists only on the Application Log).
- Status bar shortcut hints, with "ON/OFF" in place of "ENABLED/DISABLED".
- Graceful handling of SIGTERM/SIGHUP while the console is up.
- Ctrl+Z (Terminal.Gui's default "suspend" key on Linux and macOS) swallowed in the admin console.
- Forced exit on a second Ctrl+Q/Ctrl+C during shutdown (with confirmation), and after 15 seconds on the signal path.
- Headless wizard prompt prints help without waiting for a key.
- Wizard cancel prints "Setup cancelled"; numeric fields accept digits only.
- Auxiliary-program output redirected into the Application Log while the UI is up.
- The one-line non-UTF-8 locale warning at startup (the owner asked for a UTF-8 requirement with minimal scope; the warning is the smallest way to explain underscores in tmux).
