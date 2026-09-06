#!/usr/bin/env python3
"""Telnet client that emulates an 80x25 IBM-PC screen.

MajorMUD's character sheet is a MajorBBS full-screen form. The server
paints field values with cursor-addressed overlays. A modern terminal
that wraps, treats a box glyph as two cells, or ignores CSI ... f
leaves the ???? template on screen and eats keystrokes into the wrong
field — or into a 2/3-byte input buffer FSD then ignores.

This client keeps an 80x25 cell grid (one CP437 byte = one cell, no
wrap), redraws that grid, and sends keys one at a time so FSD sees
either a single ASCII byte or a complete ESC[A / ESC[B packet.
"""

from __future__ import annotations

import argparse
import collections
import json
import os
import re
import select
import shutil
import socket
import sys
import termios
import time
import tkinter
import traceback
import tty
from pathlib import Path

IAC, DONT, DO, WONT, WILL = 255, 254, 253, 252, 251
SB, SE = 250, 240
ECHO, SGA, TTYPE, NAWS = 1, 3, 24, 31
TTYPE_IS, TTYPE_SEND = 0, 1
# Match system telnet (xterm), not a bare "ANSI" IS WG may ignore.
TTYPE_NAME = b"xterm-256color"

COLS, ROWS = 80, 25
CHROME = 5
# MajorMUD scolds under ~2s ("slow down for a few seconds"). Combat can
# Combat can stay snappier; walks and looks use WALK_GAP. Human typing
# (creation fields, WG menus) is TYPE_GAP — KEY_GAP on every letter
# feels like one key every few seconds. A flood pauses longer.
KEY_GAP = 0.55
TYPE_GAP = 0.07
WALK_GAP = 2.0
FLOOD_PAUSE = 5.0
# After first [HP=], sit still — creation can last minutes after Autopilot's E.
REALM_SETTLE = 8.0
ROOT = Path(__file__).resolve().parent.parent
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from client.brain import Brain
from client.localnet import LOOPBACK_HOSTS, is_local_play_host, is_loopback_host
from client.parse import events_from_payload, harvest_screen, parse_events
from client.paths import attack_line, is_unlatch_step
from client.pvp import clear_lock, is_locked
from client.realm_map import DEFAULT_PATH, Atlas
from client.signoff import paint as paint_signoff
from client.splash import paint as paint_piece
from client.state import WorldState
from client.transcript import Transcript

LOCAL_HOSTS = LOOPBACK_HOSTS
KEY_F1 = b"\x00F1"
KEY_F2 = b"\x00F2"
KEY_F3 = b"\x00F3"
KEY_F4 = b"\x00F4"
KEY_F5 = b"\x00F5"
KEY_F6 = b"\x00F6"
KEY_F7 = b"\x00F7"
KEY_F8 = b"\x00F8"
KEY_F9 = b"\x00F9"
KEY_F10 = b"\x00F10"
KEY_F11 = b"\x00F11"
KEY_F12 = b"\x00F12"
KEY_ESC = b"\x1b"
KEY_UP = bytes((0x1B, ord("["), 65))
KEY_DN = bytes((0x1B, ord("["), 66))
HOLD_SNAPSHOT = ROOT / "data" / "screen-hold.txt"
_CLIP_ROOT: tkinter.Tk | None = None

# Peek only — never takeover. `inv` is speech (Invite/Invoke share the prefix).
# Inventory is `i` or `inventory` — never send `inv`.
PEEK_COMMANDS = {
    KEY_F2: "look",
    KEY_F3: "health",
    KEY_F4: "i",
    KEY_F5: "exp",
    KEY_F6: "party",
}

# F1–F11 chrome. One table: footer tip and help overlay read this.
# on = live/active; off = the other state.
# F10 off is "held" (copy freeze). F11 held is SHEET (creation form only).
# Train hold is brain paused — not F10 copy, not a screen freeze.
FKEYS: dict[int, dict[str, str]] = {
    1: {"on": "panic"},
    2: {"on": "look"},
    3: {"on": "health", "short": "hp"},
    4: {"on": "i"},
    5: {"on": "exp"},
    6: {"on": "party"},
    7: {"on": "hunt", "off": "hunt off"},
    8: {"on": "ambush", "off": "walk", "note": "(ninja) · aa"},
    9: {"on": "join", "off": "join off"},
    10: {"on": "copy", "off": "held"},
    11: {"on": "train", "off": "live", "held": "SHEET", "note": "(hold, brain paused)"},
    12: {"on": "logoff — yours truly"},
}


def fkey_label(
    n: int,
    *,
    style: str = "tip",
    active: bool | None = None,
    word: str | None = None,
    short: bool = False,
    held: bool = False,
) -> str:
    """One F-key formatter. Flags pick tip, help, or F11 SHEET header."""
    spec = FKEYS[n]
    if style == "sheet_tip":
        tag = fkey_label(n, held=True)
        live = fkey_label(n, active=False)
        return f"{tag}  {live}  no health/exp/hunt until SAVE"
    if held and spec.get("held"):
        return spec["held"]
    if word is not None:
        action = word
    elif short and spec.get("short"):
        action = spec["short"]
    elif active is False:
        action = spec.get("off") or spec["on"]
    elif style == "help" and spec.get("off"):
        action = f"{spec['on']} / {spec['off']}"
        if spec.get("note"):
            action = f"{action} {spec['note']}"
    else:
        action = spec["on"]
    if style == "word":
        return action
    if style == "help":
        return f"F{n:<11}{action}"
    return f"F{n} {action}"

# SS3 / 8-bit SS3: P–S are F1–F4; T–W are F5–F8 on some terms.
_SS3_FKEYS = {
    80: KEY_F1,
    81: KEY_F2,
    82: KEY_F3,
    83: KEY_F4,
    84: KEY_F5,
    85: KEY_F6,
    86: KEY_F7,
    87: KEY_F8,
    88: KEY_F9,
    89: KEY_F10,
}
# Linux console ESC [[ A–E is F1–F5.
_LINUX_FKEYS = {
    65: KEY_F1,
    66: KEY_F2,
    67: KEY_F3,
    68: KEY_F4,
    69: KEY_F5,
}
# CSI n~  (xterm F5+; also F1–F4 in VT220 mode). 16 is unused.
_CSI_FKEYS = {
    11: KEY_F1,
    12: KEY_F2,
    13: KEY_F3,
    14: KEY_F4,
    15: KEY_F5,
    17: KEY_F6,
    18: KEY_F7,
    19: KEY_F8,
    20: KEY_F9,
    21: KEY_F10,
    23: KEY_F11,
    24: KEY_F12,
}
_CSI_SS3 = {
    b"\x1b[OP": KEY_F1,
    b"\x1b[OQ": KEY_F2,
    b"\x1b[OR": KEY_F3,
    b"\x1b[OS": KEY_F4,
}


class Telnet:
    def __init__(self, sock: socket.socket) -> None:
        self.sock = sock
        self._iac = bytearray()

    def feed(self, data: bytes) -> bytes:
        out = bytearray()
        i = 0
        buf = self._iac + data
        self._iac.clear()
        while i < len(buf):
            b = buf[i]
            if b != IAC:
                out.append(b)
                i += 1
                continue
            if i + 1 >= len(buf):
                self._iac.append(IAC)
                break
            cmd = buf[i + 1]
            if cmd == IAC:
                out.append(IAC)
                i += 2
                continue
            if cmd in (WILL, WONT, DO, DONT):
                if i + 2 >= len(buf):
                    self._iac.extend(buf[i:])
                    break
                self._negotiate(cmd, buf[i + 2])
                i += 3
                continue
            if cmd == SB:
                end = buf.find(bytes((IAC, SE)), i + 2)
                if end < 0:
                    self._iac.extend(buf[i:])
                    break
                self._subneg(buf[i + 2 : end])
                i = end + 2
                continue
            i += 2
        return bytes(out)

    def _send(self, data: bytes) -> None:
        try:
            self.sock.sendall(data)
        except (ConnectionResetError, BrokenPipeError, OSError):
            return

    def _negotiate(self, cmd: int, opt: int) -> None:
        if cmd == WILL and opt in (ECHO, SGA):
            self._send(bytes((IAC, DO, opt)))
        elif cmd == DO and opt == SGA:
            self._send(bytes((IAC, WILL, SGA)))
        elif cmd == DO and opt == TTYPE:
            self._send(bytes((IAC, WILL, TTYPE)))
            self._send_ttype()
        elif cmd == DO and opt == NAWS:
            self._send(bytes((IAC, WILL, NAWS)))
            self._send(bytes((IAC, SB, NAWS, 0, 80, 0, 25, IAC, SE)))
        elif cmd == DO:
            self._send(bytes((IAC, WONT, opt)))
        elif cmd == WILL:
            self._send(bytes((IAC, DONT, opt)))

    def _send_ttype(self) -> None:
        self._send(bytes((IAC, SB, TTYPE, TTYPE_IS)) + TTYPE_NAME + bytes((IAC, SE)))

    def _subneg(self, payload: bytes) -> None:
        if len(payload) >= 2 and payload[0] == TTYPE and payload[1] == TTYPE_SEND:
            self._send_ttype()

    def send(self, data: bytes) -> None:
        self._send(data)


class Cell:
    __slots__ = ("ch", "fg", "bg", "bold", "rev", "bright_bg")

    def __init__(self) -> None:
        self.ch = " "
        self.fg = 7
        self.bg = 0
        self.bold = False
        self.rev = False
        self.bright_bg = False

    def style_key(self) -> tuple[int, int, bool, bool, bool]:
        return (self.fg, self.bg, self.bold, self.rev, self.bright_bg)


def _blank() -> Cell:
    return Cell()


