"""Where we are, and how to reach the useful rooms.

Unique titles (Town Square, Helfgrim's Blades, Western End, …) are pins:
one tile, one job. Same-title corridors (mid Guild / Silver / Temple) only
have a heading. The atlas graph still records live edges, but a corridor
tile must not steal a unique-to-unique exit (Southern End `n` is the north
end, not the next generic Guild Street).
"""

from __future__ import annotations

from . import megapath, paths, realm_map

GOALS = (
    "farm",
    "graveyard",
    "square",
    "store",
    "weapons",
    "armour",
    "spells",
    "guild",
    "healer",
    "bank",
)

# `goto ts` on the client bar — walk here, then stop. Not a hunt loop.
_LANDMARK_ALIAS = {
    "ts": "square",
    "town": "square",
    "square": "square",
    "fountain": "square",
    "townsquare": "square",
    "town-square": "square",
    "gy": "graveyard",
    "graveyard": "graveyard",
    "grave": "graveyard",
    "yard": "graveyard",
    "bridge": "graveyard",
    "store": "store",
    "gs": "store",
    "general": "store",
    "weapons": "weapons",
    "weapon": "weapons",
    "helfgrim": "weapons",
    "blades": "weapons",
    "armour": "armour",
    "armor": "armour",
    "skali": "armour",
    "spells": "spells",
    "spell": "spells",
    "dathalar": "spells",
    "guild": "guild",
    "trainer": "guild",
    "train": "guild",
    "healer": "healer",
    "sewer": "sewer",
    "sewers": "sewer",
    "manhole": "sewer",
    "pipes": "sewer",
    "rest": "restpark",
    "park": "restpark",
    "restpark": "restpark",
    "gybridge": "restpark",
    "gy-bridge": "restpark",
    "bank": "bank",
    "godfrey": "bank",
    "deposit": "bank",
}

_LANDMARK_TAG = {
    "square": "ts",
    "graveyard": "gy",
    "store": "store",
    "weapons": "weapons",
    "armour": "armour",
    "spells": "spells",
    "guild": "guild",
    "healer": "healer",
    "sewer": "sewer",
    "restpark": "rest",
    "bank": "bank",
}


def parse_landmark(text: str) -> str | None:
    """Map `ts` / `gy` / … to a Map.step goal. Empty is a catalog, not a dest."""
    raw = (text or "").strip().lower().replace("_", "-").replace(" ", "-")
    if not raw:
        return None
    return _LANDMARK_ALIAS.get(raw)


def list_landmarks() -> str:
    return (
        "goto/run: ts, gy, rest, bank, store, weapons, armour, spells, guild, healer, sewer, pile  "
        "(bank is Godfrey west of TS then south — deposits the purse; "
        "rest parks on the creek bridge SW of the GY gate; guild/train is class-specific; run skips fights)"
    )


def landmark_tag(goal: str) -> str:
    if goal == "_pile":
        return "pile"
    return _LANDMARK_TAG.get(goal, goal or "")


def at_goal(
    room: str,
    goal: str,
    *,
    level: int | None = None,
    gated: bool = False,
    klass: str = "",
) -> bool:
    return _goal_here(room, goal, level=level, gated=gated, klass=klass)

_CORRIDOR_KEYS = frozenset(
    {
        "silver street",
        "temple street",
        "guild street",
        "graveyard",
        "river street",
        "sewer tunnel",
        "secret passage",
    }
)

_MISSING = object()

# First step from a unique room toward a goal. None = already there.
_PIN: dict[str, dict[str, str | None]] = {}


def _pin(title: str, **goals: str | None) -> None:
    _PIN[realm_map.room_key(title)] = dict(goals)


def _out(door: str, **stay: str | None) -> dict[str, str | None]:
    """Dead-end / shop: leave by `door` except for the goals in stay."""
    row: dict[str, str | None] = {name: door for name in GOALS}
    row.update(stay)
    return row


