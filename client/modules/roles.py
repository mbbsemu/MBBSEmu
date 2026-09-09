"""Class and race intelligence. Single source for front/back, torch, kit cues.

Brain, kit, and party ask here — they do not keep parallel class tables.
Tick / combat / shop *drivers* stay in brain; roles only answer what a class
or race wants (row, light, staff vs club, caster vs melee, stealth).
"""

from __future__ import annotations

from .. import paths

# Lower = more front. Same class always sorts the same; name breaks ties.
# Live party: druid/warlock front, mystic mid, bard/gypsy/mage back.
CLASS_BIAS = {
    "warrior": 0,
    "paladin": 1,
    "witchunter": 2,
    "ranger": 3,
    "cleric": 4,
    "druid": 5,
    "warlock": 6,
    "mystic": 10,
    "missionary": 20,
    "bard": 21,
    "priest": 22,
    "gypsy": 23,
    "mage": 24,
    "thief": 25,
    "ninja": 26,
}
# Front tanks/heal line. Mid is mystic. Everyone else defaults back.
FRONT_CLASSES = frozenset(
    name for name, bias in CLASS_BIAS.items() if bias < 10
)
MID_CLASSES = frozenset(
    name for name, bias in CLASS_BIAS.items() if 10 <= bias < 20
)
BACK_CLASSES = frozenset(
    name for name, bias in CLASS_BIAS.items() if bias >= 20
)

# Nathaniel cheap kit: club for melee/thief/warlock; staff for mage/druid/priest/mystic.
# Warlock casts with harm but still buys a club (MajorMUD starter quirk).
STAFF_CLASSES = paths.STAFF_CLASSES
UNARMED_CLASSES = paths.UNARMED_CLASSES
# Mage / warlock swing with harm, not a desperation poke.
SPELL_WEAPON_CLASSES = frozenset({"mage", "warlock"})
STEALTH_CLASSES = frozenset({"ninja"})
PALADIN_CLASSES = frozenset({"paladin", "pal"})


def normalize_klass(klass: str = "") -> str:
    return (klass or "").strip().lower()


def normalize_race(race: str = "") -> str:
    return paths.normalize_race(race)


def class_bias(klass: str = "") -> int:
    return CLASS_BIAS.get(normalize_klass(klass), 12)


def class_rank(klass: str = "") -> str:
    """Default row from class: warrior/warlock front, mystic mid, mage/gypsy back."""
    job = normalize_klass(klass)
    if job in FRONT_CLASSES:
        return "front"
    if job in MID_CLASSES:
        return "mid"
    return "back"


def always_front(klass: str = "") -> bool:
    return class_rank(klass) == "front"


def is_ninja(klass: str = "") -> bool:
    return normalize_klass(klass) in STEALTH_CLASSES


def is_stealth(klass: str = "") -> bool:
    return is_ninja(klass)


def is_paladin(klass: str = "") -> bool:
    return normalize_klass(klass) in PALADIN_CLASSES


def punches(klass: str = "") -> bool:
    """No weapon hand — MajorMUD `att` is a punch. Mystic fists after a staff."""
    return normalize_klass(klass) in UNARMED_CLASSES


def uses_staff(klass: str = "") -> bool:
    return normalize_klass(klass) in STAFF_CLASSES


def spell_is_weapon(klass: str = "") -> bool:
    """True when harm (or class damage) is the attack, not a desperation poke."""
    return normalize_klass(klass) in SPELL_WEAPON_CLASSES


def kit_style(klass: str = "") -> str:
    """Caster vs melee kit cue for shops / chrome. Not a per-toon flag."""
    job = normalize_klass(klass)
    if job in STEALTH_CLASSES:
        return "stealth"
    if job in UNARMED_CLASSES:
        return "unarmed"
    if job in STAFF_CLASSES or job in SPELL_WEAPON_CLASSES:
        return "caster"
    return "melee"


def is_caster_kit(klass: str = "") -> bool:
    return kit_style(klass) in {"caster", "unarmed"}


def is_melee_kit(klass: str = "") -> bool:
    return kit_style(klass) == "melee"


def sees_in_dark(race: str = "", klass: str = "") -> bool:
    """Night vision / legacy empty-race ninja. Inverse of needs_torch."""
    return not needs_torch(race, klass)


def needs_torch(race: str = "", klass: str = "") -> bool:
    return paths.needs_torch(race, klass)


def needs_weapon(klass: str = "") -> bool:
    return paths.needs_weapon(klass)


def needs_padded(klass: str = "") -> bool:
    return paths.needs_padded(klass)


def starter_weapon(klass: str = "") -> str:
    return paths.starter_weapon(klass)


def starter_light() -> str:
    return paths.STARTER_LIGHT


def uses_bash_aa(klass: str = "", aa: object = None) -> bool:
    """Client `aa` is paladin/basher auto-swing. Mystic punches with `att`."""
    return paths.uses_bash_aa(klass, aa)


def opens_swing(klass: str = "", *, aa: bool = True) -> bool:
    """Start or continue a swing. Ninja and mystic always; bashers follow `aa`."""
    if is_ninja(klass) or punches(klass):
        return True
    return bool(aa)


def bless_sort_key(klass: str = "") -> int:
    """Lower sorts first. Ninja (crit luck) before other party alts."""
    return 0 if is_ninja(klass) else 1