class AnsiScreen:
    """80x25 CP437 screen. One incoming byte is one cell. Wrap stays off."""

    def __init__(self, cols: int = COLS, rows: int = ROWS) -> None:
        self.cols = cols
        self.rows = rows
        self.buf = [[_blank() for _ in range(cols)] for _ in range(rows)]
        self.cx = 0
        self.cy = 0
        self.saved = (0, 0)
        self.fg = 7
        self.bg = 0
        self.bold = False
        self.rev = False
        self.bright_bg = False
        self._esc = bytearray()
        self.generation = 0
        self.replies: list[bytes] = []

    def feed(self, data: bytes) -> None:
        if not data:
            return
        i = 0
        while i < len(data):
            if self._esc:
                self._esc.append(data[i])
                i += 1
                if self._esc_complete():
                    self._apply_esc(bytes(self._esc))
                    self._esc.clear()
                elif len(self._esc) > 48:
                    self._esc.clear()
                continue
            b = data[i]
            if b == 0x1B:
                self._esc.append(b)
                i += 1
                continue
            if b == 0x9B:
                self._esc.extend(b"\x1b[")
                i += 1
                continue
            i = self._put_from(data, i)
        self.generation += 1

    def _esc_complete(self) -> bool:
        e = self._esc
        if len(e) == 1:
            return False
        if e[1] == ord("["):
            return len(e) >= 3 and 0x40 <= e[-1] <= 0x7E
        if e[1] in (ord("]"),):
            return e[-1] in (7, ord("\\"))
        return True

    def _apply_esc(self, seq: bytes) -> None:
        if seq in (b"\x1b7", b"\x1b[s"):
            self.saved = (self.cx, self.cy)
            return
        if seq in (b"\x1b8", b"\x1b[u"):
            self.cx, self.cy = self.saved
            return
        if not seq.startswith(b"\x1b["):
            return
        body = seq[2:-1]
        final = seq[-1]
        priv = False
        if body.startswith(b"?"):
            priv = True
            body = body[1:]
        params = []
        if body:
            for part in body.split(b";"):
                part = part.strip()
                if part.isdigit():
                    params.append(int(part))
                elif part == b"":
                    params.append(0)
                else:
                    return
        self._csi(priv, params, final)

    def _csi(self, priv: bool, params: list[int], final: int) -> None:
        if priv:
            if final == ord("n"):
                self._dsr(params)
            return

        def p(idx: int, default: int) -> int:
            if idx < len(params) and params[idx]:
                return params[idx]
            return default

        if final in (ord("H"), ord("f")):
            y = p(0, 1)
            x = p(1, 1)
            self.cy = min(self.rows - 1, max(0, y - 1))
            self.cx = min(self.cols - 1, max(0, x - 1))
            return
        if final == ord("A"):
            self.cy = max(0, self.cy - p(0, 1))
            return
        if final == ord("B"):
            self.cy = min(self.rows - 1, self.cy + p(0, 1))
            return
        if final == ord("C"):
            self.cx = min(self.cols - 1, self.cx + p(0, 1))
            return
        if final == ord("D"):
            self.cx = max(0, self.cx - p(0, 1))
            return
        if final == ord("J"):
            mode = p(0, 0)
            if mode == 2:
                self.buf = [[_blank() for _ in range(self.cols)] for _ in range(self.rows)]
                self.cx = 0
                self.cy = 0
            elif mode == 0:
                self._erase(self.cx, self.cy, self.cols, self.cy)
                for y in range(self.cy + 1, self.rows):
                    self._erase(0, y, self.cols, y)
            elif mode == 1:
                for y in range(0, self.cy):
                    self._erase(0, y, self.cols, y)
                self._erase(0, self.cy, self.cx + 1, self.cy)
            return
        if final == ord("K"):
            mode = p(0, 0)
            if mode == 0:
                self._erase(self.cx, self.cy, self.cols, self.cy)
            elif mode == 1:
                self._erase(0, self.cy, self.cx + 1, self.cy)
            else:
                self._erase(0, self.cy, self.cols, self.cy)
            return
        if final == ord("n"):
            self._dsr(params)
            return
        if final == ord("m"):
            self._sgr(params or [0])

    def _dsr(self, params: list[int]) -> None:
        """Worldgroup auto-sense sends CSI 6n; xterm answers or the BBS stays ASCII."""
        mode = params[0] if params else 0
        if mode == 6:
            self.replies.append(
                f"\x1b[{self.cy + 1};{self.cx + 1}R".encode("ascii")
            )
        elif mode == 5:
            self.replies.append(b"\x1b[0n")

    def _erase(self, x0: int, y: int, x1: int, _y1: int) -> None:
        row = self.buf[y]
        for x in range(x0, min(x1, self.cols)):
            row[x] = _blank()

    def _sgr(self, params: list[int]) -> None:
        if not params:
            params = [0]
        i = 0
        while i < len(params):
            n = params[i]
            if n == 0:
                self.fg, self.bg, self.bold, self.rev, self.bright_bg = (
                    7,
                    0,
                    False,
                    False,
                    False,
                )
            elif n == 1:
                self.bold = True
            elif n == 5:
                # VGA/iCE: blink bit is bright background (░▒▓ dither).
                self.bright_bg = True
            elif n == 7:
                self.rev = True
            elif n == 22:
                self.bold = False
            elif n == 27:
                self.rev = False
            elif 30 <= n <= 37:
                self.fg = n - 30
            elif 40 <= n <= 47:
                self.bg = n - 40
            elif 90 <= n <= 97:
                self.fg = n - 90
                self.bold = True
            elif 100 <= n <= 107:
                self.bg = n - 100
            elif n == 38 and i + 2 < len(params) and params[i + 1] == 5:
                self.fg = params[i + 2] & 7
                if params[i + 2] >= 8:
                    self.bold = True
                i += 2
            elif n == 48 and i + 2 < len(params) and params[i + 1] == 5:
                self.bg = params[i + 2] & 7
                i += 2
            i += 1

    def _put_from(self, data: bytes, i: int) -> int:
        b = data[i]
        if b in (0, 7):
            return i + 1
        if b == 8:
            self.cx = max(0, self.cx - 1)
            return i + 1
        if b == 9:
            self.cx = min(self.cols - 1, (self.cx + 8) & ~7)
            return i + 1
        if b == 10:
            if self.cy < self.rows - 1:
                self.cy += 1
            else:
                self.buf.pop(0)
                self.buf.append([_blank() for _ in range(self.cols)])
            return i + 1
        if b == 13:
            self.cx = 0
            return i + 1
        self._put_char(bytes((b,)).decode("cp437", "replace"))
        return i + 1

    def _put_char(self, ch: str) -> None:
        if self.cx >= self.cols:
            self.cx = self.cols - 1
        cell = Cell()
        cell.ch = ch
        cell.fg = self.fg
        cell.bg = self.bg
        cell.bold = self.bold
        cell.rev = self.rev
        cell.bright_bg = self.bright_bg
        self.buf[self.cy][self.cx] = cell
        if self.cx < self.cols - 1:
            self.cx += 1

    def line(self, y: int) -> str:
        return "".join(c.ch for c in self.buf[y])

    def text(self) -> str:
        return "\n".join(self.line(y) for y in range(self.rows))

    def looks_like_creation(self) -> bool:
        """FSD sheet or race/class pick.

        Leftover [HP=] on Character Creation / TRAIN STATS is still the
        form. Obvious exits / Also here / You notice is the realm — even
        if TRAIN STATS leftover is still on the grid.
        """
        blob = self.text()
        leftover_hp = "[HP=" in blob
        if (
            "Obvious exits" in blob
            or "Also here" in blob
            or "You notice" in blob
        ):
            return False
        low_early = blob.lower()
        if (
            "please enter a new name" in low_early
            or "you may not use" in low_early
            or "validating your name" in low_early
        ):
            return True
        if (
            "Character Creation" in blob
            or "Point Cost Chart" in blob
            or "TRAIN STATS" in blob
            or "Exit: SAVE" in blob
            or "cp left" in blob.lower()
        ):
            return True
        if leftover_hp:
            return False
        low = blob.lower()
        return (
            "select a race" in low
            or "choose a race" in low
            or "select a class" in low
            or "choose a class" in low
            or "available races" in low
            or "available classes" in low
        )

    def leave_form(self) -> None:
        """Drop leftover FSD field style — same defaults as a new screen."""
        self.fg, self.bg, self.bold, self.rev, self.bright_bg = (
            7,
            0,
            False,
            False,
            False,
        )
        self._esc.clear()
        self.generation += 1

    def render(self) -> bytes:
        out = bytearray(b"\x1b[?25l")
        prev: tuple[int, int, bool, bool, bool] | None = None
        for y in range(self.rows):
            out += f"\x1b[{y + 1};1H".encode()
            for cell in self.buf[y]:
                key = cell.style_key()
                if key != prev:
                    out += _sgr_bytes(cell)
                    prev = key
                # Keep CP437 shade/box glyphs (░▒▓). isprintable() drops nbsp.
                ch = cell.ch
                if ch == "\xa0":
                    ch = " "
                elif not ch.isprintable() and ch != " ":
                    ch = " "
                out += ch.encode("utf-8", "replace")
        out += _sgr_bytes(Cell())
        out += f"\x1b[{self.cy + 1};{self.cx + 1}H".encode()
        out += b"\x1b[?25h"
        return bytes(out)


def _sgr_bytes(cell: Cell) -> bytes:
    fg, bg = cell.fg, cell.bg
    if cell.rev:
        fg, bg = bg, fg
    parts = ["0"]
    if cell.bold:
        parts.append("1")
    parts.append(str(30 + fg))
    if cell.bright_bg:
        parts.append(str(100 + bg))
    else:
        parts.append(str(40 + bg))
    return f"\x1b[{';'.join(parts)}m".encode()


def realm_line(text: str, *, paladin: bool = False) -> str:
    """Visible swing is `att`. Paladin bash short form is `aa`. `k` is speech."""
    raw = text.strip()
    low = raw.lower()

    def _aim_after(prefix: str) -> str:
        rest = raw[len(prefix) :].strip()
        return rest

    if paladin:
        if low in {"aa on", "aa off"}:
            return raw
        if low in {"attack", "att", "bash", "aa", "kill", "k"}:
            return "aa"
        for prefix in ("attack ", "att ", "bash ", "aa ", "kill "):
            if low.startswith(prefix):
                aim = _aim_after(prefix)
                if prefix == "bash " and is_unlatch_step(raw):
                    return raw
                return f"aa {aim}" if aim else "aa"
        if low.startswith("k ") and not low.startswith("kly"):
            aim = _aim_after("k ")
            return f"aa {aim}" if aim else "aa"
        return raw
    if low in {"attack", "att", "kill", "k"}:
        return attack_line()
    if low.startswith("attack "):
        return attack_line(raw[7:])
    if low.startswith("att "):
        return attack_line(raw[4:])
    if low.startswith("kill "):
        return attack_line(raw[5:])
    if low.startswith("k ") and not low.startswith("kly"):
        return attack_line(raw[2:])
    if low in {"bash"}:
        return "aa"
    if low.startswith("bash "):
        if is_unlatch_step(raw):
            return raw
        rest = raw[5:].strip()
        return f"aa {rest}" if rest else "aa"
    return raw


_WALK_VERBS = frozenset(
    {
        "n",
        "s",
        "e",
        "w",
        "u",
        "d",
        "ne",
        "nw",
        "se",
        "sw",
        "look",
        "l",
        "sn",
        "sneak",
        "follow",
        "fo",
        "join",
        "backr",
        "backrank",
        "go",
        "borrow",
        "search",
        "invite",
    }
)


class KeyPacer:
    """FSD only accepts one ASCII byte or one 3-byte arrow per poll."""

    def __init__(self, *, paladin: bool = False) -> None:
        self._q: collections.deque[bytes] = collections.deque()
        self._ready_at = 0.0
        self.paladin = paladin

    def push(self, key: bytes) -> None:
        if key:
            self._q.append(key)

    def push_text(self, text: str, *, wipe: bool = True) -> None:
        """One line, one CR. Do not prefix backspaces — extras eat `at giant rat`."""
        line = realm_line(text, paladin=self.paladin) if wipe else text
        if line:
            self._q.append(line.encode("ascii", "replace") + b"\r")

    def queued(self) -> bool:
        """A line is waiting. Cooldown alone is not queued — login can stage M/E."""
        return bool(self._q)

    def pending(self) -> bool:
        """Busy while a line is queued or the last send is still cooling down."""
        return bool(self._q) or time.monotonic() < self._ready_at

    def clear(self) -> None:
        self._q.clear()

    def pause(self, now: float, hold: float) -> None:
        self._ready_at = max(self._ready_at, now + hold)

    def take(self, now: float) -> bytes | None:
        if not self._q or now < self._ready_at:
            return None
        key = self._q.popleft()
        self._ready_at = now + self._gap(key)
        return key

    def _gap(self, key: bytes) -> float:
        # Whole commands (hunt / login lines) keep KEY_GAP. One keystroke
        # on the sheet or a WG prompt must not wait half a second.
        if not key.endswith(b"\r") or len(key) <= 1:
            return TYPE_GAP
        line = key.decode("ascii", "replace").strip().lower()
        verb = line.split()[0] if line else ""
        if verb in _WALK_VERBS:
            return WALK_GAP
        return KEY_GAP


class LineHistory:
    """Up/Down on the > bar recalls sent lines."""

    def __init__(self, cap: int = 64) -> None:
        self._lines: list[str] = []
        self._i = 0
        self._draft = ""
        self._cap = cap

    def remember(self, line: str) -> None:
        text = line.strip()
        if not text:
            self.reset_cursor()
            return
        if not self._lines or self._lines[-1] != text:
            self._lines.append(text)
            if len(self._lines) > self._cap:
                self._lines.pop(0)
        self.reset_cursor()

    def reset_cursor(self) -> None:
        self._i = len(self._lines)
        self._draft = ""

    def up(self, typed: str) -> str:
        if self._i == len(self._lines):
            self._draft = typed
        if not self._lines:
            return typed
        self._i = max(0, self._i - 1)
        return self._lines[self._i]

    def down(self, typed: str) -> str:
        if self._i >= len(self._lines):
            return typed
        self._i += 1
        if self._i >= len(self._lines):
            return self._draft
        return self._lines[self._i]


