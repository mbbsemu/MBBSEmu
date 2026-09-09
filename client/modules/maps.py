"""Realm rooms and areas. Brain asks; it does not invent a walk.

Rest parks on the creek bridge SW of the GY gate — never the shack.
"""

from __future__ import annotations

from .. import paths


def area_of(room: str = "") -> str:
    """Coarse area key for party isolation (graveyard vs newhaven vs sewer)."""
    if paths.at_graveyard(room) or paths.at_gy_shack(room) or paths.at_graveyard_gate(room):
        return "graveyard"
    if paths.at_crypt(room):
        return "crypt"
    if paths.at_sewer(room):
        return "sewer"
    if paths.in_newhaven(room):
        return "newhaven"
    title = (room or "").strip()
    return title.split(",")[0].strip().lower() if title else ""


def in_newhaven(room: str = "") -> bool:
    return paths.in_newhaven(room)


def at_sewer(room: str = "") -> bool:
    return paths.at_sewer(room)


def at_crypt(room: str = "") -> bool:
    return paths.at_crypt(room)


def at_graveyard(room: str = "") -> bool:
    return paths.at_graveyard(room)


def at_gy_shack(room: str = "") -> bool:
    """West of the GY gate — walk SW to the bridge; do not sit here."""
    return paths.at_gy_shack(room)


def at_graveyard_gate(room: str = "") -> bool:
    return paths.at_graveyard_gate(room)


def at_rest_park(room: str = "") -> bool:
    """Creek bridge one step SW of the GY gate. Not the shack / Bridge Street."""
    return paths.at_rest_park(room)


def at_farm(room: str = "") -> bool:
    """Newhaven pit, Silvermere graveyard, or sewers if we fell in."""
    return paths.at_farm(room)


def at_manhole_entry(room: str = "") -> bool:
    low = (room or "").lower()
    return "town square" in low or "fountain" in low


def park_rest_room(room: str = "", *, pit: bool = False) -> bool:
    """GY / shack / crypt / sewer — walk to the bridge instead of sitting here."""
    if pit or in_newhaven(room):
        return False
    return (
        at_graveyard(room)
        or at_gy_shack(room)
        or at_crypt(room)
        or at_sewer(room)
    )


def gy_to_bridge_step(room: str = "") -> str | None:
    """SW onto the creek bridge. Gate west is the shack — do not linger W."""
    return paths.gy_to_bridge_step(room)


def farm_arrived(room: str = "", *, dangerous: bool = False) -> bool:
    """Kit walk ends when the pit / farm is underfoot."""
    return at_farm(room) or bool(dangerous)
