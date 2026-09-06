"""Base Attack / Defense vs worn kit."""

from __future__ import annotations

from .combat import accuracy_base, combat_level, gear_from_worn, sheet
from .paths import ARMOUR_ITEMS
from .state import WorldState


def test_accuracy_base_uses_stored_combat() -> None:
    """Displayed combat 3 (ninja) is stored 5 in the OG formula."""
    assert combat_level("ninja") == 3
    assert combat_level("paladin") == 4
    assert accuracy_base(1, 3, 70, 80) == 35
    assert accuracy_base(3, 4, 80, 50) == 42


def test_padded_and_value_weapons() -> None:
    acc, ac, lo, hi = gear_from_worn(["stiletto", *ARMOUR_ITEMS])
    assert acc == 0 and ac == 10 and lo == 1 and hi == 4
    acc, ac, lo, hi = gear_from_worn(["battle axe", *ARMOUR_ITEMS])
    assert acc == 0 and ac == 10 and lo == 4 and hi == 15
    acc, ac, lo, hi = gear_from_worn(["ebony ninjato"])
    assert lo == 5 and hi == 18
    acc, ac, lo, hi = gear_from_worn(["shimmering greatsword"])
    assert lo == 9 and hi == 28


def test_sheet_splits_stat_total_minus_kit() -> None:
    ninja = sheet(
        klass="ninja",
        level=1,
        strength=70,
        agility=80,
        attack=35,
        ac=14,
        worn=["stiletto", *ARMOUR_ITEMS],
    )
    assert ninja.at_base == 35
    assert ninja.at_gear == 0
    assert ninja.df_base == 4
    assert ninja.df_gear == 10
    assert ninja.dmg_min == 1 and ninja.dmg_max == 4
    assert ninja.label() == "AT 35+0  DF 4+10  1-4"
    paladin = sheet(
        klass="paladin",
        level=3,
        strength=80,
        agility=50,
        attack=42,
        ac=16,
        worn=["battle axe", *ARMOUR_ITEMS],
    )
    assert paladin.label() == "AT 42+0  DF 6+10  4-15"
    assert paladin.label(compact=True) == "AT 42+0  DF 6+10"


def test_state_combat_label_from_worn() -> None:
    s = WorldState()
    s.klass = "ninja"
    s.level = 1
    s.strength = 70
    s.agility = 80
    s.worn = ["stiletto", *ARMOUR_ITEMS]
    assert s.combat_label() == "AT 35+0  DF +10  1-4"
    s.apply({"kind": "stats", "attack": 35, "ac": 14})
    assert s.combat_label() == "AT 35+0  DF 4+10  1-4"


if __name__ == "__main__":
    test_accuracy_base_uses_stored_combat()
    test_padded_and_value_weapons()
    test_sheet_splits_stat_total_minus_kit()
    test_state_combat_label_from_worn()
    print("ok")