class RealmGate:
    """Hold health/exp/look/hunt until the first realm prompt has sat still.

    Autopilot's E is not the start — the character sheet can last minutes.
    TRAIN / creation resets the wait so SAVE does not dump a queued walk.
    """

    def __init__(self) -> None:
        self._ready_at = 0.0
        self._seen = False

    def note(self, *, in_realm: bool, frozen: bool, now: float) -> None:
        if frozen:
            self._seen = False
            self._ready_at = 0.0
            return
        if in_realm and not self._seen:
            self._seen = True
            self._ready_at = now + REALM_SETTLE

    def quiet(self, now: float) -> bool:
        return self._ready_at > 0.0 and now < self._ready_at

    def remain(self, now: float) -> float:
        if not self.quiet(now):
            return 0.0
        return self._ready_at - now


# Status / combat / walk ticks — `i` after these floods the prompt.
_PRY_SKIP = frozenset(
    {
        "inv",
        "inventory",
        "i",
        "sell",
        "buy",
        "wear",
        "look",
        "l",
        "list",
        "prices",
        "appraise",
        "health",
        "exp",
        "who",
        "party",
        "n",
        "s",
        "e",
        "w",
        "u",
        "d",
        "ne",
        "nw",
        "se",
        "sw",
        "attack",
        "att",
        "bash",
        "aa",
        "at",
        "k",
        "kill",
        "bs",
        "break",
        "rest",
        "sn",
        "sneak",
        "follow",
        "fo",
        "join",
        "backrank",
        "backr",
        "invite",
        "quit",
        "x",
        "exit",
    }
)
_PRY_DIRS = frozenset({"n", "s", "e", "w", "u", "d", "ne", "nw", "se", "sw"})


class ActionPry:
    """After an action returns to [HP=], send `i` once at KEY_GAP.

    Skips peeks, combat, walks, and shop `list` so hunt does not
    double-fire or wipe a catalog. Shop "be more specific" stops
    further auto-buys until they type the name.
    """

    def __init__(self) -> None:
        self._wait_seq = 0
        self._last = ""
        self.stuck = False
        self._prying = False

    def note_send(self, text: str, prompt_seq: int, *, gearing: bool = False) -> None:
        low = text.strip().lower()
        verb = low.split()[0] if low else ""
        if not verb:
            return
        if self.stuck:
            if verb in {"buy", "sell"} or verb in _PRY_DIRS:
                self.stuck = False
            elif verb in _PRY_SKIP:
                return
            else:
                self.stuck = False
        # Walks get a `look`, not an inventory pry — even while gearing.
        if verb in _PRY_DIRS or verb in {"go", "borrow", "search"}:
            return
        if verb in _PRY_SKIP:
            return
        self._last = low
        self._wait_seq = prompt_seq
        self._prying = False

    def note_shop_vague(self) -> None:
        if self._last.startswith(("buy", "sell", "read")) or not self._last:
            self.stuck = True
            self._prying = False
            self._last = ""

    def note_flood(self) -> None:
        self._last = ""
        self._prying = False

    def blocks(self, text: str) -> bool:
        low = text.strip().lower()
        return self.stuck and low.startswith("buy")

    def maybe_send(
        self,
        state: WorldState,
        send,
        *,
        frozen: bool = False,
        settling: bool = False,
        pending: bool = False,
    ) -> bool:
        if frozen or settling or pending or self.stuck or self._prying:
            return False
        if not state.in_realm or state.in_combat:
            return False
        if not self._last or state.prompt_seq <= self._wait_seq:
            return False
        self._last = ""
        self._prying = True
        send("i")
        return True


def drop_stray_keys(
    pilot: Autopilot | None, *, on_form: bool, in_realm: bool
) -> bool:
    """True when login leftovers must not reach the BBS.

    A click or key during "signing in..." / "entering the realm..." used to
    sit behind Autopilot's E and arrive as `n` (no exit that way).
    After E, race / class / the sheet need every key — a new toon has no [HP=].
    """
    if pilot is None or on_form or in_realm:
        return False
    return bool(pilot.play) and pilot.phase in {
        "user",
        "pass",
        "bbs",
        "mud",
        "signup_new",
        "signup_user",
        "signup_pass",
        "signup_confirm",
        "signup_email",
    }


class LogoffWalk:
    """F12: one X per screen. Save the toon, skip the BBS Y/N modem bit."""

    FAREWELL = "Logged off. Finn's Realm is still here."

    def __init__(self) -> None:
        self.active = False
        self.done = False
        self.hint = "logging off..."
        self._sent = ""
        self._began = 0.0

    def start(self, brain: Brain, now: float) -> None:
        self.active = True
        self.done = False
        self._sent = ""
        self._began = now
        self.hint = "logging off..."
        if brain.hunting():
            brain.toggle_hunt()
        brain.mode = "manual"

    def tick(
        self, text: str, *, in_realm: bool, pacer: KeyPacer, now: float
    ) -> None:
        if not self.active or self.done:
            return
        if now - self._began > 10:
            self.done = True
            self.hint = self.FAREWELL
            return
        if pacer.pending():
            return
        low = text.lower()
        if "make your selection" in low:
            self.done = True
            self.hint = self.FAREWELL
            return
        if "[majormud]" in low or "enter the realm" in low:
            if self._sent != "mud":
                pacer.push_text("x", wipe=False)
                self._sent = "mud"
                self.hint = "leaving MajorMUD..."
            return
        if (in_realm or "[hp=" in low) and self._sent in ("", "guess"):
            pacer.push_text("x", wipe=False)
            self._sent = "realm"
            self.hint = "leaving the realm..."
            return
        if in_realm or "[hp=" in low:
            return
        if self._sent == "":
            pacer.push_text("x", wipe=False)
            self._sent = "guess"
            self.hint = "logging off..."
            return


class Autopilot:
    """BBS login and enter the realm. Character creation is yours."""

    def __init__(self, player: dict[str, object], play: bool) -> None:
        self.username = str(player.get("username", "klymacks"))
        self.password = str(player.get("password", "klymacks1"))
        self.play = play
        self.phase = "user"
        self.blocked_why = ""
        self._until = 0.0
        self._board_m = False

    def hint(self) -> str:
        if self.phase == "blocked":
            return self.blocked_why or (
                f"{self.username} already logged in — close that window"
            )
        return {
            "user": "signing in...",
            "pass": "signing in...",
            "bbs": "opening the board...",
            "mud": "entering the realm...",
            "signup_new": "no BBS login yet — creating one...",
            "signup_user": "no BBS login yet — creating one...",
            "signup_pass": "no BBS login yet — creating one...",
            "signup_confirm": "no BBS login yet — creating one...",
            "signup_email": "no BBS login yet — creating one...",
            "signup_gender": "type M or F for the BBS account, then Enter",
            "play": "your keyboard",
        }.get(self.phase, "connected")

    def takeover(self) -> None:
        if self.phase not in ("user", "pass"):
            self.phase = "play"

    def _pause(self, seconds: float) -> None:
        self._until = time.monotonic() + seconds

    def tick(self, text: str, pacer: KeyPacer) -> None:
        low = text.lower()
        if "already logged in" in low or "only 1 connection" in low:
            self.phase = "blocked"
            self.blocked_why = (
                f"{self.username} already logged in — close that window"
            )
            pacer.clear()
            return
        if "invalid credentials" in low:
            # NEW on the username prompt creates the BBS login. No sqlite edits.
            self.phase = "signup_new"
            self.blocked_why = ""
            pacer.clear()
            return
        if pacer.queued() or time.monotonic() < self._until:
            return
        if self.phase == "play" or self.phase == "blocked":
            return
        if self.phase == "user" and (
            "Username:" in text or "user-id:" in low or "user id:" in low
        ):
            pacer.push_text(self.username, wipe=False)
            self.phase = "pass"
            return
        if self.phase == "pass" and "password:" in low:
            pacer.push_text(self.password, wipe=False)
            self.phase = "bbs"
            return
        if self.phase == "signup_new" and "Username:" in text:
            pacer.push_text("NEW", wipe=False)
            self.phase = "signup_user"
            return
        if self.phase == "signup_user" and (
            "unique Username" in text or "unique username" in low
        ):
            pacer.push_text(self.username, wipe=False)
            self.phase = "signup_pass"
            return
        if self.phase == "signup_user" and "unavailable" in low:
            self.phase = "play"
            return
        if self.phase == "signup_pass" and "Password:" in text:
            pacer.push_text(self.password, wipe=False)
            self.phase = "signup_confirm"
            return
        if self.phase == "signup_confirm" and "confirm" in low:
            pacer.push_text(self.password, wipe=False)
            self.phase = "signup_email"
            return
        if self.phase == "signup_email" and "e-mail" in low:
            pacer.push_text(f"{self.username}@finns.realm", wipe=False)
            self.phase = "signup_gender"
            return
        if self.phase == "signup_gender" and "Make your selection" in text:
            pacer.push_text("M", wipe=False)
            self.phase = "mud" if self.play else "play"
            return
        if self.phase == "signup_gender":
            return
        at_mud = "[majormud]" in low or "enter the realm" in low
        at_bbs = "make your selection" in low
        # [MAJORMUD]: wants E. M is the BBS module key — Invalid Option here.
        if at_mud and self.play and self.phase in {"bbs", "mud"}:
            pacer.push_text("E", wipe=False)
            self.phase = "play"
            self._pause(KEY_GAP)
            return
        if self.phase == "bbs" and at_bbs:
            pacer.push_text("M", wipe=False)
            self._board_m = True
            self.phase = "mud" if self.play else "play"
            return
        if not self.play:
            return
        if self.phase == "mud":
            return


def load_player(path: Path) -> dict[str, object]:
    data: dict[str, object] = {
        "username": "klymacks",
        "password": "klymacks1",
        "auto_login": True,
        "auto_play": True,
        "pvp": False,
    }
    if path.is_file():
        loaded = json.loads(path.read_text())
        if isinstance(loaded, dict):
            data.update(loaded)
    return data


def board_aka(player: dict[str, object]) -> str:
    """Every local login and given name — sysop must not lock out Matt."""
    parts = [
        str(player.get("username") or ""),
        str(player.get("given") or ""),
        str(player.get("character") or ""),
    ]
    for path in (
        ROOT / "config" / "player.json",
        ROOT / "config" / "sysop.json",
        ROOT / "config" / "matt.json",
    ):
        if not path.is_file():
            continue
        try:
            loaded = json.loads(path.read_text())
        except (OSError, json.JSONDecodeError):
            continue
        if isinstance(loaded, dict):
            for key in ("username", "given", "character"):
                parts.append(str(loaded.get(key) or ""))
    return " ".join(parts)


def paint_splash(
    screen: AnsiScreen, host: str, port: int, *, kind: str = "client"
) -> None:
    screen.feed(b"\x1b[2J")
    paint_piece(screen, host, port, kind=kind)
    screen.feed(b"\x1b[0m")
    screen.generation += 1


def show_signoff(screen: AnsiScreen, stdin: int, pending: bytearray) -> None:
    """F12 closer: klymacks tag, then one key and we leave."""
    screen.feed(b"\x1b[2J")
    paint_signoff(screen)
    screen.feed(b"\x1b[0m")
    sys.stdout.buffer.write(b"\x1b[?7l\x1b[2J\x1b[H")
    sys.stdout.buffer.write(screen.render())
    sys.stdout.buffer.flush()
    while True:
        readable, _, _ = select.select([stdin], [], [])
        if stdin not in readable:
            continue
        key = read_key(stdin, pending)
        if key is None:
            continue
        if key == b"":
            continue
        return


def render_login_ans(screen: AnsiScreen, rows: int = 20) -> bytes:
    """CP437 .ANS for MBBSEmu ANSI.Login — board already homes and clears."""
    out = bytearray()
    prev: tuple[int, int, bool, bool] | None = None
    for y in range(min(rows, screen.rows)):
        out += f"\x1b[{y + 1};1H".encode()
        for cell in screen.buf[y]:
            key = cell.style_key()
            if key != prev:
                out += _sgr_bytes(cell)
                prev = key
            out += cell.ch.encode("cp437", "replace")
        out += b"\x1b[K"
    out += b"\x1b[0m\x1b[21;1H"
    return bytes(out)


