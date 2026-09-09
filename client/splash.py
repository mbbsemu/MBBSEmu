"""Finn's Realm connect splash. Unique piece — not a font dump."""

from __future__ import annotations

import struct
from pathlib import Path
from typing import Any

# 0-indexed. Graffiti lives in 0–19. Row 20 is the ice rule. Prompts 21–23
# (rows 22–24) sit under the piece so Username / disconnect never paint the
# letters or run into chrome at row 26. Row 25 stays a gutter.
SPLASH_DOCK_RULE = 20
SPLASH_DOCK_TOP = 21
SPLASH_DOCK_BOT = 24

HERE = Path(__file__).resolve().parent
CHARLIST = (
    "!\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ"
    "[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~"
)
MAGIC = b"\x13TheDraw FONTS file"
_TD_TO_ANSI = (0, 4, 2, 6, 1, 5, 3, 7)
# Infinity ice: white / grey / cyan / dark blue / black. No green, no acid.
_ICE_ANSI = (0, 4, 6, 7, 4, 4, 6, 7)


class Glyph:
    __slots__ = ("width", "height", "cells")

    def __init__(self, width: int, height: int, cells: list[tuple[str, int]]) -> None:
        self.width = width
        self.height = height
        self.cells = cells


class ColorFont:
    def __init__(self, name: str, spacing: int, glyphs: dict[str, Glyph]) -> None:
        self.name = name
        self.spacing = spacing
        self.glyphs = glyphs

    def lookup(self, ch: str) -> Glyph | None:
        return self.glyphs.get(ch)


def _tdf_dir() -> Path:
    for path in (HERE / "tdf", HERE / "splash-previews" / "tdf"):
        if (path / "font30.tdf").is_file():
            return path
    raise FileNotFoundError("shard font files missing under client/tdf")


def tdf_path(name: str) -> Path:
    for folder in (HERE / "tdf", HERE / "splash-previews" / "tdf"):
        path = folder / name
        if path.is_file():
            return path
    raise FileNotFoundError(f"TheDraw font missing: {name}")


def load_color_tdf(path: Path) -> ColorFont:
    data = path.read_bytes()
    if data[:19] != MAGIC:
        raise ValueError(f"not a TheDraw font: {path}")
    namelen = data[24]
    name = data[25 : 25 + namelen].decode("latin1", "replace").rstrip("\x00")
    if data[41] != 2:
        raise ValueError(f"{path} is not a color TheDraw font")
    spacing = data[42]
    offsets = struct.unpack_from("<94H", data, 45)
    payload = data[233:]
    glyphs: dict[str, Glyph] = {}
    for i, ch in enumerate(CHARLIST):
        off = offsets[i]
        if off == 0xFFFF or off >= len(payload):
            continue
        glyphs[ch] = _read_glyph(payload, off)
    return ColorFont(name, spacing, glyphs)


def _read_glyph(payload: bytes, off: int) -> Glyph:
    width = payload[off]
    height = payload[off + 1]
    p = off + 2
    grid = [(" ", 0)] * (width * height)
    row = 0
    col = 0
    while p < len(payload) and payload[p] != 0:
        ch = payload[p]
        p += 1
        if ch == 0x0D:
            row += 1
            col = 0
            if row >= height:
                break
            continue
        if p >= len(payload):
            break
        color = payload[p]
        p += 1
        if ch < 0x20:
            ch = 0x20
        if 0 <= row < height and 0 <= col < width:
            grid[row * width + col] = (bytes([ch]).decode("cp437"), color)
        col += 1
    return Glyph(width, height, grid)


def pack_color(color: int) -> tuple[int, int, bool]:
    fg16 = color & 0x0F
    bg = (color >> 4) & 0x07
    return (
        _ICE_ANSI[_TD_TO_ANSI[fg16 & 7]],
        _ICE_ANSI[_TD_TO_ANSI[bg]],
        fg16 >= 8,
    )


def word_size(font: ColorFont, word: str, spacing: int) -> tuple[int, int]:
    present = [g for g in (font.lookup(ch) for ch in word) if g is not None]
    if not present:
        return 0, 0
    width = sum(g.width for g in present) + spacing * max(0, len(present) - 1)
    height = max(g.height for g in present)
    return width, height


def _put(
    screen: Any,
    y: int,
    x: int,
    ch: str,
    fg: int = 7,
    bg: int = 0,
    bold: bool = False,
    *,
    overlay: bool = True,
) -> None:
    if y < 0 or y >= screen.rows or x < 0 or x >= screen.cols or ch == "":
        return
    cell = screen.buf[y][x]
    if not overlay and cell.ch not in {" ", "░", "▒", "·", "*"}:
        return
    cell.ch = ch
    cell.fg = fg
    cell.bg = bg
    cell.bold = bold


def _puts(
    screen: Any, y: int, x: int, text: str, fg: int, bg: int = 0, bold: bool = False
) -> None:
    for i, ch in enumerate(text):
        _put(screen, y, x + i, ch, fg, bg, bold)


def blit_tdf(
    screen: Any, y0: int, x0: int, font: ColorFont, word: str, spacing: int
) -> None:
    x = x0
    for ch in word:
        gl = font.lookup(ch)
        if gl is None:
            continue
        for row in range(gl.height):
            for col in range(gl.width):
                mark, color = gl.cells[row * gl.width + col]
                if mark == " " and (color & 0x0F) == 0:
                    continue
                fg, bg, bold = pack_color(color)
                _put(screen, y0 + row, x + col, mark, fg, bg, bold, overlay=True)
        x += gl.width + spacing


