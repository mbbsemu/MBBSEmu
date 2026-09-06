"""Named hunt runs: walk in via the map, then loop on-site.

F7 still only starts and stops. Pick the destination on the `>` bar
(`hunt gy`, `hunt sewer`, `hunt list`) or with player.json `"hunt"`.
Add more rows here as new tapes and pins land.
"""

from __future__ import annotations

from dataclasses import dataclass

from .megapath import DEFAULT_SEWER


@dataclass(frozen=True)
class HuntRun:
    """One grind: approach pin, on-site loop, optional MegaMud tape."""

    id: str
    name: str
    aliases: tuple[str, ...]
    approach: str
    loop: str
    tape: str = ""
    torch: bool = False


RUNS: tuple[HuntRun, ...] = (
    HuntRun(
        "gy",
        "Silvermere graveyard",
        ("gy", "graveyard", "grave", "silvermere"),
        "farm",
        "graveyard",
    ),
    HuntRun(
        "sewer-east",
        "Silvermere sewers (east)",
        ("sewer", "sewers", "sewer-east", "pipes", "east"),
        "sewer",
        "sewer",
        DEFAULT_SEWER,
        torch=True,
    ),
    HuntRun(
        "arena",
        "Newhaven arena",
        ("arena", "pit", "newhaven"),
        "farm",
        "pit",
    ),
)

DEFAULT = RUNS[0]

_INDEX: dict[str, HuntRun] = {}
for _run in RUNS:
    _INDEX[_run.id] = _run
    for _alias in _run.aliases:
        _INDEX[_alias] = _run


def _key(text: str) -> str:
    return (text or "").strip().lower().replace("_", "-").replace(" ", "-")


def parse(text: str) -> HuntRun | None:
    """Match an id or alias. Empty string is the default graveyard run."""
    raw = _key(text)
    if not raw:
        return DEFAULT
    return _INDEX.get(raw)


def list_runs() -> str:
    """Short catalog for the chrome `next_action` line."""
    bits = []
    for run in RUNS:
        extra = [a for a in run.aliases if a != run.id]
        alias = f" ({', '.join(extra[:2])})" if extra else ""
        bits.append(f"{run.id}{alias}")
    return "runs: " + ", ".join(bits)
