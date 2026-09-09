"""MajorMUD spells. Classes buy the Newhaven scroll that matches their magery."""

from __future__ import annotations

import json
import re
from pathlib import Path

# One cast per combat round. 1.11p is about eight seconds; spam just fizzles.
ROUND = 8.0

# kind: heal = self/friend, harm = combat target, buff = self (luck). mana is the 1.11p cost.
# level is the 1.11p character level to cast (spells.jsonl). Scrolls can be bought earlier.
# verb invoke = mystic kai (WCCSPELS). say is the typed name; self_only skips a target.
SPELLBOOK = {
    "minor healing": {"kind": "heal", "mana": 2, "short": "mihe", "level": 1},
    "major healing": {"kind": "heal", "mana": 6, "short": "mahe", "level": 8},
    "harm": {"kind": "harm", "mana": 1, "short": "harm", "level": 1},
    "bless": {"kind": "buff", "mana": 2, "short": "bles", "level": 2},
    "magic missile": {"kind": "harm", "mana": 2, "short": "mami", "level": 1},
    "illuminate": {"kind": "light", "mana": 2, "short": "illu", "level": 3},
    "smite": {"kind": "might", "mana": 4, "short": "smit", "level": 3},
    "vine strike": {"kind": "harm", "mana": 1, "short": "vine", "level": 1},
    "mend": {"kind": "heal", "mana": 4, "short": "mend", "level": 2},
    "starlight": {
        "kind": "light",
        "mana": 4,
        "short": "star",
        "level": 1,
        "self_only": True,
        "auto": False,
    },
    "way of the swan": {
        "kind": "heal",
        "mana": 3,
        "short": "swan",
        "level": 2,
        "verb": "invoke",
        "say": "way of swan",
        "self_only": True,
    },
    "way of the owl": {
        "kind": "buff",
        "mana": 10,
        "short": "owl",
        "level": 3,
        "verb": "invoke",
        "say": "owl",
        "self_only": True,
        # Kai power unlocks on train/ding — no scroll, shop, or learn path.
        "level_up": True,
    },
}

# Priest-1 at creation. Other classes buy a scroll at the Newhaven spell shop
# (Rayth / Dathalar, north of Narrow Path) and `read` it to memorize.
# Mystics invoke kai powers; they do not buy mage scrolls.
# Druid-3 / Ranger Druid-1 buy bark parchments (vine/mend), not holy writs.
# Starlight is Druid-1 room light (WCCSPELS). Offered on ding, not auto-shopped.
CLASS_SPELLS = {
    "paladin": ("minor healing", "harm", "bless", "major healing"),
    "cleric": ("minor healing", "harm", "major healing"),
    "priest": ("minor healing", "harm", "major healing"),
    "missionary": ("minor healing", "harm", "major healing"),
    "mage": ("magic missile", "illuminate", "smite"),
    "warlock": ("magic missile",),
    "mystic": ("way of the swan", "way of the owl"),
    "druid": ("vine strike", "mend", "starlight"),
    "ranger": ("vine strike", "mend", "starlight"),
    "warrior": (),
    "witchunter": (),
    "ninja": (),
    "thief": (),
}

# Shop item names. Harm's scroll is "cause harm", not "harm".
SCROLLS = {
    "minor healing": "scroll of minor healing",
    "harm": "scroll of cause harm",
    "bless": "scroll of bless",
    "major healing": "scroll of major healing",
    "magic missile": "scroll of magic missile",
    "illuminate": "scroll of illuminate",
    "smite": "scroll of smite",
    "vine strike": "scroll of vine strike",
    "mend": "scroll of mend",
    "starlight": "scroll of starlight",
}

_SHOP_FIRST = (
    "minor healing",
    "harm",
    "magic missile",
    "illuminate",
    "smite",
    "vine strike",
    "starlight",
    "mend",
    "bless",
    "major healing",
)
SPELL_SHOP = "Newhaven, Spell Shop"
_SPELL_ALIAS = {
    "way of swan": "way of the swan",
    "swan": "way of the swan",
    "way of owl": "way of the owl",
    "owl": "way of the owl",
    "vine": "vine strike",
    "star": "starlight",
}


def canonical_spell(name: str) -> str:
    low = name.strip().lower()
    return _SPELL_ALIAS.get(low, low)


def normalize_spell_list(raw: object) -> list[str]:
    if isinstance(raw, (list, tuple)):
        return [str(item).strip().lower() for item in raw if str(item).strip()]
    return [part.strip().lower() for part in str(raw or "").split(",") if part.strip()]


def known_spells(klass: str, listed: object = None) -> list[str]:
    if listed is None:
        return list(CLASS_SPELLS.get(klass.strip().lower(), ()))
    return normalize_spell_list(listed)


def info(name: str) -> dict[str, object] | None:
    return SPELLBOOK.get(canonical_spell(name))


def of_kind(names: list[str], kind: str) -> str:
    for name in names:
        spell = info(name)
        if spell and spell["kind"] == kind:
            return name
    return ""


def shop_spells(klass: str, listed: object = None) -> list[str]:
    """Spells this class auto-buys. Offer-only scrolls (starlight) stay off this list."""
    names = []
    for name in known_spells(klass, listed):
        if name not in SCROLLS:
            continue
        meta = info(name)
        if meta is not None and not meta.get("auto", True):
            continue
        names.append(name)
    rank = {name: i for i, name in enumerate(_SHOP_FIRST)}
    names.sort(key=lambda name: (rank.get(name, len(_SHOP_FIRST)), name))
    return names