def _sauce_plate(screen: Any) -> None:
    cols = screen.cols
    for x in range(cols):
        edge = min(x, cols - 1 - x)
        if edge < 3:
            ch = "░"
        elif edge < 8:
            ch = "▒"
        else:
            ch = "▀"
        fg = 4 if edge < 10 else (6 if edge < 22 else 7)
        _put(screen, 0, x, ch, fg, 0, edge >= 8)
    label = " FINN'S REALM "
    pack = " 1.11p "
    tag = " klymacks "
    _puts(screen, 0, 2, label, 7, 0, True)
    _puts(screen, 0, 2 + len(label), pack, 6, 0, False)
    _puts(screen, 0, cols - len(tag) - 2, tag, 6, 0, True)


def _ice_wash(screen: Any, y: int, x: int, w: int, h: int) -> None:
    x0 = max(0, x - 1)
    x1 = min(screen.cols, x + w + 1)
    y0 = min(19, max(0, y + max(2, h // 3)))
    y1 = min(19, y + h + 1)
    mid = (x0 + x1) // 2
    for yy in range(y0, y1 + 1):
        t = (yy - y0) / max(1, y1 - y0)
        inset = int((x1 - x0) * t * 0.16)
        left, right = x0 + inset, x1 - inset
        for xx in range(left, right):
            if screen.buf[yy][xx].ch not in {" ", "░", "▒", "·"}:
                continue
            edge = min(xx - left, right - 1 - xx)
            dist = abs(xx - mid) / max(1, (right - left) / 2)
            if t < 0.4 and edge > 3:
                ch, bold = "▓", True
            elif t < 0.7 or edge > 2:
                ch, bold = "▒", False
            else:
                ch, bold = "░", False
            if dist > 0.88:
                ch, bold = "░", False
            _put(screen, yy, xx, ch, 4, 0, bold, overlay=False)


def _ice_hook(screen: Any) -> None:
    """Hand flourish under the right side — Infinity-style ice tail, not a font."""
    marks = (
        (16, 62, "▄", 4, True),
        (16, 63, "▄", 4, True),
        (17, 64, "▀", 4, False),
        (17, 65, "▄", 4, True),
        (17, 66, "▓", 4, False),
        (18, 67, "▀", 4, True),
        (18, 68, "▄", 6, True),
        (18, 69, "·", 7, True),
        (19, 66, "▀", 4, False),
        (19, 67, "▀", 4, False),
    )
    for y, x, ch, fg, bold in marks:
        _put(screen, y, x, ch, fg, 0, bold, overlay=False)


def _sparkles(screen: Any) -> None:
    marks = (
        (1, 6, "·"),
        (1, 36, "+"),
        (1, 74, "·"),
        (2, 70, "▀"),
        (11, 4, "·"),
        (11, 76, "+"),
        (19, 8, "·"),
    )
    for y, x, ch in marks:
        if screen.buf[y][x].ch == " ":
            _put(screen, y, x, ch, 6, 0, True, overlay=False)


def _ice_ticks(screen: Any) -> None:
    """Two quiet drips off the plate — nod to G/0, not a throw-up."""
    _put(screen, 1, 3, "▄", 6, 0, True, overlay=False)
    _put(screen, 2, 3, "▓", 4, 0, False, overlay=False)
    _put(screen, 1, 76, "▄", 6, 0, True, overlay=False)


def _login_dock(screen: Any) -> None:
    """Ice rule + blank prompt band. Sign-in and hangup sit here, not on FINNS."""
    cols = screen.cols
    y = SPLASH_DOCK_RULE
    for x in range(cols):
        edge = min(x, cols - 1 - x)
        if edge < 4:
            ch = "░"
        elif edge < 12:
            ch = "▒"
        else:
            ch = "▀"
        fg = 4 if edge < 14 else 6
        _put(screen, y, x, ch, fg, 0, edge >= 12)
    for yy in range(SPLASH_DOCK_TOP, min(SPLASH_DOCK_BOT, screen.rows)):
        for x in range(cols):
            _put(screen, yy, x, " ", 0, 0, False)


def paint(screen: Any, host: str, port: int, *, kind: str = "client") -> None:
    fonts = _tdf_dir()
    shards = load_color_tdf(fonts / "font30.tdf")
    razors = load_color_tdf(fonts / "razor2.tdf")
    finn = "FINNS"
    realm = "REALM"
    fw, fh = word_size(shards, finn, 0)
    rw, rh = word_size(razors, realm, 1)
    y1, y2 = 1, 10
    x1 = max(0, (screen.cols - fw) // 2)
    x2 = max(0, (screen.cols - rw) // 2)
    _sauce_plate(screen)
    _ice_wash(screen, y1, x1, fw, fh)
    _ice_wash(screen, y2, x2, rw, rh)
    blit_tdf(screen, y1, x1, shards, finn, 0)
    blit_tdf(screen, y2, x2, razors, realm, 1)
    _ice_hook(screen)
    _sparkles(screen)
    _ice_ticks(screen)
    _login_dock(screen)
    if kind == "board":
        return
    _puts(screen, SPLASH_DOCK_TOP, 2, "connecting", 6, 0, False)
    addr = f"{host}:{port}"
    _puts(screen, SPLASH_DOCK_TOP, screen.cols - len(addr) - 2, addr, 4, 0, False)