_pin(
    "Town Square",
    farm="n",
    graveyard="n",
    square=None,
    store="e",
    weapons="n",
    armour="w",
    spells="w",
    guild="n",
    healer="w",
    bank="w",
)
_pin(
    "Fountain",
    farm="n",
    graveyard="n",
    square="n",
    store="e",
    weapons="n",
    armour="w",
    spells="w",
    guild="n",
    healer="w",
    bank="w",
)
_pin(
    "Guild Street, Southern End",
    farm="n",
    graveyard="n",
    square="s",
    store="s",
    weapons="w",
    armour="s",
    spells="s",
    guild="n",
    healer="s",
)
_pin(
    "Guild Street, Northern End",
    farm="n",
    graveyard="n",
    square="s",
    store="s",
    weapons="s",
    armour="s",
    spells="s",
    guild="e",
    healer="s",
)
_pin(
    "Intersection of Guild St. & River St.",
    farm="e",
    graveyard="e",
    square="s",
    store="s",
    weapons="s",
    armour="s",
    spells="s",
    guild="s",
    healer="s",
)
_pin(
    "Intersection of River St. & Bridge St.",
    farm="n",
    graveyard="n",
    square="w",
    store="w",
    weapons="w",
    armour="w",
    spells="w",
    guild="w",
    healer="w",
)
_pin(
    "Bridge Street",
    farm="n",
    graveyard="n",
    square="s",
    store="s",
    weapons="s",
    armour="s",
    spells="s",
    guild="s",
    healer="s",
)
_pin(
    "Bridge",
    farm="ne",
    graveyard="ne",
    square="s",
    store="s",
    weapons="s",
    armour="s",
    spells="s",
    guild="s",
    healer="s",
)
_pin(
    "Graveyard Bridge",
    farm="ne",
    graveyard="ne",
    square="s",
    store="s",
    weapons="s",
    armour="s",
    spells="s",
    guild="s",
    healer="s",
)
_pin(
    "River Street, Eastern End",
    farm="w",
    graveyard="w",
    square="w",
    store="w",
    weapons="w",
    armour="w",
    spells="w",
    guild="w",
    healer="w",
)
_pin(
    "Adventurer's Guild, Foyer",
    farm="w",
    graveyard="w",
    square="w",
    store="w",
    weapons="w",
    armour="w",
    spells="w",
    guild="e",
    healer="w",
)
_pin(
    "Adventurer's Guild, Main Room",
    farm="w",
    graveyard="w",
    square="w",
    store="w",
    weapons="w",
    armour="w",
    spells="w",
    guild="e",
    healer="w",
)
_pin(
    "Adventurer's Guild, Universal Trainer",
    **_out("w", guild=None),
)
_pin("Helfgrim's Blades", **_out("e", weapons=None))
_pin(
    "Silver Street, Western End",
    farm="w",
    graveyard="w",
    square="w",
    store="e",
    weapons="w",
    armour="w",
    spells="w",
    guild="w",
    healer="w",
)
_pin(
    "Intersection of Silver St. & Brass St.",
    farm="w",
    graveyard="w",
    square="w",
    store="w",
    weapons="w",
    armour="w",
    spells="w",
    guild="w",
    healer="w",
)
_pin(
    "Silver Street, Eastern End",
    farm="w",
    graveyard="w",
    square="w",
    store="w",
    weapons="w",
    armour="w",
    spells="w",
    guild="w",
    healer="w",
)
_pin("General Store", **_out("n", store=None))
_pin("Homely Hearth", **_out("n"))
_pin("Curious Goods", **_out("s", store="s"))
_pin("Lucky Strike Casino", **_out("n"))
_pin(
    "Intersection of Temple St. & Stone St.",
    farm="e",
    graveyard="e",
    square="e",
    store="e",
    weapons="e",
    armour="e",
    spells="w",
    guild="e",
    healer="w",
)
_pin(
    "Temple Street, Eastern End",
    farm="e",
    graveyard="e",
    square="e",
    store="e",
    weapons="e",
    armour="n",
    spells="w",
    guild="e",
    healer="w",
    bank="s",
)
_pin("Bank of Godfrey", **_out("n", bank=None))
_pin("Skali's Fine Armour, Front Room", **_out("s", armour=None))
_pin("Skali's Fine Armour, Showroom", **_out("s", armour="s"))
_pin("Skali's Fine Armour, Back Room", **_out("s", armour="s"))
_pin("Sentara's Clothing, Front Room", **_out("s"))
# Farm stays on grass (`e`). Town leave / rest: SW onto the creek bridge.
_pin(
    "Graveyard Entrance",
    farm="e",
    graveyard=None,
    square="sw",
    store="sw",
    weapons="sw",
    armour="sw",
    spells="sw",
    guild="sw",
    healer="sw",
    bank="sw",
)
_pin(
    "Shack",
    farm="e",
    graveyard="e",
    square="sw",
    store="sw",
    weapons="sw",
    armour="sw",
    spells="sw",
    guild="sw",
    healer="sw",
    bank="sw",
)
_pin(
    "Sovereign Street, Northern End",
    farm="n",
    graveyard="n",
    square="n",
    store="n",
    weapons="n",
    armour="n",
    spells="n",
    guild="n",
    healer="n",
)
_pin(
    "Temple Hall",
    farm="e",
    graveyard="e",
    square="e",
    store="e",
    weapons="e",
    armour="e",
    spells="s",
    guild="e",
    healer="n",
)
_pin("Temple Spell Store", **_out("n", spells=None))
_pin("Temple Healer", **_out("s", healer=None))
_pin("Halls of the Dead", **_out("e"))
_pin("The Halls of the Dead", **_out("e"))
_pin(
    "Newhaven, Village Entrance",
    square="se",
    store="w",
    weapons="n",
    armour="s",
    spells="w",
    guild="w",
    healer="w",
)
_pin(
    "Newhaven, Forest Path",
    square="s",
)
_pin(
    "Newhaven, Docks",
    square="borrow skiff",
)
_pin("Newhaven, Weapon Shop", **_out("s", weapons=None))
_pin("Newhaven, Nathaniel", **_out("s", weapons=None))
_pin("Newhaven, Armour Shop", **_out("n", armour=None))
_pin("Newhaven, Betram", **_out("n", armour=None))
_pin("Newhaven, General Store", **_out("n", store=None))
_pin("Newhaven, Spell Shop", **_out("s", spells=None))
_pin("Newhaven, Dathalar", **_out("s", spells=None))
_pin("Newhaven, Guild", **_out("s", guild=None))
_pin("Newhaven, Healer", **_out("e", healer=None))
_pin(
    "Newhaven, Arena",
    farm="u",
    graveyard="u",
    square="u",
    store="u",
    weapons="u",
    armour="u",
    spells="u",
    guild="u",
    healer="u",
)
_pin(
    "Newhaven, Narrow Road",
    graveyard="e",
    square="e",
    store="e",
    weapons="e",
    armour="e",
    spells="e",
    guild="n",
    healer="w",
)
_pin(
    "Newhaven, Narrow Path",
    square="e",
    store="s",
    weapons="e",
    armour="e",
    spells="n",
    guild="w",
    healer="w",
)


