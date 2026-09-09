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
from datetime import datetime, timezone

IAC, DONT, DO, WONT, WILL = 255, 254, 253, 252, 251
SB, SE = 250, 240
ECHO, SGA, TTYPE, NAWS = 1, 3, 24, 31
TTYPE_IS, TTYPE_SEND = 0, 1
# RFC 1091: first IS is preferred for the whole session.
# Login Auto-sensing sends CSI 6n; answer the real caret (row+col). Idle
# room listings are Statline Full bare Enter / move / combat — not a WG
# timer. ANSI-BBS first keeps parchment TRAIN STATS (EDITCHA1).
TTYPE_NAMES = (b"ANSI-BBS", b"ANSI", b"xterm-256color")

COLS, ROWS = 80, 25
CHROME = 5
CHROME_ROW = ROWS + 1  # 26 — under the 80×25 sheet
SHEET_CHROME_LIFT = 3  # title 23 / ░ throbber 25; below the curl (~22)
# MajorMUD scolds under ~2s ("slow down for a few seconds"). Combat can
# Combat can stay snappier; walks and looks use WALK_GAP. Human typing
# (creation fields, WG menus) is TYPE_GAP — KEY_GAP on every letter
# feels like one key every few seconds. A flood pauses longer.
KEY_GAP = 0.55
TYPE_GAP = 0.07
WALK_GAP = 2.0
FLOOD_PAUSE = 5.0
# Hunt / gear / realm heartbeat. F7 on with timeout None never sends look/buy/att.
PLAY_TICK = 0.4
CHROME_PULSE = 0.12
# After first [HP=], sit still — creation can last minutes after Autopilot's E.
REALM_SETTLE = 8.0
SIGNOFF_HOLD = 10.0  # klymacks splash, then the play window closes
ROOT = Path(__file__).resolve().parent.parent
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from client.brain import Brain
from client import modules
from client.localnet import LOOPBACK_HOSTS, is_local_play_host, is_loopback_host
from client.parse import (
    events_from_payload,
    harvest_screen,
    keep_party_lf,
    parse_events,
)
from client.paths import attack_line, attack_name, is_unlatch_step, uses_bash_aa
from client.pvp import clear_lock, is_locked
from client.realm_map import DEFAULT_PATH, Atlas
from client import resume as resume_mod
from client.signoff import paint as paint_signoff
from client.splash import (
    SPLASH_DOCK_BOT,
    SPLASH_DOCK_RULE,
    SPLASH_DOCK_TOP,
    paint as paint_piece,
)
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
    12: {"on": "logoff — intentional (no resume)"},
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
        self._ttype_i = 0

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
        names = TTYPE_NAMES
        name = names[self._ttype_i % len(names)]
        self._ttype_i += 1
        self._send(bytes((IAC, SB, TTYPE, TTYPE_IS)) + name + bytes((IAC, SE)))

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
        self.form_snap: list[list[Cell]] | None = None
        self.form_cursor: tuple[int, int] | None = None
        self.form_released = False
        self.needs_resume = False
        self.form_saving = False
        self.sheet_notice = ""
        # When True, CSI 6n always CPR row 25 (realm prompt). Wrong CPR keeps
        # Worldgroup in FSD and the timed room tick never reprints Also here.
        self.realm_cpr = False

    def feed(self, data: bytes) -> None:
        if not data:
            return
        pending = bytes(self._esc)
        if pending in (b"\x1b", b"\x1b[") and data.startswith(b"["):
            # ESC was in the last packet; this `[0;37m` continues it.
            # Repair would insert a second ESC and print `[0;37m` as text.
            data = data[:1] + _repair_bare_csi(data[1:])
        else:
            data = _repair_bare_csi(data)
        i = 0
        while i < len(data):
            if self._esc:
                if bytes(self._esc) == b"\x1b" and data[i] == 0x1B:
                    i += 1
                    continue
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
        if (
            "Obvious exits" in self.text()
            or "Also here" in self.text()
            or "You notice" in self.text()
        ):
            self.needs_resume = False

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
        """Worldgroup auto-sense sends CSI 6n; xterm answers or the BBS stays ASCII.

        Standing play reports the real caret (row and column) — same as HEAD /
        system telnet. Forcing ``25;1R`` lied about column after ``[HP=…]: ``
        (often col 16) and idle Also here never followed. Live FSD alone uses
        the field caret after ``_sync_form_cursor``.
        """
        mode = params[0] if params else 0
        if mode == 6:
            if _live_fsd_session(self) and self.form_cursor is not None:
                x = self.form_cursor[0] + 1
                y = self.form_cursor[1] + 1
            else:
                y, x = self.cy + 1, self.cx + 1
            self.replies.append(f"\x1b[{y};{x}R".encode("ascii"))
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
            if self._splash_dock_lf():
                return i + 1
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

    def _copy_row(self, y: int) -> list[Cell]:
        copy: list[Cell] = []
        for cell in self.buf[y]:
            n = Cell()
            n.ch = cell.ch
            n.fg = cell.fg
            n.bg = cell.bg
            n.bold = cell.bold
            n.rev = cell.rev
            n.bright_bg = cell.bright_bg
            copy.append(n)
        return copy

    def _splash_dock_active(self) -> bool:
        """Graffiti connect screen — login text must not scroll FINNS off."""
        if self.looks_like_creation():
            return False
        return "FINN'S REALM" in self.line(0)

    def _splash_dock_lf(self) -> bool:
        """LF inside the login band scrolls only that band. Art stays put."""
        if not self._splash_dock_active():
            return False
        if self.cy < SPLASH_DOCK_TOP:
            if self.cy < self.rows - 1:
                self.cy += 1
            return True
        last = min(SPLASH_DOCK_BOT, self.rows) - 1
        if self.cy < last:
            self.cy += 1
            return True
        top = SPLASH_DOCK_TOP
        for y in range(top, last):
            self.buf[y] = self._copy_row(y + 1)
        self.buf[last] = [_blank() for _ in range(self.cols)]
        self.cy = last
        return True

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
        if self.form_released:
            return False
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

    def snapshot(self) -> list[list[Cell]]:
        rows: list[list[Cell]] = []
        for row in self.buf:
            copy: list[Cell] = []
            for cell in row:
                n = Cell()
                n.ch = cell.ch
                n.fg = cell.fg
                n.bg = cell.bg
                n.bold = cell.bold
                n.rev = cell.rev
                n.bright_bg = cell.bright_bg
                copy.append(n)
            rows.append(copy)
        return rows

    def restore_rows(self, snap: list[list[Cell]], rows: list[int]) -> None:
        for y in rows:
            if y < 0 or y >= self.rows or y >= len(snap):
                continue
            self.buf[y] = []
            for cell in snap[y]:
                n = Cell()
                n.ch = cell.ch
                n.fg = cell.fg
                n.bg = cell.bg
                n.bold = cell.bold
                n.rev = cell.rev
                n.bright_bg = cell.bright_bg
                self.buf[y].append(n)
        self.generation += 1

    def restore_all(self, snap: list[list[Cell]]) -> None:
        self.restore_rows(snap, list(range(min(self.rows, len(snap)))))

    def leaked_prompt_rows(self) -> list[int]:
        """Rows where a realm [HP=]/Hits prompt punched the FSD grid."""
        full = _sheet_full_name(self.text())
        found: list[int] = []
        for y in range(self.rows):
            line = self.line(y)
            low = line.lower()
            if "[hp=" in low or "[hp =" in low:
                found.append(y)
                continue
            if "/ma=" in low and "]:" in line:
                found.append(y)
                continue
            stripped = line.strip()
            if stripped.lower().startswith("hits:") and "/" in stripped:
                found.append(y)
                continue
            if _name_status_line(line, full):
                found.append(y)
                continue
            if _sheet_leak_line(line) or _exit_field_wounded(line):
                found.append(y)
                continue
            if y >= self.rows - 3 and _hp_prompt_tail(line):
                found.append(y)
        return found

    def form_scrolled(self, snap: list[list[Cell]] | None) -> bool:
        if not snap:
            return False
        return self.line(0) != "".join(c.ch for c in snap[0])

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
        self.form_snap = None
        self.form_cursor = None
        self.generation += 1

    def close_form(self) -> None:
        """FSD ended. Wipe the parchment; the realm is not an overlay."""
        leftover = bytes(self._esc)
        self.leave_form()
        # leave_form drops a split CSI; keep a dangling ESC so `[0;37m` still SGR.
        if leftover == b"\x1b" or leftover == b"\x1b[":
            self._esc = bytearray(leftover)
        self.buf = [[_blank() for _ in range(self.cols)] for _ in range(self.rows)]
        self.cx = 0
        self.cy = 0
        self.form_released = False
        self.needs_resume = True
        self.form_saving = False
        self.sheet_notice = ""
        self.realm_cpr = True

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
        return attack_name(raw[len(prefix) :].strip())

    if paladin:
        if low in {"aa on", "aa off"}:
            return raw
        if low in {"attack", "att", "bash", "aa", "kill", "k"}:
            return "aa"
        for prefix in ("attack ", "att ", "bash ", "aa ", "kill "):
            if low.startswith(prefix):
                if prefix == "bash " and is_unlatch_step(raw):
                    return raw
                aim = _aim_after(prefix)
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
    if low in {"bash", "aa"}:
        return "aa"
    if low.startswith("bash "):
        if is_unlatch_step(raw):
            return raw
        aim = _aim_after("bash ")
        return f"aa {aim}" if aim else "aa"
    if low.startswith("aa "):
        aim = _aim_after("aa ")
        return f"aa {aim}" if aim else "aa"
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
        "join",
        "backr",
        "backrank",
        "frontr",
        "frontrank",
        "midr",
        "midrank",
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
        """One line, one CR. Empty text is bare Enter (Statline Full room pulse)."""
        if text == "":
            self._q.append(b"\r")
            return
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
        "frontrank",
        "frontr",
        "midrank",
        "midr",
        "invite",
        "say",
        "quit",
        "x",
        "exit",
    }
)
_PRY_DIRS = frozenset({"n", "s", "e", "w", "u", "d", "ne", "nw", "se", "sw"})


