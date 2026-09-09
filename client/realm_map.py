"""Live MajorMUD room graph. Seeded from Newhaven and Silvermere walks.

Persists under data/ (gitignored). Does not read WCCMMUD module files.
"""

from __future__ import annotations

import json
from collections import deque
from dataclasses import dataclass, field
from pathlib import Path

from .paths import in_newhaven, in_silvermere

ROOT = Path(__file__).resolve().parent.parent
DEFAULT_PATH = ROOT / "data" / "realm-map.json"

CARDINALS = ("n", "s", "e", "w", "u", "d", "ne", "nw", "se", "sw")
SPECIALS = ("borrow skiff", "search down", "go manhole")
DIRS = CARDINALS + SPECIALS
REVERSE = {
    "n": "s",
    "s": "n",
    "e": "w",
    "w": "e",
    "u": "d",
    "d": "u",
    "ne": "sw",
    "nw": "se",
    "se": "nw",
    "sw": "ne",
    "borrow skiff": "borrow skiff",
    "go manhole": "u",
    "search down": "u",
}

# Mirrors client/paths.py: village entrance, shops, narrow path/road, arena.
NEWHAVEN = {
    "Newhaven, Village Entrance": {
        "n": "Newhaven, Weapon Shop",
        "s": "Newhaven, Armour Shop",
        "w": "Newhaven, Narrow Path",
        "se": "Newhaven, Forest Path",
    },
    "Newhaven, Weapon Shop": {"s": "Newhaven, Village Entrance"},
    "Newhaven, Nathaniel": {"s": "Newhaven, Village Entrance"},
    "Newhaven, Armour Shop": {"n": "Newhaven, Village Entrance"},
    "Newhaven, Betram": {"n": "Newhaven, Village Entrance"},
    "Newhaven, Narrow Path": {
        "n": "Newhaven, Spell Shop",
        "s": "Newhaven, General Store",
        "e": "Newhaven, Village Entrance",
        "w": "Newhaven, Narrow Road",
    },
    "Newhaven, Spell Shop": {"s": "Newhaven, Narrow Path"},
    "Newhaven, General Store": {"n": "Newhaven, Narrow Path"},
    "Newhaven, Narrow Road": {
        "n": "Newhaven, Guild",
        "e": "Newhaven, Narrow Path",
        "w": "Newhaven, Healer",
        "d": "Newhaven, Arena",
    },
    "Newhaven, Guild": {"s": "Newhaven, Narrow Road"},
    "Newhaven, Healer": {"e": "Newhaven, Narrow Road"},
    "Newhaven, Arena": {"u": "Newhaven, Narrow Road"},
    "Newhaven, Forest Path": {
        "nw": "Newhaven, Village Entrance",
        "s": "Newhaven, Docks",
    },
    "Newhaven, Docks": {
        "n": "Newhaven, Forest Path",
        "borrow skiff": "Pier",
    },
}

