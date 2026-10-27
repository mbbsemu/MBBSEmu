# Admin Console & Setup Wizard on Terminal.Gui v2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rebuild MBBSEmu's admin console and Setup Wizard on Terminal.Gui 2.5.0 with feature parity. The new UI adapts to any terminal size from 80×24 up, is stable over SSH and tmux, and falls back to `-CLI` behavior when there is no terminal.

**Architecture:**
- **Pure, unit-tested logic** under `MBBSEmu/UI/`: layout math, formatting, bounded logs, mode selection, the shutdown state machine and the wizard model.
- **Thin Terminal.Gui views** on top of that logic: `AdminConsoleView` and `SetupWizardView`.
- **A controller** (`AdminConsole`) that owns the `IApplication` lifecycle on the main thread, plus the 250 ms refresh timer, key filtering, shutdown, signals and failure handling.
- **Logging hookup:** the UI receives logs by swapping targets on the *existing* logger instances, never by replacing loggers.

**Tech Stack:** C# / .NET 10, Terminal.Gui 2.5.0 (instance model: `Application.Create()` returning `IApplication`), xUnit 2.9, FluentAssertions 7.

**Spec:** `docs/superpowers/specs/2026-10-26-tui-terminal-gui-v2-design.md`. Read it in full before starting any task. It is the source for every value below.

## Global Constraints

**Packages and platforms:**
- Package `Terminal.Gui` 2.5.0.
- Every `Microsoft.Extensions.*` package in `MBBSEmu/MBBSEmu.csproj` and `MBBSEmu.Tests/MBBSEmu.Tests.csproj` moves from 9.0.0 to **10.0.12**, the version `Microsoft.Data.Sqlite` already uses.
- Target framework stays `net10.0`. All 8 CI runtime identifiers must publish: `win-x64`, `win-x86`, `win-arm64`, `linux-x64`, `linux-arm`, `linux-arm64`, `osx-x64`, `osx-arm64`.

**Admin console values:**
- Minimum screen size is 80×24. Below that, show the "Terminal too small" screen.
- Refresh interval is about 250 ms.
- Retention: 5,000 Application Log entries and 1,000 Audit Log entries. UI queue targets hold 10,000 pending entries.
- The unattended shutdown bound is 15 seconds.
- Colors are fixed in code:
  - Base: White on Blue.
  - Borders and titles: BrightCyan on Blue, double line.
  - Menu and status bar: White on DarkGray, with hotkeys in BrightYellow.
  - Selected row: Black on Gray.
  - ERROR and CRIT rows: BrightRed. WARN rows: Yellow (BrightYellow on Blue).
  - DISCONNECTED channels: dimmed (Gray on Blue).

**Behavior rules:**
- Nothing writes to `Console` while `ConsoleOwnership.IsUiActive` is true.
- In the admin console, Esc and Ctrl+Z never reach Terminal.Gui's application bindings (Quit and Suspend). Esc still closes dialogs.
- User-visible strings are exactly as in the spec. Strings moved from the old views are copied verbatim.

**Code style:**
- Block-scoped namespaces and 4-space indents.
- Doc comments in the repo's `/// <summary>\n///     text` form. Inline comments as `//Comment`.
- Test classes are named `Thing_Tests`, test methods `Scenario_Expectation`, under `MBBSEmu.Tests/UI/...` with matching namespaces.

**Test conventions:**
- Every test class that creates an `IApplication` carries `[Collection("TerminalGui")]`, defined once in Task 10, so those tests never run in parallel.
- Tests use the `ansi` driver (`DriverRegistry.Names.ANSI`). It runs headless with redirected stdio, which the API spike confirmed.

**Build and test:**
- With a local .NET 10 SDK, run `dotnet test MBBSEmu.Tests/MBBSEmu.Tests.csproj --filter "FullyQualifiedName~<name>"`. Otherwise use `docker/test.sh --filter "FullyQualifiedName~<name>"`.
- Docker needs access to the repo path. If macOS refuses the mount ("operation not permitted"), `rsync -a --exclude bin --exclude obj --exclude .git ./ <scratch>/repo/` and run the same `docker run` that `docker/test.sh` uses with `-v <scratch>/repo:/src`.
- Baseline before any change: **2,957 tests, all passing.**

**Git:**
- Work on branch `feature/terminal-gui-v2`.
- Never `git add -A` or `git add .`. Stage explicit paths only. These four pre-existing modified files belong to the owner and are **never** staged: `MBBSEmu.Tests/Assets/IntegrationTest.msg`, `MBBSEmu/Assets/fseTemplate.ans`, `MBBSEmu/Assets/login.ans`, `MBBSEmu/Assets/signup.ans`.
- End every commit message with the line `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

## Review Focus

1. **Ctrl+Q while modules are still loading** (services half-started): the confirmation works, `stopServices` runs after the start finishes or fails, and the console exits cleanly. Test owned by Task 11.
2. **Resizing to absurd sizes** (10×3, 1×1) or back from them: no exception, the too-small text is clipped, and the layout returns at 80×24. Test owned by Task 10.
3. **A development build string** (`Development (Overwritten in Github Actions)`) at 80 columns: the right side of the status bar stays whole and the hints drop. Test owned by Task 6.
4. **A 29-character username at 80 columns**: the sidebar stays 20 wide and the name ends in `…`, not overflowing into the border. Test owned by Task 10.
5. **A registration number with leading zeros** (`00012345`): it is accepted, and written as the string `"00012345"`. Test owned by Task 13.

---

### Task 1: Branch, package upgrade, remove the v1 UI

**Files:**
- Modify: `MBBSEmu/MBBSEmu.csproj:79-84`, `MBBSEmu.Tests/MBBSEmu.Tests.csproj` (the `Microsoft.Extensions.Logging` line), `MBBSEmu/Program.cs`
- Create: `MBBSEmu/UI/Setup/SetupWizardText.cs`, `MBBSEmu/UI/Main/AdminConsoleText.cs`
- Delete: `MBBSEmu/UI/UIBase.cs`, `MBBSEmu/UI/UIMetadataAttribute.cs`, `MBBSEmu/UI/Main/MainView.cs`, `MBBSEmu/UI/Setup/SetupView.cs`

**Interfaces:**
- Produces:
  - `MBBSEmu.UI.Setup.SetupWizardText`, a `public static class` holding `const string`s: every title, label, help text, dialog text and validation message from `SetupView.cs`, copied verbatim. Names are per field, e.g. `BbsNameHelp`, `ChannelsHelp`, `TelnetPortHelp`, `ValidChannelCount`. Two additions: `CancelledMessage = "Setup cancelled"`, and `SuccessMessage(string path)` as a static method returning `$"Settings were successfully written to:\n{path}"`.
  - `MBBSEmu.UI.Main.AdminConsoleText` (`public static class`) with `AboutTitle = "About MBBSEmu"` and `static string AboutMessage(string build)`, which returns today's About text verbatim.

- [ ] **Step 1:** `git checkout -b feature/terminal-gui-v2`.
- [ ] **Step 2:** Create `SetupWizardText.cs` and `AdminConsoleText.cs` by moving the string literals out of `SetupView.cs` and `MainView.cs`. Diff the old files against the new constants and confirm that no literal changed.
- [ ] **Step 3:** Bump the packages:
  - `Terminal.Gui` to `2.5.0`.
  - `Microsoft.Extensions.Configuration`, `.Configuration.FileExtensions`, `.Configuration.Json`, `.DependencyInjection` and `.Logging` to `10.0.12` in `MBBSEmu.csproj`.
  - `Microsoft.Extensions.Logging` to `10.0.12` in `MBBSEmu.Tests.csproj`.
- [ ] **Step 4:** Delete the four v1 UI files and remove the `using MBBSEmu.UI.Main;` and `using MBBSEmu.UI.Setup;` lines from `Program.cs`. Temporary wiring, each marked `// TEMPORARY (restored in Task 12/14)`:
  - The no-arguments branch behaves as `-?`.
  - The `if (!_cliMode && !_isConsoleSession)` block becomes `_cliMode = true;`.
- [ ] **Step 5:** Run `dotnet build` (or the Docker equivalent). Expected: `Build succeeded` with no `NU1605` or `NU1608` warnings.
- [ ] **Step 6:** Run the full test suite. Expected: `Passed! - Failed: 0, Passed: 2957`.
- [ ] **Step 7:** Run `dotnet publish MBBSEmu/MBBSEmu.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o /tmp/pub-linux-x64`. Expected: success.
- [ ] **Step 8:** Commit the csproj files, `Program.cs`, the two new files and the four deletions: `git commit -m "Upgrade to Terminal.Gui 2.5.0 and remove the v1 UI"`.

### Task 2: Thread-safe logger targets