def _fill_bank_pins() -> None:
    """Bank is west of TS then south. Everyone else walks like square first."""
    for row in _PIN.values():
        if "bank" in row:
            continue
        row["bank"] = row.get("square")


_fill_bank_pins()

_GOAL_TITLES = {
    "square": ("Town Square", "Fountain"),
    "store": ("General Store", "Newhaven, General Store"),
    "graveyard": ("Graveyard Entrance", "Graveyard"),
    "farm": (),
    "weapons": ("Helfgrim's Blades", "Newhaven, Weapon Shop", "Newhaven, Nathaniel"),
    "armour": (
        "Skali's Fine Armour, Front Room",
        "Newhaven, Armour Shop",
        "Newhaven, Betram",
    ),
    "spells": ("Newhaven, Spell Shop", "Temple Spell Store", "Dathalar"),
    "guild": (
        "Newhaven, Guild",
        "Adventurer's Guild, Universal Trainer",
        "Paladin Training Room",
        "Ninja Training Room",
    ),
    "healer": ("Newhaven, Healer", "Temple Healer"),
    "bank": ("Bank of Godfrey",),
}


def is_corridor(title: str) -> bool:
    if paths.same_title_corridor(title):
        return True
    return realm_map.room_key(title) in _CORRIDOR_KEYS


def is_unique(title: str) -> bool:
    raw = (title or "").strip()
    if not raw or is_corridor(raw):
        return False
    return realm_map.room_key(raw) in _PIN


