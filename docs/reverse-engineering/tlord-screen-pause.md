# T-LORD screen-pause / counter chars (0x13, 0x14) — the real seam

Research date: 2026-08-17. Module: RTSLORD.DLL (Tournament Legend of the Red
Dragon). This documents *why* T-LORD's 0x13/0x14 screen-pause markers can leak
in MBBSEmu and where the faithful fix belongs, so we don't have to re-derive
it.

## The short version

T-LORD's embedded 0x13 and registered 0x14 bytes are **screen-pause /
line-counter markers** meant to be consumed by GALGSBL's screen-pause
subsystem. Real MajorBBS transmits zero of them. MBBSEmu did not implement
that subsystem, allowing the markers to leak onto the wire (the captured
native journey contained raw 0x13/XOFF; CP437→UTF-8 rendered it as `‼`). The
fix is to implement the screen-pause subsystem, **not** to filter or convert
the bytes blindly.

`RTSLORD.MCV` also contains one 0x11 byte, but it was not emitted in the
captured journey and its meaning has not been established. This document does
not classify it as a screen-pause marker.

## Evidence (static, from disassembly)

`MBBSDASM -I RTSLORD.DLL -ANALYSIS`. T-LORD imports **GALGSBL ordinal 39 =
`btupbc`** (set screen-pause character) and calls it at two sites, both pushing
`0x14`:

    push 0x14                      ; pausch = 0x14 (DC4)
    mov  ax, 0xffff / mov es, ax
    push word [es:0x0]             ; chan = usrnum (current channel)
    call ...                       ; int err=btupbc(int chan, char pausch)

So **0x14 is T-LORD's registered pause character.** Call sites: seg 0002 file
offsets ~0xB683 and ~0xD820.

T-LORD's full GALGSBL import set (ordinals): 64, 53, 30, 21, 52, 5, 4, 3,
**39 (btupbc)**, 8, 6, 11, 58, 72. Terminal-control calls it makes: `btupbc`,
`btutru`, `btumil`, `btuche`, `btuech`, `btuchi`, `btuclc`, `btucli`, `btucls`,
`btuinj`, `btutsw`, `btuxmn`. Notably **no `btucpc`** and no other pause/counter
registration.

## Authoritative GSBL spec (Galacticomm SBL Reference, Rev N, 1994)

The `MBBS4EVER/MajorBBS Docs/gsblref.pdf` reference (image-only; read pages
directly) defines the exact mechanism. Two functions, two characters, both
**consumed and never transmitted**:

- **`btupbc(chan, pausch)` — set screen-pause character** (GSBL p.133).
  *"When the pausch character is transmitted to the user **in ASCII output
  mode**, output pauses and the channel goes into screen-pause mode… **The
  Major BBS uses Control-T** for the pause character."* Control-T = **0x14**.
- **`btucpc(chan, cpchar)` — set clear pause-counter character** (GSBL p.81).
  *"When the cpchar character is discovered in the output stream… the internal
  line counter is reset to 0… **The character itself is never actually
  output.**… **In The Major BBS this function is used to prevent screen pauses
  by inserting the Control-S character** at strategic points."* Control-S =
  **0x13**.

So the Major BBS host defaults are **0x14 = pause char** and **0x13 =
clear-pause-counter char**, both host-consumed. T-LORD registers pausch = 0x14
via `btupbc` and embeds 0x13 relying on the host default — which is why real
MajorBBS transmits zero of either. `RTSLORD.MCV` holds 51× 0x13, 12× 0x14,
1× 0x11; the dominant wire leak is 0x13 (20× native).

## The distinguishing context: ASCII vs binary output mode

The pause char is only special **in ASCII output mode** (GSBL p.133). This is
the seam that separates "consume-me marker" from "display glyph" **without
naming any game**:

- T-LORD runs its pager text in ASCII output mode → 0x13/0x14 are consumed.
- MajorMUD's full-screen editor uses binary/raw output → 0x11–0x14 pass through
  as CP437 display glyphs (◄↕‼¶).

GSBL output modes are covered in the reference (§2.3, p.19; `btuxmt` ASCII
transmit vs `btuxmn` non-clearable/binary transmit). The distinction is a
per-channel GSBL state, not a byte-value table and not a per-game switch.

## Pre-change MBBSEmu state

- `Galgsbl.btupbc()` (ordinal 39) was a `//TODO -- Handle this?` no-op.
- `btucpc` (clear-pause-counter char) was not implemented.
- No pause-char, line-counter, or screen-pause state existed in any session.
- The XON/XOFF sibling above `btupbc` was also an explicit no-op
  ("we won't deal with XON/XOFF").

## Why the earlier "candidate fixes" were wrong