class ActionPry:
    """After an action returns to [HP=], send `i` once at KEY_GAP.

    Skips peeks, combat, walks, and shop `list` so hunt does not
    double-fire or wipe a catalog. Shop "be more specific" holds
    the inventory pry until they type; it must not eat the next buy.
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

    def blocks(self, _text: str) -> bool:
        """Never drop a typed buy. Helgrim's scimitar vs serrated scimitar
        is answered by buying again; stuck only holds the inventory pry.
        """
        return False

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


def _logoff_at_hp(text: str) -> bool:
    return "[hp=" in (text or "").lower()


def _logoff_at_mud(text: str) -> bool:
    """[MAJORMUD] / Enter the Realm — ignore leftover [HP=] scrollback."""
    low = (text or "").lower()
    return "[majormud]" in low or "enter the realm" in low


def _logoff_at_bbs(text: str) -> bool:
    """TOP / main menu. Prefer over leftover mud/[HP=] on the same grid."""
    low = (text or "").lower()
    return "make your selection" in low or "main system menu" in low


def _logoff_at_confirm(text: str) -> bool:
    """Y/N (or R to re-logon) after TOP X — answer y, never another x."""
    return "are you sure" in (text or "").lower()


class LogoffWalk:
    """F12: realm x → mud x → TOP x → confirm y. Never drop carrier in-game."""

    FAREWELL = "Logged off. Finn's Realm is still here."

    def __init__(self) -> None:
        self.active = False
        self.done = False
        self.hint = "logging off..."
        self._sent = ""
        self._began = 0.0
        self._step_at = 0.0

    def start(self, brain: Brain, now: float) -> None:
        self.active = True
        self.done = False
        self._sent = ""
        self._began = now
        self._step_at = 0.0
        self.hint = "logging off..."
        if brain.hunting():
            brain.toggle_hunt()
        # Paladin aa keeps the server swinging after hunt is off — clear it
        # so F12 is not stuck on break forever.
        if brain.aa:
            brain.stop_aa(None)
        brain.mode = "manual"

    def _send(self, pacer: KeyPacer, now: float, cmd: str, stage: str, hint: str) -> None:
        pacer.push_text(cmd, wipe=False)
        self._sent = stage
        self._step_at = now
        self.hint = hint

    def tick(
        self,
        text: str,
        *,
        in_realm: bool,
        pacer: KeyPacer,
        now: float,
        in_combat: bool = False,
    ) -> None:
        if not self.active or self.done:
            return
        if pacer.pending():
            return
        low = (text or "").lower()
        at_confirm = _logoff_at_confirm(text)
        at_bbs = _logoff_at_bbs(text)
        at_mud = _logoff_at_mud(text)
        # After leaving the realm, board prompts beat leftover [HP=].
        # While in_realm, leftover TOP/[MAJORMUD] text must not hang up.
        on_board = (not in_realm) and (at_confirm or at_bbs or at_mud)
        in_game = (not on_board) and bool(in_realm or _logoff_at_hp(text))
        if self._sent in {"mud", "bbs", "yes"}:
            in_game = False
        if (
            not in_game
            and not at_confirm
            and any(
                mark in low
                for mark in (
                    "thanks for calling",
                    "thank you for calling",
                    "please hang up",
                    "now logged off",
                )
            )
        ):
            self.done = True
            self.hint = self.FAREWELL
            return
        # Confirm before menus — leftover HP / stale in_realm must not re-x.
        if at_confirm:
            if self._sent != "yes":
                self._send(pacer, now, "y", "yes", "confirming logoff...")
            elif now - self._step_at > 2:
                self.done = True
                self.hint = self.FAREWELL
            return
        if in_game:
            # One break to stop aa/swing, then x even if in_combat is sticky.
            if in_combat and self._sent not in {"break", "quit"}:
                self._send(pacer, now, "break", "break", "breaking combat...")
                return
            if self._sent == "break" and now - self._step_at < 0.4:
                return
            # WG/BBS: realm exit is X (quit does nothing).
            if self._sent != "quit" or now - self._step_at >= 6:
                self._send(pacer, now, "x", "quit", "leaving the realm...")
            return
        # TOP before leftover [MAJORMUD] so we do not x the module twice.
        if at_bbs:
            if self._sent not in {"bbs", "yes"}:
                self._send(pacer, now, "x", "bbs", "leaving the board...")
            elif self._sent == "bbs" and now - self._step_at > 4:
                self._send(pacer, now, "y", "yes", "confirming logoff...")
            elif self._sent == "yes" and now - self._step_at > 2:
                self.done = True
                self.hint = self.FAREWELL
            return
        if at_mud:
            if self._sent != "mud" or now - self._step_at >= 6:
                self._send(pacer, now, "x", "mud", "leaving MajorMUD...")
            return
        if self._sent == "yes" and now - self._step_at > 2:
            self.done = True
            self.hint = self.FAREWELL
            return
        if self._sent == "":
            self._send(pacer, now, "x", "quit", "logging off...")
            return


def login_pager_nonstop(text: str) -> bool:
    """Hangup-penalty more-prompt after E. Not in-game help `more`."""
    blob = " ".join((text or "").lower().split())
    if "[hp=" in blob:
        return False
    if "(n)onstop" not in blob or "(c)ontinue" not in blob:
        return False
    return (
        "disconnected while playing" in blob
        or "the gods have punished you" in blob
    )


class Autopilot:
    """BBS login and enter the realm. Character creation is yours."""

    def __init__(
        self,
        player: dict[str, object],
        play: bool,
        *,
        enter_mud: bool = True,
    ) -> None:
        self.username = str(player.get("username", "klymacks"))
        self.password = str(player.get("password", "klymacks1"))
        self.play = play
        self.enter_mud = enter_mud
        self.phase = "user"
        self.blocked_why = ""
        self._until = 0.0
        self._board_m = False
        self._sent_wg_userid = False
        self._sent_nonstop = False

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
            "play": (
                "BBS menu — S sysop, A account (no Mud)"
                if not self.enter_mud
                else "your keyboard"
            ),
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
        at_bbs = "make your selection" in low
        at_mud = "[majormud]" in low or "enter the realm" in low
        wg_uid = "wccrequireduserid" in low or "wccrequesteduserid" in low
        # [MAJORMUD]: wants E even when auto_play is off (NT klymacks).
        if at_mud and self.enter_mud and self.phase in {"bbs", "mud"}:
            pacer.push_text("E", wipe=False)
            self.phase = "play"
            self._sent_nonstop = False
            self._pause(KEY_GAP)
            return
        # After M, MajorMUD waits on this Worldgroup var. Never type it while
        # still on the BBS menu. Once M has gone, answer even if the menu
        # leftover is still on the 80x25 grid (that was the deadlock).
        if wg_uid and not self._sent_wg_userid and not at_mud:
            in_module = self.phase == "mud" or self._board_m
            if in_module or (self.phase == "play" and not at_bbs):
                pacer.push_text(self.username, wipe=False)
                self._sent_wg_userid = True
                self.phase = "play"
                self._pause(KEY_GAP)
                return
        if self.phase == "blocked":
            return
        # After E the hangup pager waits on N/Q/C. Bare N dumps the rest
        # without a CR that could walk north once [HP=] lands. Phase stays
        # play so C/Q from the keyboard are not drop_stray_keys'd.
        if (
            self.enter_mud
            and not self._sent_nonstop
            and login_pager_nonstop(text)
        ):
            pacer.push(b"N")
            self._sent_nonstop = True
            self.phase = "play"
            self._pause(KEY_GAP)
            return
        if self.phase == "play":
            return
        if self.phase == "user" and (
            "Username:" in text or "user-id:" in low or "user id:" in low
        ) and "wccrequireduserid" not in low and "wccrequesteduserid" not in low:
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
            self.phase = "mud"
            return
        if self.phase == "signup_gender":
            return
        if self.phase == "bbs" and at_bbs:
            if not self.enter_mud:
                self.phase = "play"
                return
            pacer.push_text("M", wipe=False)
            self._board_m = True
            self.phase = "mud"
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
    """Every local login and given name — sysop must not lock out Matt.

    WG roster identity comes from modules.CHARS (not per-toon behavior keys).
    DOS configs still contribute username/given when present.
    """
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
    for profile, user, given, _klass, _race in modules.CHARS:
        parts.extend([profile, user, given])
    return " ".join(parts)


def paint_splash(
    screen: AnsiScreen, host: str, port: int, *, kind: str = "client"
) -> None:
    screen.feed(b"\x1b[2J")
    paint_piece(screen, host, port, kind=kind)
    screen.feed(b"\x1b[0m")
    screen.generation += 1


def paint_login_notice(screen: AnsiScreen, message: str) -> None:
    """Hangup text in the splash dock — never a raw newline over chrome."""
    text = " ".join((message or "disconnected").split())[:80]
    row = SPLASH_DOCK_TOP + 1
    screen.feed(f"\x1b[{row};1H\x1b[J".encode("ascii"))
    screen.feed(text.encode("ascii", "replace"))
    screen.generation += 1


_BOARD_LOGOFF_MARKERS = (
    "thanks for calling",
    "thank you for calling",
    "please hang up",
    "now logged off",
)


def board_logoff_screen(text: str) -> bool:
    """WG goodbye ANSI after G/X. Not splash graffiti, not 'are you sure'."""
    low = (text or "").lower()
    if "are you sure" in low:
        return False
    # Connect splash says "1.11p" — never treat graffiti as hangup.
    if "finn's realm" in low and "connecting" in low:
        return False
    return any(mark in low for mark in _BOARD_LOGOFF_MARKERS)


def note_cleanup_text(text: str) -> bool:
    """True when payload/screen looks like nightly cleanup / board drop."""
    return resume_mod.cleanup_text(text) or board_logoff_screen(text)


def resume_file_for(player: dict[str, object]) -> Path | None:
    who = " ".join(
        str(player.get(key) or "")
        for key in ("username", "given", "character")
    )
    return resume_mod.resume_path(ROOT / "data", who)


def capture_and_save_resume(
    path: Path | None,
    *,
    who: str,
    state: WorldState,
    brain: Brain,
    reason: str,
) -> resume_mod.ResumeState | None:
    snap = resume_mod.capture(who=who, state=state, brain=brain, reason=reason)
    resume_mod.save(path, snap)
    return snap


def _realm_play_evidence(text: str) -> bool:
    """Live room chrome — leftover TOP/[MAJORMUD] lines must not win."""
    low = (text or "").lower()
    return any(
        mark in low
        for mark in (
            "obvious exits",
            "also here",
            "you notice",
            "you see:",
            "*combat",
        )
    )


def board_menu_screen(text: str) -> bool:
    """WG TOP / module menus — not the realm. Hunt/peeks/Enter→look stay off.

    Canonical TOP prompt includes Doors (D), Electronic Mail (E), MajorMUD (M).
    Menu letters and plain CR must still reach the BBS.

    A standing room often keeps scrolled `[MAJORMUD]` / Enter the Realm text
    on the 80×25 grid; that is still play, not the board.
    """
    if _realm_play_evidence(text):
        return False
    low = (text or "").lower()
    return (
        "make your selection" in low
        or "main system menu" in low
        or "[majormud]" in low
        or "enter the realm" in low
    )


def leave_realm_on_board(state: WorldState, text: str) -> bool:
    """Drop in_realm when the screen is a BBS/module menu. True if cleared."""
    if not state.in_realm or not board_menu_screen(text):
        return False
    state.in_realm = False
    return True


def session_on_board(text: str, *, in_realm: bool) -> bool:
    if in_realm:
        return True
    low = (text or "").lower()
    return board_menu_screen(text) or "[hp=" in low


def signoff_secs(remain: float) -> int:
    remain = max(0.0, float(remain))
    n = int(remain)
    if remain > n:
        n += 1
    return n


def signoff_clock_line(remain: float) -> str:
    n = signoff_secs(remain)
    if n <= 0:
        return "closing..."
    return f"any key  ·  window closes in {n}s"


def write_signoff_clock(remain: float) -> None:
    msg = signoff_clock_line(remain).center(COLS)
    # utf-8: clock line uses middle-dot · (ascii encode crashed F12 signoff)
    sys.stdout.buffer.write(
        f"\x1b[25;1H{ICE_DIM_SGR}{msg}\x1b[0m\x1b[?25l".encode("utf-8")
    )
    sys.stdout.buffer.flush()


def await_any_key(
    stdin: int,
    pending: bytearray,
    hold: float,
    *,
    on_tick=None,
) -> None:
    """Any key, or `hold` seconds — then the caller exits and xterm dies."""
    deadline = time.monotonic() + max(0.0, hold)
    last_n = None
    while True:
        remain = deadline - time.monotonic()
        if remain <= 0:
            if on_tick is not None:
                on_tick(0.0)
            return
        n = signoff_secs(remain)
        if on_tick is not None and n != last_n:
            on_tick(remain)
            last_n = n
        readable, _, _ = select.select([stdin], [], [], min(0.25, remain))
        if stdin not in readable:
            continue
        key = read_key(stdin, pending)
        if key:
            return


def show_signoff(
    screen: AnsiScreen,
    stdin: int,
    pending: bytearray,
    *,
    hold: float = SIGNOFF_HOLD,
) -> None:
    """F12 hangup: klymacks tag, any key or ~10s, then the play window closes."""
    screen.feed(b"\x1b[2J")
    paint_signoff(screen, when=datetime.now())
    screen.feed(b"\x1b[0m")
    sys.stdout.buffer.write(b"\x1b[?7l\x1b[2J\x1b[H")
    sys.stdout.buffer.write(screen.render())
    sys.stdout.buffer.flush()
    await_any_key(stdin, pending, hold, on_tick=write_signoff_clock)


def render_login_ans(screen: AnsiScreen, rows: int | None = None) -> bytes:
    """CP437 .ANS for MBBSEmu ANSI.Login — board already homes and clears.

    Include the ice rule. Home to the prompt band so Username sits under
    FINNS, not on it.
    """
    limit = SPLASH_DOCK_RULE + 1 if rows is None else rows
    out = bytearray()
    prev: tuple[int, int, bool, bool, bool] | None = None
    for y in range(min(limit, screen.rows)):
        out += f"\x1b[{y + 1};1H".encode()
        for cell in screen.buf[y]:
            key = cell.style_key()
            if key != prev:
                out += _sgr_bytes(cell)
                prev = key
            out += cell.ch.encode("cp437", "replace")
        out += b"\x1b[K"
    out += b"\x1b[0m" + f"\x1b[{SPLASH_DOCK_TOP + 1};1H".encode()
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
        phase = creation_phase(screen)
        if phase == "name_taken":
            return "name taken"
        if phase == "checking":
            return "checking name"
        if phase == "saving":
            return "saving"
        return "character sheet"
    if "[HP=" in text or "hits:" in text.lower():
        return ""
    if login_pager_nonstop(text):
        return "hangup penalty  ·  N nonstop"
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


_FORM_PICK = re.compile(
    r"\b(Race|Class)\s+([A-Za-z][A-Za-z\-]*(?:\s+[A-Za-z][A-Za-z\-]*)?)",
    re.I,
)


def _form_pick(screen: AnsiScreen, label: str) -> str:
    """Race / Class value from the FSD sheet, not the list prompt."""
    want = label.strip().lower()
    for y in range(screen.rows):
        matched = _FORM_PICK.search(screen.line(y))
        if not matched or matched.group(1).lower() != want:
            continue
        value = matched.group(2).strip()
        if value and "?" not in value:
            return value
    return ""


def _sheet_caret_row(screen: AnsiScreen) -> int:
    cy = screen.cy
    if screen.form_cursor is None:
        return cy
    fy = screen.form_cursor[1]
    if fy == cy:
        return cy
    # Live CUP on a field wins over a stale form_cursor (often Given Name).
    if 0 <= cy < screen.rows and _phase_from_line(screen.line(cy)):
        return cy
    return fy


def _phase_from_line(line: str) -> str:
    """One FSD row → names / stats / looks / save, or empty."""
    low = line.lower()
    if "hair" in low or "eye colour" in low or "eye color" in low:
        return "looks"
    if any(
        name in low
        for name in (
            "strength",
            "intellect",
            "willpower",
            "agility",
            "health",
            "charm",
        )
    ):
        return "stats"
    if "given name" in low or "family name" in low:
        return "names"
    if "exit:" in low or "cp left" in low:
        return "save"
    if re.search(r"\brace\b", low) and "choose" not in low and "select" not in low:
        return "race_field"
    if re.search(r"\bclass\b", low) and "choose" not in low and "select" not in low:
        return "class_field"
    return ""


def _name_wait(screen: AnsiScreen) -> bool:
    """True while WG is checking a *new* given name.

    TRAIN STATS is an existing toon — the validate drip still leaks onto the
    foot, but must not arm indefinite wait chrome (no-op SAVE / Audrey).
    """
    if "TRAIN STATS" in screen.text():
        return False
    blob = f"{screen.text()}\n{screen.sheet_notice}".lower()
    return "validating your name" in blob or "please wait" in blob


def _looks_filled(screen: AnsiScreen) -> bool:
    blob = screen.text()
    hair = re.search(r"Hair Length\s+([A-Za-z][A-Za-z'\-]*)", blob, re.I)
    eyes = re.search(r"Eye Colou?r\s+([A-Za-z][A-Za-z'\-]*)", blob, re.I)
    return bool(hair and eyes)


def _sheet_ready_to_save(screen: AnsiScreen) -> bool:
    """Given, family, race, class, and Exit are on the parchment."""
    blob = screen.text()
    if not _GIVEN_NAME_FIELD.search(blob) or not _FAMILY_NAME_FIELD.search(blob):
        return False
    if "Exit:" not in blob:
        return False
    return bool(_form_pick(screen, "Race") and _form_pick(screen, "Class"))


def creation_phase(screen: AnsiScreen) -> str:
    """Where the caret is on create/reroll: lists, names, stats, looks, save."""
    low = screen.text().lower()
    if "you may not use" in low or "please enter a new name" in low:
        return "name_taken"
    waiting = _name_wait(screen)
    # TRAIN STATS: form_saving still marks Enter→SAVE for HP-leave, but must
    # not lock ice "saving — wait" when the sheet stays interactive (no-op).
    arm_save_wait = waiting or (screen.form_saving and "train stats" not in low)
    if arm_save_wait:
        if screen.form_saving or _sheet_ready_to_save(screen):
            return "saving"
        if waiting:
            return "checking"
    if (
        "choose a race" in low
        or "select a race" in low
        or "available races" in low
    ):
        return "race"
    if (
        "choose a class" in low
        or "select a class" in low
        or "available classes" in low
    ):
        return "class"
    y = _sheet_caret_row(screen)
    if 0 <= y < screen.rows:
        phase = _phase_from_line(screen.line(y))
        if phase:
            return phase
    if _sheet_ready_to_save(screen):
        return "save"
    return "sheet"


def creation_status(screen: AnsiScreen) -> str:
    """Row under FINN'S REALM on the sheet. Never the login username hint."""
    phase = creation_phase(screen)
    labels = {
        "name_taken": "name taken",
        "checking": "checking name",
        "saving": "saving",
        "race": "pick race",
        "class": "pick class",
        "names": "names",
        "stats": "stats",
        "looks": "looks",
        "save": "save",
        "race_field": "race",
        "class_field": "class",
        "sheet": "character sheet",
    }
    tag = labels.get(phase, "character sheet")
    who = " ".join(
        part for part in (_form_pick(screen, "Race"), _form_pick(screen, "Class")) if part
    )
    if who and phase in {
        "names",
        "stats",
        "looks",
        "save",
        "saving",
        "race_field",
        "class_field",
        "sheet",
    }:
        if phase == "saving":
            return f"{who}  ·  saving — wait"
        return f"{who}  ·  {tag}"
    return tag