def write_login_ans(path: Path, *, port: int = 2323) -> Path:
    screen = AnsiScreen()
    paint_splash(screen, "127.0.0.1", port, kind="board")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(render_login_ans(screen))
    return path


def status_line(screen: AnsiScreen, host: str) -> str:
    text = screen.text()
    if screen.looks_like_creation():
        low = text.lower()
        if "you may not use" in low or "please enter a new name" in low:
            return "name taken"
        if "validating your name" in low:
            return "checking name"
        return "character sheet"
    if "[HP=" in text or "hits:" in text.lower():
        return ""
    if "[MAJORMUD]" in text or "Enter the Realm" in text:
        return "MajorMUD menu  ·  press E"
    if "Make your selection" in text:
        return "board menu"
    if (
        "create a new Account" in text
        or "unique Username" in text
        or "e-Mail Address" in text
        or "gender 'M' or 'F'" in text
    ):
        return "BBS signup"
    if "Username:" in text or "Password:" in text:
        return "signing in"
    if is_loopback_host(host):
        return "Finn's Realm"
    return "blocked"


def creation_tip(screen: AnsiScreen) -> str:
    """Create/reroll is yours. After a wipe, say why the name bounced."""
    low = screen.text().lower()
    if "you may not use" in low or "please enter a new name" in low:
        return "That given name is taken. Type another, then Enter."
    if "validating your name" in low:
        return "Scanning monsters, NPCs, and players for that name. Wait — do not type."
    if "choose a race" in low or "select a race" in low:
        return "You pick race. Client will not."
    if "choose a class" in low or "select a class" in low:
        return "You pick class. Client will not."
    return "Type on the form. F11 live after SAVE. Last name required."


def form_frozen(screen: AnsiScreen, sheet_lock: bool = False) -> bool:
    """TRAIN STATS / creation, or F11 lock. Status peeks must not type here."""
    return bool(sheet_lock or screen.looks_like_creation())


_FORM_STATUS_PREFIXES = (
    b"[HP=",
    b"[hp=",
    b"[Hp=",
    b"/MA=",
    b"Health:",
    b"Hits:",
    b"Mana:",
    b"Exp:",
    b"Obvious exits:",
    b"Also here:",
    b"You notice ",
    b"You are carrying ",
    b"You have no keys",
    b"*Combat",
    b"* Combat",
)
_FORM_PROMPT_HEADS = (b"[HP=", b"[hp=", b"[Hp=", b"/MA=")


class FormHoldFilter:
    """Drop delayed [HP=/MA=] prompts while TRAIN STATS / creation is up.

    The game reprints the prompt on a timer (and after a CP change). That
    often arrives as CSI-to-cursor then `[HP=…/MA=…]:`, sometimes split
    across packets so only `/MA=15]:` lands on Intellect.
    """

    def __init__(self) -> None:
        self._skip_prompt = False
        self._pending = b""

    def reset(self) -> None:
        self._skip_prompt = False
        self._pending = b""

    def filter(self, payload: bytes) -> bytes:
        data = self._pending + payload
        self._pending = b""
        out = bytearray()
        i = 0
        n = len(data)
        line_start = True
        while i < n:
            if self._skip_prompt:
                if data[i] == 0x1B:
                    self._skip_prompt = False
                    continue
                if data[i] in (10, 13):
                    self._skip_prompt = False
                    i += 1
                    continue
                i += 1
                continue
            if data[i] == 0x1B:
                j = i + 1
                if j >= n:
                    self._pending = data[i:]
                    break
                if data[j] == 0x5B:
                    j += 1
                    while j < n and not (0x40 <= data[j] <= 0x7E):
                        j += 1
                    if j >= n:
                        self._pending = data[i:]
                        break
                    j += 1
                    if data.startswith(_FORM_PROMPT_HEADS, j):
                        i = j
                        self._skip_prompt = True
                        line_start = False
                        continue
                    out += data[i:j]
                    i = j
                    line_start = False
                    continue
                out.append(data[i])
                i += 1
                line_start = False
                continue
            if data.startswith(_FORM_PROMPT_HEADS, i):
                self._skip_prompt = True
                continue
            if data[i] in (10, 13):
                line_start = True
                out.append(data[i])
                i += 1
                continue
            if line_start:
                rest = data[i:]
                if any(rest.startswith(pref) for pref in _FORM_STATUS_PREFIXES):
                    self._skip_prompt = True
                    continue
            out.append(data[i])
            line_start = False
            i += 1
        return bytes(out)


_FORM_LEAVE_MARKERS = (
    b"Obvious exits:",
    b"Also here:",
    b"You notice ",
    b"*Combat",
    b"* Combat",
)


def form_returns_to_realm(payload: bytes) -> bool:
    """SAVE/EXIT reprinted the room. Do not keep filtering that packet as the sheet."""
    return any(mark in payload for mark in _FORM_LEAVE_MARKERS)


def paint_mud(
    screen: AnsiScreen,
    payload: bytes,
    *,
    hold: bool,
    filt: FormHoldFilter,
) -> bytes:
    """Feed the grid. After TRAIN STATS, a room reprint must not stay star-masked."""
    if hold and form_returns_to_realm(payload):
        filt.reset()
        screen.leave_form()
        screen.feed(payload)
        return payload
    shown = filt.filter(payload) if hold else payload
    if not hold:
        filt.reset()
    screen.feed(shown)
    return shown


def form_hold_payload(payload: bytes) -> bytes:
    """Keep FSD CSI and field text. Drop [HP=] / look / i that would write on the sheet."""
    return FormHoldFilter().filter(payload)


def form_blocks_line(outgoing: bytes, *, frozen: bool) -> bool:
    """Queued look/health/i must not send while F11 holds the form. Lone CR is a field."""
    return bool(frozen and outgoing.endswith(b"\r") and outgoing != b"\r")


def play_paused(brain: Brain, *, frozen: bool = False) -> bool:
    """Hunt/party/exp/pry must not type. Train hold still leaves the keyboard live."""
    return bool(frozen or brain.train_holding())


def use_local_input(
    screen: AnsiScreen, state: WorldState, sheet_lock: bool = False
) -> bool:
    """Type on the > bar in the realm. Sheet / TRAIN STATS keeps FSD keys."""
    return state.in_realm and not form_frozen(screen, sheet_lock)


def realm_bar_text(typed: str) -> str:
    """Letters on the > bar. Never password-mask."""
    return typed[:76]


def handle_special_key(
    key: bytes,
    brain: Brain,
    pacer: KeyPacer,
    *,
    in_realm: bool,
    state: WorldState | None = None,
) -> str | None:
    """F1 panic, F2–F6 peek, F7 hunt, F8 ambush/aa, F9 join, F10 copy, F11 train, F12 logoff.

    Peek / copy never call takeover(). F10 freezes the painted grid and
    copies it; hunt keeps ticking. That is copy hold, not train hold.
    F11 in the realm walks to the trainer or pauses the brain so the
    player types stats. F11 does not freeze the screen. F8 walk→ambush
    paces `look` immediately if Also here is stale; a listed lop is a
    fight. Empty room then one `sn`. Paladin F8 flips client `aa`
    (1.11p has no aa command).
    """
    if key == KEY_F1:
        if in_realm and state is not None:
            brain.panic(state, pacer.push_text)
        return "panic"
    if key == KEY_F7:
        if in_realm:
            brain.toggle_hunt()
            if brain.mode == "gear" and state is not None:
                line = brain.open_gear_inv(state)
                if line:
                    pacer.push_text(line)
        return "hunt"
    if key == KEY_F8:
        if not brain._ninja():
            line = brain.toggle_aa(state)
            if line:
                pacer.push_text(line)
            return "aa"
        was_on = brain.stealth_label() == "ambush"
        brain.toggle_stealth()
        now_on = brain.stealth_label() == "ambush"
        if not was_on and now_on and in_realm and state is not None:
            for cmd in brain.on_ambush_on(state):
                pacer.push_text(cmd)
        return "ambush"
    if key == KEY_F9:
        brain.toggle_auto_join()
        return "join"
    if key == KEY_F10:
        return "hold"
    if key == KEY_F11:
        return "sheet"
    if key == KEY_F12:
        if brain.hunting():
            brain.toggle_hunt()
        brain.mode = "manual"
        return "logoff"
    cmd = PEEK_COMMANDS.get(key)
    if cmd is None:
        return None
    if in_realm:
        pacer.push_text(cmd)
    return "peek"


def toggle_sheet(
    brain: Brain,
    state: WorldState,
    *,
    locked: bool,
    on_form: bool,
) -> tuple[str, str | None]:
    """F11. Creation sheet lock, or in-realm train hold. Never sends train."""
    if locked or on_form:
        brain.cancel_train()
        return "unlock", None
    if not state.in_realm:
        return "lock", None
    if brain.train_holding() or brain._want_train:
        brain.cancel_train()
        return "idle", None
    brain.request_train(state)
    if brain.train_holding():
        return "pause", None
    return "walk", None


def write_hold_snapshot(screen: AnsiScreen, path: Path | None = None) -> Path | None:
    """Write the frozen 80×25 glyphs (CP437→UTF-8, same as display) for a grab."""
    dest = path or HOLD_SNAPSHOT
    try:
        dest.parent.mkdir(parents=True, exist_ok=True)
        dest.write_text(screen.text() + "\n", encoding="utf-8")
    except OSError:
        return None
    return dest


def copy_hold_clipboard(text: str) -> bool:
    """Put the held screen on the desktop clipboard (Ctrl+V)."""
    global _CLIP_ROOT
    try:
        if _CLIP_ROOT is None:
            root = tkinter.Tk()
            root.withdraw()
            _CLIP_ROOT = root
        _CLIP_ROOT.clipboard_clear()
        _CLIP_ROOT.clipboard_append(text)
        _CLIP_ROOT.update_idletasks()
        _CLIP_ROOT.update()
    except tkinter.TclError:
        return False
    return True


def pump_clipboard() -> None:
    """Serve X11 paste requests while F10 copy owns the clipboard."""
    if _CLIP_ROOT is None:
        return
    try:
        _CLIP_ROOT.update_idletasks()
        _CLIP_ROOT.update()
    except tkinter.TclError:
        return


def realm_fkey_tip(
    *, hunting: bool, ambush: str = "", join: str = "", held: bool = False
) -> str:
    """Current state / current action — not 'press to…'."""
    bits = [fkey_label(n) for n in range(1, 7)]
    bits.append(fkey_label(7, active=hunting))
    if ambush:
        bits.append(fkey_label(8, word=ambush))
    if join:
        bits.append(fkey_label(9, word=join))
    bits.append(fkey_label(10, active=not held))

    def render(rows: list[str]) -> str:
        return " ".join(rows)

    if len(render(bits)) > 80:
        bits[2] = fkey_label(3, short=True)
    # Never drop F10 — it lives only on this row now.
    if len(render(bits)) > 80:
        bits = [bit for bit in bits if not bit.startswith("F5 ")]
    if len(render(bits)) > 80:
        bits = [bit for bit in bits if not bit.startswith("F9 ")]
    mid = render(bits)
    if len(mid) > 80:
        f10 = bits[-1]
        mid = f"{mid[: 80 - len(f10) - 1].rstrip()} {f10}"[:80]
    if hunting:
        extra = f"{mid}  letter takes over"
        return extra if len(extra) <= 80 else mid
    for prefix in ("> bar  Enter  ", "> bar  "):
        text = f"{prefix}{mid}"
        if len(text) <= 80:
            return text
    return mid[:80]


