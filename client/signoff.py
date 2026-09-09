"""klymacks sysop sign-off. F12 closer — not the connect splash."""

from __future__ import annotations

from datetime import datetime
from pathlib import Path
from typing import Any

from client.splash import (
    blit_tdf,
    load_color_tdf,
    tdf_path,
    word_size,
    _ice_wash,
    _put,
    _puts,
)

HERE = Path(__file__).resolve().parent
PREVIEW = HERE / "splash-previews"


def signed_off_line(when: datetime | None = None) -> str:
    """Local wall-clock line under the yours-truly art."""
    stamp = (when or datetime.now()).strftime("%a %Y-%m-%d  %H:%M")
    return f"signed off  {stamp}"


def _plate(screen: Any) -> None:
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
    _puts(screen, 0, 2, " FINN'S REALM ", 7, 0, True)
    _puts(screen, 0, 16, " 1.11p ", 6, 0, False)
    _puts(screen, 0, 24, " klymacks ", 7, 0, True)
    tag = " sysop "
    _puts(screen, 0, cols - len(tag) - 2, tag, 6, 0, True)


def _clear(screen: Any) -> None:
    for y in range(screen.rows):
        for x in range(screen.cols):
            _put(screen, y, x, " ", 0, 0, False)


def _center(screen: Any, word: str, width: int) -> int:
    return max(0, (screen.cols - width) // 2)


def paint(screen: Any, *, when: datetime | None = None) -> None:
    """80×25 ice tag: klymacks + yours truly art; wall-clock under it."""
    ice = load_color_tdf(tdf_path("font23.tdf"))
    juice = load_color_tdf(tdf_path("juicex.tdf"))
    name = "klymacks"
    nw, nh = word_size(ice, name, 0)
    yours_w, yours_h = word_size(juice, "yours", 1)
    truly_w, truly_h = word_size(juice, "truly", 1)
    gap = 3
    tag_w = yours_w + gap + truly_w
    tag_h = max(yours_h, truly_h)
    y1 = 4
    x1 = _center(screen, name, nw)
    y2 = 15
    x2 = _center(screen, "yours truly", tag_w)
    _clear(screen)
    _plate(screen)
    _ice_wash(screen, y1, x1, nw, nh)
    blit_tdf(screen, y1, x1, ice, name, 0)
    _ice_wash(screen, y2, x2, tag_w, tag_h)
    blit_tdf(screen, y2, x2, juice, "yours", 1)
    blit_tdf(screen, y2, x2 + yours_w + gap, juice, "truly", 1)
    stamp = signed_off_line(when)
    _puts(screen, 22, _center(screen, stamp, len(stamp)), stamp, 6, 0, False)
    _put(screen, 22, 8, "·", 6, 0, True, overlay=False)
    _put(screen, 22, 71, "·", 6, 0, True, overlay=False)


class _Cell:
    __slots__ = ("ch", "fg", "bg", "bold")

    def __init__(self) -> None:
        self.ch = " "
        self.fg = 7
        self.bg = 0
        self.bold = False


class _Screen:
    def __init__(self) -> None:
        self.rows = 25
        self.cols = 80
        self.buf = [[_Cell() for _ in range(self.cols)] for _ in range(self.rows)]


def write_previews(dest: Path = PREVIEW) -> tuple[Path, Path]:
    """CP437 + xterm dumps for `splash-previews/show.sh`."""
    dest.mkdir(parents=True, exist_ok=True)
    screen = _Screen()
    paint(screen)
    cp437 = dest / "signoff-klymacks.ans"
    utf8 = dest / "signoff-klymacks.utf8.ans"
    raw = bytearray()
    for y in range(screen.rows):
        raw += f"\x1b[{y + 1};1H".encode()
        prev = None
        for cell in screen.buf[y]:
            key = (cell.fg, cell.bg, cell.bold)
            if key != prev:
                parts = ["0"]
                if cell.bold:
                    parts.append("1")
                parts.append(str(30 + cell.fg))
                if cell.bg:
                    parts.append(str(40 + cell.bg))
                raw += f"\x1b[{';'.join(parts)}m".encode()
                prev = key
            raw += cell.ch.encode("cp437", "replace")
        raw += b"\x1b[0m"
    cp437.write_bytes(bytes(raw) + b"\x1b[0m\x1b[25;1H\n")
    text = []
    for y in range(screen.rows):
        text.append("".join(c.ch for c in screen.buf[y]))
    utf8.write_text("\n".join(text) + "\n", encoding="utf-8")
    return cp437, utf8


if __name__ == "__main__":
    paths = write_previews()
    print(" ".join(str(p) for p in paths))