# Given name → STR INT WIL AGI HEA CHA after the 100 CP creation spend.
_START_STATS: dict[str, tuple[int, int, int, int, int, int]] = {
    "klymacks": (60, 50, 30, 80, 40, 40),
    "matthew": (70, 40, 40, 50, 60, 40),
    "matt": (70, 40, 40, 50, 60, 40),
    "ryan": (50, 30, 40, 80, 50, 40),
    "sarah": (40, 65, 40, 75, 60, 40),
    "alex": (40, 90, 60, 50, 50, 30),
    "robald": (50, 60, 50, 80, 50, 30),
    "rhiannon": (50, 40, 40, 70, 60, 40),
    "kevin": (90, 20, 25, 50, 70, 25),
    "sherry": (40, 40, 70, 60, 50, 40),
    "emily": (35, 50, 40, 70, 40, 80),
    "rita": (90, 20, 25, 50, 70, 25),
    "audrey": (35, 80, 50, 50, 50, 50),
    "curtis": (60, 40, 40, 70, 50, 40),
    "betty": (40, 55, 70, 65, 40, 35),
    "rose": (50, 30, 70, 50, 60, 30),
    "ron": (70, 30, 50, 50, 60, 30),
}


def _sheet_given(screen: AnsiScreen) -> str:
    matched = _GIVEN_NAME_FIELD.search(screen.text())
    return matched.group(1).strip().lower() if matched else ""


def creation_start_line(screen: AnsiScreen) -> str:
    """Parchment order: STR INT WIL AGI HEA CHA. Empty if this toon has no plan."""
    stats = _START_STATS.get(_sheet_given(screen))
    if not stats:
        return ""
    names = ("STR", "INT", "WIL", "AGI", "HEA", "CHA")
    return "  ".join(f"{name} {value}" for name, value in zip(names, stats))


def creation_tip(screen: AnsiScreen) -> str:
    """Create/reroll is yours. After a wipe, say why the name bounced."""
    phase = creation_phase(screen)
    if phase == "name_taken":
        return "That given name is taken. Type another, then Enter."
    if phase == "checking":
        return "Scanning monsters, NPCs, and players for that name. Wait — do not type."
    if phase == "saving":
        return "Saving your character. Wait — do not type."
    if phase == "race":
        return "You pick race. Client will not."
    if phase == "class":
        return "You pick class. Client will not."
    if phase == "names":
        return "Given name, then family. Last name required. Enter next field."
    if phase == "stats":
        line = creation_start_line(screen)
        if line:
            return line
        return "+/- spends CP."
    if phase == "looks":
        return "Space cycles hair and eyes. Enter next field."
    if phase == "save":
        return "Space SAVE / EXIT. Enter submits. F11 live after SAVE."
    if phase == "race_field":
        return "Race is set. Move to stats or SAVE."
    if phase == "class_field":
        return "Class is set. Move to stats or SAVE."
    if _sheet_given(screen) and _FAMILY_NAME_FIELD.search(screen.text()):
        return "Space SAVE / EXIT. Enter submits. F11 live after SAVE."
    return "Type on the form. F11 live after SAVE. Last name required."


def form_frozen(screen: AnsiScreen, sheet_lock: bool = False) -> bool:
    """TRAIN STATS / creation, or F11 lock. Status peeks must not type here.

    Leftover sheet_lock after SAVE must not pause the realm. Obvious exits /
    Also here / a live [HP=] grid is play. Mid-sheet [HP=] on the parchment
    is still the form — `_realm_on_grid` stays false there.

    Bare sheet_lock on a blank / post-close grid must not freeze: that blocked
    resume `l` and WG short looks. Lock only wins with real form fields.
    """
    if _realm_on_grid(screen):
        return False
    if screen.looks_like_creation() or _fsd_parchment(screen):
        return True
    # F11 lock with Given Name still on the grid (TRAIN STATS title gone).
    return bool(sheet_lock and "given name" in screen.text().lower())


def realm_thaws_sheet(screen: AnsiScreen) -> bool:
    """Room chrome is up. Drop F11 / creation lock so hunt can tick."""
    return _realm_on_grid(screen) and not _fsd_parchment(screen)


def play_select_timeout(
    *,
    pending: bool,
    hunting: bool,
    in_realm: bool,
    paused: bool,
    settling: bool,
    remain: float = 0.0,
    chrome_pulse: bool = False,
) -> float | None:
    """select() wait. Hunt used to pass None unless F7 was on, so ticks died."""
    live = (hunting or in_realm) and not paused and not settling
    timeout: float | None = 0.03 if pending else (PLAY_TICK if live else None)
    if settling:
        wait = min(0.5, max(0.05, remain))
        timeout = wait if timeout is None else min(timeout, wait)
    if chrome_pulse:
        timeout = CHROME_PULSE if timeout is None else min(timeout, CHROME_PULSE)
    return timeout


def play_may_tick(
    *,
    in_play: bool,
    walk_active: bool,
    typed: str,
    paused: bool,
    settling: bool,
) -> bool:
    """Brain heartbeat: kit, hunt, exp/stat pry. Not while FSD is actually up."""
    return bool(in_play and not walk_active and not typed and not paused and not settling)


def freeze_on_parchment(screen: AnsiScreen) -> bool:
    """Clear queued hunt keys only on the FSD sheet, not race/class Enter."""
    return _fsd_parchment(screen)


_GIVEN_NAME_FIELD = re.compile(r"Given Name\s+([A-Za-z][A-Za-z'\-]*)", re.I)
_FAMILY_NAME_FIELD = re.compile(r"Family Name\s+([A-Za-z][A-Za-z'\-]*)", re.I)


def _sheet_full_name(blob: str) -> str:
    """Given + Family from the two FSD fields. Empty if the sheet has no names."""
    given = _GIVEN_NAME_FIELD.search(blob)
    family = _FAMILY_NAME_FIELD.search(blob)
    if not given:
        return ""
    first = given.group(1)
    last = family.group(1) if family else ""
    return f"{first} {last}".strip()


def _name_status_line(line: str, full_name: str = "") -> bool:
    """WG post-SAVE status (label or just Given Family), not the two name fields.

    Same idea as Health: vs the Health (min to max) stat row: the field stays,
    the leftover status line on the last row does not.
    """
    low = line.lower()
    if "given name" in low or "family name" in low:
        return False
    stripped = line.strip().strip("«»").strip()
    if stripped.lower().startswith("name:"):
        return True
    if full_name and stripped.lower() == full_name.lower():
        return True
    return False