def maybe_auto_party(
    state: WorldState,
    brain: Brain,
    send,
    *,
    invited: bool,
    followed: bool,
    they_followed: bool = False,
    frozen: bool = False,
) -> None:
    """Join / backrank from an invite even when the hunter is stopped."""
    if frozen or not state.in_realm:
        return
    if they_followed:
        brain._sync_party(state)
    if invited:
        brain.on_invite(state, send)
    if followed:
        brain.on_follow(state, send)


def maybe_ask_exp(
    state: WorldState, send, *, pending: bool = False, frozen: bool = False
) -> bool:
    """Send `exp` once per gap, like health. `needs_exp` blocks a second send."""
    if frozen or pending or not state.in_realm or state.in_combat:
        return False
    if not state.needs_exp():
        return False
    state.exp_asked = True
    send("exp")
    return True


def maybe_ask_stat(
    state: WorldState, send, *, pending: bool = False, frozen: bool = False
) -> bool:
    """Send `stat` once so the ice row can split base Attack / Defense from kit."""
    if frozen or pending or not state.in_realm or state.in_combat:
        return False
    if not state.needs_stat():
        return False
    state.stat_asked = True
    send("stat")
    return True


def handle_client_line(cmd: str, brain: Brain, state: WorldState) -> tuple[str, str | None]:
    """> bar client commands. Returns (kind, mud_line_or_none)."""
    raw = cmd.strip()
    low = raw.lower()
    if low == "hunt" or low.startswith("hunt "):
        arg = low[4:].strip()
        if arg in {"list", "?"}:
            brain.next_action = brain.list_hunt_runs()
            return "hunt", None
        if arg in {"off", "stop"}:
            if brain.hunting():
                brain.toggle_hunt()
            return "hunt", None
        if arg in {"on", "go"}:
            if brain.mode == "goto":
                brain._start_hunt()
            elif not brain.hunting():
                brain.toggle_hunt()
            if brain.mode == "gear":
                return "hunt", brain.open_gear_inv(state)
            return "hunt", None
        if arg:
            got = brain.set_hunt_run(arg)
            if got is None:
                brain.next_action = f"unknown run {arg}; {brain.list_hunt_runs()}"
                return "hunt", None
            if brain.mode == "goto":
                brain._start_hunt()
            elif not brain.hunting():
                brain.toggle_hunt()
            if brain.mode == "gear":
                return "hunt", brain.open_gear_inv(state)
            return "hunt", None
        brain.toggle_hunt()
        if brain.mode == "gear":
            return "hunt", brain.open_gear_inv(state)
        return "hunt", None
    if low == "goto" or low.startswith("goto "):
        arg = low[4:].strip()
        if not arg or arg in {"list", "?"}:
            brain.next_action = brain.list_gotos()
            return "goto", None
        if arg in {"off", "stop"}:
            if brain.mode == "goto":
                brain.takeover()
            return "goto", None
        got = brain.start_goto(arg)
        if got is None:
            if brain.next_action in {"no deathpile", "hunter stays on localhost"}:
                return "goto", None
            brain.next_action = f"unknown goto {arg}; {brain.list_gotos()}"
            return "goto", None
        return "goto", None
    if low == "boost":
        if not state.in_realm:
            brain.next_action = "boost after E — sysop tweak, not a license"
            return "boost", None
        return (
            "boost",
            "SYS TWEAK EXPERIENCE 120000\nSYS TWEAK LEVEL 10",
        )
    if low == "run" or low.startswith("run "):
        arg = low[3:].strip()
        if not arg or arg in {"list", "?"}:
            brain.next_action = brain.list_gotos()
            return "goto", None
        if arg in {"off", "stop"}:
            if brain.mode == "goto":
                brain.takeover()
            return "goto", None
        got = brain.start_run(arg)
        if got is None:
            if brain.next_action in {"no deathpile", "hunter stays on localhost"}:
                return "goto", None
            brain.next_action = f"unknown run {arg}; {brain.list_gotos()}"
            return "goto", None
        return "goto", None
    if low == "stop":
        brain.takeover()
        return "stop", None
    if low in {"aa", "aa on", "aa off"}:
        if brain._ninja():
            return "aa", None
        if low == "aa on":
            brain.aa = True
            brain.next_action = "aa"
            return "aa", None
        if low == "aa off":
            return "aa", brain.stop_aa(state)
        return "aa", brain.toggle_aa(state)
    if brain.pending_offer() and low in {"y", "yes", "n", "no"}:
        kind = "gear" if brain.gear_offer() else "spell"
        brain.answer_offer(low in {"y", "yes"}, state)
        return kind, None
    if low in {"train", "go train"}:
        if brain.train_holding():
            if low == "go train":
                brain.cancel_train()
                return "train", None
            return "game", raw
        if state.at_trainer():
            brain.request_train(state)
            return "train", None
        if brain._want_train:
            brain.cancel_train()
            return "train", None
        brain.request_train(state)
        return "train", None
    if raw:
        if not brain.train_holding():
            brain.cancel_train()
        return "game", raw
    return "enter", ""


def character_label(player: dict[str, object]) -> str:
    """Window-title name. BBS user sysop is the klymacks toon."""
    username = str(player.get("username") or "").strip()
    key = username.lower()
    if key in {"klymacks", "sysop"}:
        return "klymacks"
    if key == "matt":
        return "Matt"
    given = str(player.get("given") or "").strip()
    if given.lower() == "klymacks":
        return "klymacks"
    return username or given or "klymacks"


def window_title(player: dict[str, object]) -> str:
    return f"Finn's Realm — {character_label(player)}"


def footer_who(brain: Brain, level: int | None = None) -> str:
    """Given name + level + class on the FINN'S REALM title row."""
    tokens = [part for part in brain.me.replace(",", " ").split() if part]
    name = ""
    if tokens:
        name = character_label({"username": tokens[0], "given": tokens[-1]})
    klass = (brain.klass or "").strip().lower()
    if klass in {"pal", "p"}:
        klass = "paladin"
    if klass:
        klass = klass[:1].upper() + klass[1:]
    inside = klass
    if level is not None and klass:
        inside = f"Lv.{level} {klass}"
    elif level is not None:
        inside = f"Lv.{level}"
    if name and inside:
        return f"{name} ({inside})"
    if inside:
        return f"({inside})" if level is not None and not name else inside
    return name


def osc_set_title(title: str) -> bytes:
    """OSC 0 icon+window title. Never emit an empty name."""
    text = (title or "").strip()
    if not text:
        return b""
    return f"\x1b]0;{text}\x07".encode()


# Bright red / yellow — VGA SGR the 80×25 HUD uses for hits. Footer bg is #0b0e0c.
# Ice chrome: cyan / white / blue — same palette as the Finn's Realm splash.
HP_RED_SGR = "\x1b[1;31m"
HP_YELLOW_SGR = "\x1b[1;33m"
CHROME_BODY_SGR = "\x1b[1;37m"
ICE_CYAN_SGR = "\x1b[1;36m"
ICE_DIM_SGR = "\x1b[0;36m"
ICE_BLUE_SGR = "\x1b[0;34m"
HP_LOW_RATIO = 0.25
_SGR_RE = re.compile(r"\x1b\[[0-9;]*m")
_HP_SEGMENT_RE = re.compile(r"HP -?\d+(?:/\d+)?")
_MA_SEGMENT_RE = re.compile(r"MA -?\d+(?:/\d+)?")
_TRAIN_SEGMENT_RE = re.compile(r"TRAIN \d+%")
_EXP_SEGMENT_RE = re.compile(r"EXP \d+%(?: \d+/\d+)?")
_DPS_SEGMENT_RE = re.compile(r"DPS (?:\d+(?:[↑↓·]\d+)?|—)")


def visible_len(text: str) -> int:
    return len(_SGR_RE.sub("", text))


def pad_visible(text: str, width: int) -> str:
    extra = width - visible_len(text)
    if extra <= 0:
        return text
    return text + (" " * extra)


def hp_chrome_sgr(state: WorldState) -> str:
    """Red if hp < 0; yellow if 0 ≤ hp and hp/max < 25%. Else chrome fg."""
    if state.hp is None:
        return ""
    if state.hp < 0:
        return HP_RED_SGR
    ratio = state.hp_ratio()
    if ratio is not None and ratio < HP_LOW_RATIO:
        return HP_YELLOW_SGR
    return ""


def color_footer_hp(plain: str, state: WorldState) -> str:
    """Color only the HP segment. MA and the rest stay chrome fg."""
    sgr = hp_chrome_sgr(state)
    if not sgr:
        return plain
    matched = _HP_SEGMENT_RE.search(plain)
    if not matched:
        return plain
    return (
        f"{plain[: matched.start()]}{sgr}{matched.group(0)}"
        f"{CHROME_BODY_SGR}{plain[matched.end() :]}"
    )


def ice_meter(ratio: float | None, width: int) -> str:
    """ACiD dither bar: ▓ filled, ▒ leading edge, ░ empty."""
    if width <= 0:
        return ""
    if ratio is None:
        return "░" * width
    clamped = max(0.0, min(1.0, float(ratio)))
    filled = int(round(clamped * width))
    if filled <= 0:
        return "░" * width
    if filled >= width:
        return "▓" * width
    return "▓" * (filled - 1) + "▒" + "░" * (width - filled)


def ice_wedge(*, closing: bool = False) -> str:
    if closing:
        return f"{ICE_CYAN_SGR}▓{ICE_DIM_SGR}▒{ICE_BLUE_SGR}░{CHROME_BODY_SGR}"
    return f"{ICE_BLUE_SGR}░{ICE_DIM_SGR}▒{ICE_CYAN_SGR}▓{CHROME_BODY_SGR}"


def ice_wash(width: int) -> str:
    """Repeating ice fade used to fill the DPS / vitals gap."""
    if width <= 0:
        return ""
    motif = (
        (ICE_BLUE_SGR, "░"),
        (ICE_DIM_SGR, "▒"),
        (ICE_CYAN_SGR, "▓"),
        (ICE_DIM_SGR, "▒"),
        (ICE_BLUE_SGR, "░"),
    )
    parts: list[str] = []
    last = ""
    for i in range(width):
        sgr, ch = motif[i % 5]
        if sgr != last:
            parts.append(sgr)
            last = sgr
        parts.append(ch)
    parts.append(CHROME_BODY_SGR)
    return "".join(parts)


def dps_chrome_sgr(state: WorldState, now: float | None = None) -> str:
    trend = state.dps_trend(now)
    if state.dps(now) is None and state.dps_long(now) is None:
        return ICE_DIM_SGR
    if trend == "ahead":
        return ICE_CYAN_SGR
    if trend == "behind":
        return HP_YELLOW_SGR
    return ICE_BLUE_SGR


def _join_ice(parts: list[str]) -> str:
    if not parts:
        return ""
    out = parts[0]
    for i, part in enumerate(parts[1:], start=1):
        out = f"{out} {ice_wedge(closing=bool(i % 2))} {part}"
    return out


def _exp_cluster(state: WorldState, *, compact: bool, width: int) -> str:
    label = state.exp_label()
    if not label:
        return ""
    if compact and not state.can_train() and state.exp_pct is not None:
        mark = "?" if state.exp_stale else ""
        label = f"EXP {state.exp_pct}%{mark}"
    sgr = HP_YELLOW_SGR if state.can_train() else ICE_BLUE_SGR
    if state.can_train():
        ratio = 1.0
    elif state.exp_pct is not None:
        ratio = state.exp_pct / 100.0
    else:
        ratio = None
    return f"{sgr}{label} {ice_meter(ratio, width)}{CHROME_BODY_SGR}"


def _combat_cluster(
    state: WorldState, *, compact: bool, klass: str = ""
) -> str:
    label = state.combat_label(klass, compact=compact)
    if not label:
        return ""
    return f"{ICE_CYAN_SGR}{label}{CHROME_BODY_SGR}"


