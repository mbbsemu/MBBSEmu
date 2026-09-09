"""Attack / Defense split: class+stats base, then worn kit.

Accuracy base is the 1.11p formula from the module (level, combat rating,
strength, agility). Kit numbers are from WCCITEMS.DAT — ACC field on the
Newhaven value weapons is 0; they gift damage. Padded AC is stored ×10.
"""

from __future__ import annotations

from dataclasses import dataclass

# Displayed combat rating (mm2json already subtracts the stored +2).
COMBAT_LEVEL = {
    "ninja": 3,
    "paladin": 4,
    "warrior": 4,
    "witchunter": 5,
    "cleric": 3,
    "priest": 1,
    "missionary": 2,
    "mage": 1,
    "druid": 2,
    "warlock": 3,
    "thief": 2,
    "gypsy": 2,
    "ranger": 4,
    "bard": 3,
    "mystic": 3,
}

# Display AC / ACC / weapon dice. ACC 0 on these shop sticks is real.
# Longer unique names first so "ebony ninjato" does not become "ninjato".
ITEMS: dict[str, dict[str, int]] = {
    "ebony ninjato": {"acc": 0, "ac": 0, "min": 5, "max": 18},
    "shimmering greatsword": {"acc": 0, "ac": 0, "min": 9, "max": 28},
    "stiletto": {"acc": 0, "ac": 0, "min": 1, "max": 4},
    "battle axe": {"acc": 0, "ac": 0, "min": 4, "max": 15},
    "club": {"acc": 0, "ac": 0, "min": 3, "max": 8},
    "quarterstaff": {"acc": 0, "ac": 0, "min": 3, "max": 8},
    "ninjato": {"acc": 0, "ac": 0, "min": 3, "max": 13},
    "padded vest": {"acc": 0, "ac": 6},
    "padded pants": {"acc": 0, "ac": 1},
    "padded helm": {"acc": 0, "ac": 1},
    "padded gloves": {"acc": 0, "ac": 1},
    "padded boots": {"acc": 0, "ac": 1},
}


@dataclass(frozen=True)
class CombatSheet:
    at_base: int | None = None
    at_gear: int = 0
    df_base: int | None = None
    df_gear: int = 0
    dmg_min: int | None = None
    dmg_max: int | None = None

    def label(self, *, compact: bool = False) -> str:
        """Footer cluster: AT 32+0  DF 4+10  4-15. Compact drops dice."""
        bits: list[str] = []
        if self.at_base is not None:
            bits.append(f"AT {self.at_base}+{self.at_gear}")
        elif self.at_gear:
            bits.append(f"AT +{self.at_gear}")
        if self.df_base is not None:
            bits.append(f"DF {self.df_base}+{self.df_gear}")
        elif self.df_gear:
            bits.append(f"DF +{self.df_gear}")
        if (
            not compact
            and self.dmg_min is not None
            and self.dmg_max is not None
        ):
            bits.append(f"{self.dmg_min}-{self.dmg_max}")
        return "  ".join(bits)


def combat_level(klass: str) -> int:
    return COMBAT_LEVEL.get((klass or "").strip().lower(), 3)


def accuracy_base(
    level: int, combat: int, strength: int, agility: int
) -> int:
    """OG 1.11p accuracy before worn ACC / encum / rank / bless."""
    stored = combat + 2
    rating = int(level**0.5)
    while (rating + 1) * (rating + 1) <= level:
        rating += 1
    rating = rating * (stored - 1)
    rating = (rating + stored * 2 + level // 2 - 2) * 2
    rating += (strength - 50) // 3
    rating += (agility - 50) // 6
    return rating


def _item_bonus(name: str) -> dict[str, int] | None:
    low = name.strip().lower()
    if not low:
        return None
    if low in ITEMS:
        return ITEMS[low]
    for key, bonus in ITEMS.items():
        if key in low:
            return bonus
    return None


def gear_from_worn(worn: list[str]) -> tuple[int, int, int | None, int | None]:
    """ACC, AC, and the best weapon dice on the worn list."""
    acc = 0
    ac = 0
    dmg_min: int | None = None
    dmg_max: int | None = None
    seen: set[str] = set()
    for raw in worn:
        bonus = _item_bonus(raw)
        if bonus is None:
            continue
        key = next(
            (name for name in ITEMS if name in raw.strip().lower()), raw.lower()
        )
        if key in seen:
            continue
        seen.add(key)
        acc += int(bonus.get("acc") or 0)
        ac += int(bonus.get("ac") or 0)
        lo = bonus.get("min")
        hi = bonus.get("max")
        if lo is not None and hi is not None:
            if dmg_max is None or hi > dmg_max:
                dmg_min, dmg_max = int(lo), int(hi)
    return acc, ac, dmg_min, dmg_max


def sheet(
    *,
    klass: str = "",
    level: int | None = None,
    strength: int | None = None,
    agility: int | None = None,
    attack: int | None = None,
    ac: int | None = None,
    worn: list[str] | None = None,
) -> CombatSheet:
    """Base from formula or `stat` totals minus kit; kit from worn names."""
    at_gear, df_gear, dmg_min, dmg_max = gear_from_worn(worn or [])
    at_base: int | None = None
    df_base: int | None = None
    if (
        level is not None
        and strength is not None
        and agility is not None
    ):
        at_base = accuracy_base(level, combat_level(klass), strength, agility)
    if attack is not None:
        at_base = attack - at_gear
    if ac is not None:
        df_base = ac - df_gear
    return CombatSheet(
        at_base=at_base,
        at_gear=at_gear,
        df_base=df_base,
        df_gear=df_gear,
        dmg_min=dmg_min,
        dmg_max=dmg_max,
    )