**Files:**
- Modify: `MBBSEmu/Logging/LoggerBase.cs`
- Test: `MBBSEmu.Tests/Logging/LoggerBase_Tests.cs`

**Interfaces:**
- Produces, on `LoggerBase`:
  - `protected IReadOnlyList<ILoggingTarget> LOGGING_TARGETS { get; }`, a snapshot. The existing `foreach` loops in `MessageLogger` and `AuditLogger` compile unchanged.
  - `public bool RemoveTarget<T>()`, true if any target was removed.
  - `public bool RemoveTarget(ILoggingTarget target)`, which removes that instance by reference.
  - `public void AddTarget(ILoggingTarget target)`.
  - `public bool HasTarget<T>()`.

- [ ] **Step 1: Write failing tests**
  - `RemoveTargetOfType_ReturnsTrueOnlyWhenPresent`: a `MessageLogger` with a `ConsoleTarget`. `RemoveTarget<ConsoleTarget>()` returns `true`, then a second call returns `false`.
  - `RemoveTargetInstance_RemovesOnlyThatInstance`: two `QueueTarget`s. Removing the first leaves `HasTarget<QueueTarget>()` `true`.
  - `HasTarget_ReflectsAddAndRemove`.
  - `ConcurrentAddRemoveWhileLogging_DoesNotThrow`: one thread calls `logger.Info("x")` 20,000 times while another adds and removes a `QueueTarget` 2,000 times. `Parallel.Invoke` completes without throwing.
- [ ] **Step 2:** Run `--filter "FullyQualifiedName~LoggerBase_Tests"`. Expected: FAIL (compile errors on the new members, and the concurrency test throws `InvalidOperationException`).
- [ ] **Step 3:** Implement copy-on-write: a private `ILoggingTarget[]` replaced under a lock on every mutation, with `LOGGING_TARGETS` returning `Volatile.Read` of the array. `CPULogger` has its own static list and is untouched.
- [ ] **Step 4:** Run the tests. Expected: PASS. Then run the full suite. Expected: 2957 + 4 passing.
- [ ] **Step 5:** Commit: `"Make logger target changes thread-safe"`.

### Task 3: Log entries and formatting

**Files:**
- Create: `MBBSEmu/UI/Logging/LogEntries.cs`, `MBBSEmu/UI/Logging/LogEntryFormatter.cs`
- Test: `MBBSEmu.Tests/UI/Logging/LogEntryFormatter_Tests.cs`

**Interfaces:**
- Produces:
  - `public sealed record ApplicationLogEntry(DateTime Timestamp, string SourceType, LogLevel Level, string Message)`. `SourceType` is the full type name, or `""`.
  - `public sealed record AuditLogEntry(string Summary, string Detail)`.
  - `public static class LogEntryFormatter`, with:
    - `string LevelLabel(LogLevel level)`
    - `string ShortSource(string fullTypeName)`
    - `string SingleLine(string text)`
    - `string FormatTime(DateTime timestamp, bool withMilliseconds)`
    - `string ApplicationDetail(ApplicationLogEntry entry)`
    - `string AuditDetail(AuditLogEntry entry)`
    - `ApplicationLogEntry ToApplicationEntry(object[] queueItem)`
    - `AuditLogEntry ToAuditEntry(object[] queueItem)`

- [ ] **Step 1: Write failing tests**
  - `LevelLabel_MatchesLegacyLabels`: Theory over (Trace→`TRACE`, Debug→`DEBUG`, Information→`INFO`, Warning→`WARN`, Error→`ERROR`, Critical→`CRIT`, None→`NONE`).
  - `ShortSource_ReturnsLastSegment`: Theory:
    - `"MBBSEmu.HostProcess.MbbsHost"`→`"MbbsHost"`
    - `"MBBSEmu.Program"`→`"Program"`
    - `"MBBSEmu.Foo+Nested"`→`"Nested"`
    - ``"MBBSEmu.Util.LRUCache`2[System.String,System.Int32]"``→`"LRUCache"`
    - `""`→`""`
    - `null`→`""`
  - `SingleLine_ReplacesLineBreaksTabsAndControlCharacters`: `"a\r\nb\tc\u001b[31md"` → `"a b c?[31md"`. CRLF collapses to one space, the tab becomes a space, and any other `char.IsControl` character becomes `?`.
  - `FormatTime_WithAndWithoutMilliseconds`: `21:07:09.204` → `"21:07:09"` and `"21:07:09.204"`.
  - `ApplicationDetail_ContainsFullFields`: the text equals `"Time: 2026-10-26 21:07:09.204\nClass: MBBSEmu.Btrieve.BtrieveFileProcessor\nLevel: ERROR\nMessage: line1\nline2"`. Detail keeps the original newlines.
  - `AuditDetail_ContainsSummaryAndDetail`: `"Summary: LOGON\nDetail: SYSOP logged on"`.
  - `ToApplicationEntry_ReadsQueueTargetLayout`: the item `{ DateTime, typeof(MBBSEmu.HostProcess.MbbsHost), LogLevel.Warning, "msg" }` becomes `SourceType == "MBBSEmu.HostProcess.MbbsHost"`, `Level == Warning`, `Message == "msg"`. A null type gives `SourceType == ""`, and a non-`LogLevel` level gives `LogLevel.None`.
  - `ToAuditEntry_ReadsQueueTargetLayout`: `{ DateTime, null, "LOGON", "SYSOP" }` becomes `("LOGON", "SYSOP")`.
