"""Sense the player and return module offers. Brain asks; it does not invent paths."""

from __future__ import annotations

from dataclasses import dataclass

from .. import spells
from .catalog import Ability, abilities_for, get


@dataclass(frozen=True)
class PlayerFacts:
    klass: str
    race: str = ""
    level: int | None = None
    known: frozenset[str] = frozenset()
    dark: bool = False
    lit: bool = False
    willed: bool = False
    blessed: bool = False
    in_combat: bool = False


@dataclass(frozen=True)
class Offer:
    kind: str
    name: str
    command: str = ""
    where: str = ""
    ask: bool = True
    reason: str = ""


def _available(ab: Ability, facts: PlayerFacts) -> bool:
    if facts.klass not in ab.classes:
        return False
    if facts.level is None:
        return False
    return int(facts.level) >= ab.level


def flag_up(ab: Ability, facts: PlayerFacts) -> bool:
    if ab.buff_flag == "willed":
        return facts.willed
    if ab.buff_flag == "blessed":
        return facts.blessed
    if ab.buff_flag == "lit":
        return facts.lit
    return False


def may_use(name: str, facts: PlayerFacts) -> bool:
    """False when the buff is already up (owl prompts if recast) or gated."""
    ab = get(name)
    if ab is None:
        return False
    if not _available(ab, facts) and facts.level is not None:
        return False
    if flag_up(ab, facts):
        return False
    return True


def can_shop(name: str) -> bool:
    ab = get(name)
    return bool(ab and ab.shop_item)


def learn_where(name: str) -> str:
    ab = get(name)
    return ab.learn_where if ab else ""


def known_room_light(klass: str, known: set[str] | frozenset[str]) -> str:
    have = {name.strip().lower() for name in known}
    for ab in abilities_for(klass):
        if ab.room_light and ab.name in have:
            return ab.name
    return ""


def light_action(facts: PlayerFacts) -> Offer | None:
    """Dark room: cast a known room-light spell, else offer to learn it."""
    if not facts.dark:
        return None
    pending: Offer | None = None
    for ab in abilities_for(facts.klass):
        if not ab.room_light:
            continue
        if ab.name in facts.known:
            if not may_use(ab.name, facts):
                return None
            return Offer(
                kind=ab.verb,
                name=ab.name,
                command=spells.command(ab.name),
                ask=False,
                reason="dark",
            )
        if _available(ab, facts) and pending is None:
            pending = Offer(
                kind="learn",
                name=ab.name,
                where=ab.learn_where,
                ask=True,
                reason="dark",
            )
    return pending


def _offer_to_learn(ab: Ability) -> bool:
    """Ask about gettable extras. Shop autos stay on next_due. Heals are not y/n.

    Level-up kai (owl) unlocks on train — never a shop/learn/goto target.
    """
    if ab.level_up or ab.auto_shop:
        return False
    if ab.room_light:
        return True
    return ab.kind == "buff" and bool(ab.buff_flag)


def next_learn(facts: PlayerFacts, listed: object = None) -> str:
    """First gettable ability: shop auto-list, then catalog extras (starlight)."""
    shop = spells.next_due(facts.klass, listed, facts.level, set(facts.known))
    if shop:
        return shop
    for ab in abilities_for(facts.klass):
        if not _offer_to_learn(ab) or ab.name in facts.known:
            continue
        if not _available(ab, facts):
            continue
        return ab.name
    return ""


def learn_offer(facts: PlayerFacts, listed: object = None) -> Offer | None:
    name = next_learn(facts, listed)
    if not name:
        return None
    ab = get(name)
    return Offer(
        kind="learn",
        name=name,
        where=ab.learn_where if ab else "",
        ask=True,
        reason="level",
    )