def scroll_name(spell: str) -> str:
    low = spell.strip().lower()
    return SCROLLS.get(low, f"scroll of {low}")


def buy_name(spell: str, alt: bool = False) -> str:
    low = spell.strip().lower()
    if alt:
        return low
    return scroll_name(low)


def read_command(spell: str) -> str:
    return f"read {scroll_name(spell)}"


def min_level(name: str) -> int:
    """Character level needed to `cast`. Scrolls can be bought and read sooner."""
    spell = info(name)
    if not spell:
        return 1
    raw = spell.get("level")
    if raw is None:
        return 1
    return int(raw)


def can_cast(name: str, level: int | None) -> bool:
    if level is None:
        return False
    return int(level) >= min_level(name)


def due_to_learn(
    klass: str,
    listed: object,
    level: int | None,
    memorized: set[str],
) -> list[str]:
    """Shop spells this class can learn now and has not memorized."""
    if level is None:
        return []
    have = {name.strip().lower() for name in memorized}
    names = shop_spells(klass, None)
    for extra in shop_spells(klass, listed):
        if extra not in names:
            names.append(extra)
    return [
        name
        for name in names
        if name not in have and int(level) >= min_level(name)
    ]


def next_due(
    klass: str,
    listed: object,
    level: int | None,
    memorized: set[str],
) -> str:
    due = due_to_learn(klass, listed, level, memorized)
    return due[0] if due else ""


def have_scroll(items: list[str], spell: str) -> bool:
    """True if `i` extras/inventory already has this spell's scroll."""
    want = scroll_name(spell)
    short = spell.strip().lower()
    for raw in items:
        low = raw.strip().lower()
        if not low:
            continue
        if want in low:
            return True
        if "scroll" in low and short in low:
            return True
    return False


def other_held_scrolls(items: list[str], spell: str) -> bool:
    """True if `i` lists a scroll that is not this spell's."""
    want = scroll_name(spell)
    for raw in items:
        low = raw.strip().lower()
        if "scroll" in low and want not in low:
            return True
    return False


def first_held_scroll(items: list[str], names: list[str], tried: set[str]) -> str:
    """Exact shop name of the first unread scroll we are holding."""
    for name in names:
        if name in tried:
            continue
        if have_scroll(items, name):
            return name
    return ""


def list_command(klass: str) -> str:
    """Mystics list kai with `powers`. Everyone else uses `spells`."""
    if (klass or "").strip().lower() == "mystic":
        return "powers"
    return "spells"


def uses_book(klass: str) -> bool:
    """True when `spells`/`powers` can tell us what is already memorized."""
    return bool(known_spells(klass))


_BOOK_ROW = re.compile(
    r"^\s*(\d{1,3})\s+(\d{1,4})\s+([A-Za-z][A-Za-z']{1,8})\s+(.+?)\s*$"
)


def from_short(code: str) -> str:
    low = code.strip().lower()
    if not low:
        return ""
    if low in SPELLBOOK:
        return low
    aliased = canonical_spell(low)
    if aliased in SPELLBOOK:
        return aliased
    for name, meta in SPELLBOOK.items():
        if str(meta.get("short") or "").lower() == low:
            return name
    return aliased or low


def parse_book_row(raw: str) -> str:
    """Level / mana / short / name row from `spells` or `powers`."""
    m = _BOOK_ROW.match(raw.strip())
    if not m:
        return ""
    short = m.group(3).strip().lower()
    rest = re.sub(r"\s+", " ", m.group(4).strip().lower())
    if not re.fullmatch(r"[a-z']{2,8}", short):
        return ""
    if re.search(r"\d", rest):
        return ""
    named = canonical_spell(rest)
    if named in SPELLBOOK:
        return named
    coded = from_short(short)
    if coded in SPELLBOOK:
        return coded
    if re.fullmatch(r"[a-z][a-z' ]*[a-z]", named) and len(named) >= 3:
        return named
    return ""


def learned_who(who: str) -> str:
    """Stable key: first token of the login/given blob."""
    parts = [part.strip().lower() for part in who.replace(",", " ").split() if part.strip()]
    return parts[0] if parts else ""


def load_learned(path: str | Path | None, who: str) -> set[str]:
    key = learned_who(who)
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


def save_learned(path: str | Path | None, who: str, names: set[str]) -> None:
    key = learned_who(who)
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


def have_known(items: list[str], spell: str) -> bool:
    """True if `i` / known-spell text already lists this memorized spell."""
    short = spell.strip().lower()
    if not short:
        return False
    for raw in items:
        low = raw.strip().lower()
        if not low or "scroll" in low:
            continue
        if low == short or low.startswith(f"{short},") or low.startswith(f"{short} "):
            return True
        parts = [part.strip() for part in low.replace(";", ",").split(",")]
        if short in parts:
            return True
    return False


def self_only(name: str) -> bool:
    spell = info(name)
    return bool(spell and spell.get("self_only"))


def spoken_name(name: str) -> str:
    spell = info(name)
    if spell:
        said = str(spell.get("say") or "").strip()
        if said:
            return said
    return canonical_spell(name)


def verb(name: str) -> str:
    spell = info(name)
    if spell and spell.get("verb") == "invoke":
        return "invoke"
    return "cast"


def command(name: str, target: str = "") -> str:
    line = f"{verb(name)} {spoken_name(name)}"
    who = target.strip()
    if who and not self_only(name):
        line = f"{line} {who}"
    return line


def cost(name: str) -> int:
    spell = info(name)
    if not spell:
        return 1
    return int(spell["mana"])