def _compose_stats(
    state: WorldState,
    now: float | None,
    *,
    compact: bool,
    exp_w: int,
    hp_w: int,
    ma_w: int,
) -> tuple[str, str]:
    dps = f"{dps_chrome_sgr(state, now)}{state.dps_label(now)}{CHROME_BODY_SGR}"
    clusters: list[str] = []
    exp = _exp_cluster(state, compact=compact, width=exp_w)
    if exp:
        clusters.append(exp)
    if state.hp is not None:
        tone = hp_chrome_sgr(state) or ICE_CYAN_SGR
        clusters.append(
            f"{tone}HP {ice_meter(state.hp_meter_ratio(), hp_w)}{CHROME_BODY_SGR}"
        )
    if state.ma is not None:
        clusters.append(
            f"{ICE_CYAN_SGR}MA {ice_meter(state.ma_ratio(), ma_w)}{CHROME_BODY_SGR}"
        )
    return dps, _join_ice(clusters)


def paint_stats_row(
    state: WorldState,
    now: float | None = None,
    width: int = 80,
    klass: str = "",
) -> str:
    """DPS left; ice wash; EXP percent + gauges for EXP/HP/MA.

    Attack / Defense sit on the status line (under who / level / class).
    """
    del klass  # kept for call-site compatibility
    layouts = (
        (False, 8, 8, 6),
        (True, 8, 8, 6),
        (True, 6, 6, 4),
        (True, 4, 4, 3),
    )
    painted = ""
    for compact, exp_w, hp_w, ma_w in layouts:
        left, right = _compose_stats(
            state,
            now,
            compact=compact,
            exp_w=exp_w,
            hp_w=hp_w,
            ma_w=ma_w,
        )
        gap = width - visible_len(left) - visible_len(right)
        if gap < 0:
            continue
        if gap >= 3:
            mid = f" {ice_wash(gap - 2)} "
        elif gap:
            mid = " " * gap
        else:
            mid = ""
        painted = f"{left}{mid}{right}"
        break
    if not painted:
        left, right = _compose_stats(
            state, now, compact=True, exp_w=4, hp_w=4, ma_w=3
        )
        painted = f"{left} {right}"
    return pad_visible(painted, width)


def format_stats_row(
    state: WorldState,
    now: float | None = None,
    width: int = 80,
    klass: str = "",
) -> str:
    """Plain-text stats row for tests; chrome uses paint_stats_row."""
    return _SGR_RE.sub("", paint_stats_row(state, now, width, klass=klass))


def color_footer_train(plain: str, state: WorldState) -> str:
    """Yellow TRAIN when there is enough exp to walk to the guild."""
    if not state.can_train():
        return plain
    matched = _TRAIN_SEGMENT_RE.search(plain)
    if not matched:
        return plain
    return (
        f"{plain[: matched.start()]}{HP_YELLOW_SGR}{matched.group(0)}"
        f"{CHROME_BODY_SGR}{plain[matched.end() :]}"
    )


def _paint_segment(plain: str, matched: re.Match[str] | None, sgr: str) -> str:
    if not matched or not sgr:
        return plain
    return (
        f"{plain[: matched.start()]}{sgr}{matched.group(0)}"
        f"{CHROME_BODY_SGR}{plain[matched.end() :]}"
    )


def color_stats_line(plain: str, state: WorldState) -> str:
    """Ice-paint HP / MA / EXP / DPS. HP keeps the hit-tone red/yellow."""
    painted = color_footer_hp(plain, state)
    painted = color_footer_train(painted, state)
    painted = _paint_segment(painted, _MA_SEGMENT_RE.search(painted), ICE_CYAN_SGR)
    if not state.can_train():
        painted = _paint_segment(painted, _EXP_SEGMENT_RE.search(painted), ICE_BLUE_SGR)
    return _paint_segment(painted, _DPS_SEGMENT_RE.search(painted), dps_chrome_sgr(state))


def room_chrome(room: str) -> str:
    text = (room or "the realm").replace(",", "")
    return text[:28] if text else "the realm"


def color_status_line(
    state: WorldState, brain: Brain, *, klass: str = ""
) -> str:
    """Room / mode / next on the left; AT/DF under who on the far right."""
    room = room_chrome(state.room)
    tag = brain.f8_label()
    shown = brain.next_action
    if tag and tag not in shown:
        shown = f"{shown}  {tag}"
    left = (
        f"{ICE_CYAN_SGR}{room}{ICE_DIM_SGR} ░ "
        f"{ICE_CYAN_SGR}{brain.mode}{ICE_DIM_SGR} ▒ "
        f"{ICE_DIM_SGR}next: {shown}"
    )
    combat = _combat_cluster(
        state, compact=False, klass=klass or brain.klass or state.klass or ""
    )
    if not combat:
        combat = _combat_cluster(
            state, compact=True, klass=klass or brain.klass or state.klass or ""
        )
    if not combat:
        return left
    gap = 80 - visible_len(left) - visible_len(combat)
    if gap < 1:
        combat = _combat_cluster(
            state, compact=True, klass=klass or brain.klass or state.klass or ""
        )
        gap = 80 - visible_len(left) - visible_len(combat)
    if gap < 1:
        return left
    return f"{left}{' ' * gap}{combat}"


def ice_title_line(who: str, extra: str = "") -> str:
    """One-row sauce plate. Same cyan / white / blue as the connect splash."""
    mark_l = "░▒▓ "
    name = "FINN'S REALM"
    mark_r = " ▓▒░"
    left = mark_l + name + mark_r
    right = "  ".join(part for part in (who, extra) if part)
    gap = max(1, 80 - len(left) - len(right))
    return (
        f"{ICE_DIM_SGR}{mark_l}{ICE_CYAN_SGR}{name}{ICE_DIM_SGR}{mark_r}"
        f"{' ' * gap}{ICE_CYAN_SGR}{right}"
    )


def chrome(
    term_rows: int,
    screen: AnsiScreen,
    hint: str,
    host: str,
    state: WorldState,
    brain: Brain,
    typed: str = "",
    wm_title: str = "",
    held: bool = False,
    hold_copied: bool = False,
    sheet_lock: bool = False,
) -> bytes:
    prefix = osc_set_title(wm_title)
    if term_rows < ROWS + 3:
        return prefix
    frozen = form_frozen(screen, sheet_lock)
    tag = ""
    if frozen:
        tag = fkey_label(11, held=True)
    elif brain.train_holding():
        tag = "TRAIN HOLD"
    elif state.can_train():
        tag = fkey_label(11)
    right = status_line(screen, host)
    if right == "Finn's Realm":
        right = ""
    if tag:
        right = f"{right}  {tag}" if right else tag
    who = (
        "new character"
        if screen.looks_like_creation()
        else footer_who(brain, state.level)
    )
    if who:
        right = f"{who}  {right}" if right else who
    head = pad_visible(ice_title_line(right), 80)
    # TRAIN STATS / creation: no HP/MA/EXP strip. F10 still refreshes this
    # chrome over a frozen mud grid, so the bar stays off the sheet.
    if state.in_realm and state.hp is not None and not frozen:
        status = pad_visible(color_status_line(state, brain), 80)
        stats = pad_visible(paint_stats_row(state), 80)
    else:
        status = pad_visible(hint[:80], 80)
        stats = pad_visible(f"{ICE_DIM_SGR}{'░' * 80}", 80)
    if screen.looks_like_creation():
        foot = ""
        for y in range(screen.rows - 1, 17, -1):
            line = screen.line(y).strip()
            if line and "────" not in line and "____" not in line:
                foot = line
                break
        tip = (foot or creation_tip(screen))[:80]
        if "you may not use" in screen.text().lower() or "please enter a new name" in screen.text().lower():
            tip = creation_tip(screen)[:80]
    elif frozen:
        tip = fkey_label(11, style="sheet_tip")
    elif brain.offer_tip(state.level):
        tip = brain.offer_tip(state.level)
    elif brain.train_holding():
        tip = "train hold  brain paused  you type  F11/Esc live"
    elif brain.bail:
        tip = "friendly fire   logged off   a human has to be at the keys"
    elif state.in_realm:
        tip = realm_fkey_tip(
            hunting=brain.hunting(),
            ambush=brain.f8_label(),
            join=brain.join_label(),
            held=held,
        )
    elif "[MAJORMUD]" in screen.text():
        tip = "E enter the realm   H help   X leave MajorMUD"
    else:
        tip = "Ctrl-C hangs up"
    out = bytearray(prefix)
    out += b"\x1b[0m"
    out += f"\x1b[26;1H{head}\x1b[0m".encode()
    out += f"\x1b[27;1H{status}\x1b[0m".encode()
    out += f"\x1b[28;1H{stats}\x1b[0m".encode()
    if use_local_input(screen, state, sheet_lock):
        shown = realm_bar_text(typed)
        cmd = f"> {shown}"
        out += f"\x1b[29;1H\x1b[1;32m{cmd:<80}\x1b[0m".encode()
        out += f"\x1b[30;1H\x1b[0;36m{tip:<80}\x1b[0m".encode()
        col = min(80, 3 + len(shown))
        out += f"\x1b[29;{col}H\x1b[?25h".encode()
        return bytes(out)
    out += f"\x1b[29;1H\x1b[0;36m{tip:<80}\x1b[0m".encode()
    if term_rows >= ROWS + CHROME:
        drip = pad_visible(f"{ICE_DIM_SGR}{'░' * 80}", 80)
        out += f"\x1b[30;1H{drip}\x1b[0m".encode()
    out += f"\x1b[{screen.cy + 1};{screen.cx + 1}H\x1b[?25h".encode()
    return bytes(out)


def help_overlay() -> bytes:
    rows = (
        "letters     type on the > bar, always visible",
        "Enter       send the line (sheet: next field)",
        "Up Down     last commands on the bar (sheet: fields)",
        "Space       cycle hair, eyes, SAVE / EXIT",
        *(fkey_label(n, style="help") for n in range(1, 13)),
        "F2-F6       peek - hunter stays on",
        "hunt stop   same as F7, not sent to the game",
        "hunt list   gy, sewer, arena — pick a run, then F7 starts it",
        "goto ts     walk to Town Square and stop — also gy, store, sewer",
        "run ts      same walk, skip fights — run pile is the deathpile",
        "train       same as F11 — train hold, brain paused, you type stats",
        "boost       DEMO wall: SYS TWEAK to level 10 (class weapons; needs WCCSYSOP)",
        "Ctrl-C      hang up now",
        "Names go on the bar under the form, then Enter",
        "At [HP=] type reroll to throw the character.",
    )
    inner = max(len(row) for row in rows)
    width = inner + 6
    title_pad = max(1, width - 9)
    box = [f"┌─ keys {'─' * title_pad}┐"]
    for row in rows:
        box.append(f"│  {row.ljust(inner)}  │")
    box.append(f"└{'─' * (width - 2)}┘")
    out = bytearray(b"\x1b[0;1;36m")
    top = 5
    left = 13
    for i, line in enumerate(box):
        out += f"\x1b[{top + i};{left}H{line}".encode()
    out += b"\x1b[0m"
    return bytes(out)


def _more_stdin(stdin: int, pending: bytearray, wait: float) -> None:
    if stdin < 0:
        return
    more, _, _ = select.select([stdin], [], [], wait)
    if more:
        pending.extend(os.read(stdin, 64))