_HP_PROMPT_TAIL = re.compile(r"^(?:/MA=\d+)?(?:\d+)?\]:\s*$", re.I)


def _hp_prompt_tail(line: str) -> bool:
    """`[HP=25]:` after the `[HP=25` prefix was dropped — leftover `]:`."""
    return bool(_HP_PROMPT_TAIL.match(line.strip()))


_FORM_STATUS_PREFIXES = (
    b"[HP=",
    b"[hp=",
    b"[Hp=",
    b"/MA=",
    b"Health:",
    b"Hits:",
    b"Mana:",
    b"Exp:",
    b"You are carrying ",
    b"You have no keys",
    # After SAVE, WG prints this on the last FSD row. [HP=] used to cover it.
    b"Name: ",
)
_FORM_PROMPT_HEADS = (
    b"[HP=",
    b"[hp=",
    b"[Hp=",
    b"/MA=",
    b"Name:",
    b"NAME:",
    b"name:",
)
_SHEET_LEAK_HEADS = (
    b"validating your name",
    b"please wait - validating",
    b"please wait",
    b"password protected",
    b"have been password",
    b"suicide password",
    b"your suicide password",
    b"set suicide",
    b"these commands",
    b"you have not yet entered",
    b"so please do so soon",
    # Leftover after `Validating your name.` is dropped mid-packet.
    b".roll",
    # Sysop footer on the SAVE sheet — must not sit on the parchment curl.
    b"to prevent accidental suicide",
    b"to prevent accidental",
    b"accidental suicide",
    b"suicide or reroll",
    b"reroll",
)


def _sheet_leak_head_len(data: bytes, i: int) -> int:
    rest = data[i:].lower()
    best = 0
    for p in _SHEET_LEAK_HEADS:
        if rest.startswith(p) and len(p) > best:
            best = len(p)
    return best


def _sheet_leak_at(data: bytes, i: int) -> bool:
    return _sheet_leak_head_len(data, i) > 0


def _sheet_leak_incomplete(data: bytes, i: int) -> bool:
    """data[i:] is a proper prefix of a leak head — wait for the next recv.

    Ignore 1–3 byte tails (`s` of Klymacks, `th` of Strength) so a packet
    boundary is not treated as `suicide` / `these commands`.
    """
    rest = data[i:].lower()
    if len(rest) < 4 or _sheet_leak_head_len(data, i):
        return False
    return any(p.startswith(rest) and len(rest) < len(p) for p in _SHEET_LEAK_HEADS)


def _sheet_leak_keeper_at(data: bytes, i: int) -> int:
    """« / << that close the Exit field; do not consume these."""
    if i >= len(data):
        return 0
    if data[i] == 0xAE:
        return 1
    if data.startswith(b"\xc2\xab", i) or data.startswith(b"<<", i):
        return 2
    return 0


def _sheet_leak_line(line: str) -> bool:
    low = line.lower()
    return any(p.decode("ascii") in low for p in _SHEET_LEAK_HEADS)


_NOTICE_CSI = re.compile(rb"\x1b\[[0-9;?]*[A-Za-z]")
_NOTICE_WOUND = re.compile(r"^these commands\s+\d+$", re.I)
_STAT_REJECT_RE = re.compile(
    r"(?:strength|intellect|willpower|agility|health|charm)"
    r"\s+may not be higher than\s+\d+",
    re.I,
)


def _notice_from_bytes(chunk: bytes) -> str:
    text = _NOTICE_CSI.sub(b"", chunk)
    text = text.replace(b"\r", b" ").replace(b"\n", b" ")
    return re.sub(r"\s+", " ", text.decode("latin-1", "replace")).strip()


def _stat_reject_line(line: str) -> bool:
    return bool(_STAT_REJECT_RE.search(line))


def _usable_sheet_notice(s: str) -> bool:
    s = s.strip()
    if len(s) < 12:
        return False
    low = s.lower()
    if low.startswith("."):
        return False
    if _NOTICE_WOUND.fullmatch(low):
        return False
    return True


def _pick_sheet_notice(old: str, new: str) -> str:
    """Keep a readable sysop line. Stat rejects beat wait; wait beats prevent."""
    if not _usable_sheet_notice(new):
        return old
    clipped = new[:80]
    low = clipped.lower()
    old_low = old.lower()
    if "may not be higher than" in low:
        return clipped
    # Do not bury a live stat reject under validate / suicide footer.
    if "may not be higher than" in old_low:
        if (
            "validating your name" in low
            or "please wait" in low
            or "to prevent accidental" in low
        ):
            return old
    if "validating your name" in low or "please wait" in low:
        return clipped
    if "to prevent accidental" in low:
        return clipped
    if "to prevent accidental" in old_low:
        return old
    return clipped


def remember_sheet_notice(screen: AnsiScreen, text: str) -> None:
    screen.sheet_notice = _pick_sheet_notice(screen.sheet_notice, text)


def _capture_stat_reject(screen: AnsiScreen) -> None:
    """WG writes `Strength may not be higher than N` on row 24 under ice chrome.

    Hoist into sheet_notice so the tip shows why the add bounced; blank the
    under-chrome row so the message is not only painted then wiped.
    """
    for y in range(screen.rows):
        line = " ".join(screen.line(y).split())
        if not _stat_reject_line(line):
            continue
        remember_sheet_notice(screen, line[:80])
        # CSI 23–25 are the lifted ice title / status / throbber.
        if y >= CHROME_ROW - SHEET_CHROME_LIFT - 1:
            screen._erase(0, y, screen.cols, y)
            screen.generation += 1
        return


class FormHoldFilter:
    """Drop delayed [HP=/MA=] prompt *text* while TRAIN STATS / creation is up.

    Do not park or eat CUP/EL for rows 23–25. Those are also the idle room
    tick's home-cursor setup; eating them left bare CSI 6n, a form-row CPR,
    and WG stopped reprinting Also here. Parchment punches are healed by
    form_snap restore after feed — not by suppressing the tick CSI.

    Suicide / validate leaks are assembled across recv chunks and dropped
    through the leak span so `«` / SAVE legend on the Exit row stay for heal.
    """

    _PREFIX_FINALS = frozenset(b"HfKmSu")

    def __init__(self) -> None:
        self._skip_prompt = False
        self._pending = b""
        self._line_start = True
        self.notice = ""

    def reset(self) -> None:
        self._skip_prompt = False
        self._pending = b""
        self._line_start = True

    def _note_leak(self, chunk: bytes) -> None:
        self.notice = _pick_sheet_notice(self.notice, _notice_from_bytes(chunk))

    def take_pending(self) -> bytes:
        data = self._pending
        self.reset()
        return data

    def _csi_span(self, data: bytes, i: int) -> int | None:
        if i >= len(data) or data[i] != 0x1B:
            return None
        if i + 1 >= len(data):
            return -1
        if data[i + 1] != 0x5B:
            return i + 2 if i + 1 < len(data) else -1
        j = i + 2
        n = len(data)
        while j < n and not (0x40 <= data[j] <= 0x7E):
            j += 1
        if j >= n:
            return -1
        return j + 1

    def _cup_row(self, csi: bytes) -> int | None:
        if len(csi) < 3 or csi[-1] not in (ord("H"), ord("f")):
            return None
        body = csi[2:-1]
        if body.startswith(b"?"):
            return None
        part = body.split(b";", 1)[0]
        if not part:
            return 1
        if not part.isdigit():
            return None
        return int(part)

    def _is_prefix_csi(self, csi: bytes) -> bool:
        if len(csi) < 2 or csi[0] != 0x1B:
            return False
        if csi == b"\x1b7" or csi == b"\x1b8":
            return True
        if len(csi) < 3 or csi[1] != 0x5B:
            return False
        return csi[-1] in self._PREFIX_FINALS

    def _look_at(self, data: bytes, i: int) -> bool:
        """Idle short look / combat — never treat as a sheet [HP=] prompt."""
        return any(data.startswith(mark, i) for mark in _FORM_LEAVE_MARKERS)

    def _prompt_at(self, data: bytes, i: int, *, realm_line: bool) -> bool:
        if i >= len(data):
            return False
        j = i
        while j < len(data) and data[j] in (9, 32):
            j += 1
        if self._look_at(data, i) or self._look_at(data, j):
            return False
        if data[j : j + 5].lower() == b"name:":
            return True
        if data[j : j + 2] == b"]:":
            return True
        if data.startswith(_FORM_PROMPT_HEADS, i):
            return True
        if not realm_line:
            return False
        rest = data[j:]
        return any(rest.startswith(pref) for pref in _FORM_STATUS_PREFIXES)

    def _leak_span(self, data: bytes, start: int, head_at: int) -> tuple[int, str, int]:
        """Walk a leak from `head_at`. end -1 means hold in `_pending`.

        kind is keeper (pad so `«`/legend keep their columns), newline, or
        cup (drop; a later CUP is a new write). pad counts cells, not CSI.
        """
        n = len(data)
        pad = head_at - start
        j = head_at
        while j < n:
            if data[j] == 0x1B:
                span = self._csi_span(data, j)
                if span == -1:
                    return -1, "hold", 0
                csi = data[j:span]
                if self._cup_row(csi) is not None:
                    return j, "cup", pad
                j = span
                continue
            if _sheet_leak_keeper_at(data, j):
                return j, "keeper", pad
            if data[j] in (10, 13):
                j += 1
                if j < n and data[j] in (10, 13) and data[j] != data[j - 1]:
                    j += 1
                return j, "newline", pad
            pad += 1
            j += 1
        return -1, "hold", 0

    def filter(self, payload: bytes) -> bytes:
        data = self._pending + payload
        self._pending = b""
        out = bytearray()
        buf = bytearray()
        i = 0
        n = len(data)

        def flush_buf() -> None:
            out.extend(buf)
            buf.clear()

        def flush_hold_csi(*, for_prompt: bool = False) -> None:
            """Keep realm-row CUP; drop EL and form-field CUP before HP text.

            Rows 23–25 are the idle room-tick / prompt band — keep that CUP so
            CSI 6n CPRs the prompt. Rows 1–22 are FSD fields; a CUP there before
            `[HP=` punches Intellect and must be dropped.
            """
            blob = bytes(buf)
            buf.clear()
            k = 0
            while k < len(blob):
                if blob[k] in (10, 13):
                    k += 1
                    continue
                sp = self._csi_span(blob, k)
                if not sp or sp < 0:
                    out.extend(blob[k:])
                    return
                csi = blob[k:sp]
                if len(csi) >= 3 and csi[-1] == ord("K"):
                    k = sp
                    continue
                row = self._cup_row(csi)
                if for_prompt and row is not None and row < 23:
                    k = sp
                    continue
                out.extend(csi)
                k = sp

        def hold_from(idx: int) -> None:
            self._pending = bytes(buf) + data[idx:]
            buf.clear()

        while i < n:
            if self._skip_prompt:
                if self._look_at(data, i):
                    self._skip_prompt = False
                    continue
                if data[i] == 0x1B:
                    # Reprocess CSI (incl. 6n / next CUP). Do not eat it —
                    # bare 6n after a dropped CUP CPR'd a form row and killed ticks.
                    self._skip_prompt = False
                    continue
                if data[i] in (10, 13):
                    self._skip_prompt = False
                    self._line_start = True
                    i += 1
                    continue
                i += 1
                continue
            if data[i] in (10, 13):
                buf.append(data[i])
                self._line_start = True
                i += 1
                continue
            span = self._csi_span(data, i)
            if span == -1:
                self._pending = bytes(buf) + data[i:]
                buf.clear()
                break
            if span is not None:
                csi = data[i:span]
                if self._cup_row(csi) is not None:
                    self._line_start = True
                if self._is_prefix_csi(csi):
                    buf.extend(csi)
                    i = span
                    continue
                flush_buf()
                out.extend(csi)
                i = span
                continue
            j = i
            while j < n and data[j] in (9, 32):
                j += 1
            if j < n and _sheet_leak_incomplete(data, j):
                hold_from(i)
                break
            if j < n and _sheet_leak_head_len(data, j):
                end, kind, pad = self._leak_span(data, i, j)
                if end < 0:
                    hold_from(i)
                    break
                flush_hold_csi(for_prompt=False)
                self._note_leak(data[j:end])
                if kind == "keeper":
                    out.extend(b" " * pad)
                    self._line_start = False
                else:
                    self._line_start = kind == "newline"
                i = end
                continue
            realm_line = (not buf) or all(b in (10, 13) for b in buf)
            if self._prompt_at(data, i, realm_line=realm_line):
                # Keep realm-row CUP; drop form-field CUP + EL before [HP=.
                flush_hold_csi(for_prompt=True)
                self._skip_prompt = True
                continue
            flush_buf()
            out.append(data[i])
            self._line_start = False
            i += 1
        if buf:
            # Trailing CUP/EL with no text yet — park until the next packet.
            # Same-packet [HP=] / Also here already flushed above. Parking the
            # split `CUP+EL` alone protects the parchment foot; flushing it
            # into a room tick on the next packet keeps WG CPR honest.
            only_prefix = True
            k = 0
            blob = bytes(buf)
            while k < len(blob):
                if blob[k] in (10, 13):
                    k += 1
                    continue
                sp = self._csi_span(blob, k)
                if not sp or sp < 0:
                    only_prefix = False
                    break
                csi = blob[k:sp]
                row = self._cup_row(csi)
                if row is not None and row >= 23:
                    k = sp
                    continue
                if csi[-1:] in (b"K", b"m", b"s", b"u") or csi in (b"\x1b7", b"\x1b8"):
                    k = sp
                    continue
                only_prefix = False
                break
            if only_prefix and any(b not in (10, 13) for b in blob):
                self._pending = blob
            else:
                out.extend(buf)
        return bytes(out)


