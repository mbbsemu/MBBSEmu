"""MegaMud / Winterhawk .mp path loader — seed for klymacks Finn's maps.

`.mp` lines look like `ROOMHASH:FLAGS:step`. Steps are compass (n/e/s/w/ne…),
`st` (straight = repeat last), or specials. Room hashes are MegaMud's; we walk
the step list and learn live titles into realm_map.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
VENDOR_PATHS = ROOT / "vendor" / "megamud-paths"
ALLPATHS = VENDOR_PATHS / "allpaths"
KLYMACKS_MAPS = ROOT / "data" / "klymacks-maps"

# Default Finn's Realm sewer grind — Winterhawk east half, renamed for us.
DEFAULT_SEWER = "silvermere-sewers-east.mp"

_CARDINALS = frozenset(
    {"n", "s", "e", "w", "u", "d", "ne", "nw", "se", "sw"}
)


@dataclass
class MegaPath:
    """One recorded MegaMud path."""

    title: str = ""
    author: str = ""
    start: str = ""
    end: str = ""
    start_code: str = ""
    end_code: str = ""
    steps: list[str] = field(default_factory=list)
    source: str = ""

    def resolve(self, index: int, last_step: str = "") -> str | None:
        """Step at index, expanding `st` to the previous compass move."""
        if not self.steps:
            return None
        i = index % len(self.steps)
        step = self.steps[i]
        if step == "st":
            prev = (last_step or "").strip().lower()
            if prev in _CARDINALS:
                return prev
            # Walk backward for the last real compass on the tape.
            for j in range(i - 1, -1, -1):
                cand = self.steps[j]
                if cand in _CARDINALS:
                    return cand
            return None
        return step


def _parse_header(line: str) -> tuple[str, str]:
    """[Title][Author] → title, author."""
    parts = []
    buf = ""
    depth = 0
    for ch in line.strip():
        if ch == "[":
            if depth == 0:
                buf = ""
            depth += 1
            continue
        if ch == "]":
            depth = max(0, depth - 1)
            if depth == 0 and buf:
                parts.append(buf)
            continue
        if depth:
            buf += ch
    title = parts[0] if parts else ""
    author = parts[1] if len(parts) > 1 else ""
    return title, author


def _parse_place(line: str) -> tuple[str, str, str]:
    """[GAV1:Sewers:Sewer Tunnel, Junction-1 766] → code, area, title."""
    raw = (line or "").strip()
    if raw.startswith("[") and raw.endswith("]"):
        raw = raw[1:-1]
    if "-" in raw and raw.rsplit("-", 1)[-1].strip()[:1].isdigit():
        raw = raw.rsplit("-", 1)[0]
    parts = [p.strip() for p in raw.split(":")]
    if len(parts) >= 3:
        return parts[0], parts[1], ":".join(parts[2:]).strip()
    if len(parts) == 2:
        return parts[0], "", parts[1]
    return "", "", raw.strip()


def _parse_start(line: str) -> str:
    """[GAV1:Sewers:Sewer Tunnel, Junction (below TS)-1 766] → room title."""
    return _parse_place(line)[2]


def _place_key(title: str) -> str:
    return " ".join((title or "").lower().replace(",", " ").split())


def load_mp(path: Path | str) -> MegaPath:
    """Load a MegaMud .mp path file."""
    p = Path(path)
    text = p.read_text(encoding="latin-1", errors="replace")
    lines = [ln.strip() for ln in text.splitlines() if ln.strip()]
    title = author = start = end = start_code = end_code = ""
    steps: list[str] = []
    places = 0
    for i, line in enumerate(lines):
        if line.startswith("[") and line.endswith("]"):
            if i == 0 and line.count("[") >= 2:
                title, author = _parse_header(line)
                continue
            if ":" in line:
                code, _area, place = _parse_place(line)
                if place and places == 0:
                    start, start_code = place, code
                    places = 1
                    continue
                if place and places == 1 and not end:
                    end, end_code = place, code
                    places = 2
                    continue
            continue
        parts = line.split(":")
        if len(parts) < 3:
            continue
        # Meta line: hash:hash:count:-1:0:::
        if parts[2].isdigit() and len(parts) >= 4 and parts[3] in {"-1", "0"}:
            continue
        step = parts[-1].strip().lower()
        if not step:
            continue
        if step in _CARDINALS or step == "st" or " " in step:
            steps.append(step)
    return MegaPath(
        title=title or p.stem,
        author=author,
        start=start,
        end=end,
        start_code=start_code,
        end_code=end_code,
        steps=steps,
        source=str(p),
    )


def resolve_map_file(name: str = DEFAULT_SEWER) -> Path | None:
    """Prefer customized klymacks maps, then vendor Winterhawk seeds."""
    for base in (KLYMACKS_MAPS, VENDOR_PATHS):
        cand = base / name
        if cand.is_file():
            return cand
    # Fall back to Winterhawk east half filename in vendor.
    for alt in (
        "GAV1LOO2.mp",
        "GAV1LOOP.mp",
        "GAV1LOO3.mp",
        "GAV1LOO2.MP",
        "GAV1LOOP.MP",
    ):
        for base in (VENDOR_PATHS, ALLPATHS):
            cand = base / alt
            if cand.is_file():
                return cand
    return None


def load_sewer_path(name: str = DEFAULT_SEWER) -> MegaPath:
    path = resolve_map_file(name)
    if path is None:
        return MegaPath(title="empty")
    return load_mp(path)


def seed_klymacks_maps() -> Path:
    """Copy Winterhawk east half into data/klymacks-maps as our starting tape."""
    KLYMACKS_MAPS.mkdir(parents=True, exist_ok=True)
    dest = KLYMACKS_MAPS / DEFAULT_SEWER
    src = VENDOR_PATHS / "GAV1LOO2.mp"
    if dest.is_file():
        return dest
    if not src.is_file():
        return dest
    raw = src.read_text(encoding="latin-1", errors="replace")
    # Retitle for Finn's Realm / klymacks ownership; keep the step tape.
    lines = raw.splitlines()
    if lines:
        lines[0] = "[Finn's Realm Silvermere Sewers (east)-1 766][klymacks]"
    dest.write_text("\n".join(lines) + "\n", encoding="latin-1")
    readme = KLYMACKS_MAPS / "README.md"
    if not readme.is_file():
        readme.write_text(
            "# klymacks maps (Finn's Realm)\n\n"
            "Seeded from Winterhawk MegaMud `.mp` paths, then customized for "
            "Finn's Realm DEMO. Do not treat vendor MegaMud packs as gospel — "
            "verify loops live, then edit these files.\n",
            encoding="utf-8",
        )
    return dest


_CATALOG: "Catalog | None" = None


def _mp_files() -> list[Path]:
    """klymacks overrides vendor root, which overrides the allpaths pack."""
    found: dict[str, Path] = {}
    for base in (ALLPATHS, VENDOR_PATHS, KLYMACKS_MAPS):
        if not base.is_dir():
            continue
        for p in base.iterdir():
            if p.is_file() and p.suffix.lower() == ".mp":
                found[p.stem.lower()] = p
    return list(found.values())


def _loop_for_dests(path: MegaPath, dests: list[str]) -> bool:
    """A start-only tape is a loop. Never fire Town Square loops at the GY."""
    start = (path.start or "").lower()
    if "town square" in start or "fountain" in start:
        return False
    if any("graveyard" in (d or "").lower() for d in dests):
        return "graveyard" in start or start in {"bridge", "graveyard bridge"}
    return False


def _here(path: MegaPath, room: str) -> bool:
    key = _place_key(room)
    if not key:
        return False
    if path.start and _place_key(path.start) == key:
        return True
    if path.start_code and path.start_code.lower() == key:
        return True
    if key == "bridge" and _place_key(path.start) == "graveyard bridge":
        return True
    if key == "graveyard bridge" and _place_key(path.start) == "bridge":
        return True
    return False


def _want(path: MegaPath, dests: list[str]) -> bool:
    keys = {_place_key(d) for d in dests if d}
    if path.end and _place_key(path.end) in keys:
        return True
    codes = {d.strip().lower() for d in dests if d}
    if path.end_code and path.end_code.lower() in codes:
        return True
    return False


class Catalog:
    """All vendor + klymacks MegaMud tapes, keyed by start/end titles."""

    def __init__(self) -> None:
        self.paths: list[MegaPath] = []
        for p in _mp_files():
            try:
                self.paths.append(load_mp(p))
            except OSError:
                continue

    def goto_step(
        self,
        room: str,
        dests: list[str],
        exits: list[str] | None = None,
    ) -> str | None:
        """First step of the shortest tape from this room toward dests."""
        from .paths import is_special_step

        if not room or not dests:
            return None
        hits: list[MegaPath] = []
        for path in self.paths:
            if not path.steps:
                continue
            if not _here(path, room):
                continue
            if path.end and _want(path, dests):
                hits.append(path)
            elif not path.end and _loop_for_dests(path, dests):
                hits.append(path)
        hits.sort(key=lambda p: len(p.steps))
        for path in hits:
            step = path.steps[0]
            if not step or step == "st":
                continue
            if (
                exits
                and step not in exits
                and not is_special_step(step)
            ):
                continue
            return step
        return None


def catalog() -> Catalog:
    global _CATALOG
    if _CATALOG is None:
        _CATALOG = Catalog()
    return _CATALOG


def goto_step(
    room: str,
    dests: list[str],
    exits: list[str] | None = None,
) -> str | None:
    return catalog().goto_step(room, dests, exits)
