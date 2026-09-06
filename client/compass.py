"""How to walk: one step, then the next once the tile reprints.

Sneak is a compass skill: `sn` only when the tile is empty of everyone
except your party. A stranger PC or a named NPC (Corwyn, Helfgrim, a
guard) blocks it — combat and a living lop do too.

Gate: send the dir → wait for a new [HP=] plus Obvious exits (or a
new room title). That is the landing. `look` only if the walk never
reprinted (blocked door, timeout). Same-title halls still count when
exits bump `travel_seq`.
"""

from __future__ import annotations

import time

from . import paths

# If the look never reprints exits, ask again. The beat itself is the
# client's KEY_GAP between the step and this look.
CONFIRM_WAIT = 3.0

MOVE = frozenset(
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
    }
) | paths.SPECIAL_STEPS


def is_move(text: str) -> bool:
    raw = (text or "").strip().lower()
    if raw in MOVE:
        return True
    parts = raw.split()
    if paths.is_unlatch_step(raw):
        return True
    return (
        len(parts) == 3
        and parts[0] == "drag"
        and parts[2] in {"n", "s", "e", "w", "u", "d"}
    )


def same_toon(left: str, right: str) -> bool:
    """Matt / matt / Matt the Paladin — party mates, not strangers."""
    a = left.strip().lower().strip(".,!;:")
    b = right.strip().lower().strip(".,!;:")
    if not a or not b:
        return False
    if a == b:
        return True
    a_parts = [p.strip(".,!;:") for p in a.split() if p.strip(".,!;:")]
    b_parts = [p.strip(".,!;:") for p in b.split() if p.strip(".,!;:")]
    return a in b_parts or b in a_parts


def in_party(name: str, party: set[str] | list[str]) -> bool:
    return any(same_toon(name, mate) for mate in party if mate and mate.strip())


def strangers_in(occupants: list[str], party: set[str] | list[str]) -> list[str]:
    """People in the room who are not us / leader / alts / followers."""
    return [name for name in occupants if not in_party(name, party)]


class Compass:
    """Check-gate for walking and sneaking."""

    def __init__(self) -> None:
        self.pending = ""
        self.phase = ""
        self.from_room = ""
        self.from_exits: tuple[str, ...] = ()
        self.prompt = 0
        self.travel = 0
        self.look_travel = 0
        self.sent_at = 0.0
        self.last_step = ""
        self.failed = False

    def reset(self) -> None:
        self.pending = ""
        self.phase = ""
        self.from_room = ""
        self.from_exits = ()
        self.prompt = 0
        self.travel = 0
        self.look_travel = 0
        self.sent_at = 0.0
        self.last_step = ""
        self.failed = False

    def can_sneak(
        self,
        occupants: list[str],
        party: set[str] | list[str],
        *,
        combat: bool = False,
        has_lop: bool = False,
    ) -> bool:
        """`sn` only when the tile is ours. Anyone else — PC or named NPC — blocks."""
        if combat or has_lop:
            return False
        return not strangers_in(occupants, party)

    def strangers(
        self, occupants: list[str], party: set[str] | list[str]
    ) -> list[str]:
        return strangers_in(occupants, party)

    def see(
        self,
        room: str,
        prompt_seq: int,
        *,
        blocked: bool = False,
        travel_seq: int = 0,
        exits: list[str] | None = None,
        now: float | None = None,
    ) -> str:
        """Tick start. `ok` walk, `wait` hold, `look` only if the step never landed."""
        stamp = time.monotonic() if now is None else now
        if blocked:
            self._fail()
            return "look"
        if self.phase == "peeking":
            if self._landed(room, travel_seq):
                self._confirm(room, exits)
                return "ok"
            if stamp - self.sent_at < CONFIRM_WAIT:
                return "wait"
            self._fail()
            return "look"
        if not self.pending:
            return "ok"
        if prompt_seq <= self.prompt:
            return "wait"
        if self._landed(room, travel_seq):
            self._confirm(room, exits)
            return "ok"
        self.phase = "peeking"
        self.look_travel = travel_seq
        self.sent_at = stamp
        return "look"

    def _landed(self, room: str, travel_seq: int) -> bool:
        if travel_seq > self.travel:
            return True
        if paths.is_unlatch_step(self.pending):
            # Bash / picklock stays in the room. Next tick walks through.
            return True
        here = (room or "").strip()
        return bool(here and self.from_room and here != self.from_room)

    def note(
        self,
        step: str,
        room: str,
        prompt_seq: int,
        now: float | None = None,
        *,
        travel_seq: int = 0,
        exits: list[str] | None = None,
    ) -> None:
        raw = (step or "").strip().lower()
        self.pending = raw
        self.phase = "moving"
        self.from_room = (room or "").strip()
        self.from_exits = tuple(exits or ())
        self.prompt = prompt_seq
        self.travel = travel_seq
        self.look_travel = travel_seq
        self.sent_at = time.monotonic() if now is None else now
        self.last_step = raw
        self.failed = False

    def _confirm(self, room: str, exits: list[str] | None = None) -> None:
        self.pending = ""
        self.phase = ""
        self.from_room = (room or "").strip()
        if exits is not None:
            self.from_exits = tuple(exits)
        self.failed = False

    def _fail(self) -> None:
        self.pending = ""
        self.phase = ""
        self.failed = True