- The pre-change DC1–DC4 `SessionBase` filter stripped these
  bytes for *every* module — accidentally matching T-LORD but breaking
  MajorMUD, which uses 0x11–0x14 as legitimate display glyphs (#553).
- A "minimal btupbc-consume" (register 0x14, strip it) misses 0x13, the bigger
  leak, because 0x13 is embedded, not registered.
- Blind conversion (`CP437Converter` mapping 0x11–0x14 to glyphs) turns the
  T-LORD markers into visible `‼¶` litter.

None of these distinguish "display glyph" (MajorMUD) from "consume-me marker"
(T-LORD), because that distinction lives in the screen-pause subsystem, not in
a byte-value table.

## The faithful fix (screen-pause subsystem)

Implement the GALGSBL screen-pause seam. Per-session state on `SessionBase`
(alongside the existing `PromptCharacter`):

- `PauseCharacter` — set by `btupbc(chan, pausch)`. GSBL/Major BBS default 0x14.
- `ClearPauseCounterCharacter` — set by `btucpc(chan, cpchar)`. Major BBS
  default 0x13.
- `OutputLineCounter` + screen height — for the actual pause trigger (optional
  for the leak fix; required for a faithful MORE prompt).

In the output path (`SessionBase.SendToClient` / `SendBreakingIntoLines`,
where the DC1–DC4 filter lives today), **consume** the two registered chars:
strip `ClearPauseCounterCharacter` and reset the counter; strip
`PauseCharacter` (and, for full fidelity, pause when the counter has filled a
screen). Both "never actually output" per the spec. Then remove the blanket
DC1–DC4 `SessionBase` filter so unregistered bytes (MajorMUD's glyphs) pass.

### The gating decision (MajorMUD vs T-LORD)

The exact GSBL model consumes these chars only **in ASCII output mode**;
MajorMUD's FSD uses binary/raw output, so its 0x11–0x14 pass as glyphs.
MBBSEmu does not model ASCII/binary output mode today, **and there is no
existing signal to add it cheaply**: `btuxmt`, `btuxmn`, and MajorMUD's FSD
(all 19 of its output calls) funnel through the single
`SessionBase.SendToClient`; the only `SendToClientRaw` caller is one file-dump
in `Majorbbs.cs`. So Option A below must build the output-mode subsystem from
scratch and additionally mark FSD as binary. Two options:

- **Option A (faithful):** add per-channel ASCII/binary output-mode state
  (drive it from `btuxmt` vs `btuxmn` and the mode setters), default
  pause=0x14/clear=0x13, and consume only in ASCII mode. Larger — new state
  threaded through the output path and GSBL transmit calls.
- **Option B (register-gated, pragmatic):** consume only characters explicitly
  registered through `btupbc` or `btucpc`. Uses "did the module ask for
  pause/counter semantics?" as the proxy for ASCII-pager intent.

**Option B was implemented first and disproven empirically (2026-08-18).** An
in-process harness test driving the full captured T-LORD journey (intro → join
→ create → stats → happenings → Town Square → List Warriors) showed neither of
T-LORD's two `btupbc` call sites fires anywhere on that journey, while 23×
embedded 0x13 leaked — beginning with the module's first two output bytes. The
real host is clean on the same journey because **The Major BBS arms the
0x14/0x13 defaults host-side per channel** ("The Major BBS uses Control-T…"),
independent of module registration; T-LORD's `btupbc` sites are re-arming
overrides on rarer paths, not the enabling event.

The implemented fix is therefore **default arming + a minimal binary-mode
seam** (Option A scoped to what's observable today):

- Sessions start with pause=0x14/clear=0x13 armed; `btupbc`/`btucpc` override
  per channel (0 disables); module exit restores the defaults.
- Consumption is skipped for binary-mode output, approximated by the
  full-screen (FSD) session states plus an explicit `BinaryOutputMode` flag
  around `fsdbkg`'s template paint (which occurs while the session is still
  `InModule` — found empirically: MajorMUD's ◄ glyphs, 0x11, arrive via
  `fsdbkg`). Full `btuxmt`/`btuxmn` output-mode modeling remains future work.

Wire-invariant tests (`Tlord_ScreenPause_Tests`, `Mmud_GlyphPassthrough_Tests`,
gated on `MBBSEMU_TEST_MODULE_PATH`) lock in both sides: zero 0x11–0x14 on the
T-LORD journey, glyphs intact on MajorMUD's FSD worksheet.

## Cross-refs

- The MajorMUD glyph side of DC1–DC4 (0x11–0x14 rendered as ◄↕‼¶) is handled by
  `CP437Converter` when CP437→UTF-8 conversion is enabled — a separate change
  from this screen-pause consume.
- Ground-truth wire captures (real MajorBBS vs MBBSEmu across the T-LORD journey)
  were taken with a raw-telnet capture harness; the byte counts cited above are
  reproducible from them.