_FORM_LEAVE_MARKERS = (
    b"Obvious exits:",
    b"Also here:",
    b"You notice ",
    b"You see:",
    b"*Combat",
    b"* Combat",
)


_CUP_ROW = re.compile(rb"\x1b\[(\d+)(?:;(\d+))?[Hf]")
_BARE_CSI = re.compile(rb"(?<!\x1b)\[(\d{1,3}(?:;\d{1,3})*)([mHfJK])")


def _repair_bare_csi(payload: bytes) -> bytes:
    """Filter reset can drop ESC and leave `[0;37;40m` as text on the sheet."""
    return _BARE_CSI.sub(lambda m: b"\x1b[" + m.group(1) + m.group(2), payload)


def _cup_rows(payload: bytes) -> list[int]:
    return [int(m.group(1)) for m in _CUP_ROW.finditer(payload)]


def _bottom_hp_prompt(payload: bytes) -> bool:
    """SAVE finished: WG parked the idle `[HP=]:` on row 24/25, FSD is done."""
    if (
        b"Given Name" in payload
        or b"Character Creation" in payload
        or b"TRAIN STATS" in payload
        or b"Point Cost" in payload
        or b"Exit: SAVE" in payload
    ):
        return False
    if not (
        b"[HP=" in payload
        or b"[hp=" in payload
        or b"]:" in payload
    ):
        return False
    rows = _cup_rows(payload)
    if rows:
        return max(rows) >= 24
    return payload.rstrip().endswith(b"]:")


def _form_home_cursor(screen: AnsiScreen) -> tuple[int, int]:
    """Put the caret back on Exit: SAVE, not the dropped `[HP=]:` row."""
    for y in range(screen.rows):
        line = screen.line(y)
        save = line.find("SAVE")
        if save >= 0 and "Exit:" in line:
            return (min(save, screen.cols - 1), y)
        exit_at = line.find("Exit:")
        if exit_at >= 0:
            return (min(exit_at + 6, screen.cols - 1), y)
    if screen.form_cursor is not None:
        return screen.form_cursor
    return (4, min(20, screen.rows - 4))


def _sync_form_cursor(screen: AnsiScreen) -> None:
    """Keep the field caret. Footer leaks must not stick on Given Name after looks."""
    if not _live_fsd_session(screen):
        return
    if screen.cy < 23:
        screen.form_cursor = (screen.cx, screen.cy)
        return
    home = screen.form_cursor or _form_home_cursor(screen)
    if (
        0 <= home[1] < screen.rows
        and _phase_from_line(screen.line(home[1])) == "names"
        and _looks_filled(screen)
        and _sheet_ready_to_save(screen)
    ):
        home = _form_home_cursor(screen)
    screen.cx, screen.cy = home
    screen.form_cursor = home
    _rewrite_dsr(screen)


def _rewrite_dsr(screen: AnsiScreen, pos: bytes | None = None) -> None:
    if pos is None:
        pos = f"\x1b[{screen.cy + 1};{screen.cx + 1}R".encode("ascii")
    screen.replies = [pos if r.endswith(b"R") else r for r in screen.replies]


def note_form_enter(screen: AnsiScreen, key: bytes) -> None:
    """Enter on Exit: SAVE — FSD is submitting. Next idle HP is the realm."""
    if key != b"\r" or not screen.looks_like_creation():
        return
    rows = [screen.cy]
    if screen.form_cursor is not None:
        rows.append(screen.form_cursor[1])
    for y in rows:
        if 0 <= y < screen.rows:
            line = screen.line(y)
            if "SAVE" in line or "Exit:" in line:
                screen.form_saving = True
                return


def release_after_sheet(brain: Brain, *, was_sheet: bool, now_sheet: bool) -> None:
    """SAVE/EXIT left the parchment. Train hold was only so you could type on it."""
    if was_sheet and not now_sheet:
        brain.cancel_train()


def _creation_left_for_realm(screen: AnsiScreen, payload: bytes) -> bool:
    """Creation SAVE parked `[HP=]:` — wipe the parchment, do not line-break under it.

    TRAIN STATS stays live through stray idle HP (that used to punch holes).
    After Enter on SAVE/EXIT, that same HP prompt means FSD is done.
    Leftover titles without a live snap must not drop standing room ticks.
    """
    if not _bottom_hp_prompt(payload):
        return False
    if not _live_fsd_session(screen):
        return False
    blob = screen.text()
    if "TRAIN STATS" in blob:
        return bool(screen.form_saving)
    if "Character Creation" not in blob and "Exit: SAVE" not in blob:
        return False
    return True


_REALM_GRID_MARKS = (
    "obvious exits",
    "also here",
    "you notice",
    "you see:",
)


def _realm_on_grid(screen: AnsiScreen) -> bool:
    """Standing in a room — the idle short look must paint, not hit FormHold.

    Combat can scroll Also here / exits off the 80×25 band. The [HP=] prompt
    still means we are in the mud, not on TRAIN STATS.
    """
    blob = screen.text().lower()
    if any(mark in blob for mark in _REALM_GRID_MARKS):
        return True
    # Name field is the parchment (live or leftover). [HP=] on that grid is
    # a leak, not a room — F11 lock must still own keys.
    if "given name" in blob:
        return False
    if "character creation" in blob or "point cost chart" in blob:
        return False
    return "[hp=" in blob


def _sync_realm_dsr(screen: AnsiScreen, *, in_realm: bool = False) -> None:
    """After feed: live FSD keeps the field caret; realm keeps the real caret."""
    if _live_fsd_session(screen):
        screen.realm_cpr = False
        _sync_form_cursor(screen)
        return
    if in_realm or _realm_on_grid(screen):
        screen.realm_cpr = True


def ensure_realm_dsr(
    screen: AnsiScreen, payload: bytes, *, in_realm: bool = False
) -> None:
    """Answer missing realm 6n with the real caret; never rewrite to ``25;1R``.

    Live FSD keeps the field caret. Trace after the final reply.
    """
    if _live_fsd_session(screen):
        screen.realm_cpr = False
        if any(r.endswith(b"R") for r in screen.replies):
            _sync_form_cursor(screen)
        _trace_dsr(screen, payload, in_realm=in_realm)
        return
    standing = in_realm or _realm_on_grid(screen)
    has_6n = (
        b"[6n" in payload
        or b"[6N" in payload
        or b"\x9b6n" in payload
        or b"\x9b6N" in payload
    )
    if standing:
        screen.realm_cpr = True
    if has_6n and not any(r.endswith(b"R") for r in screen.replies):
        # 6n was in the packet but never fed (hold ate it).
        screen.replies.append(
            f"\x1b[{screen.cy + 1};{screen.cx + 1}R".encode("ascii")
        )
    _trace_dsr(screen, payload, in_realm=in_realm)


def _trace_dsr(
    screen: AnsiScreen, payload: bytes, *, in_realm: bool = False
) -> None:
    """Append one line to data/dsr-trace.jsonl for live 6n/CPR/Also here/HP."""
    has_tick = (
        b"Also here:" in payload
        or b"Obvious exits:" in payload
        or b"You notice " in payload
    )
    has_hp = b"[HP=" in payload or b"[hp=" in payload or b"[Hp=" in payload
    has_6n = (
        b"[6n" in payload
        or b"[6N" in payload
        or b"\x9b6n" in payload
        or b"\x9b6N" in payload
    )
    if (
        not has_6n
        and not has_tick
        and not has_hp
        and not any(r.endswith(b"R") for r in screen.replies)
    ):
        return
    try:
        path = ROOT / "data" / "dsr-trace.jsonl"
        path.parent.mkdir(parents=True, exist_ok=True)
        if path.exists() and path.stat().st_size > 200_000:
            path.write_text("", encoding="utf-8")
        # Tail preview — spot 8-bit CSI / missing 6n without dumping secrets.
        tail = payload[-80:] if len(payload) > 80 else payload
        preview = "".join(
            chr(b) if 32 <= b < 127 else f"\\x{b:02x}" for b in tail
        )
        rec = {
            "ts": datetime.now(timezone.utc).isoformat(timespec="seconds"),
            "in_realm": bool(in_realm),
            "on_grid": _realm_on_grid(screen),
            "live_fsd": _live_fsd_session(screen),
            "realm_cpr": bool(screen.realm_cpr),
            "cy": screen.cy + 1,
            "cx": screen.cx + 1,
            "has_6n": has_6n,
            "has_hp": bool(has_hp),
            "also_here": b"Also here:" in payload,
            "exits": b"Obvious exits:" in payload,
            "you_notice": b"You notice " in payload,
            "cup_rows": _cup_rows(payload)[:8],
            "c1_csi": 0x9B in payload,
            "ttype0": TTYPE_NAMES[0].decode("ascii", "replace"),
            "preview": preview,
            "replies": [
                r.decode("ascii", "replace")
                for r in screen.replies
                if r.endswith(b"R")
            ],
        }
        with path.open("a", encoding="utf-8") as fh:
            fh.write(json.dumps(rec, separators=(",", ":")) + "\n")
    except OSError:
        pass


def _sheet_on_screen(screen: AnsiScreen) -> bool:
    """Parchment still occupies the grid. Realm `Also here` is not a form."""
    blob = screen.text()
    if _realm_on_grid(screen):
        return False
    return (
        screen.looks_like_creation()
        or "TRAIN STATS" in blob
        or "Character Creation" in blob
        or "Exit: SAVE" in blob
        or "Point Cost Chart" in blob
    )


def _exit_field_value(line: str) -> str:
    """Text after `Exit:` inside the FSD field, not the SAVE/EXIT legend."""
    if "Exit:" not in line:
        return ""
    rest = line.split("Exit:", 1)[1]
    for stop in ("«", "<<"):
        at = rest.find(stop)
        if at >= 0:
            return rest[:at]
    pipe = rest.find(" |")
    if pipe >= 0:
        return rest[:pipe]
    return rest


def _exit_field_choice(line: str) -> str:
    return _exit_field_value(line).strip().upper().strip("\\,")