def read_key(stdin: int, pending: bytearray) -> bytes | None:
    _more_stdin(stdin, pending, 0)
    if not pending:
        return None

    first = pending[0]
    if first == 0x8F:
        if len(pending) < 2:
            _more_stdin(stdin, pending, 0.04)
        if len(pending) < 2:
            return None
        code = pending[1]
        del pending[:2]
        return _SS3_FKEYS.get(code, b"")
    if first != 0x1B:
        return bytes([pending.pop(0)])

    if len(pending) == 1:
        _more_stdin(stdin, pending, 0.04)
        if len(pending) == 1:
            pending.pop(0)
            return KEY_ESC

    if len(pending) >= 2 and pending[1] == ord("O"):
        if len(pending) < 3:
            _more_stdin(stdin, pending, 0.04)
        if len(pending) < 3:
            return None
        code = pending[2]
        del pending[:3]
        if code in (65, 66):
            return bytes((0x1B, ord("["), code))
        return _SS3_FKEYS.get(code, b"")

    if len(pending) >= 3 and pending[1] == ord("[") and pending[2] == ord("["):
        if len(pending) < 4:
            _more_stdin(stdin, pending, 0.04)
        if len(pending) < 4:
            return None
        code = pending[3]
        del pending[:4]
        return _LINUX_FKEYS.get(code, b"")

    if len(pending) >= 2 and pending[1] == ord("["):
        if len(pending) >= 3 and pending[2] == ord("O"):
            if len(pending) < 4:
                _more_stdin(stdin, pending, 0.04)
            if len(pending) < 4:
                return None
            code = pending[3]
            del pending[:4]
            return _SS3_FKEYS.get(code, b"")
        end = None
        for i in range(2, len(pending)):
            if 0x40 <= pending[i] <= 0x7E:
                end = i
                break
        if end is None:
            return None
        final = pending[end]
        seq = bytes(pending[: end + 1])
        del pending[: end + 1]
        mapped = _CSI_SS3.get(seq)
        if mapped:
            return mapped
        if seq.endswith(b"~") and seq.startswith(b"\x1b["):
            num = seq[2:-1].split(b";", 1)[0]
            if num.isdigit():
                fkey = _CSI_FKEYS.get(int(num))
                if fkey:
                    return fkey
        if len(seq) == 3 and final in (65, 66):
            return seq
        return b""

    pending.pop(0)
    return b""


def run_plain(host: str, port: int) -> int:
    """Loopback pipe: scrolling text, no ice splash, no 1.11p footer."""
    sock = socket.create_connection((host, port), timeout=8)
    sock.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
    telnet = Telnet(sock)
    stdin = sys.stdin.fileno()
    old = termios.tcgetattr(stdin)
    pending = bytearray()
    typed = ""
    sys.stdout.buffer.write(b"\x1b[2J\x1b[H")
    sys.stdout.buffer.flush()
    try:
        tty.setraw(stdin)
        sock.setblocking(False)
        while True:
            readable, _, _ = select.select([stdin, sock], [], [], 0.05)
            if stdin in readable:
                while True:
                    key = read_key(stdin, pending)
                    if key is None:
                        break
                    if key == b"":
                        continue
                    if key in (b"\x03", b"\x1d"):
                        return 0
                    if key == b"\x7f":
                        key = b"\x08"
                    if key in (b"\n", b"\r"):
                        cmd = typed.strip()
                        typed = ""
                        sys.stdout.buffer.write(b"\r\n")
                        sys.stdout.buffer.flush()
                        if cmd:
                            telnet.send(cmd.encode("ascii", "replace") + b"\r")
                        continue
                    if key == b"\x08":
                        if typed:
                            typed = typed[:-1]
                            sys.stdout.buffer.write(b"\b \b")
                            sys.stdout.buffer.flush()
                        continue
                    if len(key) == 1 and 32 <= key[0] < 127:
                        typed += chr(key[0])
                        sys.stdout.buffer.write(key)
                        sys.stdout.buffer.flush()
            if sock in readable:
                try:
                    chunk = sock.recv(4096)
                except BlockingIOError:
                    chunk = b""
                if not chunk:
                    sys.stdout.write("\r\n[disconnected]\r\n")
                    return 0
                payload = telnet.feed(chunk)
                if payload:
                    if typed:
                        sys.stdout.buffer.write(b"\r\n")
                    sys.stdout.buffer.write(payload.replace(b"\n", b"\r\n"))
                    if typed:
                        sys.stdout.buffer.write(typed.encode("ascii", "replace"))
                    sys.stdout.buffer.flush()
    except (ConnectionResetError, BrokenPipeError):
        sys.stdout.write("\r\n[disconnected]\r\n")
        return 0
    finally:
        termios.tcsetattr(stdin, termios.TCSADRAIN, old)
        sock.close()
    return 0