# Live Silvermere titles (no "Silvermere," prefix). Landmarks only — generic
# River Street tiles share a name, so streets walk by compass in paths.py.
SILVERMERE = {
    "Town Square": {
        "n": "Guild Street, Southern End",
        "s": "Sovereign Street, Northern End",
        "e": "Silver Street, Western End",
        "w": "Temple Street, Eastern End",
        "go manhole": "Sewer Tunnel, Junction (below TS)",
    },
    "Sovereign Street, Northern End": {
        "n": "Town Square",
    },
    "Silver Street, Western End": {
        # One step east of Town Square — west end of the street.
        "w": "Town Square",
        "e": "Silver Street",
        "n": "Sentara's Clothing, Front Room",
        "s": "Homely Hearth",
    },
    # Mid Silver reuses one title for two tiles (near-square, then shops).
    # Shop door is south; east reaches Brass. Torch runs count tiles in brain.
    "Silver Street": {
        "w": "Silver Street, Western End",
        "e": "Intersection of Silver St. & Brass St.",
        "n": "Curious Goods",
        "s": "General Store",
    },
    "Intersection of Silver St. & Brass St.": {
        "w": "Silver Street",
    },
    # Dead-end east of Brass. Town Square is west, never east (live atlas
    # once learned `e` → Town Square and the GY walk bounced e/w here).
    "Silver Street, Eastern End": {
        "w": "Intersection of Silver St. & Brass St.",
    },
    "Homely Hearth": {"n": "Silver Street, Western End"},
    "Curious Goods": {"s": "Silver Street"},
    "Guild Street, Southern End": {
        "s": "Town Square",
        "n": "Guild Street, Northern End",
        "w": "Helfgrim's Blades",
    },
    "Guild Street, Northern End": {
        "s": "Guild Street, Southern End",
        "n": "Intersection of Guild St. & River St.",
        "e": "Adventurer's Guild, Foyer",
    },
    "Intersection of Guild St. & River St.": {
        "s": "Guild Street, Northern End",
        "w": "Docks",
        "e": "River Street",
    },
    "Docks": {
        "n": "Pier",
        "search down": "Pier",
    },
    "Pier": {
        "s": "Docks",
        "borrow skiff": "Newhaven, Docks",
    },
    # Live / Winterhawk titles for the Town Square manhole drop.
    "Sewer Tunnel, Junction": {"u": "Town Square"},
    "Sewer Tunnel, Junction (below TS)": {"u": "Town Square"},
    "Fountain": {"go manhole": "Sewer Tunnel, Junction (below TS)"},
    "Temple Hall": {"s": "Temple Spell Store", "n": "Temple Healer", "e": "Temple Street"},
    "Temple Spell Store": {"n": "Temple Hall"},
    "Temple Healer": {"s": "Temple Hall"},
    "Temple Chapel": {"e": "Temple Hall"},
    "Clerical Training Room": {"s": "Temple Hall"},
    "Priestly Training Room": {"n": "Temple Hall"},
    "Adventurer's Guild, Foyer": {
        "w": "Guild Street, Northern End",
        "e": "Adventurer's Guild, Main Room",
    },
    "Adventurer's Guild, Main Room": {
        "w": "Adventurer's Guild, Foyer",
        "e": "Adventurer's Guild, Universal Trainer",
    },
    "Adventurer's Guild, Universal Trainer": {
        "w": "Adventurer's Guild, Main Room",
    },
    "Intersection of Temple St. & Stone St.": {
        "e": "Temple Street",
        "w": "Temple Hall",
    },
    # Mid-block tiles share the title; east is always toward the square.
    "Temple Street": {
        "e": "Temple Street, Eastern End",
        "w": "Intersection of Temple St. & Stone St.",
        "s": "Lucky Strike Casino",
    },
    "Temple Street, Eastern End": {
        "e": "Town Square",
        "w": "Temple Street",
        "n": "Skali's Fine Armour, Front Room",
        "s": "Bank of Godfrey",
    },
    "Lucky Strike Casino": {"n": "Temple Street"},
    "Helfgrim's Blades": {"e": "Guild Street, Southern End"},
    "Skali's Fine Armour, Front Room": {
        "s": "Temple Street, Eastern End",
        "e": "Skali's Fine Armour, Showroom",
    },
    "Skali's Fine Armour, Showroom": {
        "s": "Skali's Fine Armour, Front Room",
        "n": "Skali's Fine Armour, Back Room",
    },
    "Skali's Fine Armour, Back Room": {"s": "Skali's Fine Armour, Showroom"},
    "Sentara's Clothing, Front Room": {
        "s": "Silver Street, Western End",
    },
    "General Store": {"n": "Silver Street"},
    "Magic Shoppe": {"n": "Intersection of Guild St. & River St."},
    "Paladin Training Room": {},
    "Ninja Training Room": {},
    "Arena Entrance": {"s": "Town Square"},
    "River Street": {
        "w": "Intersection of Guild St. & River St.",
        "e": "Intersection of River St. & Bridge St.",
    },
    "Intersection of River St. & Bridge St.": {
        "w": "River Street",
        "s": "Bridge Street",
        "n": "Bridge",
    },
    "Bridge Street": {
        "n": "Intersection of River St. & Bridge St.",
    },
    # Live: closed gate south + northeast. SW is a wall — never map it.
    "Bridge": {
        "ne": "Graveyard Entrance",
        "s": "Bridge Street",
    },
    "River Street, Eastern End": {
        "w": "River Street",
    },
    "Graveyard Entrance": {
        "sw": "Bridge",
        "w": "Shack",
        "e": "Graveyard",
    },
    # West of the GY gate. Rest is SW onto the creek bridge, never sit here.
    "Shack": {
        "sw": "Bridge",
        "e": "Graveyard Entrance",
    },
    # Mid-grass tiles share this title; hunt ping-pongs e/w only.
    "Graveyard": {
        "w": "Graveyard Entrance",
        "e": "Graveyard",
    },
}

