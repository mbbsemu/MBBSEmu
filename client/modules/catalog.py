"""All-at-once ability catalog: WCC files plus the client's SPELLBOOK."""

from __future__ import annotations

import functools
import re
from dataclasses import dataclass
from pathlib import Path

from .. import spells

REPO = Path(__file__).resolve().parents[2]
WCC = REPO / "modules" / "WCCMMUD"

# Room titles the atlas already knows. Shop names in WCCSHOPS that are not
# these rooms stay off the walk list — ask without inventing a path.
_MAPPED_SHOPS = {
    "newhaven, spell shop": "Rayth",
    "dathalar": "Rayth",
    "rayth": "Rayth",
}

# On/off lines mined from WCCMSG.DAT (confirmed present at catalog load).
_WCCMSG_EFFECTS = {
    "bless": {
        "on": ("you feel lucky",),
        "off": ("effects of bless wear off",),
        "buff": "blessed",
    },
    "way of the owl": {
        "on": ("you feel strong-willed", "you feel strong willed"),
        "off": ("effects of way of the owl wear off",),
        "buff": "willed",
    },
    "starlight": {
        "on": ("you are surrounded by a shimmering light",),
        "off": ("your starlight spell fades away",),
        "buff": "lit",
    },
}

# Illuminate conjures a light-ball item (WCCSPELS). Starlight lights the room.
_ROOM_LIGHT = frozenset({"starlight"})


@dataclass(frozen=True)
class Ability:
    name: str
    short: str = ""
    kind: str = ""
    verb: str = "cast"
    say: str = ""
    mana: int = 1
    level: int = 1
    classes: tuple[str, ...] = ()
    self_only: bool = False
    room_light: bool = False
    auto_shop: bool = True
    shop_item: str = ""
    learn_where: str = ""
    level_up: bool = False
    help: str = ""
    on_messages: tuple[str, ...] = ()
    off_messages: tuple[str, ...] = ()
    buff_flag: str = ""


@dataclass(frozen=True)
class MessageHit:
    name: str
    on: bool
    buff_flag: str


def _printable_strings(data: bytes, minlen: int = 4) -> list[str]:
    out: list[str] = []
    cur = bytearray()
    for byte in data:
        if 32 <= byte < 127:
            cur.append(byte)
        else:
            if len(cur) >= minlen:
                out.append(cur.decode("ascii"))
            cur = bytearray()
    if len(cur) >= minlen:
        out.append(cur.decode("ascii"))
    return out


def _read_wcc(name: str) -> bytes:
    path = WCC / name
    if not path.is_file():
        return b""
    try:
        return path.read_bytes()
    except OSError:
        return b""


def _mine_spell_help(blob: bytes) -> dict[str, tuple[str, str]]:
    """name -> (short, help) from WCCSPELS printable runs."""
    found: dict[str, tuple[str, str]] = {}
    if not blob:
        return found
    strs = [part.strip() for part in _printable_strings(blob, 3) if part.strip()]
    want = {name.lower() for name in spells.SPELLBOOK}
    for i, raw in enumerate(strs):
        key = raw.lower()
        if key not in want or key in found:
            continue
        help_bits: list[str] = []
        short = ""
        for nxt in strs[i + 1 : i + 12]:
            low = nxt.lower()
            if re.fullmatch(r"[a-z][a-z']{1,7}", low) and len(low) <= 8:
                short = low
                break
            if low in want:
                break
            if re.search(r"[A-Za-z]", nxt) and len(nxt) > 8:
                help_bits.append(nxt)
        found[key] = (short, " ".join(help_bits))
    return found


def _mine_scrolls(blob: bytes) -> dict[str, str]:
    """spell name -> shop item, from WCCITEMS `scroll of …` strings."""
    found: dict[str, str] = {}
    if not blob:
        return found
    for raw in _printable_strings(blob, 10):
        low = raw.strip().lower()
        m = re.fullmatch(r"scroll of ([a-z][a-z ']+)", low)
        if not m:
            continue
        spell = spells.canonical_spell(m.group(1).strip())
        found[spell] = low
    return found


def _mine_confirmed_messages(blob: bytes) -> set[str]:
    if not blob:
        return set()
    low = blob.lower()
    have: set[str] = set()
    for meta in _WCCMSG_EFFECTS.values():
        for line in (*meta["on"], *meta["off"]):
            if line.encode("ascii") in low:
                have.add(line)
    return have


def _classes_for(name: str) -> tuple[str, ...]:
    found = [
        klass
        for klass, names in spells.CLASS_SPELLS.items()
        if name in names
    ]
    return tuple(found)


def _build() -> dict[str, Ability]:
    help_map = _mine_spell_help(_read_wcc("WCCSPELS.DAT"))
    scrolls = _mine_scrolls(_read_wcc("WCCITEMS.DAT"))
    present = _mine_confirmed_messages(_read_wcc("WCCMSG.DAT"))
    book: dict[str, Ability] = {}
    for name, meta in spells.SPELLBOOK.items():
        mined_short, mined_help = help_map.get(name, ("", ""))
        effect = _WCCMSG_EFFECTS.get(name, {})
        on_msgs = tuple(
            line for line in effect.get("on", ()) if not present or line in present
        )
        off_msgs = tuple(
            line for line in effect.get("off", ()) if not present or line in present
        )
        shop_item = str(spells.SCROLLS.get(name) or scrolls.get(name) or "")
        level_up = bool(meta.get("level_up"))
        if shop_item and not level_up:
            where = _MAPPED_SHOPS.get(spells.SPELL_SHOP.lower(), "Rayth")
        else:
            where = ""
            shop_item = "" if level_up else shop_item
        book[name] = Ability(
            name=name,
            short=str(meta.get("short") or mined_short),
            kind=str(meta.get("kind") or ""),
            verb=spells.verb(name),
            say=spells.spoken_name(name),
            mana=int(meta.get("mana") or 1),
            level=int(meta.get("level") or 1),
            classes=_classes_for(name),
            self_only=bool(meta.get("self_only")),
            room_light=name in _ROOM_LIGHT,
            auto_shop=bool(meta.get("auto", True)) and name in spells.SCROLLS and not level_up,
            shop_item=shop_item,
            learn_where=where,
            level_up=level_up,
            help=mined_help,
            on_messages=on_msgs,
            off_messages=off_msgs,
            buff_flag=str(effect.get("buff") or ""),
        )
    return book


@functools.lru_cache(maxsize=1)
def catalog() -> dict[str, Ability]:
    """Every playable ability, gated by class/level, with mined text when present."""
    return _build()


def get(name: str) -> Ability | None:
    return catalog().get(spells.canonical_spell(name))


def abilities_for(klass: str) -> tuple[Ability, ...]:
    low = (klass or "").strip().lower()
    return tuple(ab for ab in catalog().values() if low in ab.classes)


def match_line(raw: str) -> MessageHit | None:
    """Map a game line to an ability on/off from the mined WCCMSG set."""
    low = raw.strip().lower()
    if not low:
        return None
    for ab in catalog().values():
        if any(mark in low for mark in ab.off_messages):
            return MessageHit(ab.name, False, ab.buff_flag)
        if any(mark in low for mark in ab.on_messages):
            return MessageHit(ab.name, True, ab.buff_flag)
    return None
