"""Class kit. Gear from class/race — not per-toon json flags.

Brain asks what to buy and when the starter loop is done; it does not invent
shops. Torch / staff / club cues come from modules.roles. Sewer dark handoff
is NV role, not a named toon.
"""

from __future__ import annotations

from .. import paths
from .party import CHARS, identity_key, same_toon
from .roles import (
    needs_torch,
    needs_weapon,
    sees_in_dark,
    starter_light,
    starter_weapon,
    uses_bash_aa,
)


def armour_count() -> int:
    return len(paths.ARMOUR_ITEMS)


def is_naked(
    worn: list[str] | None = None,
    inventory: list[str] | None = None,
    extras: list[str] | None = None,
) -> bool:
    return paths.is_naked(worn, inventory, extras)


def kit_ready(
    *,
    armour_i: int = 0,
    weapon_worn: bool = False,
    torch_bought: bool = False,
) -> bool:
    return armour_i >= armour_count() and weapon_worn and torch_bought


def shop_goal(
    *,
    armour_i: int = 0,
    weapon_worn: bool = False,
    torch_bought: bool = False,
    spells_shopped: bool = False,
) -> str:
    """Next Newhaven pin: padded, club/staff, torch, class scrolls, then pit."""
    if armour_i < armour_count():
        return "armour"
    if not weapon_worn:
        return "weapons"
    if not torch_bought:
        return "store"
    if not spells_shopped:
        return "spells"
    return "farm"


def sewer_torch_needed(
    race: str = "",
    klass: str = "",
    *,
    hunt_torch: bool = False,
    room_light_spell: bool = False,
) -> bool:
    """Sewer kit. Night vision / known room-light skip."""
    if room_light_spell:
        return False
    return bool(hunt_torch) and needs_torch(race, klass)


def _roster_nv_name(token: str = "") -> str:
    """Given name for a night-vision campaign toon matching token, else ''."""
    raw = (token or "").strip()
    if not raw:
        return ""
    for profile, user, given, klass, race in CHARS:
        if not sees_in_dark(race, klass):
            continue
        if same_toon(raw, profile) or same_toon(raw, user) or same_toon(raw, given):
            return given or profile
    return ""


def dark_pinch_leader(
    me: str = "",
    race: str = "",
    klass: str = "",
    *,
    default_leader: str = "",
    alts: list[str] | tuple[str, ...] | set[str] = (),
    torch_est: int = 0,
    tried_torch: bool = False,
) -> str | None:
    """Low torches in the dark: NV toon leads; torch-needy defers to a NV alt.

    Returns a new leader name, or None to keep the current leader.
    """
    if not tried_torch or torch_est > 1:
        return None
    if sees_in_dark(race, klass):
        key = identity_key(me)
        return key or None
    # Need light — hand lead to a known night-vision alt / roommate.
    for token in alts:
        nv = _roster_nv_name(str(token))
        if nv:
            return nv
    # Fall back: first NV on the campaign roster (dark-elf ninja, gaunt, …).
    for profile, _user, given, job, r in CHARS:
        if sees_in_dark(r, job):
            return given or profile
    return None


def restore_default_leader(
    room: str = "",
    *,
    current: str = "",
    default_leader: str = "",
) -> str:
    """Leave the sewer dark pinch: restore the configured campaign leader."""
    from . import maps

    if maps.at_sewer(room):
        return current or default_leader
    return default_leader or current


def gear_f7_action(
    *,
    mode: str,
    gear_done: bool,
    kit_ready: bool,
    weapon_worn: bool,
    armour_done: bool,
    needs_torch: bool,
) -> str:
    """F7 while dressing: promote kit-ready toons into auto-play, not cancel.

    Returns ``promote`` (gear finished → hunt), ``arm_torch`` (NV races: mark
    torch bought then promote), ``cancel``, or ``start``. Brain only dispatches.
    """
    if mode != "gear":
        return "start"
    if gear_done or kit_ready:
        return "promote"
    # Dressed enough; dark-sight races never shop a torch — stamp it done.
    if weapon_worn and armour_done and not needs_torch:
        return "arm_torch"
    return "cancel"