_NEWHAVEN_HOME = (
    "arena",
    "narrow road",
    "village entrance",
    "healer",
    "newhaven",
)
_SILVERMERE_HOME = (
    "graveyard",
    "town square",
)
_HOME_HINTS = _SILVERMERE_HOME + _NEWHAVEN_HOME


def room_key(title: str) -> str:
    """Dedup Newhaven, Arena / Newhaven Arena; Graveyard, Entry / Entrance."""
    cleaned = title.lower().replace(",", " ")
    key = " ".join(cleaned.split())
    if key == "graveyard entry":
        return "graveyard entrance"
    return key


def reverse_dir(step: str) -> str:
    return REVERSE.get(step.strip().lower(), "")


def _mappable(title: str) -> bool:
    raw = title.strip()
    if not raw or len(raw) > 80:
        return False
    # Sign scrap like council" — never a room title.
    if '"' in raw:
        return False
    low = raw.lower()
    if len(raw) < 4:
        return False
    if low.startswith(("this is", "this huge", "it is", "there is", "you are ")):
        return False
    if raw.endswith("St."):
        words = raw.split()
        return 2 <= len(words) <= 10
    if raw.endswith(("!", ":", "]", ".", '"')):
        return False
    if raw[0].islower() or raw[0].isdigit():
        return False
    if raw.startswith("["):
        return False
    if raw.startswith("You "):
        return False
    if raw.startswith(("A ", "An ", "The ")):
        # Title-case "A Dark Hall" is a room. "A large rat" is not.
        rest = raw.split(" ", 1)[-1]
        words = raw.split()
        return bool(rest) and rest[0].isupper() and 2 <= len(words) <= 8
    return True


def _valid_edge_dest(dest: str) -> bool:
    """Reject sign-scrap / parse junk that used to poison BFS (e.g. council\")."""
    key = room_key(dest)
    if not key or '"' in key or key.startswith("?"):
        return False
    if len(key) < 4:
        return False
    if key.startswith(("this is", "this huge", "it is", "there is")):
        return False
    return True


def _plausible_edge(src: str, _step: str, dest: str) -> bool:
    """Reject collapsed-street lies that send GY the wrong way."""
    if src == "silver street eastern end" and dest == "town square":
        return False
    if src == "town square" and dest == "silver street eastern end":
        return False
    if src == "river street" and dest == "graveyard entrance":
        return False
    # Live dump once sent gate `w` to the creek. West is the shack; SW is the bridge.
    if src == "graveyard entrance" and dest == "bridge" and _step == "w":
        return False
    # Live: Bridge Street `n` is the River/Bridge gate, not the creek.
    if src == "bridge street" and dest in {"bridge", "graveyard entrance"}:
        return False
    if (
        src == "bridge street"
        and dest == "intersection of river st. & bridge st."
        and _step == "s"
    ):
        return False
    if (
        src == "intersection of river st. & bridge st."
        and dest == "bridge street"
        and _step == "n"
    ):
        return False
    if (
        src == "intersection of river st. & bridge st."
        and dest == "guild street southern end"
    ):
        return False
    # Live dump once sent Northern End `e` to River Street. The guild is that east door.
    if src == "guild street northern end" and dest == "river street" and _step == "e":
        return False
    # Pier → TS is 3s 6e 10s. Docks is not a one-step shortcut onto Guild/River.
    if src == "docks" and dest == "intersection of guild st. & river st.":
        return False
    if src == "pier" and dest == "intersection of guild st. & river st.":
        return False
    if src == "docks" and dest.startswith("guild street"):
        return False
    return True


@dataclass
class Hint:
    action: str = ""
    step: str = ""
    route: list[str] = field(default_factory=list)
    chrome: str = ""