- [ ] **Step 2:** Run `--filter "FullyQualifiedName~LogEntryFormatter_Tests"`. Expected: FAIL (types not defined).
- [ ] **Step 3:** Implement. `SourceType` comes from `Type.FullName ?? ToString()`. `ShortSource` strips anything from the first `` ` `` or `[`, then takes the text after the last `.` or `+`.
- [ ] **Step 4:** Run the tests. Expected: PASS.
- [ ] **Step 5:** Commit: `"Add admin console log entry formatting"`.

### Task 4: Bounded logs and follow-mode selection

**Files:**
- Create: `MBBSEmu/UI/Logging/BoundedLog.cs`, `MBBSEmu/UI/Logging/FollowSelection.cs`
- Test: `MBBSEmu.Tests/UI/Logging/BoundedLog_Tests.cs`, `MBBSEmu.Tests/UI/Logging/FollowSelection_Tests.cs`

**Interfaces:**
- Produces:
  - `public sealed class BoundedLog<T>`, with:
    - `BoundedLog(int capacity)`
    - `int Capacity`
    - `int Count`
    - `T this[int index]` (0 = oldest retained)
    - `int Append(IReadOnlyList<T> items)`, which returns how many of the oldest entries were dropped
  - `public sealed record FollowResult(int SelectedRow, bool Following)`.
  - `public static class FollowSelection` with `FollowResult Adjust(int selectedRow, bool lastRowVisible, int previousCount, int dropped, int newCount)`.

- [ ] **Step 1: Write failing tests**
  - `Append_UnderCapacity_DropsNothing`: capacity 5, append 3 → returns 0 and `Count == 3`.
  - `Append_OverCapacity_DropsOldest`: capacity 5, append 3 then 4 → the second call returns 2, `Count == 5`, `[0]` is the 3rd item ever added, and `[4]` is the last.
  - `Append_BatchLargerThanCapacity_KeepsNewest`: capacity 5, one batch of 12 → returns 7, and `[0]` is item #8.
  - `Adjust_EmptyLog_Follows`: `(−1, true, 0, 0, 10)` → `(9, true)`.
  - `Adjust_SelectedLastAndVisible_Follows`: `(9, true, 10, 0, 15)` → `(14, true)`.
  - `Adjust_SelectedOlderRow_StaysOnSameEntry`: `(4, true, 10, 0, 15)` → `(4, false)`.
  - `Adjust_SelectedOlderRow_ShiftsByDropped`: `(4, true, 10, 3, 12)` → `(1, false)`.
  - `Adjust_SelectedRowDropped_ClampsToZero`: `(1, true, 10, 3, 12)` → `(0, false)`.
  - `Adjust_LastRowScrolledOutOfView_DoesNotFollow`: `(9, false, 10, 0, 15)` → `(9, false)`.
  - `Adjust_NoSelectionButBottomVisible_Follows`: `(−1, true, 10, 0, 11)` → `(10, true)`.
- [ ] **Step 2:** Run both test classes. Expected: FAIL.
- [ ] **Step 3:** Implement `BoundedLog<T>` as a ring buffer. `Adjust` follows when `previousCount == 0`, or when `(selectedRow < 0 || selectedRow >= previousCount − 1) && lastRowVisible`. When it follows, the result is `newCount − 1`. Otherwise the result is `selectedRow < 0 ? −1 : Math.Max(0, selectedRow − dropped)`.
- [ ] **Step 4:** Run the tests. Expected: PASS.
- [ ] **Step 5:** Commit: `"Add bounded log buffer and follow-mode selection"`.

### Task 5: Minimum size and admin console layout math

**Files:**
- Create: `MBBSEmu/UI/ScreenSize.cs`, `MBBSEmu/UI/Main/AdminConsoleLayout.cs`
- Test: `MBBSEmu.Tests/UI/ScreenSize_Tests.cs`, `MBBSEmu.Tests/UI/Main/AdminConsoleLayout_Tests.cs`

**Interfaces:**
- Produces:
  - `public static class ScreenSize`, with:
    - `const int MinimumWidth = 80`
    - `const int MinimumHeight = 24`
    - `bool IsBelowMinimum(int width, int height)`
    - `IReadOnlyList<string> TooSmallMessage(int width, int height, string quitHint)`
  - `public sealed record AdminConsoleLayout(int SidebarWidth, int LeftWidth, int AuditHeight, int ApplicationLogHeight, int AuditSummaryWidth, bool ShowSource, int SourceWidth, bool ShowMilliseconds)`, with:
    - `const int MinimumSidebarWidth = 20`
    - `static AdminConsoleLayout Calculate(int screenWidth, int screenHeight, int longestChannelLine)`

- [ ] **Step 1: Write failing tests**
  - `IsBelowMinimum`: Theory over (79,24)→true, (80,23)→true, (80,24)→false, (200,60)→false.
  - `TooSmallMessage_MatchesSpec`: `(70, 20, "Ctrl+Q still shuts down MBBSEmu.")` → exactly `["Terminal too small", "", "Currently 70×20, need at least 80×24", "", "Resize the window to continue.", "Ctrl+Q still shuts down MBBSEmu."]`.
  - `Calculate_80x24`: `(80, 24, 16)` → `(20, 60, 8, 14, 10, false, 14, false)`.
  - `Calculate_100x30`: `(100, 30, 16)` → `(20, 80, 10, 18, 14, true, 14, true)`.
  - `Calculate_132x40_LongName`: `(132, 40, 25)` → `(27, 105, 13, 25, 14, true, 20, true)`.
  - `Calculate_SidebarCappedAtQuarterWidth`: `(132, 40, 34)` → `SidebarWidth == 33`.
  - `Calculate_AuditHeightNeverBelowEight`: `(80, 24, 16)` → `AuditHeight == 8`. `(200, 60, 16)` → `AuditHeight == 20` (body 58 × 0.35 = 20.3, rounded).
- [ ] **Step 2:** Run both test classes. Expected: FAIL.
- [ ] **Step 3:** Implement, using the spec's sizing table:
  - `body = h − 2`
  - `SidebarWidth = Clamp(longest + 2, 20, Max(20, w / 4))`
  - `AuditHeight = Max(8, (int)Math.Round(body * 0.35, MidpointRounding.AwayFromZero))`
  - `AuditSummaryWidth = LeftWidth < 80 ? 10 : 14`
  - With `interior = LeftWidth − 2`: `ShowSource = ShowMilliseconds = interior >= 70`, and `SourceWidth = interior >= 100 ? 20 : 14`.
- [ ] **Step 4:** Run the tests. Expected: PASS.
- [ ] **Step 5:** Commit: `"Add admin console layout calculation"`.

### Task 6: Channel list and status line text

**Files:**
- Create: `MBBSEmu/UI/Main/ChannelList.cs`, `MBBSEmu/UI/Main/StatusLine.cs`
- Test: `MBBSEmu.Tests/UI/Main/ChannelList_Tests.cs`, `MBBSEmu.Tests/UI/Main/StatusLine_Tests.cs`

**Interfaces:**
- Produces:
  - `public sealed record ChannelSession(int Channel, string Username)`.
  - `public sealed record ChannelLine(string Text, bool IsDisconnected)`.
  - `public static class ChannelList`, with:
    - `IReadOnlyList<ChannelLine> Build(int channelCount, IReadOnlyList<ChannelSession> sessions)`
    - `int CountOnline(int channelCount, IReadOnlyList<ChannelSession> sessions)`
    - `string Title(int online, int total)`
  - `public sealed record StatusHint(string Key, string Label)`.
  - `public static class StatusLine`, with:
    - `IReadOnlyList<StatusHint> AdminConsoleHints` (`^Q`/`Shutdown`, `Enter`/`View entry`, `Tab`/`Next panel`)
    - `string RightText(bool telnetEnabled, bool rloginEnabled, string build)`
    - `IReadOnlyList<StatusHint> FitHints(int screenWidth, IReadOnlyList<StatusHint> hints, string rightText)`

- [ ] **Step 1: Write failing tests**
  - `Build_FormatsEachChannel`: count 4; sessions `(0, "sysop")`, `(2, "")` → `["0: SYSOP", "1: DISCONNECTED", "2: CONNECTING", "3: DISCONNECTED"]`. `IsDisconnected` is true for 1 and 3 only.
  - `Build_IgnoresSessionsOutsideChannelCount`: count 2 with a session on channel 5 → 2 lines, both DISCONNECTED.
  - `CountOnline_CountsOnlyNamedSessionsInRange`: `(0,"A")`, `(1,"")`, `(9,"B")` with count 4 → 1.
  - `Title_Format`: `(5, 32)` → `"Channels (5/32)"`.
  - `RightText_Format`: `(true, false, "2026.10.26.1")` → `"Telnet: ON  Rlogin: OFF  Build 2026.10.26.1"`.
  - `FitHints_80Columns_KeepsTwo`: width 80 with the right text above → `^Q` and `Enter` only.
  - `FitHints_132Columns_KeepsAll`: 3 hints.
  - `FitHints_DevelopmentBuildAt80_DropsAllHints` (Review Focus 3): `RightText(true, true, "Development (Overwritten in Github Actions)")` at width 80 → empty list, and the right text length (73) is at most 80 − 2.
- [ ] **Step 2:** Run both test classes. Expected: FAIL.
- [ ] **Step 3:** Implement. Start at `x = 1`. A hint fits when `x + key.Length + 1 + label.Length <= screenWidth − rightText.Length − 3`. After each fitted hint, `x += key.Length + 1 + label.Length + 2`. Stop at the first hint that doesn't fit.
- [ ] **Step 4:** Run the tests. Expected: PASS.
- [ ] **Step 5:** Commit: `"Add channel list and status line formatting"`.

### Task 7: Terminal detection, UTF-8 check, UI mode selection

**Files:**
- Create: `MBBSEmu/UI/TerminalEnvironment.cs`, `MBBSEmu/UI/UserInterfaceModeSelector.cs` (holds `EnumUserInterfaceMode`)
- Test: `MBBSEmu.Tests/UI/TerminalEnvironment_Tests.cs`, `MBBSEmu.Tests/UI/UserInterfaceModeSelector_Tests.cs`

**Interfaces:**
- Produces:
  - `public static class TerminalEnvironment`, with:
    - `bool IsInteractive(bool inputRedirected, bool outputRedirected)`
    - `bool IsInteractive()`, which uses `Console.IsInputRedirected` and `Console.IsOutputRedirected`
    - `bool IsUtf8Locale(bool isWindows, Func<string, string> getEnvironmentVariable)`
    - `string Utf8Warning(bool insideTmux)`
    - `const string NoTerminalMessage = "No interactive terminal detected; running in CLI mode"`
  - `public enum EnumUserInterfaceMode { AdminConsole, Cli }`.
  - `public static class UserInterfaceModeSelector`, with:
    - `EnumUserInterfaceMode Select(bool cliRequested, bool consoleSessionRequested, bool apiReportRequested, bool interactiveTerminal)`
    - `bool IsHeadlessFallback(bool cliRequested, bool consoleSessionRequested, bool apiReportRequested, bool interactiveTerminal)`

- [ ] **Step 1: Write failing tests**
  - `IsInteractive`: true only for `(false, false)`.
  - `IsUtf8Locale_Windows_AlwaysTrue`.
  - `IsUtf8Locale_PrecedenceAndMatching`: Theory over environment dictionaries:
    - `{LANG: "en_US.UTF-8"}`→true
    - `{LC_ALL: "C", LANG: "en_US.UTF-8"}`→false
    - `{LC_CTYPE: "UTF-8"}`→true
    - `{LANG: "C.utf8"}`→true
    - `{}`→false
    - `{LANG: "C"}`→false
    - `{LC_ALL: "", LANG: "en_US.UTF-8"}`→true (empty is skipped)
  - `Utf8Warning_MentionsLangAndTmuxOnlyInsideTmux`:
    - Outside tmux, it contains `LANG=C.UTF-8` and not `tmux -u`.
    - Inside tmux, it contains both.
    - Exact text: `"Warning: the terminal locale is not UTF-8, so the admin console may not draw its borders correctly. Set LANG=C.UTF-8 before starting MBBSEmu."`, with `" Inside tmux, also start tmux with -u."` appended inside tmux.
  - `Select_AdminConsoleOnlyForInteractiveNormalRun`: `(false, false, false, true)`→AdminConsole. Each of `cli`/`console`/`apiReport` true → Cli. Non-interactive → Cli.
  - `IsHeadlessFallback_OnlyWhenNoFlagAndNoTerminal`: `(false, false, false, false)`→true. Any flag → false. Interactive → false.
- [ ] **Step 2:** Run both test classes. Expected: FAIL.
- [ ] **Step 3:** Implement. `IsUtf8Locale` takes the first non-empty value of `LC_ALL`, `LC_CTYPE` and `LANG`, then checks `Contains("UTF-8")` or `Contains("UTF8")` with `OrdinalIgnoreCase`.
- [ ] **Step 4:** Run the tests. Expected: PASS.
- [ ] **Step 5:** Commit: `"Add terminal detection and UI mode selection"`.

### Task 8: Shutdown coordinator

**Files:**
- Create: `MBBSEmu/UI/Main/ShutdownCoordinator.cs` (holds `EnumShutdownPhase`)
- Test: `MBBSEmu.Tests/UI/Main/ShutdownCoordinator_Tests.cs`

**Interfaces:**
- Produces:
  - `public enum EnumShutdownPhase { Running, ShuttingDown, Stopped }`.
  - `public sealed class ShutdownCoordinator`, with:
    - `static readonly TimeSpan UnattendedTimeout = TimeSpan.FromSeconds(15)`
    - `ShutdownCoordinator(Action stopServices, Func<DateTime> utcNow)`
    - `EnumShutdownPhase Phase`
    - `Exception StopException`
    - `bool Begin(bool unattended)`
    - `bool DeadlineExpired`

- [ ] **Step 1: Write failing tests**
  - `Begin_RunsStopOnceInBackground`: `stopServices` blocks on a `ManualResetEventSlim`. `Begin(false)` returns true and `Phase == ShuttingDown`. A second `Begin(false)` returns false. After the event is released, `Phase` becomes `Stopped` within 5 s, and stop was called once.
  - `Begin_StopThrows_RecordsExceptionAndStops`: `Phase == Stopped` and `StopException` is the thrown exception.
  - `DeadlineExpired_UserInitiated_NeverExpires`: with a fake clock advanced by 1 hour → false.
  - `DeadlineExpired_Unattended_AfterFifteenSeconds`: `Begin(true)` with stop blocked. At +14.9 s it is false; at +15 s, true.
  - `Begin_UnattendedAfterUserInitiated_AddsDeadline`: `Begin(false)`, then `Begin(true)` returns false, but at +15 s `DeadlineExpired` is true.
  - `DeadlineExpired_FalseOnceStopped`.
- [ ] **Step 2:** Run the tests. Expected: FAIL.
- [ ] **Step 3:** Implement. `stopServices` runs via `Task.Run`, and the phase moves to `Stopped` in a `finally`. Phase and deadline are guarded by a lock.
- [ ] **Step 4:** Run the tests. Expected: PASS.
- [ ] **Step 5:** Commit: `"Add shutdown coordinator for the admin console"`.

### Task 9: UI log capture and keeping raw output off the screen

**Files:**
- Create: `MBBSEmu/UI/Logging/UiLogCapture.cs`, `MBBSEmu/Logging/LogLineWriter.cs`, `MBBSEmu/IO/ConsoleOwnership.cs`
- Modify: `MBBSEmu/HostProcess/MbbsHost.cs:1020-1033` (the auxiliary program runner)
- Test: `MBBSEmu.Tests/UI/Logging/UiLogCapture_Tests.cs`, `MBBSEmu.Tests/Logging/LogLineWriter_Tests.cs`

**Interfaces:**
- Consumes: the Task 2 `LoggerBase` members; Task 3 `ToApplicationEntry`/`ToAuditEntry`.
- Produces:
  - `public sealed class UiLogCapture : IDisposable`, with:
    - `const int QueueCapacity = 10_000`
    - `UiLogCapture(LoggerBase messageLogger, LoggerBase auditLogger)`
    - `IReadOnlyList<ApplicationLogEntry> DrainApplicationLog()`
    - `IReadOnlyList<AuditLogEntry> DrainAuditLog()`
    - `Dispose()`
  - `public sealed class LogLineWriter : TextWriter`, with `LogLineWriter(Action<string> writeLine)`.
  - `public static class ConsoleOwnership`, with `bool IsUiActive` and `void SetUiActive(bool active)`.

- [ ] **Step 1: Write failing tests**
  - `Constructor_RemovesConsoleTargetAndCaptures`: fresh `MessageLogger(new ConsoleTarget())` and `AuditLogger(new ConsoleTarget())`. After construction, `HasTarget<ConsoleTarget>()` is false on both. `logger.Info("hello")` → `DrainApplicationLog()` has one entry with `Message == "hello"` and `Level == Information`, and a second drain is empty.
  - `AuditLog_IsCaptured`: `audit.Log("LOGON", "SYSOP")` → `DrainAuditLog()` equals `[("LOGON", "SYSOP")]`.
  - `Dispose_RestoresConsoleTargetOnlyWhereItExisted`: the message logger had a `ConsoleTarget` and the audit logger had none. After `Dispose`, the message logger has a `ConsoleTarget`, the audit logger doesn't, and neither has a `QueueTarget`.
  - `LogLineWriter_EmitsCompleteLines`: writing `"hello\nworld\r\npartial"` emits `["hello", "world"]`. `Flush()` then emits `"partial"`. `Dispose()` with nothing buffered emits nothing more.
- [ ] **Step 2:** Run both test classes. Expected: FAIL.
- [ ] **Step 3:** Implement. Queue targets are `new QueueTarget(QueueCapacity)`. `Dispose` uses `RemoveTarget(instance)`. In `MbbsHost`, when `ConsoleOwnership.IsUiActive`, pass `ExeRuntime`:
  - `TextReader.Null` for stdin.
  - `new LogLineWriter(l => Logger.Info($"[{exe}] {l}"))` for stdout.
  - `new LogLineWriter(l => Logger.Error($"[{exe}] {l}"))` for stderr.

  Flush and dispose both writers after `runtime.Dispose()`. Otherwise keep today's `Console` streams. This branch has no unit test, because it needs a DOS executable. The code review covers it.
- [ ] **Step 4:** Run the tests. Expected: PASS. Run the full suite. Expected: all passing.
- [ ] **Step 5:** Commit: `"Capture logs for the UI without replacing loggers"`.

### Task 10: Admin console view

**Files:**
- Create:
  - `MBBSEmu/UI/ConsoleTheme.cs`
  - `MBBSEmu/UI/Main/IAdminConsoleDialogs.cs`
  - `MBBSEmu/UI/Main/TerminalGuiAdminConsoleDialogs.cs`
  - `MBBSEmu/UI/Main/LogTableSources.cs`
  - `MBBSEmu/UI/Main/AdminConsoleView.cs`
  - `MBBSEmu/UI/TooSmallView.cs`
- Modify: `MBBSEmu/UI/Main/AdminConsoleText.cs` (add strings)
- Test: `MBBSEmu.Tests/UI/TerminalGuiHarness.cs`, `MBBSEmu.Tests/UI/TerminalGuiCollection.cs`, `MBBSEmu.Tests/UI/Main/AdminConsoleView_Tests.cs`

**Interfaces:**
- Consumes: Tasks 3–6.
- Produces:
  - `public interface IAdminConsoleDialogs`, with:
    - `bool ConfirmShutdown()`
    - `bool ConfirmForceQuit()`
    - `void ShowEntry(string title, string text)`
    - `void ShowAbout()`
  - `TerminalGuiAdminConsoleDialogs(IApplication app, string build) : IAdminConsoleDialogs`. Its prompts use `MessageBox.Query(app, …)` with buttons `"Yes"` and `"No"`, defaulting to No. Shutdown and force quit return true only for Yes.
  - `AdminConsoleText` additions:
    - `ShutdownTitle = "Shutdown"`
    - `ShutdownMessage = "Shut down MBBSEmu? All connected users will be disconnected."`
    - `ForceQuitTitle = "Force Quit"`
    - `ForceQuitMessage = "Force quit now? Shutdown has not finished."`
    - `ShuttingDown = "Shutting down…"`
    - `TooSmallQuitHint = "Ctrl+Q still shuts down MBBSEmu."`
    - `ApplicationEntryTitle = "Log Entry"`
    - `AuditEntryTitle = "Audit Entry"`
  - `ConsoleTheme`: `public static` `Scheme`s `Base`, `Frame`, `Menu`, `Selected`, and `Attribute`s `ErrorRow`, `WarningRow`, `DimChannel`, with the colors from Global Constraints.
  - `ApplicationLogTableSource : ITableSource`: columns `Time`, `Source`, `Level`, `Message` over a `BoundedLog<ApplicationLogEntry>`. Cells come from `LogEntryFormatter`, with Message through `SingleLine`. It has a `bool ShowMilliseconds` property.
  - `AuditLogTableSource : ITableSource`: columns `Summary`, `Detail`.
  - `TooSmallView : View`, with `TooSmallView(string quitHint)` and `void Show(int width, int height)`.
  - `public sealed class AdminConsoleView : Window`, with:
    - `AdminConsoleView(IAdminConsoleDialogs dialogs, string statusRightText)`
    - `const int ApplicationLogCapacity = 5_000` and `const int AuditLogCapacity = 1_000`
    - `event EventHandler ShutdownRequested`
    - `void ApplyScreenSize(int width, int height)`
    - `void Update(IReadOnlyList<ApplicationLogEntry> newApplicationEntries, IReadOnlyList<AuditLogEntry> newAuditEntries, IReadOnlyList<ChannelLine> channels, int onlineCount)`
    - `void SetShuttingDown()`
    - `int ApplicationLogCount` and `int AuditLogCount`
    - `internal TableView ApplicationLogTable`, `internal TableView AuditLogTable` and `internal ListView ChannelListView` for tests
  - Test harness: `[CollectionDefinition("TerminalGui", DisableParallelization = true)] public class TerminalGuiCollection {}`, and `sealed class TerminalGuiHarness : IDisposable`, with:
    - `TerminalGuiHarness(int width, int height)`, which runs `Application.Create()`, `Init(DriverRegistry.Names.ANSI)` and `Driver.SetScreenSize`
    - `IApplication App`
    - `void Show(Window window)`, which calls `Begin` and `LayoutAndDraw(true)`
    - `string[] Screen()`, which splits `App.ToString()` into lines with trailing `\r` trimmed
    - `void Resize(int width, int height)`, which calls `SetScreenSize` and `LayoutAndDraw(true)`
    - `Dispose()`, which ends the session and disposes the app

- [ ] **Step 1: Write failing tests.** Every class has `[Collection("TerminalGui")]`. Each test builds the view with a fake `IAdminConsoleDialogs` that records calls, `statusRightText = StatusLine.RightText(true, false, "2026.10.26.1")`, 8 channels with `0: SYSOP` online, and the sample entries from the spec mockup. It calls `ApplyScreenSize` after `Show` and after each `Resize`.
  - `At80x24_LaysOutSidebarAndLogs`: `Screen()[1]` contains `"Audit Log"` and `"Channels (1/8)"`. The character at `[1][60]` is `'╔'`. The Application Log header line contains `"Time"`, `"Level"` and `"Message"` but not `"Source"`. `Screen()[23]` contains `"^Q"`, `"Shutdown"` and `"Telnet: ON  Rlogin: OFF  Build 2026.10.26.1"`, but not `"Next panel"`.
  - `At132x40_ShowsSourceAndMilliseconds`: the header contains `"Source"`, a row contains `"21:07:09.204"` and `"BtrieveFileProcessor"`, and `Screen()[39]` contains `"Next panel"`.
  - `Below80x24_ShowsTooSmallScreen`: Theory over (79,24) and (80,23). Some line contains `"Terminal too small"`, some line contains `$"Currently {w}×{h}, need at least 80×24"`, and no line contains `"Audit Log"`.
  - `ResizeAcrossFloor_RestoresLayout`: 80×24, then 70×20, then 80×24 → `"Audit Log"` is present again and `"Terminal too small"` is absent.
  - `TinySizes_DoNotThrow` (Review Focus 2): resizing through (10,3), (1,1) and back to (80,24) throws nothing and ends with `"Audit Log"` present.
  - `LongMessage_TruncatesWithEllipsis`: a 200-character message → the row containing its first 20 characters also contains `"…"`, and no line is wider than 80.
  - `LongUsername_TruncatesInSidebar` (Review Focus 4): channel line `"1: ABCDEFGHIJKLMNOPQRSTUVWXYZABC"` at 80×24 → its line contains `"1: ABCDEFGHIJKLMN…"`, and `[1][60]` is still `'╔'`.
  - `Update_FollowsWhenAtBottom`: append 50 entries → the last message is visible. Set `ApplicationLogTable.SelectedRow = 0` and append 5 more → `SelectedRow` stays 0 and the newest message is not visible.
  - `Update_RetainsCapacity_UnderBurst`: one `Update` with 20,000 application entries → `ApplicationLogCount == 5000` and the last entry is visible. 1,500 audit entries → `AuditLogCount == 1000`.
  - `EnterOnApplicationRow_ShowsEntry`: focus `ApplicationLogTable`, select the ERROR row and inject `Key.Enter` → the fake recorded `ShowEntry("Log Entry", text)`, where `text` contains `"Class: MBBSEmu.Btrieve.BtrieveFileProcessor"`.
  - `EnterOnAuditRow_ShowsEntry`: `ShowEntry("Audit Entry", "Summary: LOGON\nDetail: …")`.
  - `MenuShutdown_RaisesShutdownRequested`: invoke the Server → Shutdown menu item's action → the event fires once.
  - `SetShuttingDown_ReplacesHints`: `Screen()[23]` contains `"Shutting down…"` and not `"^Q"`.
- [ ] **Step 2:** Run `--filter "FullyQualifiedName~AdminConsoleView_Tests"`. Expected: FAIL.
- [ ] **Step 3: Implement the view.**
  - **Structure:** a menu bar, Audit and Application `FrameView`s with `LineStyle.Double` and `ConsoleTheme.Frame`, a channels `FrameView` holding a `ListView`, a one-row status line built from `Label`s (hints on the left, right text right-aligned), and a full-screen `TooSmallView`.
  - **`ApplyScreenSize`:** toggles between the panels and `TooSmallView` using `ScreenSize.IsBelowMinimum`. When at or above 80×24, it applies `AdminConsoleLayout.Calculate(...)` with absolute positions and sizes, the column `MinWidth`/`MaxWidth`, Source `Visible`, `ShowMilliseconds`, and `StatusLine.FitHints`. `TruncationIndicator` stays at its default `"…"`.
  - **Tables:** `FullRowSelect = true` and `Style.AlwaysShowHeaders = true`. Turn off the header overline and the outer left and right cell lines, so each table sits flush inside its frame as in the spec mockup; keep the header underline and the lines between columns. Set every column's `ColumnStyle` so `TruncationIndicator` applies. `RowColorGetter` returns ErrorRow for ERROR/CRIT and WarningRow for WARN.
  - **`Update`:** appends to both `BoundedLog`s. It applies `FollowSelection.Adjust` using `RowOffset` and `Viewport.Height` to compute `lastRowVisible`. When following, it calls `EnsureSelectedCellIsVisible`. Otherwise it sets `RowOffset = Max(0, RowOffset − dropped)`. It then rebuilds the channel list from the `ChannelLine`s (dimmed when disconnected), sets the title with `ChannelList.Title`, and calls `SetNeedsDraw`. Channel text longer than the sidebar interior (`SidebarWidth − 2`) is cut to `interior − 1` characters plus `…`. The channel `ListView` shows its vertical scroll bar automatically when there are more channels than rows.
  - **Row activation:** the application table's activation event calls `dialogs.ShowEntry(ApplicationEntryTitle, LogEntryFormatter.ApplicationDetail(entry))`. The audit table does the same with `AuditEntryTitle` and `AuditDetail`.
  - **Menu:** Server → `_Shutdown` raises `ShutdownRequested`, and Help → `_About` calls `dialogs.ShowAbout()`.
- [ ] **Step 4:** Run the tests. Expected: PASS. Run the full suite. Expected: all passing.
- [ ] **Step 5:** Commit: `"Add the Terminal.Gui v2 admin console view"`.

### Task 11: Admin console controller

**Files:**
- Create: `MBBSEmu/UI/Main/AdminConsole.cs`, `MBBSEmu/UI/Main/AdminConsoleKeys.cs`
- Modify: `MBBSEmu/UI/Main/AdminConsoleText.cs` (add exit strings)
- Test: `MBBSEmu.Tests/UI/Main/AdminConsoleKeys_Tests.cs`, `MBBSEmu.Tests/UI/Main/AdminConsole_Tests.cs`

**Interfaces:**
- Consumes: Tasks 7–10.
- Produces:
  - `public enum EnumConsoleKeyAction { None, Swallow, Shutdown }`.
  - `public static class AdminConsoleKeys` with `EnumConsoleKeyAction Classify(Key key)`.
  - `public enum EnumAdminConsoleOutcome { Shutdown, StartupFailed, UiFailed, ForceQuit, TerminalUnavailable }`.
  - `public sealed record AdminConsoleResult(EnumAdminConsoleOutcome Outcome, Exception Exception)`.
  - `public sealed class AdminConsoleOptions`, with init-only properties:
    - `MessageLogger MessageLogger`
    - `AuditLogger AuditLogger`
    - `Func<IReadOnlyList<ChannelSession>> GetSessions`
    - `int ChannelCount`
    - `string StatusRightText`
    - `string Build`
    - `string StartupWarning` (nullable)
    - `Func<IApplication> CreateApplication` (default: `Application.Create()` then `Init()`)
    - `Func<IApplication, IAdminConsoleDialogs> CreateDialogs` (default: `TerminalGuiAdminConsoleDialogs`)
    - `Func<DateTime> UtcNow` (default: `DateTime.UtcNow`)
    - `Action<int> ExitProcess` (default: `Environment.Exit`)
  - `public sealed class AdminConsole`, with:
    - `static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(250)`
    - `AdminConsole(AdminConsoleOptions options)`
    - `AdminConsoleResult Run(Action startServices, Action stopServices)`
  - `AdminConsoleText` additions:
    - `ShutDownComplete = "MBBSEmu shut down"`
    - `ForceQuitExit = "MBBSEmu force quit before shutdown finished"`
    - `UnattendedTimeoutExit = "MBBSEmu shutdown did not finish within 15 seconds; exiting"`

- [ ] **Step 1: Write failing tests**
  - `Classify`: Theory:
    - `Key.Q.WithCtrl` and `Key.C.WithCtrl` → `Shutdown`
    - `Key.Esc` and `Key.Z.WithCtrl` → `Swallow`
    - `Key.Tab`, `Key.Enter` and `Key.A` → `None`
  - `AdminConsole_Tests` is a `[Collection("TerminalGui")]` class. Each test runs `Run` inside `Task.Run` and asserts it finishes within 10 s. `CreateApplication` builds an ANSI app at 80×24 and schedules the test's injected keys with `app.AddTimeout`. Fake dialogs answer from queues, fresh `MessageLogger`/`AuditLogger` instances are created per test, and `ExitProcess` is a recorder.
  - `CtrlQConfirmed_StopsServicesAndReturnsShutdown`: `startServices` is called once and `stopServices` once. The outcome is `Shutdown`.
  - `CtrlQDeclined_KeepsRunning`: the first confirm answers No. 300 ms later `stopServices` has not been called. A second Ctrl+Q answered Yes then ends with `Shutdown`.
  - `Esc_DoesNotStopTheConsole`: inject `Esc`. 300 ms later the console is still running and no dialog was asked. Ctrl+Q with Yes then ends it.
  - `CtrlQDuringStartup_StopsAfterStartFinishes` (Review Focus 1): `startServices` blocks on an event. Ctrl+Q with Yes, then release the event → `stopServices` is called after start returns, and the outcome is `Shutdown`.
  - `SecondCtrlQDuringShutdown_AsksForceQuit`: `stopServices` blocks. Ctrl+Q with Yes, then Ctrl+Q again with force-quit Yes → `ExitProcess(1)` is recorded and the outcome is `ForceQuit`.
  - `StartupThrows_ReturnsStartupFailed`: `startServices` throws `InvalidOperationException("boom")` → outcome `StartupFailed` with that exception.
  - `TimerFailure_ReturnsUiFailedAndStopsServices`: `GetSessions` throws on its 2nd call → outcome `UiFailed`, and `stopServices` was called.
  - `LogsGoToUiDuringRunAndConsoleAfter`: inside a scheduled timeout, `MessageLogger.HasTarget<ConsoleTarget>()` is false and `ConsoleOwnership.IsUiActive` is true. After `Run` returns, both are reversed.
  - `StartupWarning_IsLoggedToApplicationLog`: before `Run`, the test adds its own `QueueTarget` to `MessageLogger`. With `StartupWarning = "W"`, after `Run` that queue holds an entry with level `Warning` and message `"W"`. `UiLogCapture` removes only `ConsoleTarget`s, so the test's target survives.
- [ ] **Step 2:** Run both test classes. Expected: FAIL.
- [ ] **Step 3: Implement `Run`.**
  1. Create the app. If that throws, return `TerminalUnavailable`. Then `ConsoleOwnership.SetUiActive(true)`, create the `UiLogCapture`, the view and the dialogs.
  2. Hook `app.ScreenChanged` to `view.ApplyScreenSize`, then apply the current `app.Screen` size.
  3. Hook `app.Keyboard.KeyDown`. Act only when `app.TopRunnableView == view`, which leaves dialogs on their own keys. `Swallow` sets `Handled`; `Shutdown` sets `Handled` and calls `RequestShutdown(unattended: false)`. `view.ShutdownRequested` also leads to `RequestShutdown(false)`.
  4. Register `PosixSignalRegistration` for `SIGINT`, `SIGTERM` and `SIGHUP`. Each handler sets `ctx.Cancel = true` and calls `app.Invoke(() => RequestShutdown(true))`. Ignore `PlatformNotSupportedException`.
  5. `AddTimeout(RefreshInterval)`:
     - Drain the capture, build the channels with `ChannelList.Build`/`CountOnline` from `GetSessions()`, and call `view.Update`.
     - If the coordinator is `Stopped`, call `RequestStop`.
     - If `DeadlineExpired`, call `ForceExit(UnattendedTimeoutExit)`.
     - Return true to keep the timer running.
     - The callback body is wrapped in `try`/`catch`. An exception is recorded as the UI failure, followed by `RequestStop`. Run's error handler covers everything else.
  6. Start services with `Task.Run(startServices)`. An exception is stored and followed by `app.Invoke(app.RequestStop)`.
  7. Run `app.Run(view, ex => { uiException = ex; return false; })` inside `try`/`catch`.
  8. In `finally`, in this order: unregister the signals, dispose the app (which restores the terminal), dispose the capture, then `SetUiActive(false)`.
  9. On a UI failure: log `LogLevel.Critical` (`"Admin console failed"`, with the exception) to the now console-bound logger, `Begin(unattended: true)`, wait until `Stopped` or `DeadlineExpired`, and on expiry print `UnattendedTimeoutExit` and call `ExitProcess(1)`. Return `UiFailed`.

  `RequestShutdown(unattended)` behaves by phase:
  - **Running:** if attended, call `ConfirmShutdown()` and return on No. Then `Begin(unattended)` and `view.SetShuttingDown()`.
  - **ShuttingDown:** if unattended, call `Begin(true)` to add the deadline. Otherwise, if `ConfirmForceQuit()` is Yes, call `ForceExit(ForceQuitExit)`.
  - **Stopping during startup:** the coordinator is built with the stop action `() => { WaitForStartTask(); stopServices(); }`, where `WaitForStartTask` waits on the start task and ignores its exception (that exception is reported separately).

  `ForceExit(msg)` disposes the app and the capture, writes `msg` to `Console`, calls `ExitProcess(1)`, then requests stop and records `ForceQuit`. Recording matters for tests, where `ExitProcess` returns.
- [ ] **Step 4:** Run the tests. Expected: PASS. Run the full suite. Expected: all passing.
- [ ] **Step 5:** Commit: `"Add admin console controller with shutdown, signals and failure handling"`.

### Task 12: Wire the admin console into Program

**Files:**
- Modify: `MBBSEmu/Program.cs`

**Interfaces:**
- Consumes: Task 7 (`TerminalEnvironment`, `UserInterfaceModeSelector`), Task 11 (`AdminConsole`, `AdminConsoleOptions`, `AdminConsoleResult`), Task 6 (`StatusLine.RightText`, `ChannelSession`).
- Produces, as private methods on `Program`:
  - `void StartServices(IMbbsHost host, AppSettingsManager configuration)`: today's host start, Telnet, Rlogin and "Started MBBSEmu Build" logic, adding to `_runningServices` under `lock (_runningServices)`.
  - `void StopServices()`: snapshots `_runningServices` under the lock, calls `Stop()` on each, then `host.WaitForShutdown()`.
  - `void PrintHelp(bool waitForKey)`.

- [ ] **Step 1:** Remove the Task 1 TEMPORARY admin-console line.
- [ ] **Step 2:** Add the mode decision after module configuration:
  - If `UserInterfaceModeSelector.IsHeadlessFallback(...)` is true, call `_logger.Info(TerminalEnvironment.NoTerminalMessage)`.
  - Select the mode with `Select(...)`.
  - `-APIREPORT` keeps its existing path.
  - `AdminConsole` mode:
    - Compute the UTF-8 warning with `TerminalEnvironment.IsUtf8Locale(OperatingSystem.IsWindows(), Environment.GetEnvironmentVariable)`. When it's needed, `Console.WriteLine` it first.
    - Build `AdminConsoleOptions`:
      - The loggers come from `new LogFactory().GetLogger<MessageLogger>()` and `GetLogger<AuditLogger>()`.
      - `GetSessions` comes from `host.GetUserSessions()` mapped to `ChannelSession(s.Channel, s.Username)`.
      - `ChannelCount` is `configuration.BBSChannels`.
      - The build string is the trimmed `version.txt` resource.
      - `StatusRightText` is `RightText(configuration.TelnetEnabled, configuration.RloginEnabled, build)`.
    - Then call `new AdminConsole(options).Run(() => StartServices(host, configuration), StopServices)`.
  - Handle the outcome:
    - `Shutdown`: `Console.WriteLine(AdminConsoleText.ShutDownComplete)`.
    - `StartupFailed`: print `"Critical Exception has occurred:"` and the exception, then `Environment.Exit(0)` (today's behavior).
    - `UiFailed`: the same print, then `Environment.Exit(1)`.
    - `TerminalUnavailable`: log `NoTerminalMessage` and continue with the CLI path.
  - CLI mode follows today's flow unchanged: `StartServices`, `CancelKeyPress`, console session.
- [ ] **Step 3:** In the `-?` case, call `PrintHelp(waitForKey: TerminalEnvironment.IsInteractive())`. For no arguments when not interactive, call `PrintHelp(false)` and return. The interactive wizard branch stays TEMPORARY until Task 14.
- [ ] **Step 4:** Build. Expected: success. Run the full suite. Expected: all passing.
- [ ] **Step 5: Create the smoke fixture.** In `/private/tmp/claude-501/-Users-ericnusbaum-Documents-GitHub-MBBSEmu/d236f1e6-f860-4dc7-8c58-d7af63c44526/scratchpad/tui-smoke/`, copy `MBBSEMU.DAT`, `.DLL`, `.MCV`, `.MDF` and `.MSG` from `MBBSEmu.Tests/Assets/` into `module/`. Add `appsettings.json`: a copy of `MBBSEmu/appsettings.json` with `"Telnet.Port": "2323"` and `"Rlogin.Enabled": "False"`.
- [ ] **Step 6: Headless check.** From the fixture directory, run `timeout 25 dotnet <MBBSEmu.dll> -M MBBSEMU -P module -S appsettings.json -DBRESET sysop < /dev/null > headless.log 2>&1`, natively or in Docker. Expected: `headless.log` contains `No interactive terminal detected; running in CLI mode` and `Started MBBSEmu Build`, and contains no `\x1b[?1049h` (alternate screen).
- [ ] **Step 7:** Commit `Program.cs`: `"Run the admin console on the main thread and fall back to CLI without a terminal"`.

### Task 13: Setup wizard model

**Files:**
- Create: `MBBSEmu/UI/Setup/SetupWizardModel.cs` (holds `EnumSetupStep`)
- Test: `MBBSEmu.Tests/UI/Setup/SetupWizardModel_Tests.cs`

**Interfaces:**
- Consumes: Task 1 `SetupWizardText`.
- Produces:
  - `public enum EnumSetupStep { Welcome, General, Telnet, Rlogin, Advanced, Security, Review }`.
  - `public sealed class SetupWizardModel`, with:
    - `SetupWizardModel(Random random)`, which sets `RegistrationNumber = random.Next(10000000, 99999999).ToString()`
    - Read/write properties, with the spec's defaults: `string BbsName`, `string Channels`, `string RegistrationNumber`, `TimeSpan CleanupTime`, `bool DoLoginRoutine`, `bool TelnetEnabled`, `string TelnetPort` (`"23"`), `bool TelnetHeartbeat`, `bool RloginEnabled`, `string RloginPort`, `string RloginRemoteIP`, `bool RloginPortPerModule`, `string DatabaseFilename`, `string BtrieveCacheSize`
    - `IReadOnlyList<string> Keys`
    - `string AddKey(string key)` (null on success)
    - `bool RemoveKey(int index)`
    - `void ResetKeys()`
    - `IReadOnlyList<string> Validate(EnumSetupStep step)`
    - `static string ValidationTitle(EnumSetupStep step)`
    - `AppSettings ToAppSettings()`
    - `string ToJson()`

- [ ] **Step 1: Write failing tests**
  - `Defaults_MatchSpec`: every default from the spec's parity inventory, including `TelnetPort == "23"`, `DoLoginRoutine == true`, `RloginEnabled == false`, `CleanupTime == 03:00` and `Keys == [DEMO, NORMAL, USER]`.
  - `ToAppSettings_UsesCurrentValues`: set `BbsName = "My BBS"`, `DoLoginRoutine = false`, `TelnetPort = "2323"` and `CleanupTime = 04:30` → `BBSTitle == "My BBS"`, `ModuleDoLoginRoutine == false`, `TelnetPort == "2323"` and `CleanupTime == "04:30"`.
  - `ToJson_ReflectsEditsMadeAfterConstruction`: the JSON contains `"BBS.Title": "My BBS"` and `"Module.DoLoginRoutine": false` after the edits.
  - `Validate_General`: Theory over invalid inputs → exact messages:
    - `BbsName = ""` → `SetupWizardText`'s BBS-name message
    - `Channels` of `"0"`, `"256"` or `""` → the channel-count message
    - `RegistrationNumber` of `"1234567"` or `"123456789"` → the registration message (exactly once)
  - `Validate_General_LeadingZerosAccepted` (Review Focus 5): `RegistrationNumber = "00012345"` → no errors, and `ToAppSettings().RegistrationNumber == "00012345"`.
  - `Validate_Telnet`:
    - Enabled with an empty port → the "enter a Telnet Port" message.
    - `"0"`, `"65536"` or `"99999999999"` → the "valid Telnet Port" message.
    - Disabled with an empty port → valid.
  - `Validate_Rlogin`: the same port rules, plus `RloginRemoteIP = "999.1.1.1"` → the remote IP message.
  - `Validate_Advanced`: empty filename → its message. `BtrieveCacheSize` of `"0"` or `""` → its message.
  - `Validate_Security`: after removing every key → the "at least one Account Default Key" message.
  - `AddKey`: `""` → `"Key cannot be empty"`. `"DEMO"` → `"Key already exists"`. `"SYSOP"` → null, and it's added.
  - `RemoveKey`: an out-of-range index → false.
  - `ResetKeys`: restores the defaults.
  - `ValidationTitle`: General→`"BBS Settings"`, Telnet→`"Telnet Settings"`, Rlogin→`"Rlogin Settings"`, Advanced→`"Advanced Settings"`, Security→`"Account Settings"`.
- [ ] **Step 2:** Run `--filter "FullyQualifiedName~SetupWizardModel_Tests"`. Expected: FAIL.
- [ ] **Step 3:** Implement. Ports are valid only when all digits and `int.TryParse` succeeds within 1–65535. `CleanupTime` is formatted `@"hh\:mm"`. `ToJson` uses `JsonSerializer.Serialize(ToAppSettings(), new JsonSerializerOptions { WriteIndented = true })`. `BtrieveCacheSize` falls back to `0` if it doesn't parse.
- [ ] **Step 4:** Run the tests. Expected: PASS.
- [ ] **Step 5:** Commit: `"Add setup wizard model with current-value settings and validation"`.

### Task 14: Setup wizard view and flow

**Files:**
- Create: `MBBSEmu/UI/Setup/ISetupWizardDialogs.cs`, `MBBSEmu/UI/Setup/TerminalGuiSetupWizardDialogs.cs`, `MBBSEmu/UI/Setup/SetupWizardView.cs`, `MBBSEmu/UI/Setup/SetupWizard.cs`
- Modify: `MBBSEmu/Program.cs` (the no-arguments branch)
- Test: `MBBSEmu.Tests/UI/Setup/SetupWizardView_Tests.cs`

**Interfaces:**
- Consumes: Task 13; Task 10 `TooSmallView`, `ConsoleTheme` and `TerminalGuiHarness`; Task 5 `ScreenSize`.
- Produces:
  - `public interface ISetupWizardDialogs`, with:
    - `void ShowError(string title, string message)`
    - `bool ConfirmWrite()`
    - `string AskSavePath(string directory, string fileName)` (null when cancelled)
    - `void ShowSaved(string path)`
  - `public sealed record SetupWizardResult(bool Saved, string Path)`.
  - `public sealed class SetupWizardView : Window`, with:
    - `SetupWizardView(SetupWizardModel model, ISetupWizardDialogs dialogs, Action<string, string> writeFile)`, where `writeFile` takes path and contents
    - `SetupWizardResult Result`
    - `void ApplyScreenSize(int width, int height)`
    - `internal Wizard Wizard`
    - `internal TextField TelnetPortField`
    - `internal TextField BbsNameField`
    - `internal TextField ChannelsField`
  - `public static class SetupWizard` with `SetupWizardResult Run()`. It creates the app with its default driver, hooks `ScreenChanged`, runs the view and disposes the app in `finally`.

- [ ] **Step 1: Write failing tests.** These are `[Collection("TerminalGui")]` tests with fake dialogs and a recording `writeFile`.
  - `At80x24_ShowsWizardInsideHostWindow`: the screen contains `"MBBSEmu Initial Setup"` and `"Welcome to MBBSEmu!"`.
  - `Below80x24_ShowsTooSmallScreen`: at 79×24 the screen contains `"Terminal too small"` and `"Esc cancels setup."`.
  - `PortField_AcceptsDigitsOnly`: focus `TelnetPortField`, clear it, inject `2`, `3`, `a` → `model.TelnetPort == "23"`.
  - `TypingInField_UpdatesModel`: inject text into `BbsNameField` → `model.BbsName` updates.
  - `NextWithInvalidChannels_ShowsErrorAndStays`: set `ChannelsField` to `"0"` on the General step and press Next → `ShowError("BBS Settings", <channel message>)`, and the step is still General.
  - `ReviewStep_ShowsCurrentValues`: change the BBS name, navigate to Review → the screen contains `"BBS.Title": "Edited"`.
  - `FinishDeclined_StaysOnReview`: `ConfirmWrite` returns false → `writeFile` is not called, the step is still Review, and `Result` is null.
  - `FinishSaved_WritesJsonAndReports`: `ConfirmWrite` true and `AskSavePath` returns `"/x/appsettings.json"` → `writeFile("/x/appsettings.json", model.ToJson())`, `ShowSaved` is called with that path, and `Result == (true, "/x/appsettings.json")`.
  - `SaveCancelled_ReturnsNotSaved`: `AskSavePath` returns null → `writeFile` is not called and `Result == (false, null)`.
  - `Esc_CancelsWizard`: inject `Key.Esc` → `Result == (false, null)`.
- [ ] **Step 2:** Run `--filter "FullyQualifiedName~SetupWizardView_Tests"`. Expected: FAIL.
- [ ] **Step 3: Implement the view.**
  - **Host:** a full-screen host `Window` titled `"MBBSEmu Initial Setup"`, double cyan border, containing a `Wizard` titled `"Initial Setup Wizard"`. The wizard is `Width = Dim.Percent(90)` and `Height = Dim.Percent(80)`, capped at screen size. There is also a `TooSmallView("Esc cancels setup.")` (add `TooSmallQuitHint = "Esc cancels setup."` to `SetupWizardText`).
  - **Steps:** seven `WizardStep`s with the `SetupWizardText` titles, labels and help texts. On focus (`HasFocusChanged`), each field sets its step's `HelpText`.
  - **Fields:** `TextField`s for the strings, with digit-only filtering on Channels, Registration Number, both ports and Btrieve Cache Size. A time editor for the cleanup time. `OptionSelector`s for the Yes/No and Enabled/Disabled choices, with the defaults from the model. The keys list has `+`, `−` and `Defaults` buttons that call `model.AddKey`/`RemoveKey`/`ResetKeys` and show any errors with the existing texts and titles.
  - **Validation:** in `Wizard.StepChanging`/`MovingNext`, run `model.Validate(oldStep)`. Show all of its messages in one `ShowError(ValidationTitle, string.Join("\n", messages))` and cancel the move.
  - **Review:** on entering it, the step shows `SetupWizardText.ReviewIntro + model.ToJson()` in a read-only, scrollable text view.
  - **Finish:**
    - `ConfirmWrite()` No: stay on Review.
    - Yes: `AskSavePath(Directory.GetCurrentDirectory(), "appsettings.json")`.
    - A null path: `Result = (false, null)` and stop.
    - Otherwise: `writeFile(path, json)`, `ShowSaved(path)`, `Result = (true, path)` and stop.
  - **Cancel:** Esc or cancel gives `Result = (false, null)` and stops. Esc keeps Terminal.Gui's default Quit binding here.
  - **Dialogs:** `TerminalGuiSetupWizardDialogs` implements the interface with `MessageBox`/`SaveDialog`, using the existing texts and allowing only `.json`.
- [ ] **Step 4:** In `Program.cs`, replace the no-arguments TEMPORARY branch. When interactive, show the existing `Y/n` prompt, and on Y call `SetupWizard.Run()`:
  - Saved: print `"Setup Wizard Complete!"` and the existing restart line.
  - Not saved: print `SetupWizardText.CancelledMessage`.
- [ ] **Step 5:** Run the tests. Expected: PASS. Run the full suite. Expected: all passing.
- [ ] **Step 6:** Commit: `"Port the setup wizard to Terminal.Gui v2 with bug fixes"`.

### Task 15: Docs, publish check, and local terminal smoke tests

**Files:**
- Modify: `MBBSEmu/Assets/commandLineHelp.txt`, `MBBSEmu/CLAUDE.md` (the `UI/` line in the directory map)
- Create (scratch, not committed): `/private/tmp/claude-501/-Users-ericnusbaum-Documents-GitHub-MBBSEmu/d236f1e6-f860-4dc7-8c58-d7af63c44526/scratchpad/tui-smoke/tui_smoke.py`

- [ ] **Step 1:** In `commandLineHelp.txt`, after the `-CONSOLE` entry, add:
  - `-CLI` with the description `Run without the admin console (logs print to the terminal). Used automatically when no terminal is attached`
  - The line `The admin console needs a UTF-8 terminal of at least 80x24.`

  Match the file's column alignment.
- [ ] **Step 2:** In `MBBSEmu/CLAUDE.md`, change `UI/  # Terminal.Gui interface (MainView, SetupView)` to `UI/  # Terminal.Gui v2 admin console (AdminConsole, AdminConsoleView) and setup wizard (SetupWizard)`.
- [ ] **Step 3: Publish check.** Publish all 8 RIDs with the CI's exact `dotnet publish` flags from `.github/workflows/dotnet.yml`. Expected: 8 successes.
- [ ] **Step 4: Write `tui_smoke.py`.** It's a Python 3 script using `pty`, `fcntl`/`termios` (`TIOCSWINSZ`) and `pyte`, installed into a scratch venv on macOS or with `apt-get install -y python3 python3-pyte tmux` inside the SDK container. It runs MBBSEmu with the Task 12 fixture arguments under a pty starting at 80×24, feeds output into a `pyte.Screen`, and prints PASS/FAIL per check:
  1. `"Audit Log"` and `"Channels ("` appear within 30 s.
  2. Resize to 132×40 → `"Source"` appears in the header.
  3. Resize to 70×20 → `"Terminal too small"`.
  4. Resize to 80×24 → `"Audit Log"` again.
  5. Send `\x1b` (Esc) → the process is still alive 2 s later.
  6. Send `\x1a` (Ctrl+Z) → still alive and not stopped (check `/proc` or `ps -o stat`, where `T` means fail).
  7. Send `\x11` (Ctrl+Q) → `"Shut down MBBSEmu?"` appears. Choose Yes (with the dialog's hotkey or Tab/Enter) → the process exits within 20 s.
  8. The raw output after the last frame contains `\x1b[?1049l` and `\x1b[?25h`, and the stream ends with `MBBSEmu shut down`.
  9. In a fresh run, `kill -TERM` the process → it exits within 20 s and check 8 holds.
- [ ] **Step 5: Run the smoke tests.**
  - In the Linux SDK container (`linux-x64` build), and natively on macOS (`osx-arm64` build from a local .NET 10 SDK, or a Docker cross-publish followed by `codesign -s - <binary>`). Expected: all checks PASS on both.
  - If tmux is available, repeat checks 1–4 inside `tmux new -d -x 80 -y 24`, resizing with `tmux resize-window`, and capture with `tmux capture-pane -p`. Expected: PASS.
  - Also repeat the Task 12 headless check with the published binaries.
- [ ] **Step 6:** Run the full test suite one final time. Expected: `Failed: 0`, with a passing count of 2957 plus all new tests.
- [ ] **Step 7:** Write the PR verification notes to `.../scratchpad/tui-smoke/RESULTS.md`:
  - The test count.
  - Each smoke check's result on macOS and Linux.
  - The owner's manual matrix from the spec as an unchecked list, for the owner to run.
- [ ] **Step 8:** Commit `commandLineHelp.txt` and `MBBSEmu/CLAUDE.md`, plus the spec and this plan under `docs/superpowers/`: `"Document admin console requirements and add design docs"`.