def run(
    host: str,
    port: int,
    player: dict[str, object],
    auto: bool,
    *,
    plain: bool = False,
) -> int:
    wm_title = window_title(player)
    cols, rows = shutil.get_terminal_size(fallback=(80, 24))
    if cols < 80 or rows < 24:
        sys.stderr.write(
            f"This terminal is {cols}x{rows}. The character sheet needs 80x25.\n"
            f"Use the window from ./scripts/play-window.sh.\n\n"
        )
        sys.stderr.flush()

    screen = AnsiScreen()
    paint_splash(screen, host, port)
    pacer = KeyPacer(
        paladin=str(player.get("class") or "").strip().lower() == "paladin"
    )
    play = bool(player.get("auto_play", True)) and auto
    clear_lock()
    pilot = Autopilot(player, play) if auto else None
    gate = RealmGate()
    pry = ActionPry()
    transcript = Transcript()
    state = WorldState()
    brain = Brain(
        allowed=is_local_play_host(host),
        pvp=bool(player.get("pvp")),
        me=" ".join(
            str(player.get(key) or "")
            for key in ("username", "given", "character")
        ),
        alts=board_aka(player),
        party_leader=str(player.get("party_leader") or ""),
        rank=str(player.get("rank") or ""),
        klass=str(player.get("class") or ""),
        spell_list=player.get("spells"),
        ambush=str(player.get("ambush") or "stand"),
        stealth=str(player.get("stealth") or ""),
        atlas=Atlas(DEFAULT_PATH),
        auto_join=bool(player.get("auto_join", True)),
        aa=player.get("aa"),
        learned_path=str(ROOT / "data" / "learned-spells.json"),
        gear_path=str(ROOT / "data" / "got-gear.json"),
        hunt=str(player.get("hunt") or ""),
    )
    typed = ""
    history = LineHistory()
    logoff_at = 0.0
    walk = LogoffWalk()
    stdin = sys.stdin.fileno()
    old = termios.tcgetattr(stdin)
    pending = bytearray()
    help_on = False
    hold_on = False
    hold_copied = False
    paint_hold_once = False
    hold_grid = b""
    sheet_lock = False
    sheet_prompt: int | None = None
    form_filter = FormHoldFilter()
    last_stamp: tuple[object, ...] = ()
    if auto:
        hint = "signing in..."
    else:
        hint = "Type your username."
    seen_rows: set[str] = set()

    def apply_payload(payload: bytes) -> None:
        streamed: set[str] = set()
        saw_invite = False
        saw_follow = False
        saw_they_follow = False

        def take(ev: dict[str, object]) -> None:
            nonlocal saw_invite, saw_follow, saw_they_follow
            streamed.add(str(ev["kind"]))
            state.apply(ev)
            kind = ev.get("kind")
            if kind in {"flood", "shop_vague"}:
                pacer.clear()
            if kind == "flood":
                pry.note_flood()
                pacer.pause(time.monotonic(), FLOOD_PAUSE)
            elif kind == "shop_vague":
                pry.note_shop_vague()
            if kind == "invited" and not ev.get("by_me"):
                saw_invite = True
            elif kind == "following":
                saw_follow = True
            elif kind == "followed":
                saw_they_follow = True

        for line in transcript.feed(payload):
            for ev in parse_events(line):
                take(ev)
        for ev in events_from_payload(payload):
            take(ev)
        blob = screen.text()
        for ev in harvest_screen(blob, seen_rows):
            if ev.get("kind") == "experience" and "experience" in streamed:
                continue
            take(ev)
        state.empty_if_look_missed(streamed, blob)
        if (
            "prompt" not in streamed
            and "[HP=" in blob
            and not screen.looks_like_creation()
        ):
            idx = blob.rfind("[HP=")
            for ev in parse_events(blob[idx : idx + 40].split("\n", 1)[0]):
                if ev.get("kind") == "prompt":
                    take(ev)
        now = time.monotonic()
        gate.note(
            in_realm=state.in_realm,
            frozen=form_frozen(screen, sheet_lock),
            now=now,
        )
        maybe_auto_party(
            state,
            brain,
            pacer.push_text,
            invited=saw_invite,
            followed=saw_follow,
            they_followed=saw_they_follow,
            frozen=play_paused(brain, frozen=form_frozen(screen, sheet_lock)),
        )

    def freeze_sheet(*, from_train: bool = False) -> None:
        nonlocal sheet_lock, sheet_prompt
        sheet_lock = True
        pacer.clear()
        if from_train and sheet_prompt is None:
            sheet_prompt = state.prompt_seq

    def thaw_sheet() -> None:
        nonlocal sheet_lock, sheet_prompt
        sheet_lock = False
        sheet_prompt = None

    def on_bar() -> bool:
        return use_local_input(screen, state, sheet_lock)

    def brain_send(text: str) -> None:
        pacer.push_text(text)

    _pace_line = pacer.push_text

    def push_game(text: str, *, wipe: bool = True) -> None:
        if wipe and pry.blocks(text):
            return
        if wipe:
            pry.note_send(
                text, state.prompt_seq, gearing=brain.mode == "gear"
            )
            brain.note_send(text, state.room)
        _pace_line(text, wipe=wipe)

    pacer.push_text = push_game  # type: ignore[method-assign]

    try:
        tty.setraw(stdin)
        sys.stdout.buffer.write(b"\x1b[?7l\x1b[8;30;80t\x1b[2J\x1b[H")
        sys.stdout.buffer.write(osc_set_title(wm_title))
        sys.stdout.buffer.write(screen.render())
        sys.stdout.buffer.flush()

        sock = socket.create_connection((host, port), timeout=8)
        sock.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
        sock.setblocking(False)
        telnet = Telnet(sock)
        try:
            while True:
                now = time.monotonic()
                frozen = form_frozen(screen, sheet_lock)
                paused = play_paused(brain, frozen=frozen)
                gate.note(in_realm=state.in_realm, frozen=frozen, now=now)
                settling = gate.quiet(now)
                timeout = 0.03 if pacer.pending() else (
                    0.4 if brain.hunting() and not paused and not settling else None
                )
                if walk.active:
                    timeout = 0.05 if timeout is None else min(timeout, 0.05)
                if settling:
                    wait = min(0.5, max(0.05, gate.remain(now)))
                    timeout = wait if timeout is None else min(timeout, wait)
                if _CLIP_ROOT is not None and timeout is None:
                    timeout = 0.25
                readable, _, _ = select.select([sock, stdin], [], [], timeout)
                if sock in readable:
                    try:
                        chunk = sock.recv(4096)
                    except BlockingIOError:
                        chunk = b""
                    except (ConnectionResetError, BrokenPipeError):
                        chunk = b""
                    if not chunk:
                        if walk.active:
                            show_signoff(screen, stdin, pending)
                            return 0
                        why = "disconnected"
                        if pilot is not None and pilot.phase == "blocked":
                            why = pilot.hint()
                        sys.stdout.write(f"\r\n[{why}]\r\n")
                        sys.stdout.flush()
                        if not state.in_realm:
                            _hold_error()
                        return 0
                    payload = telnet.feed(chunk)
                    was_sheet = screen.looks_like_creation()
                    hold = sheet_lock or was_sheet
                    paint_mud(screen, payload, hold=hold, filt=form_filter)
                    for reply in screen.replies:
                        telnet.send(reply)
                    screen.replies.clear()
                    try:
                        apply_payload(payload)
                    except Exception:
                        tb = traceback.format_exc()
                        log = ROOT / "data" / "client-crash.log"
                        try:
                            log.parent.mkdir(parents=True, exist_ok=True)
                            log.write_text(tb, encoding="utf-8")
                        except OSError:
                            pass
                        hint = "parse error — see data/client-crash.log"
                    now_sheet = screen.looks_like_creation()
                    if now_sheet:
                        freeze_sheet()
                    elif was_sheet:
                        thaw_sheet()
                        form_filter.reset()
                        screen.leave_form()
                    elif (
                        sheet_lock
                        and sheet_prompt is not None
                        and state.prompt_seq > sheet_prompt
                    ):
                        thaw_sheet()
                        form_filter.reset()
                        screen.leave_form()

                if stdin in readable:
                    while True:
                        key = read_key(stdin, pending)
                        if key is None:
                            break
                        if key == b"":
                            continue
                        if key == KEY_ESC:
                            if (
                                brain.pending_offer()
                                or brain._want_spell
                                or brain._want_gear
                            ):
                                brain.cancel_offer_run()
                                hint = "your keyboard"
                                continue
                            if brain.train_holding() or brain._want_train:
                                brain.cancel_train()
                                hint = "your keyboard"
                            continue
                        if key in (b"\x03", b"\x1d"):
                            return 0
                        if drop_stray_keys(
                            pilot,
                            on_form=screen.looks_like_creation(),
                            in_realm=state.in_realm,
                        ):
                            continue
                        if is_locked():
                            clear_lock()
                            hint = "your keyboard"
                        special = handle_special_key(
                            key,
                            brain,
                            pacer,
                            in_realm=on_bar(),
                            state=state,
                        )
                        if special in ("hunt", "panic", "ambush", "aa", "join"):
                            hint = f"{brain.mode}  ·  {brain.next_action}"
                            continue
                        if special == "peek":
                            continue
                        if special == "sheet":
                            action, mud = toggle_sheet(
                                brain,
                                state,
                                locked=sheet_lock,
                                on_form=screen.looks_like_creation(),
                            )
                            if action == "lock":
                                freeze_sheet(from_train=bool(mud))
                                if mud:
                                    pacer.push_text(mud)
                                hint = "sheet"
                            elif action == "walk":
                                if brain._with_leader(state):
                                    hint = "following — leader owns movement"
                                else:
                                    hint = f"{brain.mode}  ·  train"
                            elif action == "pause":
                                hint = "train hold  ·  you type"
                            elif action == "idle":
                                hint = "your keyboard"
                            else:
                                thaw_sheet()
                                hint = "your keyboard"
                            continue
                        if special == "logoff":
                            walk.start(brain, now)
                            hint = walk.hint
                            continue
                        if special == "hold":
                            hold_on = not hold_on
                            if hold_on:
                                write_hold_snapshot(screen)
                                hold_copied = copy_hold_clipboard(screen.text())
                                hold_grid = screen.render()
                                paint_hold_once = True
                            else:
                                hold_copied = False
                                hold_grid = b""
                                last_stamp = ()
                            continue
                        if key == b"\x7f":
                            key = b"\x08"
                        if key == b"\t":
                            key = KEY_DN
                        if key in (b"\n", b"\r"):
                            key = b"\r"
                        in_play = pilot is None or pilot.phase == "play"
                        if in_play and brain.hunting() and on_bar():
                            brain.takeover()
                            pacer.clear()
                            hint = "your keyboard"
                        if in_play and on_bar():
                            if key == KEY_UP:
                                typed = history.up(typed)
                                continue
                            if key == KEY_DN:
                                typed = history.down(typed)
                                continue
                            if key == b"\r":
                                cmd = typed.strip()
                                history.remember(cmd)
                                typed = ""
                                kind, mud = handle_client_line(cmd, brain, state)
                                if kind in ("hunt", "stop", "aa", "goto", "boost"):
                                    hint = f"{brain.mode}  ·  {brain.next_action}"
                                    if kind == "boost":
                                        hint = (
                                            brain.next_action
                                            if not mud
                                            else "boost  ·  SYS TWEAK exp/level"
                                        )
                                    if mud:
                                        for line in mud.splitlines():
                                            pacer.push_text(line)
                                    continue
                                if kind in {"spell", "gear"}:
                                    offered = brain.offer_tip(state.level)
                                    if offered:
                                        hint = offered
                                    else:
                                        hint = f"{brain.mode}  ·  {brain.next_action}"
                                    continue
                                if kind == "train":
                                    if brain.train_holding():
                                        hint = "train hold  ·  you type"
                                    elif brain._want_train:
                                        if brain._with_leader(state):
                                            hint = "following — leader owns movement"
                                        else:
                                            hint = f"{brain.mode}  ·  train"
                                    else:
                                        hint = "your keyboard"
                                    continue
                                if mud:
                                    pacer.push_text(mud)
                                else:
                                    pacer.push(b"\r")
                                continue
                            if key == b"\x08":
                                typed = typed[:-1]
                                continue
                            if len(key) == 1 and 32 <= key[0] < 127:
                                if len(typed) < 76:
                                    typed += chr(key[0])
                                continue
                            continue
                        pacer.push(key)

                if not on_bar():
                    typed = ""
                    history.reset_cursor()

                if pilot is not None and not walk.active:
                    # Every loop — not only on socket data. After password the
                    # BBS menu sits still; cooldown used to skip M forever.
                    pilot.tick(screen.text(), pacer)
                    if pilot.phase != "play":
                        hint = pilot.hint()

                in_play = pilot is None or pilot.phase == "play"
                frozen = form_frozen(screen, sheet_lock)
                paused = play_paused(brain, frozen=frozen)
                now = time.monotonic()
                gate.note(in_realm=state.in_realm, frozen=frozen, now=now)
                settling = gate.quiet(now)
                if walk.active:
                    walk.tick(
                        screen.text(),
                        in_realm=state.in_realm,
                        pacer=pacer,
                        now=now,
                    )
                    hint = walk.hint
                    if walk.done:
                        try:
                            sock.shutdown(socket.SHUT_RDWR)
                        except OSError:
                            pass
                        show_signoff(screen, stdin, pending)
                        return 0
                elif (
                    in_play
                    and not paused
                    and not brain.bail
                    and (state.invited_by or state.following)
                ):
                    maybe_auto_party(
                        state,
                        brain,
                        brain_send,
                        invited=bool(state.invited_by),
                        followed=bool(state.following),
                        frozen=paused,
                    )
                if (
                    in_play
                    and not walk.active
                    and not typed
                    and not paused
                    and not settling
                ):
                    if not pry.stuck:
                        brain.tick(state, brain_send, pacer.pending(), pacer.clear)
                        if (
                            not pacer.pending()
                            and not brain.bail
                            and maybe_ask_exp(
                                state, brain_send, pending=False, frozen=paused
                            )
                        ):
                            pass
                        elif (
                            not pacer.pending()
                            and not brain.bail
                            and maybe_ask_stat(
                                state, brain_send, pending=False, frozen=paused
                            )
                        ):
                            pass
                    if (
                        not pacer.pending()
                        and not brain.bail
                        and pry.maybe_send(
                            state,
                            brain_send,
                            frozen=paused,
                            settling=settling,
                            pending=False,
                        )
                    ):
                        pass
                    if brain.bail and not logoff_at:
                        hint = f"friendly fire — {brain.bail}"
                        pacer.clear()
                        pacer.push_text("quit")
                        logoff_at = time.monotonic() + 1.5
                    elif pry.stuck:
                        hint = "type the full item name"
                    elif state.in_realm and not brain.bail:
                        hint = f"{brain.mode}  ·  {brain.next_action}"
                    if logoff_at and time.monotonic() >= logoff_at and not pacer.pending():
                        return 0
                elif settling and state.in_realm and not brain.bail:
                    hint = "settling in..."

                outgoing = pacer.take(now)
                if outgoing is not None:
                    if form_blocks_line(
                        outgoing, frozen=form_frozen(screen, sheet_lock)
                    ):
                        continue
                    telnet.send(outgoing)

                stamp = (
                    screen.generation,
                    brain.mode,
                    brain.next_action,
                    brain.stealth,
                    brain.auto_join,
                    state.hp,
                    state.exp,
                    state.exp_pct,
                    state.exp_stale,
                    state.dps_label(),
                    state.dps_long(),
                    int(time.monotonic()) if state.in_combat or state.dps() or state.dps_long() else 0,
                    state.room,
                    brain._want_train,
                    brain.train_holding(),
                    hint,
                    help_on,
                    hold_on,
                    hold_copied,
                    typed,
                    on_bar(),
                    sheet_lock,
                    settling,
                )
                # HOLD: mud grid stays; chrome still paints (HP off on TRAIN STATS).
                # Hunt keeps ticking. Unhold shows the live grid.
                pump_clipboard()
                if hold_on and (paint_hold_once or not hold_grid):
                    hold_grid = screen.render()
                    paint_hold_once = False
                if stamp == last_stamp:
                    continue
                last_stamp = stamp
                _, term_rows = shutil.get_terminal_size(fallback=(80, rows))
                bar = chrome(
                    term_rows,
                    screen,
                    hint,
                    host,
                    state,
                    brain,
                    typed,
                    wm_title=wm_title,
                    held=hold_on,
                    hold_copied=hold_copied,
                    sheet_lock=sheet_lock,
                )
                grid = hold_grid if hold_on else screen.render()
                frame = grid + bar
                if help_on:
                    frame += help_overlay()
                sys.stdout.buffer.write(frame)
                sys.stdout.buffer.flush()
        finally:
            sock.close()
    finally:
        termios.tcsetattr(stdin, termios.TCSADRAIN, old)
    return 0


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Local telnet client for Finn's Realm (loopback / KVM LAN)"
    )
    parser.add_argument("host", nargs="?", default="127.0.0.1")
    parser.add_argument("port", nargs="?", type=int, default=2323)
    parser.add_argument(
        "--config",
        default="",
        help="player json (default config/player.json)",
    )
    parser.add_argument("--user", default="")
    parser.add_argument("--password", default="")
    parser.add_argument("--sysop", action="store_true", help="log in as sysop / sysop")
    parser.add_argument("--matt", action="store_true", help="log in as matt / matt")
    parser.add_argument("--no-auto", action="store_true", help="type everything yourself")
    parser.add_argument(
        "--plain",
        action="store_true",
        help="scrolling text, no BBS splash/footer (Finn's Mud pipe)",
    )
    args = parser.parse_args()
    if args.matt:
        player = load_player(ROOT / "config" / "matt.json")
        player["username"] = "matt"
        player["password"] = str(player.get("password") or "matt")
    elif args.sysop:
        sysop_path = ROOT / "config" / "sysop.json"
        player = load_player(sysop_path)
        player["username"] = "sysop"
        player["password"] = "sysop"
    else:
        cfg = Path(args.config) if args.config else ROOT / "config" / "player.json"
        player = load_player(cfg)
        if args.user:
            player["username"] = args.user
        if args.password:
            player["password"] = args.password
    auto = bool(player.get("auto_login", True)) and not args.no_auto
    plain = bool(args.plain) or args.port == 4000
    if plain:
        auto = False
    if not is_local_play_host(args.host):
        print(
            "This client only opens local telnet "
            "(127.0.0.1 / localhost / ::1 / 192.168.122.x). Not a public BBS.",
            file=sys.stderr,
        )
        return 2
    if not sys.stdin.isatty():
        print("Need a real terminal.", file=sys.stderr)
        return 1
    try:
        if plain:
            return run_plain(args.host, args.port)
        return run(args.host, args.port, player, auto)
    except (ConnectionRefusedError, TimeoutError, socket.timeout):
        print(
            f"Nothing is listening on {args.host}:{args.port}. "
            "For DOS Finn's Realm that is MBBSEmu on 2323; "
            "for Worldgroup that is the VM telnet port (often 23).",
            file=sys.stderr,
        )
        _hold_error()
        return 1
    except KeyboardInterrupt:
        return 0
    except (ConnectionResetError, BrokenPipeError):
        print("\n[disconnected]", file=sys.stderr)
        _hold_error()
        return 0
    except Exception:
        tb = traceback.format_exc()
        log = ROOT / "data" / "client-crash.log"
        try:
            log.parent.mkdir(parents=True, exist_ok=True)
            log.write_text(tb, encoding="utf-8")
        except OSError:
            pass
        sys.stderr.write(tb)
        sys.stderr.write("\nClient crashed. Enter to close.\n")
        sys.stderr.flush()
        _hold_error()
        return 1


def _hold_error() -> None:
    if not sys.stdin.isatty():
        return
    try:
        sys.stdin.read(1)
    except Exception:
        time.sleep(20)


if __name__ == "__main__":
    raise SystemExit(main())