def _exit_field_wounded(line: str) -> bool:
    """`SAV\\, these commands` is a wound; spacebar EXIT is not."""
    if "Exit:" not in line:
        return False
    field = _exit_field_value(line)
    if _sheet_leak_line(field) or _sheet_leak_line(line):
        return True
    compact = field.strip().upper()
    if not compact:
        # Overlay blanked SAVE; `«` still marks a real Exit field.
        return "«" in line or "<<" in line
    if compact in ("SAVE", "EXIT") or compact.startswith("EXIT"):
        return False
    if compact.startswith("SAVE") and "SAV\\" not in field.upper():
        return False
    return True


def _heal_exit_save_field(screen: AnsiScreen) -> None:
    """Put SAVE back when the legend still says SAVE but the field does not."""
    healed = False
    for y in range(screen.rows):
        line = screen.line(y)
        if not _exit_field_wounded(line):
            continue
        start = line.find("Exit:") + len("Exit:")
        while start < len(line) and line[start] == " ":
            start += 1
        for i, ch in enumerate("SAVE"):
            if start + i >= screen.cols:
                break
            screen.buf[y][start + i].ch = ch
        x = start + 4
        while x < screen.cols:
            ch = screen.buf[y][x].ch
            if ch in ("«", "<", "│"):
                break
            if ch == "|" and x > start:
                break
            screen.buf[y][x].ch = " "
            x += 1
        healed = True
    if healed:
        screen.generation += 1


def _exit_row_wounded(old: str, now: str) -> bool:
    """Suicide banner ate SAVE. Spacebar SAVE/EXIT is not a wound."""
    if "Exit:" not in old:
        return False
    if _exit_field_wounded(now):
        return True
    old_choice = _exit_field_choice(old)
    now_choice = _exit_field_choice(now)
    if now_choice == "EXIT":
        return False
    return old_choice == "SAVE" and now_choice != "SAVE"


def _payload_has_sheet_leak(payload: bytes) -> bool:
    low = payload.lower()
    return any(p in low for p in _SHEET_LEAK_HEADS)


def _parchment_wound_rows(
    screen: AnsiScreen, snap: list[list[Cell]] | None
) -> list[int]:
    found = list(screen.leaked_prompt_rows())
    if snap is None:
        return found
    seen = set(found)
    for y, row in enumerate(snap):
        if y >= screen.rows or y in seen:
            continue
        old = "".join(c.ch for c in row)
        if _exit_row_wounded(old, screen.line(y)):
            found.append(y)
            seen.add(y)
    return found


def _erase_parchment_leak_rows(screen: AnsiScreen) -> None:
    """Blank suicide/validate leftovers. Exit: SAVE is healed in place."""
    full = _sheet_full_name(screen.text())
    wiped = False
    for y in list(screen.leaked_prompt_rows()):
        line = screen.line(y)
        if "Exit:" in line:
            continue
        leak = _sheet_leak_line(line)
        if (
            _name_status_line(line, full)
            or _hp_prompt_tail(line)
            or leak
        ):
            if leak:
                remember_sheet_notice(screen, " ".join(line.split()))
            screen._erase(0, y, screen.cols, y)
            wiped = True
    if wiped:
        screen.generation += 1


def _snap_blob(snap: list[list[Cell]]) -> str:
    return "\n".join("".join(c.ch for c in row) for row in snap)


def _snap_is_parchment(snap: list[list[Cell]]) -> bool:
    blob = _snap_blob(snap)
    return (
        "TRAIN STATS" in blob
        or "Character Creation" in blob
        or "Exit: SAVE" in blob
        or "Point Cost Chart" in blob
    )


def _parchment_foot_line(line: str) -> bool:
    """Scroll curl / underline. Never the Exit: SAVE field."""
    if "Exit:" in line:
        return False
    return "____" in line or "⌐┴" in line


def _restore_parchment_foot(
    screen: AnsiScreen, snap: list[list[Cell]] | None
) -> None:
    """Repaint the curl from form_snap when ice chrome sits on the last rows."""
    if snap is None:
        return
    rows = [
        y
        for y, row in enumerate(snap)
        if y < screen.rows and _parchment_foot_line("".join(c.ch for c in row))
    ]
    if rows:
        screen.restore_rows(snap, rows)


def _chrome_parchment_foot(screen: AnsiScreen) -> bytes:
    """Opaque-repaint the curl so lifted chrome cannot leave suicide holes."""
    if chrome_row(screen) >= CHROME_ROW:
        return b""
    src = screen.form_snap
    if src is None or not _snap_is_parchment(src):
        src = screen.buf
    out = bytearray()
    for y, row in enumerate(src):
        if y >= screen.rows:
            break
        line = "".join(c.ch for c in row)
        if not _parchment_foot_line(line):
            continue
        padded = line[: screen.cols].ljust(screen.cols)
        out += f"\x1b[{y + 1};1H\x1b[K{padded}".encode("utf-8", "replace")
    return bytes(out)


def _fsd_parchment(screen: AnsiScreen) -> bool:
    """TRAIN STATS / Character Creation fields. Race lists type on the last row."""
    if _realm_on_grid(screen):
        return False
    blob = screen.text()
    return (
        "TRAIN STATS" in blob
        or "Character Creation" in blob
        or "Exit: SAVE" in blob
        or "Point Cost Chart" in blob
    )


def _live_fsd_session(screen: AnsiScreen) -> bool:
    """True only while an FSD form is actually open — not leftover titles.

    Leftover Exit:SAVE / TRAIN STATS on the grid after close used to CPR a
    form row and keep FormHold eating short looks, which suppresses WG ticks.
    """
    if screen.form_snap is None and not screen.form_saving:
        return False
    return _fsd_parchment(screen) or screen.looks_like_creation()


def _payload_is_parchment(payload: bytes) -> bool:
    """First Character Creation packet arrives while the race/class list is still up."""
    return (
        b"TRAIN STATS" in payload
        or b"Character Creation" in payload
        or b"Exit: SAVE" in payload
        or b"Exit: SAV" in payload
        or b"Point Cost Chart" in payload
    )


def form_returns_to_realm(payload: bytes) -> bool:
    """SAVE/EXIT reprinted the room. Do not keep filtering that packet as the sheet."""
    return any(mark in payload for mark in _FORM_LEAVE_MARKERS)


def form_hold_now(
    screen: AnsiScreen,
    payload: bytes,
    *,
    sheet_lock: bool = False,
    in_realm: bool = False,
    train_hold: bool = False,
) -> bool:
    """Drop delayed [HP=] only while F11 train / live parchment FSD is open.

    Nowhere else — leftover Exit:SAVE, bare ``sheet_lock``, or standing play
    must never FormHold (that ate room ticks). ``sheet_lock`` / ``in_realm`` /
    ``train_hold`` remain for call-site compat; only a live parchment arms hold.
    """
    _ = (sheet_lock, in_realm, train_hold)
    if form_returns_to_realm(payload):
        return False
    # First TRAIN STATS / Character Creation packet (snap not captured yet).
    if _payload_is_parchment(payload):
        return True
    return _live_fsd_session(screen)


def sheet_needs_resume(was_sheet: bool, now_sheet: bool, payload: bytes) -> bool:
    """After SAVE the parchment is gone. Bare Enter so Statline Full reprints."""
    return bool(was_sheet and not now_sheet and not form_returns_to_realm(payload))


def realm_needs_look(screen: AnsiScreen) -> bool:
    """Blank 80×25 after FSD close — remind (and auto-Enter) until the room prints."""
    if not screen.needs_resume or screen.looks_like_creation():
        return False
    return not _realm_on_grid(screen)


def paint_mud(
    screen: AnsiScreen,
    payload: bytes,
    *,
    hold: bool,
    filt: FormHoldFilter,
    in_realm: bool = False,
) -> bytes:
    """Feed the grid. After TRAIN STATS, a room reprint must not stay star-masked."""
    payload = keep_party_lf(payload)
    # Arm prompt-row CPR before feed so mid-packet 6n never reports a combat row.
    if (in_realm or _realm_on_grid(screen)) and not _live_fsd_session(screen):
        screen.realm_cpr = True
    if hold and form_returns_to_realm(payload) and _sheet_on_screen(screen):
        data = filt.take_pending() + payload
        screen.close_form()
        screen.feed(data)
        screen.needs_resume = False
        _sync_realm_dsr(screen, in_realm=in_realm)
        return data
    if not _payload_is_parchment(payload) and (
        (in_realm and not _live_fsd_session(screen))
        or _realm_on_grid(screen)
        or form_returns_to_realm(payload)
    ):
        hold = False
    if hold and form_returns_to_realm(payload):
        # Idle short look / Also here while standing — not FSD close.
        filt.reset()
        screen.form_saving = False
        screen.feed(payload)
        _sync_realm_dsr(screen, in_realm=in_realm)
        return payload
    idle_done = hold and _creation_left_for_realm(screen, payload)
    if idle_done:
        filt.take_pending()
        if b"[6n" in payload or b"[6N" in payload:
            screen.replies.append(
                f"\x1b[{screen.cy + 1};{screen.cx + 1}R".encode("ascii")
            )
        screen.close_form()
        if form_returns_to_realm(payload):
            screen.feed(payload)
            screen.needs_resume = False
            _sync_realm_dsr(screen, in_realm=in_realm)
            return payload
        return b""
    shown = filt.filter(payload) if hold else payload
    if hold and filt.notice:
        remember_sheet_notice(screen, filt.notice)
        filt.notice = ""
    if not hold:
        # Stuck hold may have parked CUP/EL for the idle prompt. Let that
        # home cursor through with the room tick; drop it only when leaving
        # a sheet without realm evidence (would punch holes in parchment).
        pending = filt.take_pending()
        if pending and (
            in_realm
            or _realm_on_grid(screen)
            or form_returns_to_realm(payload)
            or screen.needs_resume
        ):
            shown = pending + payload
        else:
            shown = payload
        screen.form_snap = None
        screen.feed(shown)
        if _realm_on_grid(screen):
            screen.form_saving = False
        _sync_realm_dsr(screen, in_realm=in_realm)
        # Heal leftover parchment glyphs for display, but never re-arm
        # form_snap. A bare CSI 6n used to snapshot a fake live FSD session;
        # the next idle 6n then CPR'd a form row and killed room ticks.
        if _fsd_parchment(screen):
            _erase_parchment_leak_rows(screen)
            _heal_exit_save_field(screen)
            _restore_parchment_foot(screen, None)
            _capture_stat_reject(screen)
        return shown
    snap = screen.form_snap
    screen.feed(shown)
    if snap is not None and _snap_is_parchment(snap):
        if screen.form_scrolled(snap) or not _fsd_parchment(screen):
            screen.restore_all(snap)
        else:
            leaked = _parchment_wound_rows(screen, snap)
            if leaked:
                restore = []
                for y in leaked:
                    now = screen.line(y)
                    if "Exit:" not in now:
                        restore.append(y)
                        continue
                    old = "".join(c.ch for c in snap[y]) if y < len(snap) else ""
                    # Keep a legend this packet just drew; otherwise snap wins.
                    if (
                        "SAVE your character" in now
                        and "SAVE your character" not in old
                    ):
                        continue
                    restore.append(y)
                screen.restore_rows(snap, restore)
        _erase_parchment_leak_rows(screen)
        _heal_exit_save_field(screen)
        _restore_parchment_foot(screen, snap)
        _capture_stat_reject(screen)
        if not _parchment_wound_rows(screen, snap):
            screen.form_snap = screen.snapshot()
        _sync_form_cursor(screen)
        return shown
    if not _fsd_parchment(screen):
        screen.form_snap = None
        _sync_realm_dsr(screen, in_realm=in_realm)
        return shown
    _erase_parchment_leak_rows(screen)
    _heal_exit_save_field(screen)
    _restore_parchment_foot(screen, screen.form_snap)
    _capture_stat_reject(screen)
    leaked = screen.leaked_prompt_rows()
    if not leaked:
        screen.form_snap = screen.snapshot()
    _sync_form_cursor(screen)
    return shown


def form_hold_payload(payload: bytes) -> bytes:
    """Keep FSD CSI and field text. Drop [HP=] / look / i that would write on the sheet."""
    return FormHoldFilter().filter(payload)


