"""Level-gated gear the footer should name. Newhaven starter kit is auto."""

from __future__ import annotations

import json
from dataclasses import dataclass
from pathlib import Path

from . import spells

# First of that class in the realm gets the unique floor drop. After the
# spirit trains you to 11 the crypt will not let you back in.
CLASS_WEAPON_LEVEL = 10
CLASS_WEAPON_KEY = "class-weapon"
CLASS_WEAPON_WHERE = "Silvermere graveyard crypt"

CLASS_WEAPONS = {
    "warrior": "golden battleaxe",
    "witchunter": "mithril cutlass",
    "paladin": "shimmering greatsword",
    "cleric": "black flail",
    "priest": "platinum mace",
    "missionary": "jeweled scimitar",
    "mage": "obsidian runestaff",
    "druid": "darkwood staff",
    "warlock": "flametongue",
    "thief": "crystal shortsword",
    "gypsy": "silver rapier",
    "ranger": "witchwood spear",
    "bard": "jeweled main gauche",
    "ninja": "ebony ninjato",
    "mystic": "clawed gloves",
}

# Atlas is Newhaven-seeded plus the skiff into Silvermere. Crypt rooms are
# not mapped — Town Square is as far as we walk, then the keys take the tomb.
QUEST_ROOMS = (
    "Silvermere, Graveyard",
    "Graveyard",
    "Town Square",
)


@dataclass(frozen=True)
class GearQuest:
    key: str
    name: str
    level: int
    klass: str
    where: str


def class_weapon(klass: str) -> str:
    return CLASS_WEAPONS.get(klass.strip().lower(), "")


def have_item(items: list[str], name: str) -> bool:
    want = name.strip().lower()
    if not want:
        return False
    for raw in items:
        if want in raw.strip().lower():
            return True
    return False


def next_due(
    klass: str,
    level: int | None,
    held: list[str],
    claimed: set[str],
) -> GearQuest | None:
    """Next named drop this class can go get. Shop towns come later."""
    if level is None:
        return None
    low = klass.strip().lower()
    weapon = class_weapon(low)
    if (
        weapon
        and int(level) >= CLASS_WEAPON_LEVEL
        and CLASS_WEAPON_KEY not in claimed
        and not have_item(held, weapon)
    ):
        return GearQuest(
            key=CLASS_WEAPON_KEY,
            name=weapon,
            level=CLASS_WEAPON_LEVEL,
            klass=low,
            where=CLASS_WEAPON_WHERE,
        )
    return None


def offer_tip(level: int, name: str, klass: str) -> str:
    who = klass.strip().lower() or "class"
    return f"lvl {level} — class weapon: {name} (first {who})?  y/n"


def quest_tip(klass: str, name: str) -> str:
    who = klass.strip().lower() or "class"
    weapon = name.strip().lower() or "class weapon"
    return f"{weapon} — first {who}. skiff to Silvermere crypt. Esc/stop"


def load_claimed(path: str | Path | None, who: str) -> set[str]:
    key = spells.learned_who(who)
    if not path or not key:
        return set()
    file = Path(path)
    if not file.is_file():
        return set()
    try:
        raw = json.loads(file.read_text())
    except (OSError, json.JSONDecodeError):
        return set()
    if not isinstance(raw, dict):
        return set()
    listed = raw.get(key)
    if not isinstance(listed, list):
        return set()
    return {str(name).strip().lower() for name in listed if str(name).strip()}


def save_claimed(path: str | Path | None, who: str, names: set[str]) -> None:
    key = spells.learned_who(who)
    if not path or not key:
        return
    file = Path(path)
    data: dict[str, list[str]] = {}
    if file.is_file():
        try:
            loaded = json.loads(file.read_text())
        except (OSError, json.JSONDecodeError):
            loaded = {}
        if isinstance(loaded, dict):
            data = loaded
    data[key] = sorted(names)
    file.parent.mkdir(parents=True, exist_ok=True)
    file.write_text(json.dumps(data, indent=2) + "\n")


def deathpile_path(gear_path: str | Path | None) -> str | None:
    if not gear_path:
        return None
    return str(Path(gear_path).with_name("deathpile.json"))


def load_deathpile(path: str | Path | None, who: str) -> str:
    key = spells.learned_who(who)
    if not path or not key:
        return ""
    file = Path(path)
    if not file.is_file():
        return ""
    try:
        raw = json.loads(file.read_text())
    except (OSError, json.JSONDecodeError):
        return ""
    if not isinstance(raw, dict):
        return ""
    room = raw.get(key)
    if not isinstance(room, str):
        return ""
    return room.strip()


def save_deathpile(path: str | Path | None, who: str, room: str) -> None:
    key = spells.learned_who(who)
    if not path or not key:
        return
    file = Path(path)
    data: dict[str, str] = {}
    if file.is_file():
        try:
            loaded = json.loads(file.read_text())
        except (OSError, json.JSONDecodeError):
            loaded = {}
        if isinstance(loaded, dict):
            data = {
                str(name): str(where).strip()
                for name, where in loaded.items()
                if str(where).strip()
            }
    if room.strip():
        data[key] = room.strip()
    else:
        data.pop(key, None)
    file.parent.mkdir(parents=True, exist_ok=True)
    if data:
        file.write_text(json.dumps(data, indent=2) + "\n")
    elif file.is_file():
        file.unlink()