def pinned_dir(title: str, goal: str) -> str | None:
    """First step a unique pin wants. None if unpinned or already there."""
    want = (goal or "farm").strip().lower()
    if want == "sewer":
        want = "square"
    if want not in GOALS:
        want = "farm"
    val = _PIN.get(realm_map.room_key(title), {}).get(want, _MISSING)
    if val is _MISSING or not val:
        return None
    return str(val)


def _open(
    step: str | None,
    exits: list[str] | None,
    closed: list[str] | None = None,
    klass: str = "",
    *,
    room: str = "",
) -> str | None:
    if not step:
        return None
    if paths.is_special_step(step):
        return step
    if closed and step in closed:
        return paths.unlatch_dir(step, klass, room=room)
    if exits and step not in exits:
        return None
    return step


def _gy_north(
    room: str,
    exits: list[str] | None,
    closed: list[str] | None,
    klass: str,
) -> str:
    """GY is north. Unlatch, look, walk n once Obvious exits lists it.

    Ninja/thief pick; others bash. Do not unlatch an already-listed north.
    """
    if "n" in (exits or []):
        return "n"
    return paths.unlatch_dir("n", klass, room=room)


def _reject_gy_south(
    goal: str,
    room: str,
    exits: list[str] | None,
    closed: list[str] | None,
    klass: str,
    step: str | None,
) -> str | None:
    if goal not in {"farm", "graveyard"}:
        return step
    if (step or "").strip().lower() != "s":
        return step
    if not paths.gy_gate_never_south(room, exits):
        return step
    return _gy_north(room, exits, closed, klass)


def _reject_rest_park_sw(
    room: str, step: str | None, exits: list[str] | None = None
) -> bool:
    """Reject inventing SW from the creek when it is not a listed exit (wall)."""
    if not step or not paths.at_rest_park(room):
        return False
    if (step or "").strip().lower() != "sw":
        return False
    listed = [x.lower() for x in (exits or [])]
    return "sw" not in listed


def _goal_here(
    room: str,
    goal: str,
    *,
    level: int | None = None,
    gated: bool = False,
    klass: str = "",
) -> bool:
    low = (room or "").lower()
    if paths.in_afterlife(room):
        if goal == "healer" and "healer" in low:
            return True
        return False
    if goal == "restpark":
        return paths.at_rest_park(room)
    if goal == "graveyard":
        return paths.at_graveyard(room) or paths.at_graveyard_gate(room)
    if goal == "farm":
        if paths.wants_silvermere_farm(level, room, gated) and "arena" in low:
            return False
        return paths.at_farm(room) or paths.at_graveyard_gate(room)
    if goal == "square":
        return "town square" in low or "fountain" in low
    if goal == "store":
        return paths.is_general_store(room)
    if goal == "weapons":
        return "helfgrim" in low or paths.is_weapon_shop(room)
    if goal == "armour":
        return "skali" in low or paths.is_armour_shop(room)
    if goal == "spells":
        return paths.is_spell_shop(room) or "temple spell" in low
    if goal == "guild":
        return paths.is_trainer(room, klass)
    if goal == "healer":
        return "healer" in low
    if goal == "bank":
        return paths.at_bank(room)
    if goal == "sewer":
        return paths.at_sewer(room)
    return False


def _shop_leave(room: str, exits: list[str] | None, last_step: str) -> str | None:
    back = realm_map.reverse_dir(last_step)
    if back and _open(back, exits):
        return back
    if exits and len(exits) == 1:
        return exits[0]
    return None


def _dead_end(room: str, exits: list[str] | None) -> str | None:
    """Unknown unique tile: the only obvious door is the way out."""
    if is_corridor(room):
        return None
    dirs = [d for d in (exits or []) if d in realm_map.DIRS]
    if len(dirs) != 1:
        return None
    return dirs[0]


def _is_side_shop(room: str) -> bool:
    low = (room or "").lower()
    if not any(word in low for word in paths.SHOP_WORDS):
        return False
    if "street" in low or "square" in low or "intersection" in low:
        return False
    return True


def _guild_shop_door(room: str, step: str) -> bool:
    low = (room or "").lower()
    if "guild street" not in low and "guild st" not in low:
        return False
    return step in {"e", "w"}