def form_blocks_line(outgoing: bytes, *, frozen: bool) -> bool:
    """Queued look/health/i must not send while F11 holds the form. Lone CR is a field."""
    if not frozen or outgoing == b"\r" or not outgoing.endswith(b"\r"):
        return False
    line = outgoing.decode("ascii", "replace").strip().lower()
    verb = line.split()[0] if line else ""
    # Join must land even if leftover FSD still looks frozen.
    if verb in {"follow", "fo", "invite", "backr", "backrank", "frontr", "frontrank", "midr", "midrank"}:
        return False
    if line.startswith("!join"):
        return False
    return True


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


def handle_escape_key(
    brain: Brain,
    pacer: KeyPacer,
    *,
    typed: str = "",
) -> tuple[str, str]:
    """Esc: cancel offer/train, else stop the brain and clear queues.

    Returns (hint, typed). typed is cleared on a full stop.
    """
    if brain.pending_offer() or brain._want_spell or brain._want_gear:
        brain.cancel_offer_run()
        return "your keyboard", typed
    if brain.train_holding() or brain._want_train:
        brain.cancel_train()
        return "your keyboard", typed
    brain.takeover()
    pacer.clear()
    return "stopped", ""


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
            if in_realm:
                line = brain.toggle_aa(state)
                if line:
                    pacer.push_text(line)
            return "aa"
        was_on = brain.stealth_label() == "ambush"
        if in_realm:
            brain.toggle_stealth()
            now_on = brain.stealth_label() == "ambush"
            if not was_on and now_on and state is not None:
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
        pacer.clear()
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


def invite_tip(state: WorldState) -> str:
    """Chrome: the mud row may be CR-wiped; still show how to follow."""
    who = (state.invited_by or "").strip()
    if not who or not state.in_realm:
        return ""
    followed = (state.following or "").strip()
    if followed and followed.lower() == who.lower():
        return ""
    word = who.split()[0]
    return f"{who} invited you  fo {word.lower()}"[:80]


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
    join_called: bool = False,
    frozen: bool = False,
) -> None:
    """Join / backrank from an invite even when the hunter is stopped."""
    if frozen or not state.in_realm:
        return
    if brain.mode == "goto":
        return
    if they_followed:
        brain._sync_party(state)
    if invited:
        brain.on_invite(state, send)
    if join_called:
        brain.on_join_call(state, send)
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
    if low in {"invite all", "inv all"}:
        if not state.in_realm:
            brain.next_action = "invite in the realm"
            return "invite", None
        cmds = brain.invite_all_cmds(state)
        if cmds:
            return "invite", "\n".join(cmds)
        if brain.next_action == "already grouped":
            return "invite", None
        brain.arm_invite_all_look(state)
        return "invite", "look"
    if low in {"!join", "join up", "say !join"}:
        if not state.in_realm:
            brain.next_action = "join in the realm"
            return "join", None
        cmds = brain.join_call_cmds(state)
        if not cmds:
            return "join", None
        return "join", "\n".join(cmds)
    if low in {"!rest", "say !rest"}:
        if not state.in_realm:
            brain.next_action = "rest in the realm"
            return "rest", None
        cmds = brain.rest_call_cmds(state)
        if not cmds:
            return "rest", None
        return "rest", "\n".join(cmds)
    if low in {"!heal", "say !heal"}:
        if not state.in_realm:
            brain.next_action = "heal in the realm"
            return "heal", None
        cmds = brain.heal_call_cmds(state)
        if not cmds:
            return "heal", None
        return "heal", "\n".join(cmds)
    if low == "boost":
        if not state.in_realm:
            brain.next_action = "boost after E — sysop tweak, not a license"
            return "boost", None
        return (
            "boost",
            "SYS TWEAK EXPERIENCE 120000\nSYS TWEAK LEVEL 10",
        )
    if low == "stash" or low.startswith("stash "):
        if not state.in_realm:
            brain.next_action = "stash after E — sysop gold, then goto bank"
            return "stash", None
        arg = low[5:].strip()
        n = 500
        if arg:
            if not arg.isdigit() or int(arg) < 1:
                brain.next_action = "stash [1-2000] gold"
                return "stash", None
            n = min(2000, int(arg))
        brain.next_action = f"stash {n} gold"
        return "stash", f"SYS TWEAK GOLD {n}"
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
    # Bare Enter at `[HP=]:` is a blank line to the mud — not an automatic look.
    # (Sending `l` here hid whether timed ticks were alive.)
    return "enter", ""


def character_label(player: dict[str, object]) -> str:
    """Window-title name. BBS user sysop is the klymacks toon."""
    username = str(player.get("username") or "").strip()
    given = str(player.get("given") or "").strip()
    key = username.lower()
    if key in {"klymacks", "sysop"} or given.lower() == "klymacks":
        return "klymacks"
    if given:
        return given[:1].upper() + given[1:] if given[1:] else given.upper()
    if username:
        return username[:1].upper() + username[1:]
    return "new"


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


def ice_wash(width: int, shift: int = 0) -> str:
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
    n = len(motif)
    for i in range(width):
        sgr, ch = motif[(i + shift) % n]
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
    if shown == "aa off":
        shown = "a"
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