class Atlas:
    def __init__(self, path: Path | str | None = None) -> None:
        self.store = Path(path) if path else None
        self.rooms: dict[str, dict[str, object]] = {}
        self.edges: dict[tuple[str, str], str] = {}
        self._seed_graph(NEWHAVEN)
        self._seed_graph(SILVERMERE)
        if self.store:
            self.load()

    def room_count(self) -> int:
        return len(self.rooms)

    def known(self, title: str) -> bool:
        key = room_key(title)
        return bool(key) and key in self.rooms

    def unmapped(self, title: str, exits: list[str] | None = None) -> list[str]:
        """Listed doors we have not recorded a destination for."""
        key = room_key(title)
        listed = [d for d in (exits or []) if d in DIRS]
        if not listed:
            node = self.rooms.get(key) or {}
            listed = [d for d in (node.get("exits") or []) if d in DIRS]
        open_doors: list[str] = []
        for step in listed:
            dest = self.edges.get((key, step), "")
            if not dest or str(dest).startswith("?"):
                open_doors.append(step)
        return open_doors

    def _seed_graph(self, graph: dict[str, dict[str, str]]) -> None:
        for title, exits in graph.items():
            key = room_key(title)
            node = self.rooms.get(key)
            if node is None:
                self.rooms[key] = {"title": title, "exits": sorted(exits)}
            else:
                old = list(node.get("exits") or [])
                node["exits"] = sorted(set(old) | set(exits))
            for step, dest in exits.items():
                dest_key = room_key(dest)
                if _plausible_edge(key, step, dest_key):
                    self.edges[(key, step)] = dest_key

    def _seed_newhaven(self) -> None:
        self._seed_graph(NEWHAVEN)

    def observe(
        self,
        title: str,
        exits: list[str] | None,
        via: str = "",
        prev: str = "",
    ) -> str:
        """Record a room and the edge that reached it. Returns the room key."""
        raw = (title or "").strip()
        if raw and not _mappable(raw):
            raw = ""
        key = room_key(raw) if raw else ""
        step = via.strip().lower()
        if step not in DIRS:
            step = ""
        prev_key = room_key(prev) if prev and _mappable(prev) else ""
        if not key and prev_key and step:
            key = f"?{prev_key}:{step}"
            raw = raw or key
        if not key:
            return ""
        seen = [d for d in (exits or []) if d in CARDINALS]
        node = self.rooms.get(key)
        if node is None:
            self.rooms[key] = {"title": raw, "exits": seen}
        else:
            if raw and not str(node.get("title") or "").startswith("?"):
                node["title"] = raw
            old = [d for d in node.get("exits") or [] if d in DIRS]
            node["exits"] = sorted(set(old) | set(seen))
        if prev_key and step and prev_key != key:
            if _valid_edge_dest(key) and _plausible_edge(prev_key, step, key):
                self.edges[(prev_key, step)] = key
            back = REVERSE.get(step, "")
            if (
                back in CARDINALS
                and (key, back) not in self.edges
                and _valid_edge_dest(prev_key)
                and _plausible_edge(key, back, prev_key)
            ):
                self.edges[(key, back)] = prev_key
            if prev_key in self.rooms:
                old = [d for d in self.rooms[prev_key].get("exits") or [] if d in DIRS]
                if step not in old:
                    self.rooms[prev_key]["exits"] = sorted(set(old) | {step})
        self.save()
        return key

    def path(self, src: str, dest: str) -> list[str]:
        start = room_key(src)
        goal = room_key(dest)
        if not start or start not in self.rooms:
            return []
        if start == goal:
            return []
        return self._bfs(start, {goal})

    def way_home(self, title: str, exits: list[str] | None = None) -> list[str]:
        """Shortest known walk toward the local farm, then the town hub."""
        start = room_key(title)
        if not start or start not in self.rooms:
            return []
        allowed = {d for d in (exits or []) if d in DIRS} if exits else None
        if in_newhaven(title):
            hints = _NEWHAVEN_HOME
        elif in_silvermere(title):
            hints = _SILVERMERE_HOME
        else:
            hints = _NEWHAVEN_HOME + _SILVERMERE_HOME
        for hint in hints:
            goals = {key for key in self.rooms if hint in key}
            goals.discard(start)
            if not goals:
                continue
            route = self._bfs(start, goals, allowed)
            if route:
                return route
        return []

    def suggest(
        self,
        title: str,
        exits: list[str] | None,
        last_step: str = "",
        scanned: bool = True,
    ) -> Hint:
        """What to do when lost. Caller skips this while following."""
        count = self.room_count()
        if not scanned:
            return Hint(action="look", chrome=f"map: {count} rooms")
        route = self.way_home(title, exits)
        if route:
            step = route[0]
            # Temple Street: east is Town Square. Never follow a poisoned west path.
            low = (title or "").lower()
            if "temple street" in low and step == "w" and exits and "e" in exits:
                step = "e"
                route = ["e", *route[1:]]
            return Hint(
                action="path",
                step=step,
                route=route,
                chrome=f"path: {','.join(route)}",
            )
        learn = self._explore(title, exits, last_step)
        if learn:
            return Hint(
                action="guess",
                step=learn,
                chrome=f"map: learn {learn}",
            )
        if self.known(title):
            return Hint()
        dirs = [d for d in (exits or []) if d in DIRS]
        if len(dirs) == 1:
            return Hint(
                action="guess",
                step=dirs[0],
                chrome=f"map: {count} rooms",
            )
        back = reverse_dir(last_step)
        for guess in (back, "u"):
            if not guess:
                continue
            if exits and guess not in exits:
                continue
            return Hint(
                action="guess",
                step=guess,
                chrome=f"map: {count} rooms",
            )
        if scanned:
            return Hint()
        return Hint(action="look", chrome=f"map: {count} rooms")

    def _explore(
        self,
        title: str,
        exits: list[str] | None,
        last_step: str,
    ) -> str:
        """Walk a door we have never recorded. Something beats nothing."""
        open_doors = self.unmapped(title, exits)
        if not open_doors:
            return ""
        horiz = [d for d in open_doors if d in {"n", "s", "e", "w"}]
        back = reverse_dir(last_step)
        for step in (*horiz, *open_doors):
            if step != back:
                return step
        return open_doors[0]

    def _bfs(
        self,
        start: str,
        goals: set[str],
        allowed: set[str] | None = None,
    ) -> list[str]:
        if start in goals:
            return []
        seen = {start}
        q: deque[tuple[str, list[str]]] = deque([(start, [])])
        while q:
            here, route = q.popleft()
            for step in DIRS:
                dest = self.edges.get((here, step))
                if not dest or dest in seen:
                    continue
                if (
                    here == start
                    and allowed is not None
                    and step not in allowed
                    and step not in SPECIALS
                ):
                    continue
                nxt = [*route, step]
                if dest in goals:
                    return nxt
                seen.add(dest)
                q.append((dest, nxt))
        return []

    def load(self) -> None:
        if not self.store or not self.store.is_file():
            return
        try:
            data = json.loads(self.store.read_text(encoding="utf-8"))
        except (OSError, json.JSONDecodeError):
            return
        if not isinstance(data, dict):
            return
        rooms = data.get("rooms") or {}
        if isinstance(rooms, dict):
            for key, node in rooms.items():
                if not isinstance(key, str) or not isinstance(node, dict):
                    continue
                title = str(node.get("title") or key)
                if '"' in key or '"' in title:
                    continue
                exits = [d for d in node.get("exits") or [] if d in DIRS]
                self.rooms[room_key(key) if " " in key or key[:1] != "?" else key] = {
                    "title": title,
                    "exits": exits,
                }
        for edge in data.get("edges") or []:
            if not isinstance(edge, dict):
                continue
            src = room_key(str(edge.get("from") or ""))
            dest = room_key(str(edge.get("to") or ""))
            step = str(edge.get("dir") or "").lower()
            if (
                src
                and dest
                and step in DIRS
                and _valid_edge_dest(dest)
                and _plausible_edge(src, step, dest)
            ):
                self.edges[(src, step)] = dest
        # Canonical Silvermere layout wins over any leftover bad edges.
        self._seed_graph(SILVERMERE)
        self._drop_impossible_edges()

    def _drop_impossible_edges(self) -> None:
        for (src, step), dest in list(self.edges.items()):
            if not _plausible_edge(src, step, dest):
                del self.edges[(src, step)]

    def save(self) -> None:
        if not self.store:
            return
        payload = {
            "rooms": {
                key: {
                    "title": node.get("title") or key,
                    "exits": list(node.get("exits") or []),
                }
                for key, node in sorted(self.rooms.items())
            },
            "edges": [
                {"from": src, "dir": step, "to": dest}
                for (src, step), dest in sorted(self.edges.items())
            ],
        }
        try:
            self.store.parent.mkdir(parents=True, exist_ok=True)
            tmp = self.store.with_suffix(".tmp")
            tmp.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
            tmp.replace(self.store)
        except OSError:
            return
