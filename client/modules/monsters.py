"""Farm/live mobs from the room list. Class hunt, not a named toon's hit list.

Brain asks who to swing; targeting rules live here.
"""

from __future__ import annotations

from .. import paths

# Newhaven / arena fodder markers (rats, bugs, worms, slimes, kobolds, …).
_ARENA_TRASH = paths.LOPS + ("bug",)


def farm_here(mobs: list[str] | tuple[str, ...] = ()) -> str | None:
    """Newest farm mob, as `attack_name` would swing it."""
    return paths.lop_in(list(mobs))


def living_farm(mobs: list[str] | tuple[str, ...] = ()) -> str | None:
    """Newest farm mob Harm can hit."""
    return paths.living_lop(list(mobs))


def attack_name(raw: str = "") -> str:
    return paths.attack_name(raw) or ""


def same_mob(left: str = "", right: str = "") -> bool:
    return paths.same_mob(left, right)


def is_trash(name: str = "") -> bool:
    """Newhaven / arena fodder."""
    low = (name or "").lower()
    return any(word in low for word in _ARENA_TRASH)


def farm_species(mobs: list[str] | tuple[str, ...] = ()) -> set[str]:
    """Unique farm swing names currently listed."""
    names: set[str] = set()
    for mob in mobs:
        live = farm_here([mob])
        if live:
            names.add(live.lower())
    return names


def still_here(mobs: list[str] | tuple[str, ...] = (), name: str = "") -> str | None:
    """Return the swing name if that farm mob is still listed here."""
    if not name:
        return None
    for mob in mobs:
        if same_mob(mob, name) and farm_here([mob]):
            return attack_name(mob) or None
    return None


def bless_priority(name: str = "") -> int:
    """Lower sorts first. Ninja (crit luck) before other party alts."""
    from .party import roster_class
    from .roles import bless_sort_key

    return bless_sort_key(roster_class(name))