def chrome_row(screen: AnsiScreen) -> int:
    """Ice-title row. Parchment wait chrome lifts into the 80×25 band.

    xterm is 80×30; FSD is ~80×25. Rows 26–30 sit under the sheet they
    look at, so the ░ throbber / checking-name tip would hide. Exit: SAVE
    is around row 20 and the curl around 22 — lift stays below those.
    """
    if _realm_on_grid(screen):
        return CHROME_ROW
    if _fsd_parchment(screen) or screen.form_saving:
        return CHROME_ROW - SHEET_CHROME_LIFT
    if _name_wait(screen):
        return CHROME_ROW - SHEET_CHROME_LIFT
    return CHROME_ROW


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
    # Login hint must not stick under FINN'S REALM on the parchment.
    if screen.looks_like_creation():
        phase = creation_phase(screen)
        status = pad_visible(creation_status(screen)[:80], 80)
        if phase in {"checking", "saving"}:
            stats = pad_visible(ice_wash(80, int(time.monotonic() * 8)), 80)
        else:
            stats = pad_visible(f"{ICE_DIM_SGR}{'░' * 80}", 80)
    elif state.in_realm and state.hp is not None and not frozen:
        status = pad_visible(color_status_line(state, brain), 80)
        stats = pad_visible(paint_stats_row(state), 80)
    else:
        status = pad_visible(hint[:80], 80)
        stats = pad_visible(f"{ICE_DIM_SGR}{'░' * 80}", 80)
    if screen.looks_like_creation():
        blob_low = screen.text().lower()
        notice_low = screen.sheet_notice.lower()
        # Stat-cap reject sits under ice chrome — tip is the only place to see it.
        if "may not be higher than" in notice_low:
            tip = screen.sheet_notice[:80]
        else:
            reject = _STAT_REJECT_RE.search(screen.text())
            if reject:
                tip = reject.group(0)[:80]
            elif (
                phase in {"checking", "saving"}
                or "you may not use" in blob_low
                or "please enter a new name" in blob_low
                or "validating your name" in blob_low
                or "validating your name" in notice_low
                or "please wait" in notice_low
            ):
                tip = creation_tip(screen)[:80]
            elif screen.sheet_notice and not _sheet_leak_line(screen.sheet_notice):
                tip = screen.sheet_notice[:80]
            else:
                tip = creation_tip(screen)[:80]
    elif frozen:
        tip = fkey_label(11, style="sheet_tip")
    elif invite_tip(state):
        tip = invite_tip(state)
    elif brain.offer_tip(state.level):
        tip = brain.offer_tip(state.level)
    elif brain.train_holding():
        tip = "train hold  brain paused  you type  F11/Esc live"
    elif brain.bail:
        tip = "friendly fire   logged off   a human has to be at the keys"
    elif realm_needs_look(screen):
        tip = "Enter   the sheet closed — refreshing the room"
    elif state.in_realm:
        tip = realm_fkey_tip(
            hunting=brain.hunting(),
            ambush=brain.f8_label(),
            join=brain.join_label(),
            held=held,
        )
    elif login_pager_nonstop(screen.text()):
        tip = "N nonstop   C page   Q menu"
    elif "[MAJORMUD]" in screen.text() or "Enter the Realm" in screen.text():
        tip = "E enter the realm   H help   X leave MajorMUD"
    elif board_menu_screen(screen.text()):
        tip = "M MajorMUD   E mail   D doors   X exit"
    else:
        tip = "Ctrl-C hangs up"
    top = chrome_row(screen)
    out = bytearray(prefix)
    out += b"\x1b[0m"
    out += _chrome_parchment_foot(screen)
    out += f"\x1b[{top};1H\x1b[K{head}\x1b[0m".encode()
    out += f"\x1b[{top + 1};1H\x1b[K{status}\x1b[0m".encode()
    out += f"\x1b[{top + 2};1H\x1b[K{stats}\x1b[0m".encode()
    if use_local_input(screen, state, sheet_lock):
        shown = realm_bar_text(typed)
        cmd = f"> {shown}"
        out += f"\x1b[{top + 3};1H\x1b[K\x1b[1;32m{cmd:<80}\x1b[0m".encode()
        out += f"\x1b[{top + 4};1H\x1b[K\x1b[0;36m{tip:<80}\x1b[0m".encode()
        col = min(80, 3 + len(shown))
        out += f"\x1b[{top + 3};{col}H\x1b[?25h".encode()
        return bytes(out)
    out += f"\x1b[{top + 3};1H\x1b[K\x1b[0;36m{tip:<80}\x1b[0m".encode()
    if term_rows >= ROWS + CHROME:
        drip = pad_visible(f"{ICE_DIM_SGR}{'░' * 80}", 80)
        out += f"\x1b[{top + 4};1H\x1b[K{drip}\x1b[0m".encode()
    # Lifted sheet chrome must not leave the old 26–30 band on screen.
    for y in range(top + CHROME, ROWS + CHROME + 1):
        out += f"\x1b[{y};1H\x1b[K".encode()
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
        "goto ts     walk to Town Square and stop — also gy, bank, store, sewer",
        "invite all  every player in this room — not roster who already left",
        "!join       leader shouts; F9 invitees follow that shout (no echo)",
        "!rest       party: park on the creek bridge SW of the GY gate",
        "!heal       party: hold the run and heal; !healed / !rested when done",
        "run ts      same walk, skip fights — run pile is the deathpile",
        "train       same as F11 — train hold, brain paused, you type stats",
        "boost       DEMO wall: SYS TWEAK to level 10 (class weapons; needs WCCSYSOP)",
        "stash       SYS TWEAK GOLD 500 on this toon — then goto bank / give",
        "overnight   leave windows up; cleanup save only (auto-resume paused)",
        "F12         intentional logoff — clears resume; no auto-come-back",
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
    klass = str(player.get("class") or "").strip().lower()
    aa = player.get("aa")
    pacer = KeyPacer(paladin=uses_bash_aa(klass, aa))
    play = bool(player.get("auto_play", True)) and auto
    enter_mud = auto and not bool(player.get("bbs_only"))
    clear_lock()
    pilot = Autopilot(player, play, enter_mud=enter_mud) if auto else None
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
        party_leader=modules.campaign_leader(player),
        rank=str(player.get("rank") or ""),
        klass=str(player.get("class") or ""),
        race=str(player.get("race") or ""),
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
    walk = LogoffWalk()
    been_on_board = False
    intentional_logoff = False
    cleanup_seen = False
    ever_in_realm = False
    resume_file = resume_file_for(player)
    me_blob = " ".join(
        str(player.get(key) or "")
        for key in ("username", "given", "character")
    )
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
        nonlocal cleanup_seen, ever_in_realm
        streamed: set[str] = set()
        saw_invite = False
        saw_follow = False
        saw_they_follow = False
        saw_join_call = False

        def take(ev: dict[str, object]) -> None:
            nonlocal saw_invite, saw_follow, saw_they_follow, saw_join_call
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
            elif kind == "join_call":
                saw_join_call = True
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
        if note_cleanup_text(blob) or note_cleanup_text(
            payload.decode("latin-1", "replace")
        ):
            cleanup_seen = True
        for ev in harvest_screen(blob, seen_rows):
            if ev.get("kind") == "experience" and "experience" in streamed:
                continue
            take(ev)
        state.empty_if_look_missed(streamed, blob)
        if (
            "prompt" not in streamed
            and "[HP=" in blob
            and not screen.looks_like_creation()
            and not board_menu_screen(blob)
        ):
            idx = blob.rfind("[HP=")
            for ev in parse_events(blob[idx : idx + 40].split("\n", 1)[0]):
                if ev.get("kind") == "prompt":
                    take(ev)
        # TOP / [MAJORMUD] menus: never keep hunt peeks or Enter→look armed.
        leave_realm_on_board(state, blob)
        if state.in_realm:
            ever_in_realm = True
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
            join_called=saw_join_call,
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

        sock: socket.socket | None = None

        def farewell() -> int:
            if sock is not None:
                try:
                    sock.shutdown(socket.SHUT_RDWR)
                except OSError:
                    pass
            show_signoff(screen, stdin, pending)
            return 0

        # Cleanup-resume auto-exit/reconnect is paused (was causing startup
        # auto-logoff / broken hangup). Inert save/load stays in client.resume;
        # F12 still clears the resume file. Do not walk.start() for cleanup here.
        while True:
            been_on_board = False
            cleanup_seen = False
            walk = LogoffWalk()
            pacer.clear()
            form_filter = FormHoldFilter()
            sheet_lock = False
            sheet_prompt = None
            typed = ""
            history.reset_cursor()
            seen_rows = set()
            state.__dict__.update(WorldState().__dict__)
            if auto:
                pilot = Autopilot(player, play, enter_mud=enter_mud)
                hint = "signing in..."
            else:
                pilot = None
                hint = "Type your username."
            paint_splash(screen, host, port)
            sys.stdout.buffer.write(b"\x1b[2J\x1b[H")
            sys.stdout.buffer.write(screen.render())
            sys.stdout.buffer.flush()

            sock = socket.create_connection((host, port), timeout=8)
            sock.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
            sock.setblocking(False)
            telnet = Telnet(sock)

            try:
                while True:
                    now = time.monotonic()
                    if sheet_lock and realm_thaws_sheet(screen):
                        thaw_sheet()
                    frozen = form_frozen(screen, sheet_lock)
                    paused = play_paused(brain, frozen=frozen)
                    gate.note(in_realm=state.in_realm, frozen=frozen, now=now)
                    settling = gate.quiet(now)
                    create_phase = (
                        creation_phase(screen) if screen.looks_like_creation() else ""
                    )
                    timeout = play_select_timeout(
                        pending=bool(pacer.pending()),
                        hunting=brain.hunting(),
                        in_realm=state.in_realm,
                        paused=paused,
                        settling=settling,
                        remain=gate.remain(now),
                        chrome_pulse=create_phase in {"checking", "saving"},
                    )
                    if walk.active:
                        timeout = 0.05 if timeout is None else min(timeout, 0.05)
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
                            if intentional_logoff or walk.active:
                                if intentional_logoff:
                                    resume_mod.clear(resume_file)
                                return farewell()
                            # Inert save only — no auto logoff / reconnect.
                            if resume_mod.should_persist_disconnect(
                                in_realm=state.in_realm or ever_in_realm,
                                intentional_logoff=intentional_logoff,
                                cleanup_seen=cleanup_seen,
                                been_on_board=been_on_board,
                            ):
                                capture_and_save_resume(
                                    resume_file,
                                    who=me_blob,
                                    state=state,
                                    brain=brain,
                                    reason="cleanup" if cleanup_seen else "disconnect",
                                )
                            if walk.active or been_on_board:
                                return farewell()
                            why = "disconnected  ·  waited too long at login"
                            if pilot is not None and pilot.phase == "blocked":
                                why = pilot.hint()
                            paint_login_notice(screen, why)
                            hint = why
                            _, term_rows = shutil.get_terminal_size(
                                fallback=(80, rows)
                            )
                            sys.stdout.buffer.write(screen.render())
                            sys.stdout.buffer.write(
                                chrome(
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
                            )
                            sys.stdout.buffer.flush()
                            if not state.in_realm:
                                _hold_error()
                            return 0
                        payload = telnet.feed(chunk)
                        raw_text = payload.decode("latin-1", "replace")
                        if note_cleanup_text(raw_text):
                            cleanup_seen = True
                        if (
                            been_on_board
                            and board_logoff_screen(raw_text)
                            and intentional_logoff
                        ):
                            return farewell()
                        # Non-intentional board_logoff must NOT start LogoffWalk
                        # (cleanup-resume paused — false positives auto-x'd clients).
                        if (
                            been_on_board
                            and board_logoff_screen(raw_text)
                            and not intentional_logoff
                            and (state.in_realm or ever_in_realm or cleanup_seen)
                        ):
                            capture_and_save_resume(
                                resume_file,
                                who=me_blob,
                                state=state,
                                brain=brain,
                                reason="board_logoff",
                            )
                        was_sheet = screen.looks_like_creation()
                        was_parchment = _fsd_parchment(screen)
                        hold = form_hold_now(
                            screen,
                            payload,
                            sheet_lock=sheet_lock,
                            in_realm=state.in_realm,
                            train_hold=brain.train_holding(),
                        )
                        paint_mud(
                            screen,
                            payload,
                            hold=hold,
                            filt=form_filter,
                            in_realm=state.in_realm,
                        )
                        ensure_realm_dsr(
                            screen, payload, in_realm=state.in_realm
                        )
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
                        # Trace again after apply so in_realm matches the room.
                        # Skip duplicate empty post-apply when nothing new to say.
                        if state.in_realm and (
                            b"[6n" in payload
                            or b"[6N" in payload
                            or b"[HP=" in payload
                            or b"Also here:" in payload
                            or b"Obvious exits:" in payload
                            or b"You notice " in payload
                        ):
                            _trace_dsr(
                                screen, payload, in_realm=True
                            )
                        now_sheet = screen.looks_like_creation()
                        now_parchment = _fsd_parchment(screen)
                        if now_parchment:
                            freeze_sheet()
                        elif was_parchment:
                            thaw_sheet()
                            form_filter.reset()
                            release_after_sheet(
                                brain, was_sheet=was_sheet, now_sheet=now_sheet
                            )
                            if sheet_needs_resume(was_sheet, now_sheet, payload):
                                # Bare Enter — Statline Full brief reprint (not look).
                                pacer.push_text("")
                                hint = "enter  room"
                        elif sheet_lock and (
                            realm_thaws_sheet(screen) or not now_parchment
                        ):
                            thaw_sheet()
                            form_filter.reset()
                        if session_on_board(screen.text(), in_realm=state.in_realm):
                            been_on_board = True
                        if (
                            been_on_board
                            and board_logoff_screen(screen.text())
                            and intentional_logoff
                        ):
                            return farewell()

                    if stdin in readable:
                        while True:
                            key = read_key(stdin, pending)
                            if key is None:
                                break
                            if key == b"":
                                continue
                            if key == KEY_ESC:
                                hint, typed = handle_escape_key(
                                    brain, pacer, typed=typed
                                )
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
                                intentional_logoff = True
                                brain.clear_resume_intent()
                                resume_mod.clear(resume_file)
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
                                    if kind in (
                                        "hunt",
                                        "stop",
                                        "aa",
                                        "goto",
                                        "boost",
                                        "stash",
                                        "invite",
                                        "join",
                                        "rest",
                                        "heal",
                                    ):
                                        hint = f"{brain.mode}  ·  {brain.next_action}"
                                        if kind == "boost":
                                            hint = (
                                                brain.next_action
                                                if not mud
                                                else "boost  ·  SYS TWEAK exp/level"
                                            )
                                        if kind == "stash":
                                            hint = (
                                                brain.next_action
                                                if not mud
                                                else "stash  ·  SYS TWEAK gold"
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
                            note_form_enter(screen, key)
                            if screen.form_saving:
                                brain.cancel_train()
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
                    if sheet_lock and realm_thaws_sheet(screen):
                        thaw_sheet()
                    frozen = form_frozen(screen, sheet_lock)
                    paused = play_paused(brain, frozen=frozen)
                    now = time.monotonic()
                    gate.note(in_realm=state.in_realm, frozen=frozen, now=now)
                    settling = gate.quiet(now)
                    if (
                        cleanup_seen
                        and not intentional_logoff
                        and not walk.active
                        and state.in_realm
                        and been_on_board
                    ):
                        # Inert save only — do not auto x-out for cleanup resume.
                        capture_and_save_resume(
                            resume_file,
                            who=me_blob,
                            state=state,
                            brain=brain,
                            reason="cleanup",
                        )
                    if walk.active:
                        walk.tick(
                            screen.text(),
                            in_realm=state.in_realm,
                            in_combat=bool(state.in_combat),
                            pacer=pacer,
                            now=now,
                        )
                        hint = walk.hint
                        if walk.done:
                            if intentional_logoff:
                                resume_mod.clear(resume_file)
                            return farewell()
                    elif (
                        in_play
                        and not paused
                        and not brain.bail
                        and (state.invited_by or state.following or state.join_call_by)
                    ):
                        maybe_auto_party(
                            state,
                            brain,
                            brain_send,
                            invited=bool(state.invited_by),
                            followed=bool(state.following),
                            join_called=bool(state.join_call_by),
                            frozen=paused,
                        )
                    if play_may_tick(
                        in_play=in_play,
                        walk_active=walk.active,
                        typed=typed,
                        paused=paused,
                        settling=settling,
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
                        if brain.bail and not walk.active:
                            hint = f"friendly fire — {brain.bail}"
                            pacer.clear()
                            walk.start(brain, now)
                        elif pry.stuck:
                            hint = "type the full item name"
                        elif state.in_realm and not brain.bail:
                            hint = f"{brain.mode}  ·  {brain.next_action}"
                    elif settling and state.in_realm and not brain.bail:
                        hint = "settling in..."

                    outgoing = pacer.take(now)
                    if outgoing is not None:
                        if not walk.active and form_blocks_line(
                            outgoing, frozen=form_frozen(screen, sheet_lock)
                        ):
                            continue
                        telnet.send(outgoing)

                    create_phase = (
                        creation_phase(screen) if screen.looks_like_creation() else ""
                    )
                    wait_tick = (
                        int(time.monotonic() * 8)
                        if create_phase in {"checking", "saving"}
                        else 0
                    )
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
                        create_phase,
                        wait_tick,
                        screen.form_saving,
                        screen.sheet_notice,
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
                if sock is not None:
                    try:
                        sock.close()
                    except OSError:
                        pass
                    sock = None
            return 0
    finally:
        termios.tcsetattr(stdin, termios.TCSADRAIN, old)
    return 0


def build_parser() -> argparse.ArgumentParser:
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
    parser.add_argument(
        "--no-auto",
        action="store_true",
        help="type BBS login yourself (not hunt — see --no-auto-play)",
    )
    parser.add_argument(
        "--auto-play",
        dest="auto_play_cli",
        action="store_const",
        const=True,
        default=None,
        help="start hunt/kit without waiting for F7 (overrides config auto_play)",
    )
    parser.add_argument(
        "--no-auto-play",
        dest="auto_play_cli",
        action="store_const",
        const=False,
        help="do not start hunt/kit until F7 (overrides config auto_play)",
    )
    parser.add_argument(
        "--bbs",
        action="store_true",
        help="log into Worldgroup only — do not press M into MajorMUD",
    )
    parser.add_argument(
        "--plain",
        action="store_true",
        help="scrolling text, no BBS splash/footer (Finn's Mud pipe)",
    )
    return parser


def apply_auto_play_override(
    player: dict[str, object], cli: bool | None
) -> None:
    """CLI --auto-play / --no-auto-play overrides json. None keeps config."""
    if cli is not None:
        player["auto_play"] = bool(cli)


def main() -> int:
    parser = build_parser()
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
    apply_auto_play_override(player, args.auto_play_cli)
    auto = bool(player.get("auto_login", True)) and not args.no_auto
    if args.bbs:
        auto = True
        player["bbs_only"] = True
        player["auto_play"] = False
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