def _corridor_heading(
    room: str,
    goal: str,
    last_step: str = "",
    exits: list[str] | None = None,
) -> str | None:
    """Same-title streets: one heading, never the shop doors."""
    if paths.plain_guild_street(room) or realm_map.room_key(room) == "guild street":
        if goal == "weapons":
            return "s"
        if goal in {"farm", "graveyard", "guild"}:
            return "n"
        return "s"
    if paths.plain_temple_street(room) or realm_map.room_key(room) == "temple street":
        if goal in {"spells", "healer"}:
            return "w"
        return "e"
    if paths.plain_silver_street(room) or realm_map.room_key(room) == "silver street":
        if goal == "store":
            return None  # tile count lives in step_toward_store
        return "w"
    if paths.plain_river_street(room):
        # East toward Bridge St. Overshoot (last_step=w) walks back. Never n.
        if goal in {"farm", "graveyard"}:
            if (last_step or "").strip().lower() == "w":
                return "w"
            return "e"
        return "w"
    if paths.plain_graveyard(room):
        return None
    return None


class Map:
    """Unique rooms + routes. Atlas is the live graph; pins win on known tiles."""

    def __init__(self, atlas: realm_map.Atlas | None = None) -> None:
        self.atlas = atlas if atlas is not None else realm_map.Atlas()

    def place(self, title: str) -> str:
        return realm_map.room_key(title)

    def arrived(self, title: str) -> str:
        """Canonical unique-room key, or empty on a corridor / unknown tile."""
        key = realm_map.room_key(title)
        return key if key in _PIN else ""

    def step(
        self,
        goal: str,
        room: str,
        exits: list[str] | None = None,
        *,
        last_step: str = "",
        silver_east: int = 0,
        skiff_i: int = 0,
        level: int | None = None,
        gated: bool = False,
        closed: list[str] | None = None,
        klass: str = "",
    ) -> str | None:
        want = (goal or "farm").strip().lower()
        seeking_rest = False
        if want == "restpark":
            if paths.at_rest_park(room):
                return None
            bridge = paths.gy_to_bridge_step(room)
            if bridge:
                return bridge
            if paths.at_crypt(room):
                hit = (
                    _open("s", exits, closed, klass, room=room)
                    or _open("n", exits, closed, klass, room=room)
                    or _open("w", exits, closed, klass, room=room)
                )
                if hit:
                    return hit
            if paths.at_graveyard(room):
                hit = _open("w", exits, closed, klass, room=room)
                if hit:
                    return hit
            want = "graveyard"
            seeking_rest = True
        if want == "sewer":
            if paths.at_sewer(room):
                return None
            low = (room or "").lower()
            if "town square" in low or "fountain" in low:
                return _open("go manhole", exits)
            # Walk home like square, then dive at the manhole.
            want = "square"
        if want not in GOALS:
            want = "farm"
        if not seeking_rest and _goal_here(
            room, want, level=level, gated=gated, klass=klass
        ):
            return None
        skiff = paths.step_skiff_to_square(room, exits, skiff_i=skiff_i)
        if skiff:
            # Leftover Pier→TS `s` count must not yank GY back down Guild Street.
            if not (
                want in {"farm", "graveyard", "guild"}
                and skiff == "s"
                and "guild street" in (room or "").lower()
            ):
                return skiff
        if want == "guild":
            hit = paths.step_toward_trainer(room, exits, klass, level)
            if hit:
                return hit
        # Live dump: closed gate north is the GY. South is Bridge Street (town).
        # Never BFS/pin south from here while hunting or goto/run gy.
        if want in {"farm", "graveyard"} and paths.gy_gate_never_south(room, exits):
            hit = _gy_north(room, exits, closed, klass)
            if hit:
                return hit
        # Creek bridge is rest-only. Hunt: NE back to grass. Town leave: listed
        # s / closed s / listed sw only — never invent or farm-fallthrough.
        if paths.at_rest_park(room):
            if want in {"farm", "graveyard"}:
                return _open("ne", exits, closed, klass, room=room)
            if want != "restpark":
                return paths.rest_park_to_town_step(
                    room, exits, closed=closed, klass=klass
                )
        pinned = _PIN.get(realm_map.room_key(room), {}).get(want, _MISSING)
        if pinned is not _MISSING:
            hit = _open(pinned, exits, closed, klass, room=room)
            # Creek leave toward town: listed s/sw or unlatch closed south.
            if (
                not hit
                and paths.at_rest_park(room)
                and want not in {"farm", "graveyard"}
            ):
                hit = paths.rest_park_to_town_step(
                    room, exits, closed=closed, klass=klass
                )
            # Entry/Shack SW onto bridge for town leave (and rest, above).
            # Farm/GY never takes that hidden SW — hunt stays on grass.
            if (
                not hit
                and pinned
                and paths.allows_hidden(room, str(pinned))
                and not paths.farm_avoids_bridge(want, room, str(pinned))
            ):
                hit = str(pinned)
            if hit and not _reject_rest_park_sw(room, hit, exits):
                if paths.farm_avoids_bridge(want, room, hit):
                    hit = None
                else:
                    return _reject_gy_south(want, room, exits, closed, klass, hit)
            # On the creek with a town goal: only real town leave, no explore.
            if paths.at_rest_park(room) and want not in {"farm", "graveyard"}:
                return paths.rest_park_to_town_step(
                    room, exits, closed=closed, klass=klass
                )
            # GY gates: parser used to drop `closed gate north`, so the pin
            # vanished and we walked south onto Bridge Street in a loop.
            if (
                want in {"farm", "graveyard"}
                and pinned == "n"
                and paths.gy_gate_never_south(room, exits)
            ):
                return paths.unlatch_dir("n", klass, room=room)
            # Pin door is not here — same title, different tile. Learn live exits.
        if paths.at_rest_park(room) and want not in {"farm", "graveyard", "restpark"}:
            return paths.rest_park_to_town_step(
                room, exits, closed=closed, klass=klass
            )
        heading = _corridor_heading(room, want, last_step, exits)
        if heading is not None:
            step = _open(heading, exits)
            if step and paths.farm_avoids_bridge(want, room, step):
                step = None
            return _reject_gy_south(want, room, exits, closed, klass, step)
        if _is_side_shop(room) and not _goal_here(
            room, want, level=level, gated=gated, klass=klass
        ):
            leave = _shop_leave(room, exits, last_step)
            if leave:
                return leave
        if want == "store":
            hit = paths.step_toward_store(
                room, exits, last_step=last_step, silver_east=silver_east
            )
            if hit:
                return hit
        elif want in {"farm", "graveyard"}:
            hit = paths.step_toward_farm(
                room, exits, level, gated, last_step=last_step
            )
            if hit and paths.farm_avoids_bridge(want, room, hit):
                hit = None
            if hit:
                return _reject_gy_south(want, room, exits, closed, klass, hit)
        elif want == "spells":
            hit = paths.step_toward_spell_shop(room, exits)
            if hit:
                return hit
        elif want == "square":
            leave = paths.rest_park_to_town_step(
                room, exits, closed=closed, klass=klass
            )
            if leave:
                return leave
            # From GY grass/entry: SW onto the bridge toward town (not farm e/n).
            bridge = paths.gy_to_bridge_step(room)
            if bridge and (
                not exits
                or bridge in exits
                or paths.allows_hidden(room, bridge)
            ):
                return bridge
            if paths.at_graveyard(room) and not paths.at_graveyard_gate(room):
                hit = _open("w", exits)
                if hit:
                    return hit
            if "guild street" in (room or "").lower():
                return _open("s", exits)
            if paths.in_newhaven(room):
                hit = paths.step_toward_silvermere(room, exits)
                if hit:
                    return hit
            # Farm path walks *into* the GY from the creek bridge. Never use it
            # as a TS fallback while still on the bridge / yard / crypt.
            if not (
                paths.at_rest_park(room)
                or paths.at_graveyard(room)
                or paths.at_crypt(room)
            ):
                hit = paths.step_toward_farm(
                    room, exits, level, gated, last_step=last_step
                )
                if hit:
                    return hit
        route = self._route(room, want, exits, klass=klass, level=level)
        if route:
            nxt = route[0]
            if paths.farm_avoids_bridge(want, room, nxt) or _reject_rest_park_sw(
                room, nxt, exits
            ):
                nxt = None
            return _reject_gy_south(want, room, exits, closed, klass, nxt)
        learn = self._explore(room, exits, last_step)
        if learn and not (
            paths.farm_avoids_bridge(want, room, learn)
            or _reject_rest_park_sw(room, learn, exits)
        ):
            return _reject_gy_south(want, room, exits, closed, klass, learn)
        dead = _dead_end(room, exits)
        if dead and (
            paths.farm_avoids_bridge(want, room, dead)
            or _reject_rest_park_sw(room, dead, exits)
        ):
            dead = None
        return _reject_gy_south(
            want, room, exits, closed, klass, dead
        )

    def _explore(
        self, room: str, exits: list[str] | None, last_step: str
    ) -> str | None:
        """Unknown hall: take a door we have not mapped yet."""
        if is_corridor(room) and _corridor_heading(room, "farm", last_step) is not None:
            return None
        open_doors = self.atlas.unmapped(room, exits)
        if not open_doors:
            return None
        back = realm_map.reverse_dir(last_step)
        horiz = [d for d in open_doors if d in {"n", "s", "e", "w"}]
        for step in (*horiz, *open_doors):
            if step != back and _open(step, exits):
                return step
        for step in open_doors:
            hit = _open(step, exits)
            if hit:
                return hit
        return None

    def _route(
        self,
        room: str,
        goal: str,
        exits: list[str] | None,
        *,
        klass: str = "",
        level: int | None = None,
    ) -> list[str]:
        titles = list(_GOAL_TITLES.get(goal) or ())
        if goal == "guild":
            titles = paths.trainer_titles(klass, room, level)
        if goal in {"farm", "graveyard"}:
            if paths.in_silvermere(room):
                # Bridge / Graveyard Bridge are rest-only — never hunt destinations.
                titles = [
                    "Graveyard Entrance",
                    "Graveyard",
                ]
            else:
                titles = ["Newhaven, Arena"]
        tape = megapath.goto_step(room, titles, exits)
        if tape:
            if not (
                paths.gy_gate_never_south(room, exits)
                and goal in {"farm", "graveyard"}
                and tape == "s"
            ):
                if not paths.farm_avoids_bridge(goal, room, tape):
                    return [tape]
        for dest in titles:
            path = self.atlas.path(room, dest)
            if not path:
                continue
            nxt = path[0]
            if (
                paths.gy_gate_never_south(room, exits)
                and goal in {"farm", "graveyard"}
                and nxt == "s"
            ):
                continue
            if paths.farm_avoids_bridge(goal, room, nxt):
                continue
            if (
                exits
                and nxt not in exits
                and not paths.is_special_step(nxt)
            ):
                continue
            if _guild_shop_door(room, nxt) and goal not in {"weapons"}:
                # TS n n n, then Northern End e into the Adventurer's Guild.
                if not (
                    goal == "guild"
                    and nxt == "e"
                    and "northern end" in (room or "").lower()
                ):
                    continue
                continue
            return path
        return []

    def walk_to(
        self,
        dest: str,
        room: str,
        exits: list[str] | None = None,
        *,
        last_step: str = "",
        level: int | None = None,
        gated: bool = False,
    ) -> str | None:
        """One step toward an exact room title (deathpile), not a hunt loop."""
        want = (dest or "").strip()
        if not want or not (room or "").strip():
            return None
        if realm_map.room_key(want) == realm_map.room_key(room):
            return None
        tape = megapath.goto_step(room, [want], exits)
        if tape:
            return tape
        path = self.atlas.path(room, want)
        if path:
            nxt = path[0]
            if (
                exits
                and nxt not in exits
                and not paths.is_special_step(nxt)
            ):
                nxt = ""
            if nxt:
                return nxt
        goal = parse_landmark(want)
        if goal and goal != "_pile":
            return self.step(
                goal,
                room,
                exits,
                last_step=last_step,
                skiff_i=0,
                level=level,
                gated=gated,
            )
        return None

    def observe(
        self,
        title: str,
        exits: list[str] | None,
        via: str = "",
        prev: str = "",
    ) -> str:
        """Record a landing. Unique exits keep their unique destinations."""
        step = (via or "").strip().lower()
        prev_key = realm_map.room_key(prev) if prev else ""
        if prev_key and step and is_unique(prev) and is_corridor(title):
            existing = self.atlas.edges.get((prev_key, step), "")
            if existing and existing in _PIN:
                return self.atlas.observe(title, exits)
        return self.atlas.observe(title, exits, via, prev)
