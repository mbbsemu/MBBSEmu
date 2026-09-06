from __future__ import annotations

import tempfile
import time
from pathlib import Path

from . import gear as G
from .brain import HEAL_ASK, HEAL_RATIO, Brain
from .parse import harvest_screen, parse_events, parse_line
from .paths import (
    ARMOUR_ITEMS,
    STARTER_LIGHT,
    STARTER_WEAPON,
    is_starter_weapon,
    starter_weapon,
    attack_line,
    attack_name,
    coins_in,
    lop_in,
    occupants_in,
    peel_presence,
)
from .realm_map import Atlas
from .state import WorldState


def _see_tile(brain: Brain, state: WorldState) -> None:
    """Finish the post-step look so the next tick may walk again."""
    brain.compass._confirm(state.room, state.exits)
    state.travel_seq += 1
    state.scanned = True
    state.look_scan = False


def _sneak_try_wait(brain: Brain, state: WorldState, sent: list[str]) -> None:
    """Attempting arms sneak. A `d` on that prompt would break it."""
    state.apply({"kind": "sneak_try"})
    state.prompt_seq += 1
    n = len(sent)
    brain.tick(state, sent.append, pending=False)
    assert "d" not in sent[n:]
    assert brain._sneak_armed
    brain._sneak_ready_at = 0.0


def test_lawful_does_not_attack_players() -> None:
    b = Brain(allowed=True, pvp=False, me="sysop")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 30
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.mobs = ["klymacks", "nasty acid slime"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["att acid slime"]
    assert not b.bail
    assert b.mode == "hunt"
    assert "klymacks" not in " ".join(sent)


def test_switches_off_dead_filthbug_to_kobold() -> None:
    b = Brain(allowed=True)
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._attacking = "filthbug"
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 51
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.in_combat = True
    state.mobs = ["filthbug", "large kobold thief"]
    state.last_kill = "The filthbug"
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["att kobold thief"]
    assert not b.bail


def test_sysop_login_follows_matt_invite() -> None:
    """BBS user sysop is klymacks — Matt's invite is not self."""
    b = Brain(
        allowed=True,
        me="sysop Klymacks",
        alts="matt",
        party_leader="Matt",
        rank="back",
        klass="ninja",
    )
    b.mode = "manual"
    state = WorldState()
    state.in_realm = True
    state.apply(parse_line("Matt has invited you to follow him."))
    sent: list[str] = []
    assert b.on_invite(state, sent.append)
    assert sent == ["follow Matt"]


def test_sysop_hunts_past_matt() -> None:
    b = Brain(allowed=True, pvp=False, me="sysop klymacks", alts="matt")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 24
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 80
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.mobs = ["Matt", "acid slime", "nasty lashworm"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert not b.bail
    assert b.mode == "hunt"
    assert sent[-1] in ("att acid slime", "att lashworm")
    assert "quit" not in sent
    assert "matt" not in " ".join(sent).lower()


def test_given_name_matt_is_not_pvp() -> None:
    b = Brain(allowed=True, pvp=False, me="klymacks Matt")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._attacking = "acid slime"
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 71
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.in_combat = True
    state.mobs = ["Matt", "acid slime", "nasty lashworm"]
    state.last_actor = "Matt"
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert not b.bail
    assert "matt" in b._aka
    assert sent == []
    assert b.next_action == "fighting acid slime"


def test_lashworm_is_not_a_player() -> None:
    b = Brain(allowed=True, pvp=False, me="klymacks")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 29
    state.max_hp_known = True
    state.prompt_seq = 70
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.apply({"kind": "arrive", "name": "nasty lashworm"})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["att lashworm"]
    assert not b.bail


def test_named_lunge_is_not_pvp() -> None:
    b = Brain(allowed=True, pvp=False, me="klymacks")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 29
    state.max_hp = 35
    state.max_hp_known = True
    state.prompt_seq = 50
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.apply(
        {
            "kind": "combat",
            "name": "kobold thief",
        }
    )
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["att kobold thief"]
    assert not b.bail
    assert b.mode == "hunt"


def test_lawful_bails_on_stranger() -> None:
    b = Brain(allowed=True, pvp=False, me="sysop")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 32
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.mobs = ["Aelthas", "nasty acid slime"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["att acid slime"]
    assert not b.bail


def test_arena_slime_lashworm_does_not_logoff() -> None:
    blob = """Also here: acid slime, nasty lashworm.
Obvious exits: closed door north, up
The acid slime flails at you!
The nasty lashworm darts forward and bites you for 4 damage!
The nasty lashworm lunges at you!
[HP=24/MA=8]:"""
    for kwargs in (
        dict(me="sysop Matt", alts="klymacks", party_leader="Matt"),
        dict(me="klymacks Klymacks", alts="matt sysop", party_leader="Matt", rank="back"),
    ):
        b = Brain(allowed=True, **kwargs)
        b.gear_done = True
        b.mode = "hunt"
        b._in_camp = True
        if b._following():
            b._joined = True
            b._followed = True
            b._ranked = True
        state = WorldState()
        state.in_realm = True
        state.hp = 24
        state.max_hp = 28
        state.max_hp_known = True
        state.prompt_seq = 90
        state.room = "Newhaven, Arena"
        state.scanned = True
        for ev in parse_events(blob):
            state.apply(ev)
        sent: list[str] = []
        b.tick(state, sent.append, pending=False)
        assert sent == ["att lashworm"]
        assert not b.bail
        assert b.mode == "hunt"
        assert b.next_action != "logoff"


def test_matt_does_not_attack_klymacks_on_the_rat() -> None:
    assert peel_presence("giant rat Klymacks") == ["giant rat", "Klymacks"]
    assert peel_presence("acid slimeKlymacks") == ["acid slime", "Klymacks"]
    assert peel_presence("d slimeKlymacks") == ["slime", "Klymacks"]
    assert peel_presence("d rat") == ["rat"]
    assert peel_presence("d giant rat") == ["giant rat"]
    assert attack_name("giant rat Klymacks") == "giant rat"
    assert attack_name("d slimeKlymacks") == "slime"
    assert attack_name("d rat") == "rat"
    assert attack_name("d giant rat") == "giant rat"
    assert attack_name("down rat") == "rat"
    assert attack_name("Also here: d rat") == "rat"
    assert "d " not in attack_name("d rat")
    assert "d " not in attack_name("d giant rat")
    b = Brain(
        allowed=True,
        me="sysop Matt",
        alts="klymacks",
        party_leader="Matt",
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._invited = True
    state = WorldState()
    state.followers = ["Klymacks"]
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 91
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.apply({"kind": "also_here", "mobs": ["giant rat Klymacks"]})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["att giant rat"]
    assert not b.bail
    assert "klymacks" not in " ".join(sent).lower()
    state.apply({"kind": "also_here", "mobs": ["d slimeKlymacks"]})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "att slime"
    assert "klymacks" not in sent[-1].lower()


def test_matt_peels_exit_off_attack() -> None:
    """After `d` into the pit, a leftover exit must not become `attack d rat`."""
    assert attack_name("d rat") == "rat"
    assert attack_name("d giant rat") == "giant rat"
    assert attack_name("d slimeKlymacks") == "slime"
    assert attack_name("down rat") == "rat"
    assert attack_name("Also here: d rat") == "rat"
    assert attack_name("drat") == "rat"
    b = Brain(
        allowed=True,
        me="sysop Matt",
        alts="klymacks",
        party_leader="Matt",
        klass="paladin",
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._invited = True
    state = WorldState()
    state.followers = ["Klymacks"]
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 120
    state.room = "Newhaven, Narrow Road"
    state.exits = ["n", "e", "w", "d"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["d"]
    assert "arena" in state.room.lower()
    look = (
        "Newhaven, Arena\n"
        "Obvious exits: u.Also here: d rat.\n"
        "[HP=28]:\n"
    )
    for ev in parse_events(look):
        state.apply(ev)
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "aa rat"
    assert sent[-1] != "aa d rat"
    state.apply({"kind": "also_here", "mobs": ["d giant rat"]})
    state.prompt_seq += 1
    b._attacking = ""
    state.in_combat = False
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "aa giant rat"
    state.mobs = ["d rat"]
    state.prompt_seq += 1
    b._attacking = ""
    state.in_combat = False
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "aa rat"


def test_paladin_heals_and_saves_harm() -> None:
    b = Brain(
        allowed=True,
        me="sysop Matt",
        klass="paladin",
        spell_list=["minor healing", "harm"],
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 13
    state.max_hp = 28
    state.max_hp_known = True
    state.ma = 8
    state.max_ma = 8
    state.prompt_seq = 92
    state.room = "Newhaven, Arena"
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["cast minor healing"]

    state.hp = 28
    state.ma = 8
    state.mobs = ["giant rat", "giant rat"]
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "aa giant rat"
    b._cast_at = time.monotonic() - 9
    n = len(sent)
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert not any("harm" in c for c in sent[n:])

    state.hp = 27
    n = len(sent)
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert not any("harm" in c for c in sent[n:])
    assert not any(c.startswith("cast minor healing") for c in sent[n:])

    state.hp = 13
    state.ma = 8
    b._cast_at = time.monotonic() - 9
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "cast minor healing"
    assert "harm" not in sent[-1]

    state.hp = 28
    state.ma = 8
    state.mobs = ["filthbug", "nasty lashworm"]
    b._attacking = "filthbug"
    state.in_combat = True
    b._last_cast = ""
    b._cast_at = time.monotonic() - 9
    n = len(sent)
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert not any("harm" in c for c in sent[n:])

    state.mobs = ["acid slime"]
    b._attacking = "acid slime"
    n = len(sent)
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert not any("harm" in c for c in sent[n:])

    state.hp = 28
    state.ma = 8
    state.mobs = ["the ogre"]
    b._attacking = ""
    state.in_combat = False
    b._last_cast = ""
    b._cast_at = 0.0
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "aa ogre"
    assert "harm" not in sent[-1]
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "cast harm ogre"

    state.ma = 4
    b._last_cast = ""
    b._cast_at = time.monotonic() - 9
    n = len(sent)
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert not any("harm" in c for c in sent[n:])


def test_harm_desperate_living_not_slime() -> None:
    b = Brain(
        allowed=True,
        me="sysop Matt",
        klass="paladin",
        spell_list=["minor healing", "harm"],
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 8
    state.max_hp = 28
    state.max_hp_known = True
    state.ma = 8
    state.max_ma = 8
    state.prompt_seq = 92
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.in_combat = True
    state.mobs = ["giant rat"]
    b._attacking = "giant rat"
    sent: list[str] = []
    assert b._try_harm(state, sent.append, "giant rat")
    assert sent == ["cast harm giant rat"]

    sent = []
    b._last_cast = ""
    b._cast_at = 0.0
    state.mobs = ["acid slime"]
    b._attacking = "acid slime"
    assert not b._try_harm(state, sent.append, "acid slime")
    assert sent == []

    sent = []
    state.hp = 28
    state.mobs = ["giant rat"]
    b._attacking = "giant rat"
    assert not b._try_harm(state, sent.append, "giant rat")
    assert sent == []

    sent = []
    b._attacking = ""
    state.mobs = ["the ogre"]
    assert not b._try_harm(state, sent.append, "ogre")
    assert sent == []


def _matt_bless() -> tuple[Brain, WorldState]:
    b = Brain(
        allowed=True,
        me="sysop Matt",
        klass="paladin",
        spell_list=["minor healing", "harm", "bless"],
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 49
    state.max_hp = 49
    state.max_hp_known = True
    state.ma = 10
    state.max_ma = 10
    state.prompt_seq = 500
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.level = 2
    return b, state


def test_matt_casts_bless_when_not_fighting() -> None:
    b, state = _matt_bless()
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["cast bless"]


def test_matt_skips_bless_when_already_lucky() -> None:
    b, state = _matt_bless()
    state.apply({"kind": "buff", "name": "bless", "on": True})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == []
    assert "rest" not in sent
    assert "cast bless" not in sent
    state.prompt_seq += 1
    b._cast_at = time.monotonic() - 9
    b._last_cast = ""
    b.tick(state, sent.append, pending=False)
    assert sent == []
    assert sent.count("cast bless") == 0


def test_matt_recasts_bless_after_combat_off() -> None:
    b, state = _matt_bless()
    state.apply({"kind": "buff", "name": "bless", "on": True})
    state.in_combat = True
    state.mobs = ["acid slime"]
    b._attacking = "acid slime"
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert "cast bless" not in sent
    state.apply({"kind": "buff", "name": "bless", "on": False})
    assert not state.blessed
    state.prompt_seq += 1
    b._cast_at = time.monotonic() - 9
    b._last_cast = ""
    b.tick(state, sent.append, pending=False)
    assert "cast bless" not in sent
    for ev in parse_events(
        "The acid slime dissolves into a puddle of bluish goo."
        "You gain 16 experience."
        "*Combat Off*"
    ):
        state.apply(ev)
    state.prompt_seq += 1
    b._cast_at = time.monotonic() - 9
    b._last_cast = ""
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "look"
    assert "cast bless" not in sent
    state.apply({"kind": "also_here", "mobs": []})
    state.prompt_seq += 1
    b._cast_at = time.monotonic() - 9
    b._last_cast = ""
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "cast bless"


def test_matt_heal_beats_bless() -> None:
    b, state = _matt_bless()
    state.hp = 39
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["cast minor healing"]
    assert "bless" not in sent[0]


def test_matt_skips_bless_at_level_1() -> None:
    b, state = _matt_bless()
    state.level = 1
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert "cast bless" not in sent
    assert "bless" in b._spells
    assert sent == []
    assert "rest" not in sent


def test_matt_skips_bless_until_level_known() -> None:
    b, state = _matt_bless()
    state.level = None
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert "cast bless" not in sent
    assert "bless" in b._spells


def test_matt_keeps_bless_when_board_says_too_low() -> None:
    b, state = _matt_bless()
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["cast bless"]
    assert "bless" in b._spells
    state.apply({"kind": "cast_fail", "reason": "level"})
    state.prompt_seq += 1
    b._cast_at = time.monotonic() - 9
    sent.clear()
    b.tick(state, sent.append, pending=False)
    assert "cast bless" not in sent
    assert "bless" in b._spells
    assert not state.blessed
    state.level = 3
    state.prompt_seq += 1
    b._cast_at = time.monotonic() - 9
    sent.clear()
    b.tick(state, sent.append, pending=False)
    assert sent == ["cast bless"]
    assert "bless" in b._spells


def test_matt_keeps_bless_when_board_says_unknown() -> None:
    b, state = _matt_bless()
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["cast bless"]
    state.apply({"kind": "cast_fail", "reason": "unknown"})
    state.prompt_seq += 1
    b._cast_at = time.monotonic() - 9
    sent.clear()
    b.tick(state, sent.append, pending=False)
    assert "cast bless" not in sent
    assert "bless" in b._spells
    state.level = 3
    state.prompt_seq += 1
    b._cast_at = time.monotonic() - 9
    sent.clear()
    b.tick(state, sent.append, pending=False)
    assert sent == ["cast bless"]


def test_second_slime_after_kill_is_live() -> None:
    b, state = _matt_bless()
    state.apply({"kind": "buff", "name": "bless", "on": False})
    state.mobs = ["acid slime"]
    b._attacking = "acid slime"
    state.in_combat = True
    for ev in parse_events(
        "The acid slime dissolves into a puddle of bluish goo."
        "You gain 16 experience."
        "*Combat Off*"
        "The large acid slime flails at you!"
    ):
        state.apply(ev)
    assert state.in_combat
    assert lop_in(state.mobs)
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "aa acid slime"
    assert "cast bless" not in sent
    assert "large" not in sent[-1]


def test_ooze_arrive_engages_acid_slime() -> None:
    b, state = _matt_bless()
    state.apply({"kind": "buff", "name": "bless", "on": True})
    state.apply(parse_line("A acid slime oozes into the room from nowhere."))
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["aa acid slime"]


def test_klymacks_never_casts() -> None:
    b = Brain(
        allowed=True,
        me="klymacks Klymacks",
        alts="matt sysop",
        party_leader="Matt",
        rank="back",
        spell_list=[],
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._joined = True
    b._followed = True
    b._ranked = True
    state = WorldState()
    state.in_realm = True
    state.hp = 24
    state.max_hp = 28
    state.max_hp_known = True
    state.ma = 8
    state.max_ma = 8
    state.prompt_seq = 93
    state.room = "Newhaven, Arena"
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["rest"]
    assert "cast" not in " ".join(sent)


def test_matt_heals_klymacks_after_hit() -> None:
    b = Brain(
        allowed=True,
        me="sysop Matt",
        alts="klymacks",
        party_leader="Matt",
        klass="paladin",
        spell_list=["minor healing", "harm"],
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._invited = True
    state = WorldState()
    state.followers = ["Klymacks"]
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.ma = 8
    state.max_ma = 8
    state.prompt_seq = 94
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.apply({"kind": "also_here", "mobs": ["giant rat", "Klymacks"]})
    ev = parse_line("The nasty giant rat hits Klymacks for 14 damage!")
    assert ev
    state.apply(ev)
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["cast minor healing klymacks"]
    assert "attack" not in " ".join(sent).lower()
    assert "klymacks" not in [c.lower() for c in sent if c.startswith(("att ", "aa ", "attack ", "bs "))]


def test_matt_self_heals_before_klymacks() -> None:
    b = Brain(
        allowed=True,
        me="sysop Matt",
        alts="klymacks",
        party_leader="Matt",
        klass="paladin",
        spell_list=["minor healing", "harm"],
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._invited = True
    state = WorldState()
    state.followers = ["Klymacks"]
    state.in_realm = True
    state.hp = 13
    state.max_hp = 28
    state.max_hp_known = True
    state.ma = 8
    state.max_ma = 8
    state.prompt_seq = 95
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.apply({"kind": "also_here", "mobs": ["giant rat", "Klymacks"]})
    ev = parse_line("The nasty giant rat hits Klymacks for 14 damage!")
    assert ev
    state.apply(ev)
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["cast minor healing"]
    assert "klymacks" not in sent[0]


def test_matt_skips_heal_at_27_of_28() -> None:
    b = Brain(
        allowed=True,
        me="sysop Matt",
        alts="klymacks",
        party_leader="Matt",
        klass="paladin",
        spell_list=["minor healing", "harm"],
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._invited = True
    state = WorldState()
    state.followers = ["Klymacks"]
    state.in_realm = True
    state.hp = 27
    state.max_hp = 28
    state.max_hp_known = True
    state.ma = 8
    state.max_ma = 8
    state.prompt_seq = 400
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.apply({"kind": "also_here", "mobs": ["giant rat", "Klymacks"]})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent
    assert all("cast" not in c for c in sent)
    ev = parse_line("The nasty giant rat hits Klymacks for 3 damage!")
    assert ev
    nick = WorldState()
    nick.followers = ["Klymacks"]
    nick.in_realm = True
    nick.hp = 27
    nick.max_hp = 28
    nick.max_hp_known = True
    nick.ma = 8
    nick.max_ma = 8
    nick.prompt_seq = 404
    nick.room = "Newhaven, Arena"
    nick.scanned = True
    nick.apply({"kind": "also_here", "mobs": ["giant rat", "Klymacks"]})
    nick.apply(ev)
    b2 = Brain(
        allowed=True,
        me="sysop Matt",
        alts="klymacks",
        party_leader="Matt",
        klass="paladin",
        spell_list=["minor healing", "harm"],
    )
    b2.gear_done = True
    b2.mode = "hunt"
    b2._in_camp = True
    b2._invited = True
    nicked: list[str] = []
    b2.tick(nick, nicked.append, pending=False)
    assert nicked
    assert all("cast" not in c for c in nicked)


def test_matt_skips_party_heal_on_small_hit() -> None:
    b = Brain(
        allowed=True,
        me="sysop Matt",
        alts="klymacks",
        party_leader="Matt",
        klass="paladin",
        spell_list=["minor healing", "harm"],
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._invited = True
    state = WorldState()
    state.followers = ["Klymacks"]
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.ma = 8
    state.max_ma = 8
    state.prompt_seq = 401
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.apply({"kind": "also_here", "mobs": ["giant rat", "Klymacks"]})
    ev = parse_line("The nasty giant rat hits Klymacks for 3 damage!")
    assert ev
    state.apply(ev)
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent
    assert all("cast" not in c for c in sent)
    assert sent[-1] == "aa giant rat"


def test_matt_party_heals_after_chips() -> None:
    b = Brain(
        allowed=True,
        me="sysop Matt",
        alts="klymacks",
        party_leader="Matt",
        klass="paladin",
        spell_list=["minor healing", "harm"],
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._invited = True
    state = WorldState()
    state.followers = ["Klymacks"]
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.ma = 8
    state.max_ma = 8
    state.prompt_seq = 402
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.apply({"kind": "also_here", "mobs": ["giant rat", "Klymacks"]})
    for line in (
        "The nasty giant rat hits Klymacks for 5 damage!",
        "The nasty giant rat hits Klymacks for 5 damage!",
        "The nasty lashworm darts forward and bites Klymacks for 4 damage!",
    ):
        ev = parse_line(line)
        assert ev
        state.apply(ev)
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["cast minor healing klymacks"]


def test_matt_skips_heal_when_max_unknown() -> None:
    b = Brain(
        allowed=True,
        me="sysop Matt",
        klass="paladin",
        spell_list=["minor healing", "harm"],
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 27
    state.max_hp = 27
    state.max_hp_known = False
    state.ma = 8
    state.max_ma = 8
    state.prompt_seq = 403
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.mobs = ["giant rat"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent
    assert all("cast" not in c for c in sent)
    assert sent[-1] == "aa giant rat"


def test_matt_heals_at_80_percent_not_above() -> None:
    b = Brain(
        allowed=True,
        me="sysop Matt",
        klass="paladin",
        spell_list=["minor healing", "harm"],
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 80
    state.max_hp = 100
    state.max_hp_known = True
    state.ma = 8
    state.max_ma = 8
    state.prompt_seq = 500
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.mobs = ["giant rat"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["cast minor healing"]
    assert state.hp_ratio() == HEAL_RATIO

    state.hp = 79
    b._cast_at = 0.0
    b._last_cast = ""
    state.prompt_seq += 1
    sent = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["cast minor healing"]

    state.hp = 81
    b._cast_at = 0.0
    b._last_cast = ""
    state.prompt_seq += 1
    sent = []
    b.tick(state, sent.append, pending=False)
    assert sent
    assert all("cast" not in c for c in sent)
    assert sent[-1] == "aa giant rat"


def test_klymacks_asks_heal_once_when_following() -> None:
    b = Brain(
        allowed=True,
        me="klymacks Klymacks",
        alts="matt sysop",
        party_leader="Matt",
        rank="back",
        klass="ninja",
        spell_list=[],
        stealth="walk",
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._joined = True
    b._followed = True
    b._ranked = True
    state = WorldState()
    state.in_realm = True
    state.following = "Matt"
    state.hp = 70
    state.max_hp = 100
    state.max_hp_known = True
    state.prompt_seq = 501
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.apply({"kind": "also_here", "mobs": ["giant rat", "Matt"]})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == [HEAL_ASK]
    assert HEAL_ASK == "heal me"
    assert "say" not in sent[0]
    assert "cast" not in " ".join(sent)
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent.count(HEAL_ASK) == 1
    assert sent[-1] == "att giant rat"

    state.hp = 76
    state.prompt_seq += 1
    n = len(sent)
    b.tick(state, sent.append, pending=False)
    assert sent[n:] == [HEAL_ASK]

    state.hp = 81
    state.prompt_seq += 1
    n = len(sent)
    b.tick(state, sent.append, pending=False)
    assert HEAL_ASK not in sent[n:]
    assert "say heal" not in sent[n:]

    solo = Brain(
        allowed=True,
        me="klymacks Klymacks",
        party_leader="Matt",
        klass="ninja",
        spell_list=[],
        stealth="walk",
    )
    solo.gear_done = True
    solo.mode = "hunt"
    solo._in_camp = True
    lonely = WorldState()
    lonely.in_realm = True
    lonely.hp = 80
    lonely.max_hp = 100
    lonely.max_hp_known = True
    lonely.prompt_seq = 502
    lonely.room = "Newhaven, Arena"
    lonely.scanned = True
    lonely.mobs = ["giant rat"]
    alone: list[str] = []
    solo.tick(lonely, alone.append, pending=False)
    assert HEAL_ASK not in alone
    assert "say heal" not in alone
    assert "cast" not in " ".join(alone)


def test_matt_heals_on_heal_me() -> None:
    b = Brain(
        allowed=True,
        me="sysop Matt",
        alts="klymacks",
        party_leader="Matt",
        klass="paladin",
        spell_list=["minor healing", "harm"],
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._invited = True
    state = WorldState()
    state.followers = ["Klymacks"]
    state.in_realm = True
    state.hp = 81
    state.max_hp = 100
    state.max_hp_known = True
    state.ma = 8
    state.max_ma = 8
    state.prompt_seq = 503
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.apply({"kind": "also_here", "mobs": ["giant rat", "Klymacks"]})
    ev = parse_line('Klymacks says "heal me"')
    assert ev and ev["kind"] == "heal_ask"
    state.apply(ev)
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["cast minor healing klymacks"]
    assert "klymacks" not in state.heal_asks
    b._cast_at = time.monotonic() - 9
    b._last_cast = ""
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent.count("cast minor healing klymacks") == 1
    assert sent[-1] == "aa giant rat"


def test_matt_still_heals_on_old_say_heal() -> None:
    """Spoken leftover `heal` / `say heal` still casts."""
    b = Brain(
        allowed=True,
        me="sysop Matt",
        alts="klymacks",
        party_leader="Matt",
        klass="paladin",
        spell_list=["minor healing", "harm"],
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._invited = True
    state = WorldState()
    state.followers = ["Klymacks"]
    state.in_realm = True
    state.hp = 81
    state.max_hp = 100
    state.max_hp_known = True
    state.ma = 8
    state.max_ma = 8
    state.prompt_seq = 504
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.apply({"kind": "also_here", "mobs": ["giant rat", "Klymacks"]})
    old = parse_line('Klymacks says "say heal"')
    assert old and old["kind"] == "heal_ask"
    state.apply(old)
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["cast minor healing klymacks"]


def test_klymacks_never_heals_party() -> None:
    b = Brain(
        allowed=True,
        me="klymacks Klymacks",
        alts="matt sysop",
        party_leader="Matt",
        rank="back",
        klass="ninja",
        spell_list=[],
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._joined = True
    b._followed = True
    b._ranked = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.ma = 8
    state.prompt_seq = 96
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.apply({"kind": "also_here", "mobs": ["giant rat", "Matt"]})
    ev = parse_line("The nasty giant rat hits Matt for 5 damage!")
    assert ev
    state.apply(ev)
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert "cast" not in " ".join(sent)
    assert all("klymacks" not in c.lower() or not c.startswith(("att ", "aa ", "attack ", "bs ")) for c in sent)


def test_matt_does_not_heal_klymacks_after_leave() -> None:
    b = Brain(
        allowed=True,
        me="sysop Matt",
        alts="klymacks",
        party_leader="Matt",
        klass="paladin",
        spell_list=["minor healing", "harm"],
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._invited = True
    state = WorldState()
    state.followers = ["Klymacks"]
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.ma = 8
    state.prompt_seq = 97
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.apply({"kind": "also_here", "mobs": ["giant rat", "Klymacks"]})
    ev = parse_line("The nasty giant rat hits Klymacks for 5 damage!")
    assert ev
    state.apply(ev)
    state.apply({"kind": "also_here", "mobs": ["giant rat"]})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent
    assert "cast minor healing" not in sent[0]
    assert "klymacks" not in sent[-1].lower()
    assert sent[-1] == "aa giant rat"


def test_matt_does_not_heal_or_attack_klymacks_corpse() -> None:
    b = Brain(
        allowed=True,
        me="sysop Matt",
        alts="klymacks",
        party_leader="Matt",
        klass="paladin",
        spell_list=["minor healing", "harm"],
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._invited = True
    state = WorldState()
    state.followers = ["Klymacks"]
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.ma = 8
    state.prompt_seq = 98
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.apply({"kind": "also_here", "mobs": ["giant rat", "Klymacks"]})
    ev = parse_line("The nasty giant rat hits Klymacks for 5 damage!")
    assert ev
    state.apply(ev)
    state.apply({"kind": "killed", "name": "Klymacks"})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent
    assert "cast" not in sent[-1]
    assert "klymacks" not in sent[-1].lower()


def test_harm_still_living_only_with_klymacks_here() -> None:
    b = Brain(
        allowed=True,
        me="sysop Matt",
        alts="klymacks",
        party_leader="Matt",
        klass="paladin",
        spell_list=["minor healing", "harm"],
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._invited = True
    state = WorldState()
    state.followers = ["Klymacks"]
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.ma = 8
    state.max_ma = 8
    state.prompt_seq = 99
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.mobs = ["Klymacks", "nasty lashworm", "giant rat"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent[-1] in ("aa lashworm", "aa giant rat")
    assert "klymacks" not in sent[-1].lower()
    assert "healing" not in " ".join(sent)
    n = len(sent)
    b._cast_at = time.monotonic() - 9
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert not any("harm" in c for c in sent[n:])
    assert "klymacks" not in sent[-1].lower()
    assert "slime" not in sent[-1]


def test_friendly_fire_logs_off() -> None:
    b = Brain(allowed=True, pvp=False, me="klymacks", alts="matt sysop")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 33
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.apply({"kind": "said", "aimed": "Matt"})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert b.bail.startswith("hit")
    assert b.mode == "manual"


def _following_klymacks(*, hidden: bool = False) -> tuple[Brain, WorldState]:
    b = Brain(
        allowed=True,
        me="klymacks",
        alts="matt sysop",
        party_leader="Matt",
        rank="back",
        klass="ninja",
        stealth="always",
    )
    b.gear_done = True
    b.mode = "manual"
    b._joined = True
    b._followed = True
    b._ranked = True
    b._in_camp = True
    b._hidden = hidden
    b._sneaking = hidden
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 500
    state.room = "Newhaven, Arena"
    state.exits = ["u"]
    state.scanned = True
    state.following = "Matt"
    state.mobs = ["Matt", "acid slime"]
    return b, state


def test_f7_following_swings() -> None:
    """F7 while following Matt: occupied room `bs`, no own u/d/sn."""
    b, state = _following_klymacks()
    b.toggle_hunt()
    assert b.mode == "hunt"
    assert b._followed
    assert b._ranked
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["att acid slime"]
    assert "u" not in sent
    assert "d" not in sent
    assert "sn" not in sent
    assert "bs " not in " ".join(sent)
    assert "join" not in " ".join(sent).lower()


def test_following_at_village_gates_does_not_sneak() -> None:
    """Town watch refuses sneak. Follow in the open; do not sn/break-loop."""
    b, state = _following_klymacks()
    b.mode = "hunt"
    b._asked_health = True
    state.hp = 36
    state.max_hp = 36
    state.room = "Newhaven, Village Entrance"
    state.exits = ["n", "s", "w", "se"]
    state.scanned = True
    state.mobs = ["Matt"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert "sn" not in sent
    assert "break" not in sent


def test_following_on_forest_path_sneaks() -> None:
    """Matt is party — sn on the road, not open walking."""
    b, state = _following_klymacks()
    b.mode = "hunt"
    b._asked_health = True
    state.hp = 36
    state.max_hp = 36
    state.room = "Newhaven, Forest Path"
    state.exits = ["nw", "s"]
    state.scanned = True
    state.look_scan = False
    state.mobs = ["Matt"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["sn"]


def test_following_matt_on_guild_street_sneaks() -> None:
    """No alts in player.json. Leader + Also here Matt still allow sn."""
    b = Brain(
        allowed=True,
        me="klymacks",
        party_leader="Matt",
        rank="back",
        klass="ninja",
        stealth="always",
    )
    b.gear_done = True
    b.mode = "hunt"
    b._joined = True
    b._followed = True
    b._ranked = True
    b._asked_health = True
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 36
    state.max_hp = 36
    state.max_hp_known = True
    state.prompt_seq = 500
    state.room = "Guild Street"
    state.exits = ["n", "s"]
    state.scanned = True
    state.look_scan = False
    state.following = "Matt"
    state.mobs = ["Matt"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["sn"]


def test_following_busy_then_new_room_retries_sneak() -> None:
    """Busy-fail is that title. Next street with only Matt: sn again."""
    b, state = _following_klymacks()
    b.mode = "hunt"
    b._asked_health = True
    state.hp = 36
    state.max_hp = 36
    state.room = "Town Square"
    state.exits = ["n", "s", "e", "w"]
    state.scanned = True
    state.look_scan = False
    state.mobs = ["Matt"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["sn"]
    state.apply({"kind": "sneak_fail", "reason": "busy"})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert "break" in sent
    state.room = "Guild Street, Southern End"
    state.exits = ["n", "s"]
    state.prompt_seq += 1
    state.in_combat = False
    n = len(sent)
    b.tick(state, sent.append, pending=False)
    assert sent[n:] == ["sn"]


def test_follow_f7_also_here_lashworm_engages() -> None:
    """F7 + follow + Also here lashworm — `bs` (room is not empty)."""
    b, state = _following_klymacks()
    b.toggle_hunt()
    state.apply({"kind": "also_here", "mobs": ["Matt", "nasty lashworm"]})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["att lashworm"]
    assert "u" not in sent
    assert "d" not in sent
    assert "sn" not in sent
    assert "bs " not in " ".join(sent)
    hidden, hid_state = _following_klymacks(hidden=True)
    hidden.mode = "hunt"
    hid_state.apply({"kind": "also_here", "mobs": ["Matt", "nasty lashworm"]})
    hid_sent: list[str] = []
    hidden.tick(hid_state, hid_sent.append, pending=False)
    assert hid_sent == ["bs lashworm"]


def test_follow_unscanned_looks_then_engages() -> None:
    """Following into a room without Also here — sn, then bs the listing."""
    b, state = _following_klymacks()
    b.mode = "hunt"
    state.scanned = False
    state.look_scan = False
    state.mobs = ["Matt"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["sn"]
    assert "attack" not in " ".join(sent)
    _sneak_try_wait(b, state, sent)
    state.apply({"kind": "sneak_ok"})
    state.apply({"kind": "also_here", "mobs": ["Matt", "nasty lashworm"]})
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "bs lashworm"
    assert "u" not in sent
    assert "d" not in sent
    assert "attack" not in " ".join(sent)


def test_empty_scanned_room_no_attack() -> None:
    """Fresh empty Also here — do not swing a ghost."""
    b, state = _following_klymacks()
    b.mode = "hunt"
    state.scanned = True
    state.mobs = ["Matt"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["sn"]
    assert not any(c.startswith(("att ", "aa ", "attack ")) or c.startswith("bs ") for c in sent)


def test_sneak_try_no_fail_assumes_hidden() -> None:
    """Attempting + settle + no fail line → hidden. Do not wait for Sneaking..."""
    b, state = _road_hunt("ninja", "always")
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["sn"]
    _sneak_try_wait(b, state, sent)
    assert b._sneak_armed
    assert not b._hidden
    b._assume_sneak()
    assert b._hidden
    assert b._sneaking
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "d"


def test_party_empty_sns_occupied_bs() -> None:
    """Following Matt: empty room `sn`; a lop in the room is `bs`, never `attack`."""
    b, state = _following_klymacks()
    b.mode = "hunt"
    state.mobs = ["Matt"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["sn"]
    _sneak_try_wait(b, state, sent)
    b.tick(state, sent.append, pending=False)
    assert b._hidden
    state.apply({"kind": "also_here", "mobs": ["Matt", "nasty acid slime"]})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "bs acid slime"
    assert "attack" not in " ".join(sent)
    assert "u" not in sent
    assert "d" not in sent


def test_matt_peels_attack_tack() -> None:
    """Matt must never send `attack tack giant rat` or `attack attack giant rat`."""
    assert attack_name("tack giant rat") == "giant rat"
    assert attack_name("tt giant rat") == "giant rat"
    assert attack_name("ttack acid slime") == "acid slime"
    assert attack_name("attack giant rat") == "giant rat"
    assert attack_name("attacktack giant rat") == "giant rat"
    assert attack_name("tackrat") == "rat"
    b = Brain(
        allowed=True,
        me="sysop Matt",
        alts="klymacks",
        party_leader="Matt",
        klass="paladin",
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 710
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.mobs = ["tack giant rat"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["aa giant rat"]
    assert sent[-1] != "aa tack giant rat"
    b._attacking = ""
    state.in_combat = False
    state.mobs = ["attack giant rat"]
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "aa giant rat"
    assert sent[-1] != "aa tack giant rat"
    assert sent[-1].split()[1:] != ["tack", "giant", "rat"]


def test_klymacks_peels_attack_tack() -> None:
    """Ninja sends attack/bs {species}, never tack, never a flavor adjective."""
    assert attack_name("tack giant rat") == "giant rat"
    assert attack_name("tt giant rat") == "giant rat"
    assert attack_line("tt giant rat") == "att giant rat"
    assert attack_name("attack giant rat") == "giant rat"
    assert attack_name("attacktack giant rat") == "giant rat"
    assert attack_name("fat carrion beast") == "carrion beast"
    assert attack_name("thin giant rat") == "giant rat"
    assert attack_name("fat giant rat") == "giant rat"
    assert attack_name("large lashworm") == "lashworm"
    assert attack_name("small giant rat") == "giant rat"
    assert attack_name("nasty lashworm") == "lashworm"
    assert attack_name("nasty acid slime") == "acid slime"
    assert attack_name("acid slime") == "acid slime"
    assert attack_name("id slime") == "acid slime"
    assert attack_line("acid slime") == "att acid slime"
    assert attack_line("giant rat") == "att giant rat"
    assert attack_line("acid slime") != "k acid slime"
    assert attack_line("acid slime") != "a id slime"
    assert attack_line("acid slime") != "at acid slime"
    b, state = _following_klymacks()
    b.mode = "hunt"
    state.mobs = ["Matt", "tack giant rat"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["att giant rat"]
    assert "tack" not in sent[-1].split()
    assert sent[-1].split()[1:] != ["tack", "giant", "rat"]
    b._attacking = ""
    state.in_combat = False
    state.mobs = ["Matt", "fat carrion beast"]
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "att carrion beast"
    assert "fat" not in sent[-1]
    b._attacking = ""
    state.in_combat = False
    state.mobs = ["Matt", "thin giant rat"]
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "att giant rat"
    assert "thin" not in sent[-1]
    b._attacking = ""
    state.in_combat = False
    state.mobs = ["Matt", "nasty lashworm"]
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "att lashworm"
    assert "nasty" not in sent[-1]
    hidden, hid = _following_klymacks(hidden=True)
    hidden.mode = "hunt"
    hid.mobs = ["Matt", "tack giant rat"]
    hid_sent: list[str] = []
    hidden.tick(hid, hid_sent.append, pending=False)
    assert hid_sent == ["bs giant rat"]
    hidden._attacking = ""
    hidden._hidden = True
    hidden._sneaking = True
    hid.in_combat = False
    hid.mobs = ["Matt", "fat carrion beast"]
    hid.prompt_seq += 1
    hidden.tick(hid, hid_sent.append, pending=False)
    assert hid_sent[-1] == "bs carrion beast"
    solo = Brain(allowed=True, klass="ninja", me="klymacks")
    for raw in (
        "fat carrion beast",
        "thin giant rat",
        "nasty lashworm",
        "tack giant rat",
    ):
        assert solo._swing_name(raw) == attack_name(raw)
        assert solo._swing_name(raw) == lop_in([raw])


def test_no_ghost_lashworm_after_leave() -> None:
    """After `u` out of the pit, do not swing a lashworm that is not listed."""
    b = Brain(allowed=True, klass="paladin", me="sysop Matt")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._attacking = "lashworm"
    b._pit_fight = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 711
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.in_combat = True
    state.mobs = ["nasty lashworm"]
    sent: list[str] = []
    b._cmd(sent.append, "u", state)
    assert "lashworm" not in " ".join(state.mobs).lower()
    assert b._attacking == ""
    state.prompt_seq += 1
    state.in_combat = False
    state.scanned = True
    state.mobs = []
    b.tick(state, sent.append, pending=False)
    assert not any("lashworm" in c for c in sent if c.startswith(("att ", "aa ", "attack ", "bs ")))


def test_falls_dead_combat_off_no_ghost_swing() -> None:
    """`falls dead at your feet` + Combat Off — drop the worm, do not retry."""
    b = Brain(allowed=True, klass="paladin", me="sysop Matt")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._attacking = "lashworm"
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 712
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.in_combat = True
    state.mobs = ["nasty lashworm"]
    for ev in parse_events("The lashworm falls dead at your feet.*Combat Off*"):
        state.apply(ev)
    assert not any("lashworm" in m.lower() for m in state.mobs)
    assert state.in_combat is False
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert not any("lashworm" in c for c in sent if c.startswith(("att ", "aa ", "attack ", "bs ")))
    assert "aa lashworm" not in sent


def test_rat_dies_beast_snaps_swings_beast() -> None:
    """Kill + Off + beast snap — swing the beast, never the corpse."""
    b, state = _following_klymacks()
    b.mode = "hunt"
    b._attacking = "giant rat"
    b._last_aim = "giant rat"
    b._last_verb = "attack"
    state.hp = 38
    state.max_hp = 38
    state.max_hp_known = True
    state.in_combat = True
    state.mobs = ["Matt", "giant rat", "big carrion beast"]
    for ev in parse_events(
        "The giant rat falls to the ground with a tortured squeak.\n"
        "You gain 4 experience.\n"
        "*Combat Off*\n"
        "The big carrion beast snaps at Matt with its teeth!\n"
    ):
        state.apply(ev)
    assert not any("rat" in m.lower() for m in state.mobs)
    assert any("carrion" in m.lower() for m in state.mobs)
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["att carrion beast"]
    assert not any("rat" in c for c in sent)


def test_pending_rat_swing_dropped_after_kill() -> None:
    """Paced `at giant rat` must not land after Off; retarget the beast."""
    b, state = _following_klymacks()
    b.mode = "hunt"
    b._attacking = "giant rat"
    b._last_aim = "giant rat"
    b._last_verb = "attack"
    b._wait_prompt = state.prompt_seq + 1
    b._sent_at = time.monotonic()
    state.hp = 38
    state.max_hp = 38
    state.max_hp_known = True
    state.in_combat = True
    state.mobs = ["Matt", "giant rat", "big carrion beast"]
    for ev in parse_events(
        "The giant rat falls to the ground with a tortured squeak.\n"
        "You gain 4 experience.\n"
        "*Combat Off*\n"
        "The big carrion beast snaps at Matt with its teeth!\n"
    ):
        state.apply(ev)
    cancelled: list[bool] = []
    sent: list[str] = []
    b.tick(state, sent.append, True, lambda: cancelled.append(True))
    assert cancelled
    assert sent == ["att carrion beast"]
    assert not any("rat" in c for c in sent)


def test_pending_rat_swing_kept_while_rat_lives() -> None:
    """A second lop in the room must not cancel the current rat swing."""
    b, state = _following_klymacks()
    b.mode = "hunt"
    b._attacking = "giant rat"
    b._last_aim = "giant rat"
    b._last_verb = "attack"
    state.in_combat = True
    state.mobs = ["Matt", "giant rat", "big carrion beast"]
    cancelled: list[bool] = []
    sent: list[str] = []
    b.tick(state, sent.append, True, lambda: cancelled.append(True))
    assert not cancelled
    assert sent == []


def test_say_whiff_does_not_retry_gone_name() -> None:
    b = Brain(allowed=True, klass="paladin", me="sysop Matt")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._attacking = "lashworm"
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 713
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.mobs = ["nasty lashworm"]
    state.apply({"kind": "said", "aimed": "lashworm"})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert "aa lashworm" not in sent
    assert b._attacking == ""


def test_combat_off_arrive_lashworm_not_ghost_slime() -> None:
    """*Combat Off* + arrive lashworm — never swing leftover acid slime."""
    b, state = _following_klymacks()
    b.mode = "hunt"
    b._attacking = "acid slime"
    state.in_combat = True
    state.mobs = ["Matt", "acid slime"]
    state.apply({"kind": "combat_off"})
    assert not any("slime" in m.lower() for m in state.mobs)
    state.apply({"kind": "arrive", "name": "large lashworm"})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["att lashworm"]
    assert "slime" not in " ".join(sent)
    assert "large" not in sent[-1]


def test_say_whiff_slime_then_attack_lashworm() -> None:
    """You say \"attack acid slime\" — do not retry slime; swing the live worm."""
    b, state = _following_klymacks()
    b.mode = "hunt"
    b._attacking = "acid slime"
    state.mobs = ["Matt", "acid slime", "large lashworm"]
    state.apply(parse_line('You say "attack acid slime"'))
    assert state.whiff
    assert not any("slime" in m.lower() for m in state.mobs)
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert "slime" not in " ".join(sent)
    assert sent[-1] == "att lashworm"


def test_say_a_carrion_beast_then_attack_live() -> None:
    """`a carrion beast` was spoken, not swung — drop the beast, hit the thief."""
    b = Brain(allowed=True, klass="paladin", me="sysop Matt")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._attacking = "carrion beast"
    state = WorldState()
    state.in_realm = True
    state.hp = 50
    state.max_hp = 50
    state.max_hp_known = True
    state.prompt_seq = 90
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.mobs = ["Matt", "fat carrion beast", "kobold thief"]
    state.apply(parse_line('You say "a carrion beast"'))
    assert state.whiff
    assert not any("carrion" in m.lower() for m in state.mobs)
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert "carrion" not in " ".join(sent)
    assert sent[-1] == "aa kobold thief"


def test_matt_strike_peels_size_adjectives() -> None:
    """Matt and klymacks swing species only — large/small/fat/thin/nasty drop."""
    assert attack_name("large lashworm") == "lashworm"
    assert attack_name("small giant rat") == "giant rat"
    assert attack_name("fat giant rat") == "giant rat"
    b, state = _matt_bless()
    state.apply({"kind": "buff", "name": "bless", "on": True})
    state.mobs = ["large lashworm"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["aa lashworm"]
    state.mobs = ["small giant rat"]
    state.in_combat = False
    b._attacking = ""
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "aa giant rat"
    assert "small" not in sent[-1]
    assert "large" not in " ".join(sent)


def test_following_hidden_backstabs() -> None:
    b, state = _following_klymacks(hidden=True)
    b.mode = "hunt"
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["bs acid slime"]
    assert "u" not in sent
    assert "sn" not in sent


def test_klymacks_follows_matt() -> None:
    b = Brain(
        allowed=True,
        me="klymacks Klymacks",
        alts="matt sysop",
        party_leader="Matt",
        rank="back",
        klass="ninja",
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 34
    state.room = "Newhaven, Arena"
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["sn"]
    assert "u" not in sent
    assert "rest" not in sent
    state.apply({"kind": "invited", "name": "Matt"})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "follow Matt"
    state.apply({"kind": "following", "name": "Matt"})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "backr"
    state.mobs = ["Matt", "acid slime"]
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "att acid slime"
    assert "bs " not in sent[-1]
    assert not b.bail


def _klymacks_manual() -> tuple[Brain, WorldState]:
    b = Brain(
        allowed=True,
        me="klymacks Klymacks",
        alts="matt sysop",
        party_leader="Matt",
        rank="back",
        klass="ninja",
    )
    b.mode = "manual"
    b.next_action = "manual"
    state = WorldState()
    state.in_realm = True
    return b, state


def test_manual_auto_join_matt_invite() -> None:
    b, state = _klymacks_manual()
    assert b.auto_join
    state.apply({"kind": "invited", "name": "Matt"})
    sent: list[str] = []
    assert b.on_invite(state, sent.append)
    assert sent == ["follow Matt"]
    b.tick(state, sent.append, pending=False)
    assert sent == ["follow Matt"]
    state.apply({"kind": "following", "name": "Matt"})
    assert b.on_follow(state, sent.append)
    assert sent == ["follow Matt", "backr"]
    b.tick(state, sent.append, pending=False)
    assert sent == ["follow Matt", "backr"]
    assert b.mode == "hunt"


def test_manual_tick_follows_him_invite() -> None:
    """1.11p: 'follow him.' Hunt-off still has to accept — not only health/exp."""
    b, state = _klymacks_manual()
    state.apply(parse_line("Matt has invited you to follow him."))
    assert state.invited_by == "Matt"
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["follow Matt"]


def test_manual_tick_backranks_after_typed_follow() -> None:
    b, state = _klymacks_manual()
    state.apply(parse_line("You are now following Matt"))
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["backr"]
    assert b.mode == "hunt"


def test_ninja_follow_sends_backr_without_rank_flag() -> None:
    """klymacks ninja: `backr` after follow even if player.json omits rank."""
    b = Brain(
        allowed=True,
        me="klymacks",
        party_leader="Matt",
        klass="ninja",
    )
    b.mode = "manual"
    state = WorldState()
    state.in_realm = True
    state.apply({"kind": "following", "name": "Matt"})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["backr"]
    assert b.mode == "hunt"


def test_paladin_follow_does_not_backr() -> None:
    b = Brain(
        allowed=True,
        me="matt",
        party_leader="sysop",
        klass="paladin",
    )
    b.mode = "manual"
    state = WorldState()
    state.in_realm = True
    state.apply({"kind": "following", "name": "sysop"})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert "backr" not in sent
    assert "backrank" not in sent


def test_manual_join_backrank_starts_hunt() -> None:
    """Hunt-off join+backrank onto Matt starts hunt — no extra F7."""
    b, state = _klymacks_manual()
    b.gear_done = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 80
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.mobs = ["Matt", "acid slime"]
    sent: list[str] = []
    state.apply({"kind": "invited", "name": "Matt"})
    assert b.on_invite(state, sent.append)
    state.apply({"kind": "following", "name": "Matt"})
    assert b.on_follow(state, sent.append)
    assert sent == ["follow Matt", "backr"]
    assert b.mode == "hunt"
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "att acid slime"
    assert "u" not in sent
    assert "d" not in sent
    assert "sn" not in sent
    assert "bs " not in sent[-1]
    hidden, hid = _klymacks_manual()
    hidden.gear_done = True
    hidden._hidden = True
    hidden._sneaking = True
    hid.hp = 28
    hid.max_hp = 28
    hid.max_hp_known = True
    hid.prompt_seq = 81
    hid.room = "Newhaven, Arena"
    hid.scanned = True
    hid.mobs = ["Matt", "nasty lashworm"]
    hid.apply({"kind": "invited", "name": "Matt"})
    hid_sent: list[str] = []
    assert hidden.on_invite(hid, hid_sent.append)
    hid.apply({"kind": "following", "name": "Matt"})
    assert hidden.on_follow(hid, hid_sent.append)
    assert hidden.mode == "hunt"
    hid.prompt_seq += 1
    hidden.tick(hid, hid_sent.append, pending=False)
    assert hid_sent[-1] == "bs lashworm"
    look_b, look_s = _klymacks_manual()
    look_b.gear_done = True
    look_s.hp = 28
    look_s.max_hp = 28
    look_s.max_hp_known = True
    look_s.prompt_seq = 82
    look_s.room = "Newhaven, Arena"
    look_s.scanned = False
    look_s.mobs = ["Matt"]
    look_sent: list[str] = []
    look_s.apply({"kind": "invited", "name": "Matt"})
    assert look_b.on_invite(look_s, look_sent.append)
    look_s.apply({"kind": "following", "name": "Matt"})
    assert look_b.on_follow(look_s, look_sent.append)
    assert look_b.mode == "hunt"
    look_s.prompt_seq += 1
    look_b.tick(look_s, look_sent.append, pending=False)
    assert look_sent[-1] == "sn"


def test_hunt_on_backrank_stays_hunt() -> None:
    b, state = _klymacks_manual()
    b.gear_done = True
    b.mode = "hunt"
    b.next_action = "lop"
    state.apply({"kind": "following", "name": "Matt"})
    sent: list[str] = []
    assert b.on_follow(state, sent.append)
    assert sent == ["backr"]
    assert b.mode == "hunt"


def test_join_off_follow_does_not_start_hunt() -> None:
    b, state = _klymacks_manual()
    b.auto_join = False
    state.apply({"kind": "following", "name": "Matt"})
    sent: list[str] = []
    assert b.on_follow(state, sent.append)
    assert sent == ["backr"]
    assert b.mode == "manual"


def test_auto_join_off_skips_invite() -> None:
    b, state = _klymacks_manual()
    b.auto_join = False
    state.apply({"kind": "invited", "name": "Matt"})
    sent: list[str] = []
    assert not b.on_invite(state, sent.append)
    assert sent == []
    b.tick(state, sent.append, pending=False)
    assert sent == []


def test_toggle_auto_join_flips_without_takeover() -> None:
    b = Brain(allowed=True, me="klymacks", party_leader="Matt")
    b.mode = "hunt"
    b.next_action = "lop"
    assert b.auto_join
    assert b.toggle_auto_join() is False
    assert b.join_label() == "join off"
    assert b.next_action == "join off"
    assert b.mode == "hunt"
    assert b.toggle_auto_join() is True
    assert b.join_label() == "join"
    assert b.next_action == "join"
    assert b.mode == "hunt"


def test_auto_join_skips_when_already_following() -> None:
    b, state = _klymacks_manual()
    state.following = "Matt"
    state.apply({"kind": "invited", "name": "Matt"})
    sent: list[str] = []
    assert not b.on_invite(state, sent.append)
    assert sent == []


def test_auto_join_skips_stranger_when_leader_set() -> None:
    b, state = _klymacks_manual()
    state.apply({"kind": "invited", "name": "Bob"})
    sent: list[str] = []
    assert not b.on_invite(state, sent.append)
    assert sent == []


def test_auto_join_no_leader_joins_inviter() -> None:
    b = Brain(allowed=True, me="klymacks", party_leader="")
    b.mode = "manual"
    state = WorldState()
    state.in_realm = True
    state.apply({"kind": "invited", "name": "Alice"})
    sent: list[str] = []
    assert b.on_invite(state, sent.append)
    assert sent == ["follow Alice"]


def test_auto_join_skips_self() -> None:
    b, state = _klymacks_manual()
    state.apply({"kind": "invited", "name": "Klymacks"})
    sent: list[str] = []
    assert not b.on_invite(state, sent.append)
    assert sent == []


def test_auto_join_skips_outside_realm() -> None:
    b, state = _klymacks_manual()
    state.in_realm = False
    state.apply({"kind": "invited", "name": "Matt"})
    sent: list[str] = []
    assert not b.on_invite(state, sent.append)
    assert sent == []


def test_hunt_invite_no_double_join() -> None:
    b = Brain(
        allowed=True,
        me="klymacks Klymacks",
        alts="matt sysop",
        party_leader="Matt",
        rank="back",
        klass="ninja",
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 34
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.apply({"kind": "invited", "name": "Matt"})
    sent: list[str] = []
    assert b.on_invite(state, sent.append)
    assert sent == ["follow Matt"]
    b.tick(state, sent.append, pending=False)
    assert sent == ["follow Matt"]
    state.apply({"kind": "following", "name": "Matt"})
    state.prompt_seq += 1
    assert b.on_follow(state, sent.append)
    assert sent == ["follow Matt", "backr"]
    b.tick(state, sent.append, pending=False)
    assert sent == ["follow Matt", "backr"]


def test_klymacks_joins_before_swinging() -> None:
    b = Brain(
        allowed=True,
        me="klymacks Klymacks",
        party_leader="Matt",
        rank="back",
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 36
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.mobs = ["Matt", "acid slime"]
    state.apply({"kind": "invited", "name": "Matt"})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["follow Matt"]
    assert "attack" not in " ".join(sent)


def test_matt_waits_for_join_before_swinging() -> None:
    b = Brain(
        allowed=True,
        me="sysop Matt",
        alts="klymacks",
        party_leader="Matt",
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 37
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.mobs = ["Klymacks", "acid slime"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["invite Klymacks"]
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert "attack" not in " ".join(sent)
    state.apply({"kind": "followed", "name": "Klymacks"})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "att acid slime"


def test_matt_invites_klymacks() -> None:
    b = Brain(
        allowed=True,
        me="sysop Matt",
        alts="klymacks",
        party_leader="Matt",
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 35
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.mobs = ["Klymacks"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["invite Klymacks"]
    assert not b.bail


def test_matt_road_after_follow_goes_down() -> None:
    """Leader sees they followed — backrank is follower-only. Then `d`."""
    b, state = _road_hunt(
        "paladin", "", me="sysop Matt", alts="klymacks", party_leader="Matt"
    )
    state.mobs = ["Klymacks"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["invite Klymacks"]
    assert b.next_action == "invite Klymacks"
    ev = parse_line("Klymacks started to follow you.")
    assert ev and ev["kind"] == "followed" and ev["name"] == "Klymacks"
    state.apply(ev)
    assert "Klymacks" in state.followers
    assert not state.backrank
    state.prompt_seq += 1
    sent = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["d"]
    assert "party" not in b.next_action
    assert not b._party_pending(state)


def _matt_hunt() -> tuple[Brain, WorldState]:
    b = Brain(
        allowed=True,
        me="sysop Matt",
        alts="klymacks",
        party_leader="Matt",
        klass="paladin",
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 38
    state.room = "Newhaven, Arena"
    state.scanned = True
    return b, state


def test_matt_invites_glued_klymacks_before_slime() -> None:
    """Peel must keep Klymacks for invite even when CSI glued him to a slime."""
    assert attack_name("d rat") == "rat"
    assert attack_name("d slimeKlymacks") == "slime"
    for blob in (
        "Also here: acid slime, Klymacks.",
        "Also here: acid slimeKlymacks.",
        "Also here: d slimeKlymacks.",
        "Also here: d Klymacks.",
    ):
        b, state = _matt_hunt()
        b.tick(state, lambda _: None, pending=False)
        for ev in parse_events(blob):
            state.apply(ev)
        sent: list[str] = []
        state.prompt_seq += 1
        b.tick(state, sent.append, pending=False)
        assert sent == ["invite Klymacks"], blob
        assert "attack" not in " ".join(sent)


def test_matt_invites_when_klymacks_arrives() -> None:
    b, state = _matt_hunt()
    state.apply({"kind": "also_here", "mobs": ["acid slime"]})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["aa acid slime"]
    ev = parse_line("Klymacks just arrived from the north.")
    assert ev and ev["kind"] == "arrive"
    state.apply(ev)
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "invite Klymacks"
    assert "attack Klymacks" not in sent


def test_matt_invites_when_klymacks_swings() -> None:
    b, state = _matt_hunt()
    state.apply({"kind": "also_here", "mobs": ["acid slime"]})
    swing = parse_line("Klymacks moves to attack acid slime.")
    assert swing and swing.get("actor") == "Klymacks"
    state.apply(swing)
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["invite Klymacks"]
    assert "attack Klymacks" not in sent


def test_matt_attacks_slime_when_klymacks_absent() -> None:
    b, state = _matt_hunt()
    state.apply({"kind": "also_here", "mobs": ["acid slime"]})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["aa acid slime"]
    assert "klymacks" not in " ".join(sent).lower()
    assert "invite" not in " ".join(sent)


def test_matt_aa_default_on() -> None:
    b, _state = _matt_hunt()
    assert b.aa
    assert b.aa_label() == "aa"
    assert b.f8_label() == "aa"


def test_matt_aa_off_holds_swing() -> None:
    b, state = _matt_hunt()
    b.aa = False
    state.apply({"kind": "also_here", "mobs": ["acid slime"]})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == []
    assert b.next_action == "aa off"
    b.toggle_aa()
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["aa acid slime"]


def test_matt_aa_off_breaks_live_bash() -> None:
    """aa off must `break` — 1.11p keeps bashing after the first swing."""
    b, state = _matt_hunt()
    state.apply({"kind": "also_here", "mobs": ["acid slime"]})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["aa acid slime"]
    assert b._attacking == "acid slime"
    state.in_combat = True
    mud = b.stop_aa(state)
    assert mud == "break"
    assert not b.aa
    assert b._attacking == ""
    state.prompt_seq += 1
    sent.clear()
    b.tick(state, sent.append, pending=False)
    assert "aa acid slime" not in sent
    assert all(not c.startswith("aa ") for c in sent)


def test_matt_aa_bashes_not_attack() -> None:
    """Paladin bash is `aa`. Visible swing for others is `att`."""
    b, state = _matt_hunt()
    assert b.aa
    state.apply({"kind": "also_here", "mobs": ["fat kobold thief"]})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["aa kobold thief"]
    assert not any(c.startswith("att ") or c.startswith("attack ") for c in sent)


def test_ninja_hunts_without_aa() -> None:
    b = Brain(allowed=True, klass="ninja", me="klymacks", stealth="walk")
    assert not b.aa
    b.gear_done = True
    b.mode = "hunt"
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.apply({"kind": "also_here", "mobs": ["filthbug"]})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["att filthbug"]


def test_klymacks_no_attack_before_follow() -> None:
    b = Brain(
        allowed=True,
        me="klymacks Klymacks",
        alts="matt sysop",
        party_leader="Matt",
        rank="back",
        klass="ninja",
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 39
    state.room = "Newhaven, Arena"
    state.scanned = True
    for ev in parse_events("Also here: Matt, acid slime."):
        state.apply(ev)
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    blob = " ".join(sent).lower()
    assert "join" not in blob
    assert "matt" not in blob
    assert sent == ["att acid slime"]
    assert "sn" not in sent
    assert "attack" not in blob
    state.apply({"kind": "invited", "name": "Matt"})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "follow Matt"


def _klymacks_near_matt(*, room: str = "Narrow Road") -> tuple[Brain, WorldState]:
    b = Brain(
        allowed=True,
        me="klymacks Klymacks",
        alts="matt sysop",
        party_leader="Matt",
        rank="back",
        klass="ninja",
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 40
    state.room = room
    state.scanned = True
    state.mobs = ["Matt"]
    return b, state


def test_no_join_without_invite_hunt() -> None:
    b, state = _klymacks_near_matt()
    state.exits = ["n", "e", "w", "d"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["sn"]
    assert "join" not in " ".join(sent).lower()
    assert "join" not in b.next_action
    _sneak_try_wait(b, state, sent)
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "d"
    assert "join" not in " ".join(sent).lower()
    state.apply({"kind": "invited", "name": "Matt"})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "follow Matt"


def test_no_join_without_invite_manual() -> None:
    b, state = _klymacks_manual()
    assert b.auto_join
    state.mobs = ["Matt"]
    sent: list[str] = []
    assert not b.on_invite(state, sent.append)
    assert sent == []
    b.tick(state, sent.append, pending=False)
    assert sent == []
    state.apply({"kind": "sneak_try"})
    assert not b.on_invite(state, sent.append)
    assert sent == []


def test_rest_look_do_not_join_without_invite() -> None:
    b, state = _klymacks_near_matt()
    b.mode = "rest"
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert "join" not in " ".join(sent).lower()
    state.look_scan = True
    state.prompt_seq += 1
    b._sent_at = 0.0
    b.tick(state, sent.append, pending=False)
    assert "join" not in " ".join(sent).lower()


def test_stale_invite_does_not_rejoin() -> None:
    b, state = _klymacks_near_matt()
    state.apply({"kind": "invited", "name": "Matt"})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["follow Matt"]
    state.apply({"kind": "following", "name": "Matt"})
    assert state.invited_by == ""
    b._sync_party(state)
    assert not b._got_invite
    state.apply({"kind": "party_fail", "reason": "invite"})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[0] == "follow Matt"
    assert sent.count("follow Matt") == 1
    assert not any(cmd.startswith("follow ") for cmd in sent[1:])
    if sent[1:]:
        assert sent[-1] in ("sn", "d", "break")
    assert state.invited_by == ""
    assert not b._got_invite


def test_pvp_fights_back_if_attacked() -> None:
    b = Brain(allowed=True, pvp=True)
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 31
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.pvp_hit = "Aelthas"
    state.mobs = ["Aelthas"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["att Aelthas"]
    assert not b.bail


def test_localhost_guard() -> None:
    b = Brain(allowed=False)
    b.toggle_hunt()
    assert b.mode == "manual"
    assert "localhost" in b.next_action


def test_nathaniel_never_sends_north() -> None:
    b = Brain(allowed=True)
    b.mode = "gear"
    b._looked = True
    b._weapon_bought = True
    b._weapon_worn = True
    state = WorldState()
    state.in_realm = True
    state.hp = 22
    state.max_hp = 22
    state.room = "Nathaniel"
    state.exits = ["s"]
    state.prompt_seq = 5
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert "n" not in sent
    assert sent[-1] == "s"


def test_gear_north_after_betram_north() -> None:
    """Betram `n` lands at the gates; weapons is another `n`. Do not wait forever."""
    b = Brain(allowed=True)
    b.mode = "gear"
    b._looked = True
    b._last_step = "n"
    b._step_room = "Newhaven, Armour Shop"
    state = WorldState()
    state.in_realm = True
    state.hp = 22
    state.max_hp = 22
    state.room = "Newhaven, Village Entrance"
    state.exits = ["n", "s", "w", "se"]
    state.worn = list(ARMOUR_ITEMS)
    state.inventory = list(ARMOUR_ITEMS)
    state.prompt_seq = 40
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["n"]
    assert b._armour_i == len(ARMOUR_ITEMS)


def test_stale_village_title_honors_south_only() -> None:
    b = Brain(allowed=True)
    b.mode = "gear"
    b._looked = True
    b._armour_i = len(ARMOUR_ITEMS)
    state = WorldState()
    state.in_realm = True
    state.hp = 22
    state.max_hp = 22
    state.room = "Newhaven, Village Entrance"
    state.exits = ["s"]
    state.prompt_seq = 6
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert "n" not in sent
    assert sent[-1] == "look"


def test_sell_duplicate_club() -> None:
    b = Brain(allowed=True)
    b.mode = "gear"
    b._looked = True
    state = WorldState()
    state.in_realm = True
    state.hp = 22
    state.max_hp = 22
    state.room = "Newhaven, Weapon Shop"
    state.inventory = ["club", "club"]
    state.prompt_seq = 7
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["sell club"]


def test_manual_sells_extra_padded() -> None:
    """Flood fix keeps mode manual — still sell extras at Betram."""
    b = Brain(allowed=True)
    assert b.mode == "manual"
    state = WorldState()
    state.in_realm = True
    state.hp = 22
    state.max_hp = 22
    state.max_hp_known = True
    state.room = "Newhaven, Armour Shop"
    state.exits = ["n"]
    state.mobs = ["Betram"]
    state.worn = list(ARMOUR_ITEMS)
    state.extras = list(ARMOUR_ITEMS)
    state.inventory = [*ARMOUR_ITEMS, *ARMOUR_ITEMS]
    state.prompt_seq = 12
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["sell padded vest"]
    sent.clear()
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["i"]
    state.apply({"kind": "sold", "item": "padded vest"})
    state.apply(
        {
            "kind": "inventory",
            "items": [*ARMOUR_ITEMS, "padded helm"],
            "worn": list(ARMOUR_ITEMS),
            "extras": ["padded helm"],
        }
    )
    sent.clear()
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["sell padded helm"]


def test_sell_extra_padded_from_i() -> None:
    b = Brain(allowed=True)
    b.mode = "gear"
    b._looked = True
    state = WorldState()
    state.in_realm = True
    state.hp = 22
    state.max_hp = 22
    state.room = "Newhaven, Armour Shop"
    state.exits = ["n"]
    state.worn = list(ARMOUR_ITEMS)
    state.extras = list(ARMOUR_ITEMS)
    state.inventory = [*ARMOUR_ITEMS, *ARMOUR_ITEMS]
    state.prompt_seq = 9
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["sell padded vest"]
    sent.clear()
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["i"]
    state.apply({"kind": "sold", "item": "padded vest"})
    state.apply(
        {
            "kind": "inventory",
            "items": [*ARMOUR_ITEMS, "padded helm", "padded pants"],
            "worn": list(ARMOUR_ITEMS),
            "extras": ["padded helm", "padded pants"],
        }
    )
    sent.clear()
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["sell padded helm"]


def test_sell_stacked_helm_until_i_is_clean() -> None:
    """One sold helm must not skip the remaining stack or walk north."""
    b = Brain(allowed=True)
    assert b.mode == "manual"
    state = WorldState()
    state.in_realm = True
    state.hp = 22
    state.max_hp = 22
    state.max_hp_known = True
    state.room = "Newhaven, Village Entrance"
    state.exits = ["n", "s", "w", "se"]
    state.worn = list(ARMOUR_ITEMS)
    state.extras = ["padded helm"] * 3 + ["padded pants"] * 4
    state.inventory = [*ARMOUR_ITEMS, *state.extras]
    state.prompt_seq = 20
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["s"]
    sent.clear()
    state.room = "Newhaven, Armour Shop"
    state.exits = ["n"]
    state.mobs = ["Betram"]
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["i"]
    sent.clear()
    state.apply(
        {
            "kind": "inventory",
            "items": [*ARMOUR_ITEMS, "padded helm", "padded helm", "padded helm"],
            "worn": list(ARMOUR_ITEMS),
            "extras": ["padded helm", "padded helm", "padded helm"],
        }
    )
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["sell padded helm"]
    sent.clear()
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["i"]
    sent.clear()
    state.apply({"kind": "sold", "item": "padded helm"})
    state.apply(
        {
            "kind": "inventory",
            "items": [*ARMOUR_ITEMS, "padded helm", "padded helm"],
            "worn": list(ARMOUR_ITEMS),
            "extras": ["padded helm", "padded helm"],
        }
    )
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["sell padded helm"]
    assert "n" not in sent


def test_stale_screen_inv_does_not_skip_i() -> None:
    """Old `You are carrying` still on the 25-line screen must not skip `i`."""
    b = Brain(allowed=True)
    assert b.mode == "manual"
    state = WorldState()
    state.in_realm = True
    state.hp = 22
    state.max_hp = 22
    state.max_hp_known = True
    state.room = "Newhaven, Armour Shop"
    state.exits = ["n"]
    state.mobs = ["Betram"]
    state.worn = list(ARMOUR_ITEMS)
    state.extras = ["padded helm"] * 3
    state.inventory = [*ARMOUR_ITEMS, *state.extras]
    state.apply(
        {
            "kind": "inventory",
            "items": list(state.inventory),
            "worn": list(ARMOUR_ITEMS),
            "extras": list(state.extras),
        }
    )
    state.prompt_seq = 30
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["sell padded helm"]
    sent.clear()
    state.apply({"kind": "sold", "item": "padded helm"})
    state.apply(
        {
            "kind": "inventory",
            "items": [*ARMOUR_ITEMS, "padded helm", "padded helm", "padded helm"],
            "worn": list(ARMOUR_ITEMS),
            "extras": ["padded helm", "padded helm", "padded helm"],
        }
    )
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["i"]
    sent.clear()
    state.apply(
        {
            "kind": "inventory",
            "items": [*ARMOUR_ITEMS, "padded helm", "padded helm"],
            "worn": list(ARMOUR_ITEMS),
            "extras": ["padded helm", "padded helm"],
        }
    )
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["sell padded helm"]


def test_sell_ignored_sells_same_extra_again() -> None:
    b = Brain(allowed=True)
    b.mode = "gear"
    b._looked = True
    state = WorldState()
    state.in_realm = True
    state.hp = 22
    state.max_hp = 22
    state.room = "Newhaven, Armour Shop"
    state.exits = ["n"]
    state.worn = list(ARMOUR_ITEMS)
    state.extras = ["padded helm"]
    state.inventory = [*ARMOUR_ITEMS, "padded helm"]
    state.prompt_seq = 4
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["sell padded helm"]
    sent.clear()
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["i"]
    sent.clear()
    state.apply(
        {
            "kind": "inventory",
            "items": [*ARMOUR_ITEMS, "padded helm"],
            "worn": list(ARMOUR_ITEMS),
            "extras": ["padded helm"],
        }
    )
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["sell padded helm"]


def test_already_worn_skips_wear_retry() -> None:
    b = Brain(allowed=True)
    b.mode = "gear"
    b._looked = True
    b._wearing = True
    b._armour_i = 4
    state = WorldState()
    state.in_realm = True
    state.hp = 22
    state.max_hp = 22
    state.room = "Newhaven, Armour Shop"
    state.exits = ["n"]
    state.already_worn = "padded gloves"
    state.prompt_seq = 10
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert not any(cmd.startswith("wear") for cmd in sent)
    assert b._armour_i == 5
    assert not b._wearing


def test_village_extras_walk_to_armour() -> None:
    b = Brain(allowed=True)
    b.mode = "gear"
    b._looked = True
    b._armour_i = len(ARMOUR_ITEMS)
    state = WorldState()
    state.in_realm = True
    state.hp = 22
    state.max_hp = 22
    state.room = "Newhaven, Village Entrance"
    state.exits = ["n", "s", "w"]
    state.extras = ["padded vest"]
    state.inventory = ["padded vest", "padded vest"]
    state.prompt_seq = 11
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["s"]


def test_shop_vague_does_not_rebuy() -> None:
    b = Brain(allowed=True)
    b.mode = "gear"
    b._looked = True
    b._wearing = True
    b._armour_i = 4
    state = WorldState()
    state.in_realm = True
    state.hp = 22
    state.max_hp = 22
    state.room = "Newhaven, Armour Shop"
    state.exits = ["n"]
    state.shop_vague = True
    state.prompt_seq = 8
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert not any(cmd.startswith("buy") for cmd in sent)
    assert b._armour_i == 5


def _town_toons() -> tuple[tuple[str, str], ...]:
    return (("paladin", "matt"), ("ninja", "klymacks"))


def _town_brain(klass: str, me: str) -> Brain:
    """Torch shop walk only. Ninja sneak/F8 stays with the ambush worker."""
    if klass == "ninja":
        return Brain(allowed=True, klass=klass, me=me, stealth="walk")
    return Brain(allowed=True, klass=klass, me=me)


def _apply_starter_i(
    state: WorldState, *, torch: bool, weapon_hand: bool = True
) -> None:
    worn = [*ARMOUR_ITEMS, STARTER_WEAPON]
    items = [*worn]
    extras: list[str] = []
    text = "club (Weapon Hand)"
    if torch:
        items.append(STARTER_LIGHT)
        extras.append(STARTER_LIGHT)
        text = "club (Weapon Hand), a torch"
    event: dict[str, object] = {
        "kind": "inventory",
        "items": items,
        "worn": worn,
        "extras": extras,
    }
    if weapon_hand:
        event["text"] = text
    state.apply(event)


def test_gear_then_any_key() -> None:
    b = Brain(allowed=True)
    sent: list[str] = []
    state = WorldState()
    state.in_realm = True
    state.hp = 22
    state.max_hp = 22
    state.max_hp_known = True
    state.prompt_seq = 1
    b.toggle_hunt()
    assert b.mode == "gear"
    b.tick(state, sent.append, pending=False)
    assert sent == ["i"]
    _apply_starter_i(state, torch=True)
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert b.mode == "hunt"
    assert b._torch_bought
    b.takeover()
    assert b.mode == "manual"


def test_gear_weapon_alone_still_gets_torch() -> None:
    """A worn club used to flip hunt and skip the store."""
    for klass, me in _town_toons():
        b = _town_brain(klass, me)
        state = WorldState()
        state.in_realm = True
        state.hp = 22
        state.max_hp = 22
        state.room = "Newhaven, Village Entrance"
        state.exits = ["n", "s", "w", "se"]
        state.prompt_seq = 20
        b.toggle_hunt()
        assert b.open_gear_inv(state) == "i"
        _apply_starter_i(state, torch=False)
        sent: list[str] = []
        state.prompt_seq += 1
        b.tick(state, sent.append, pending=False)
        assert sent == ["w"], (klass, sent)
        assert b.mode == "gear"
        assert not b.gear_done
        assert not b._torch_bought


def test_gear_buys_torch_at_store() -> None:
    for klass, me in _town_toons():
        b = _town_brain(klass, me)
        b.mode = "gear"
        b._looked = True
        b._armour_i = len(ARMOUR_ITEMS)
        b._weapon_bought = True
        b._weapon_worn = True
        state = WorldState()
        state.in_realm = True
        state.hp = 22
        state.max_hp = 22
        state.room = "Newhaven, General Store"
        state.exits = ["n"]
        state.prompt_seq = 21
        sent: list[str] = []
        b.tick(state, sent.append, pending=False)
        assert sent == [f"buy {STARTER_LIGHT}"], (klass, sent)
        assert b._torch_bought
        assert b.mode == "gear"
        sent.clear()
        state.prompt_seq += 1
        b.tick(state, sent.append, pending=False)
        assert sent == ["i"], (klass, sent)
        assert "inv" not in sent


def test_gear_skips_buy_when_i_shows_torch() -> None:
    for klass, me in _town_toons():
        b = _town_brain(klass, me)
        b.mode = "gear"
        b._looked = True
        state = WorldState()
        state.in_realm = True
        state.hp = 22
        state.max_hp = 22
        state.room = "Newhaven, General Store"
        state.exits = ["n"]
        state.prompt_seq = 22
        _apply_starter_i(state, torch=True, weapon_hand=False)
        sent: list[str] = []
        b.tick(state, sent.append, pending=False)
        assert sent == ["n"], (klass, sent)
        assert f"buy {STARTER_LIGHT}" not in sent
        assert b._torch_bought


def test_narrow_path_without_torch_walks_south() -> None:
    b = Brain(allowed=True)
    b.mode = "gear"
    b._looked = True
    b._armour_i = len(ARMOUR_ITEMS)
    b._weapon_worn = True
    state = WorldState()
    state.in_realm = True
    state.hp = 22
    state.max_hp = 22
    state.room = "Newhaven, Narrow Path"
    state.exits = ["n", "s", "w"]
    state.prompt_seq = 23
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["s"]
    assert b.mode == "gear"


def test_narrow_road_without_torch_walks_east() -> None:
    b = Brain(allowed=True)
    b.mode = "gear"
    b._looked = True
    b._armour_i = len(ARMOUR_ITEMS)
    b._weapon_worn = True
    state = WorldState()
    state.in_realm = True
    state.hp = 22
    state.max_hp = 22
    state.room = "Newhaven, Narrow Road"
    state.exits = ["n", "e", "w", "d"]
    state.prompt_seq = 24
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["e"]
    assert b.mode == "gear"
    assert not b.gear_done


def test_shop_vague_at_nathaniel_does_not_skip_torch() -> None:
    b = Brain(allowed=True)
    b.mode = "gear"
    b._looked = True
    b._armour_i = len(ARMOUR_ITEMS)
    b._weapon_bought = True
    b._weapon_worn = True
    state = WorldState()
    state.in_realm = True
    state.hp = 22
    state.max_hp = 22
    state.room = "Nathaniel"
    state.exits = ["s"]
    state.shop_vague = True
    state.prompt_seq = 25
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert not any(cmd.startswith("buy") for cmd in sent)
    assert not b._torch_bought
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "s"
    assert not b._torch_bought


def test_gear_assesses_i_then_walks_north() -> None:
    """F7 gear sends `i` first; a full worn set goes to Nathaniel, not wait."""
    b = Brain(allowed=True)
    state = WorldState()
    state.in_realm = True
    state.hp = 22
    state.max_hp = 22
    state.room = "Newhaven, Village Entrance"
    state.exits = ["n", "s", "w", "se"]
    state.prompt_seq = 7
    b.toggle_hunt()
    assert b.mode == "gear"
    assert b.open_gear_inv(state) == "i"
    assert b.open_gear_inv(state) is None
    state.apply(
        {
            "kind": "inventory",
            "items": list(ARMOUR_ITEMS),
            "worn": list(ARMOUR_ITEMS),
            "extras": [],
        }
    )
    sent: list[str] = []
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["n"]


def _matt_town_gear() -> tuple[Brain, WorldState]:
    b = Brain(
        allowed=True,
        me="sysop Matt",
        klass="paladin",
        spell_list=["minor healing", "harm", "bless"],
    )
    b.mode = "gear"
    b._looked = True
    b._armour_i = len(ARMOUR_ITEMS)
    b._weapon_worn = True
    b._weapon_bought = True
    b._torch_bought = True
    state = WorldState()
    state.in_realm = True
    state.hp = 22
    state.max_hp = 22
    state.max_hp_known = True
    state.prompt_seq = 8
    return b, state


def test_paladin_gear_village_walks_west_for_spells() -> None:
    b, state = _matt_town_gear()
    state.room = "Newhaven, Village Entrance"
    state.exits = ["n", "s", "w"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["w"]
    assert b.mode == "gear"
    assert "inv" not in sent


def test_paladin_gear_path_walks_north_to_spell_shop() -> None:
    b, state = _matt_town_gear()
    state.room = "Newhaven, Narrow Path"
    state.exits = ["n", "s", "e", "w"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["n"]
    assert b.mode == "gear"


def test_paladin_gear_buys_minor_healing_first() -> None:
    b, state = _matt_town_gear()
    state.room = "Newhaven, Spell Shop"
    state.exits = ["s"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["buy scroll of minor healing"]
    assert "inv" not in sent
    sent.clear()
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["read scroll of minor healing"]
    sent.clear()
    state.apply({"kind": "learned"})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["buy scroll of cause harm"]
    sent.clear()
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["read scroll of cause harm"]
    sent.clear()
    state.apply({"kind": "learned"})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["buy scroll of bless"]
    sent.clear()
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["read scroll of bless"]
    sent.clear()
    state.apply({"kind": "learned"})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["s"]
    assert b._spells_shopped
    assert b.mode == "gear"


def test_paladin_reads_held_scroll_not_missing_bless() -> None:
    b, state = _matt_town_gear()
    state.room = "Newhaven, Spell Shop"
    state.exits = ["s"]
    state.inventory = ["scroll of minor healing", "scroll of cause harm"]
    state.extras = ["scroll of minor healing", "scroll of cause harm"]
    b._spell_i = 2
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["read scroll of minor healing"]
    assert "bless" not in sent[0]


def test_hunt_reads_held_scroll_not_missing_bless() -> None:
    b, state = _matt_bless()
    state.blessed = True
    state.inventory = ["scroll of minor healing", "scroll of cause harm"]
    state.extras = ["scroll of minor healing", "scroll of cause harm"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["read scroll of minor healing"]
    sent.clear()
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["read scroll of cause harm"]
    sent.clear()
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert "bless" not in " ".join(sent)


def test_paladin_gear_reads_scroll_already_in_i() -> None:
    b, state = _matt_town_gear()
    state.room = "Newhaven, Spell Shop"
    state.exits = ["s"]
    state.inventory = ["scroll of minor healing"]
    state.extras = ["scroll of minor healing"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["read scroll of minor healing"]
    assert "inv" not in sent


def test_paladin_geared_still_gets_spells() -> None:
    b, state = _matt_town_gear()
    state.geared = True
    state.inventory = [STARTER_LIGHT, STARTER_WEAPON, *ARMOUR_ITEMS]
    state.worn = [STARTER_WEAPON, *ARMOUR_ITEMS]
    state.room = "Newhaven, Village Entrance"
    state.exits = ["n", "s", "w"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert b.mode == "gear"
    assert sent == ["w"]


def test_paladin_spell_vague_retries_short_name() -> None:
    b, state = _matt_town_gear()
    state.room = "Newhaven, Spell Shop"
    state.exits = ["s"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["buy scroll of minor healing"]
    sent.clear()
    state.shop_vague = True
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["buy minor healing"]


def test_paladin_skips_buy_when_i_lists_known_spells() -> None:
    b, state = _matt_town_gear()
    state.room = "Newhaven, Spell Shop"
    state.exits = ["s"]
    state.inventory = ["minor healing", "harm", "bless"]
    state.extras = ["minor healing", "harm", "bless"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["s"]
    assert b._spells_shopped
    assert "bless" in b._spells
    assert not any(cmd.startswith("buy") for cmd in sent)


def test_paladin_still_buys_bless_scroll_at_level_1() -> None:
    b, state = _matt_town_gear()
    state.level = 1
    state.room = "Newhaven, Spell Shop"
    state.exits = ["s"]
    state.inventory = ["minor healing", "harm"]
    state.extras = ["minor healing", "harm"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["buy scroll of bless"]
    assert "bless" in b._spells


def test_paladin_already_knows_spell_buys_harm() -> None:
    b, state = _matt_town_gear()
    state.room = "Newhaven, Spell Shop"
    state.exits = ["s"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "buy scroll of minor healing"
    sent.clear()
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["read scroll of minor healing"]
    sent.clear()
    state.apply({"kind": "learned", "already": True})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["buy scroll of cause harm"]


def test_paladin_does_not_rebuy_after_read() -> None:
    b, state = _matt_town_gear()
    state.room = "Newhaven, Spell Shop"
    state.exits = ["s"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["buy scroll of minor healing"]
    sent.clear()
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["read scroll of minor healing"]
    sent.clear()
    state.apply({"kind": "learned"})
    state.inventory = []
    state.extras = []
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["buy scroll of cause harm"]
    assert "minor healing" not in " ".join(sent)


def test_paladin_skips_shop_for_memorized_spells() -> None:
    b, state = _matt_town_gear()
    b._remember("minor healing")
    b._remember("harm")
    b._remember("bless")
    state.room = "Newhaven, Spell Shop"
    state.exits = ["s"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["s"]
    assert not any(cmd.startswith("buy") for cmd in sent)


def test_paladin_cast_marks_known_no_rebuy() -> None:
    b, state = _matt_bless()
    state.inventory = []
    state.extras = []
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["cast bless"]
    assert b._knows("bless")
    b.mode = "gear"
    b._looked = True
    b._armour_i = len(ARMOUR_ITEMS)
    b._weapon_worn = True
    b._torch_bought = True
    b._spells_shopped = False
    b._spell_i = 2
    state.room = "Newhaven, Spell Shop"
    state.exits = ["s"]
    state.blessed = True
    sent.clear()
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["s"]
    assert not any("bless" in cmd and cmd.startswith("buy") for cmd in sent)


def test_due_spells_follow_level() -> None:
    from . import spells as S

    assert S.due_to_learn("paladin", None, 1, set()) == ["minor healing", "harm"]
    assert S.next_due("paladin", None, 2, {"minor healing", "harm"}) == "bless"
    assert S.next_due("paladin", None, 7, {"minor healing", "harm", "bless"}) == ""
    assert S.next_due("paladin", None, 8, {"minor healing", "harm", "bless"}) == (
        "major healing"
    )


def test_spell_offer_after_train() -> None:
    b, state = _matt_bless()
    b._memorized = {"minor healing", "harm"}
    b._seen_level = 1
    state.level = 2
    state.blessed = True
    state.apply({"kind": "trained", "level": 2})
    state.room = "Newhaven, Guild"
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert b.spell_offer() == "bless"
    assert not any(cmd in {"s", "n", "e", "w"} for cmd in sent)


def test_spell_offer_yes_walks_from_guild() -> None:
    b, state = _matt_bless()
    b.mode = "manual"
    b._memorized.clear()
    state.level = 2
    state.room = "Newhaven, Guild"
    state.exits = ["s"]
    b._spell_offer = "bless"
    b._spell_offer_at = 2
    sent: list[str] = []
    assert b.answer_spell_offer(True, state) == "get bless"
    b.tick(state, sent.append, pending=False)
    assert sent == ["s"]
    _see_tile(b, state)
    sent.clear()
    state.room = "Newhaven, Narrow Road"
    state.exits = ["n", "e", "w", "d"]
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["e"]
    _see_tile(b, state)
    sent.clear()
    state.room = "Newhaven, Narrow Path"
    state.exits = ["n", "s", "e", "w"]
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["n"]
    _see_tile(b, state)
    sent.clear()
    state.room = "Newhaven, Spell Shop"
    state.exits = ["s"]
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["buy scroll of bless"]
    sent.clear()
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["read scroll of bless"]
    sent.clear()
    state.apply({"kind": "learned"})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert b.mode == "manual"
    assert not b._want_spell
    assert b._knows("bless")
    assert not any(cmd.startswith("buy") for cmd in sent)


def test_spell_offer_no_stays_put() -> None:
    b, state = _matt_bless()
    b.mode = "manual"
    state.level = 2
    state.room = "Newhaven, Guild"
    state.exits = ["s"]
    b._spell_offer = "bless"
    b._spell_offer_at = 2
    assert b.answer_spell_offer(False, state) == "skip bless"
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == []
    assert b.mode == "manual"
    assert not b._want_spell


def test_class_weapons_name_the_uniques() -> None:
    assert G.class_weapon("ninja") == "ebony ninjato"
    assert G.class_weapon("paladin") == "shimmering greatsword"
    assert G.next_due("ninja", 9, [], set()) is None
    quest = G.next_due("ninja", 10, [], set())
    assert quest is not None
    assert quest.name == "ebony ninjato"
    assert G.next_due("ninja", 10, ["ebony ninjato"], set()) is None
    assert G.next_due("ninja", 10, [], {G.CLASS_WEAPON_KEY}) is None
    assert len(G.offer_tip(10, "ebony ninjato", "ninja")) <= 80
    assert len(G.quest_tip("paladin", "shimmering greatsword")) <= 80


def test_shop_weapons_are_value_picks() -> None:
    assert starter_weapon("ninja") == "stiletto"
    assert starter_weapon("paladin") == "battle axe"
    assert starter_weapon("") == "club"
    assert starter_weapon("mage") == "club"
    assert is_starter_weapon("stiletto (Weapon Hand)", "ninja")
    assert is_starter_weapon("battle axe (Weapon Hand)", "paladin")
    assert is_starter_weapon("club (Weapon Hand)")


def _weapon_shop(klass: str) -> tuple[Brain, WorldState]:
    if klass == "ninja":
        b = Brain(allowed=True, klass=klass, stealth="walk")
    else:
        b = Brain(allowed=True, klass=klass)
    b.mode = "gear"
    b._looked = True
    b._armour_i = len(ARMOUR_ITEMS)
    state = WorldState()
    state.in_realm = True
    state.hp = 22
    state.max_hp = 22
    state.room = "Newhaven, Weapon Shop"
    state.exits = ["s"]
    state.prompt_seq = 50
    return b, state


def test_ninja_buys_stiletto() -> None:
    b, state = _weapon_shop("ninja")
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["buy stiletto"]
    sent.clear()
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["i"]
    state.apply(
        {
            "kind": "inventory",
            "items": ["stiletto"],
            "worn": [],
            "extras": ["stiletto"],
        }
    )
    sent.clear()
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["wear stiletto"]


def test_paladin_buys_battle_axe() -> None:
    b, state = _weapon_shop("paladin")
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["buy battle axe"]
    sent.clear()
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["i"]
    state.apply(
        {
            "kind": "inventory",
            "items": ["battle axe"],
            "worn": [],
            "extras": ["battle axe"],
        }
    )
    sent.clear()
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["wear battle axe"]


def test_gear_offer_after_train() -> None:
    b = Brain(allowed=True, klass="ninja", me="klymacks")
    b.mode = "manual"
    b._seen_level = 9
    state = WorldState()
    state.in_realm = True
    state.level = 10
    state.apply({"kind": "trained", "level": 10})
    state.room = "Newhaven, Guild"
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert b.gear_offer() == "ebony ninjato"
    assert b.spell_offer() == ""
    assert not any(cmd in {"s", "n", "e", "w"} for cmd in sent)
    tip = b.offer_tip(state.level)
    assert "ebony ninjato" in tip
    assert "y/n" in tip
    assert len(tip) <= 80


def test_gear_offer_yes_walks_skiff_from_guild() -> None:
    b = Brain(allowed=True, klass="ninja", me="klymacks")
    b.mode = "manual"
    state = WorldState()
    state.in_realm = True
    state.level = 10
    state.room = "Newhaven, Guild"
    state.exits = ["s"]
    b._gear_offer = "ebony ninjato"
    b._gear_offer_key = "class-weapon"
    b._gear_offer_at = 10
    assert b.answer_gear_offer(True, state) == "get ebony ninjato"
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["s"]
    assert b._want_gear == "ebony ninjato"
    hint = b.offer_tip(state.level)
    assert "ebony ninjato" in hint
    assert "skiff" in hint
    assert "y/n" not in hint
    _see_tile(b, state)
    sent.clear()
    state.room = "Newhaven, Narrow Road"
    state.exits = ["n", "e", "w", "d"]
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["e"]
    _see_tile(b, state)
    sent.clear()
    state.room = "Newhaven, Narrow Path"
    state.exits = ["n", "s", "e", "w"]
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["e"]
    _see_tile(b, state)
    sent.clear()
    state.room = "Newhaven, Village Entrance"
    state.exits = ["n", "s", "w", "se"]
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["se"]
    _see_tile(b, state)
    sent.clear()
    state.room = "Newhaven, Forest Path"
    state.exits = ["nw", "s"]
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["s"]
    _see_tile(b, state)
    sent.clear()
    state.room = "Newhaven, Docks"
    state.exits = ["n"]
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["borrow skiff"]


def test_gear_offer_yes_stops_at_town_square() -> None:
    b = Brain(allowed=True, klass="ninja", me="klymacks")
    b.mode = "manual"
    b._want_gear = "ebony ninjato"
    state = WorldState()
    state.in_realm = True
    state.level = 10
    state.room = "Town Square"
    state.exits = ["n", "s", "e", "w"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == []
    assert b.mode == "manual"
    assert b._want_gear == "ebony ninjato"
    assert "crypt" in b.offer_tip(state.level)


def test_gear_offer_yes_keeps_hunting() -> None:
    b = Brain(allowed=True, klass="ninja", me="klymacks")
    b.mode = "hunt"
    b.gear_done = True
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.level = 10
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.mobs = []
    b._gear_offer = "ebony ninjato"
    b._gear_offer_key = "class-weapon"
    b._gear_offer_at = 10
    assert b.answer_gear_offer(True, state) == "get ebony ninjato"
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert b.mode == "hunt"
    assert b._want_gear == "ebony ninjato"
    assert sent == ["u"]
    assert "skiff" in b.offer_tip(state.level)


def test_gear_offer_no_stays_put() -> None:
    b = Brain(allowed=True, klass="ninja", me="klymacks")
    b.mode = "manual"
    state = WorldState()
    state.in_realm = True
    state.level = 10
    state.room = "Newhaven, Guild"
    state.exits = ["s"]
    b._gear_offer = "ebony ninjato"
    b._gear_offer_key = "class-weapon"
    b._gear_offer_at = 10
    assert b.answer_gear_offer(False, state) == "skip ebony ninjato"
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == []
    assert b.mode == "manual"
    assert not b._want_gear
    assert not b.gear_offer()


def test_gear_offer_skipped_if_holding_weapon() -> None:
    b = Brain(allowed=True, klass="ninja", me="klymacks")
    b.mode = "manual"
    b._seen_level = 9
    state = WorldState()
    state.in_realm = True
    state.level = 10
    state.worn = ["ebony ninjato"]
    state.apply({"kind": "trained", "level": 10})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert b.gear_offer() == ""
    assert "class-weapon" in b._claimed


def test_silvermere_square_walks_north_to_guild() -> None:
    """Outdoor graveyard farm — TS walks Guild Street, no manhole/torch ritual."""
    b = Brain(allowed=True, klass="paladin", me="matt", party_leader="matt")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    state = WorldState()
    state.in_realm = True
    state.hp = 67
    state.max_hp = 67
    state.max_hp_known = True
    state.level = 4
    state.prompt_seq = 410
    state.room = "Town Square"
    state.exits = ["n", "s", "e", "w"]
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["n"]
    assert "i" not in sent
    assert "go manhole" not in sent
    assert "light torch" not in sent


def test_sewer_run_ninja_dives_at_square() -> None:
    """Selected sewer run walks the manhole at TS, not Guild Street."""
    b = Brain(
        allowed=True,
        klass="ninja",
        me="klymacks",
        hunt="sewer",
        stealth="walk",
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    state = WorldState()
    state.in_realm = True
    state.hp = 40
    state.max_hp = 40
    state.max_hp_known = True
    state.level = 4
    state.prompt_seq = 411
    state.room = "Town Square"
    state.exits = ["n", "s", "e", "w"]
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["go manhole"]
    assert "n" not in sent


def test_sewer_run_paladin_stocks_at_square() -> None:
    """Human sewer run checks the bag before diving."""
    b = Brain(
        allowed=True,
        klass="paladin",
        me="matt",
        party_leader="matt",
        hunt="sewer",
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    state = WorldState()
    state.in_realm = True
    state.hp = 67
    state.max_hp = 67
    state.max_hp_known = True
    state.level = 4
    state.prompt_seq = 412
    state.room = "Town Square"
    state.exits = ["n", "s", "e", "w"]
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["i"]
    assert "go manhole" not in sent
    assert "n" not in sent


def test_guild_street_keeps_walking_north() -> None:
    """Mid Guild Street reuses the title — do not stall on last_step=n."""
    b = Brain(allowed=True, klass="paladin", me="matt", party_leader="matt")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    b._last_step = "n"
    b._step_room = "Guild Street"
    b._step_prompt = 410
    state = WorldState()
    state.in_realm = True
    state.hp = 67
    state.max_hp = 67
    state.max_hp_known = True
    state.level = 4
    state.prompt_seq = 411
    state.room = "Guild Street"
    state.exits = ["n", "s", "w"]
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["n"]
    assert "w" not in sent


def test_guild_southern_end_walks_north_not_into_helfgrim() -> None:
    """Shops sit east/west of Guild Street. Farm is north, never west."""
    b = Brain(allowed=True, klass="paladin", me="matt", party_leader="matt")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    state = WorldState()
    state.in_realm = True
    state.hp = 67
    state.max_hp = 67
    state.max_hp_known = True
    state.level = 4
    state.prompt_seq = 411
    state.room = "Guild Street, Southern End"
    state.exits = ["n", "s", "e", "w"]
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["n"]
    assert "w" not in sent
    assert "e" not in sent


def test_helfgrim_walks_east_back_to_guild() -> None:
    b = Brain(allowed=True, klass="paladin", me="matt", party_leader="matt")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    state = WorldState()
    state.in_realm = True
    state.hp = 67
    state.max_hp = 67
    state.max_hp_known = True
    state.level = 4
    state.prompt_seq = 412
    state.room = "Helfgrim's Blades"
    state.exits = ["e"]
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["e"]


def test_secret_passage_walks_north_not_look() -> None:
    """Matt reprint-looked this hall with klymacks. Leave by the only exit."""
    b = Brain(
        allowed=True,
        klass="paladin",
        me="matt",
        party_leader="matt",
        alts="klymacks",
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    b._invited = True
    state = WorldState()
    state.in_realm = True
    state.hp = 67
    state.max_hp = 67
    state.max_hp_known = True
    state.ma = 14
    state.max_ma = 14
    state.blessed = True
    state.level = 4
    state.prompt_seq = 700
    state.room = "Secret Passage"
    state.exits = ["n"]
    state.scanned = True
    state.look_scan = True
    state.mobs = ["klymacks"]
    state.followers = ["klymacks"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["n"]
    sent.clear()
    state.look_scan = False
    state.apply({"kind": "exits", "exits": ["n"]})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["n"]
    assert "look" not in sent
    assert "rest" not in sent


def test_secret_passage_continues_southeast_not_camp() -> None:
    """Same title, new tile: came east, exits west/se — do not sit."""
    b = Brain(
        allowed=True,
        klass="paladin",
        me="matt",
        party_leader="matt",
        alts="klymacks",
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    b._invited = True
    b._last_step = "e"
    b._step_room = "Secret Passage"
    state = WorldState()
    state.in_realm = True
    state.hp = 67
    state.max_hp = 67
    state.max_hp_known = True
    state.blessed = True
    state.level = 4
    state.prompt_seq = 701
    state.room = "Secret Passage"
    state.exits = ["w", "se"]
    state.scanned = True
    state.mobs = ["klymacks"]
    state.followers = ["klymacks"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["se"]
    assert "look" not in sent
    assert "rest" not in sent
    assert b.next_action != "camping"


def test_secret_passage_no_exit_looks_not_west() -> None:
    """A closed door is not a landing. Look; do not immediately try west."""
    b = Brain(
        allowed=True,
        klass="paladin",
        me="matt",
        party_leader="matt",
        alts="klymacks",
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    b._invited = True
    state = WorldState()
    state.in_realm = True
    state.hp = 67
    state.max_hp = 67
    state.max_hp_known = True
    state.blessed = True
    state.level = 4
    state.prompt_seq = 720
    state.room = "Secret Passage"
    state.exits = ["e", "w"]
    state.scanned = True
    state.mobs = ["klymacks"]
    state.followers = ["klymacks"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["e"]
    state.blocked = True
    state.prompt_seq += 1
    sent.clear()
    b.tick(state, sent.append, pending=False)
    assert sent == ["look"]
    assert "e" not in state.exits
    assert b.compass.failed


def test_river_eastern_end_walks_west_not_locked_east() -> None:
    b = Brain(
        allowed=True,
        klass="paladin",
        me="matt",
        party_leader="matt",
        alts="klymacks",
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    b._invited = True
    state = WorldState()
    state.in_realm = True
    state.hp = 67
    state.max_hp = 67
    state.max_hp_known = True
    state.blessed = True
    state.level = 4
    state.prompt_seq = 730
    state.room = "River Street, Eastern End"
    state.exits = ["s", "w"]
    state.scanned = True
    state.mobs = ["klymacks"]
    state.followers = ["klymacks"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["w"]


def test_silver_eastern_end_walks_west_not_atlas_east() -> None:
    """Poisoned e→Town Square plus last_step=e used to bounce e/w here."""
    atlas = Atlas()
    atlas.edges[("silver street eastern end", "e")] = "town square"
    b = Brain(
        allowed=True,
        klass="paladin",
        me="matt",
        party_leader="matt",
        alts="klymacks",
        atlas=atlas,
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    b._invited = True
    b._last_step = "e"
    b._step_room = "Silver Street, Eastern End"
    state = WorldState()
    state.in_realm = True
    state.hp = 67
    state.max_hp = 67
    state.max_hp_known = True
    state.blessed = True
    state.level = 4
    state.prompt_seq = 741
    state.room = "Silver Street, Eastern End"
    state.exits = ["e", "n", "s", "w"]
    state.scanned = True
    state.mobs = ["klymacks"]
    state.followers = ["klymacks"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["w"]
    assert "e" not in sent


def test_river_street_walks_east_toward_bridge() -> None:
    """GY is off Bridge St, not a north door on every River Street tile."""
    b = Brain(allowed=True, klass="paladin", me="matt", party_leader="matt")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    b._invited = True
    b._last_step = "e"
    state = WorldState()
    state.in_realm = True
    state.hp = 67
    state.max_hp = 67
    state.max_hp_known = True
    state.blessed = True
    state.level = 4
    state.prompt_seq = 732
    state.room = "River Street"
    state.exits = ["e", "n", "s", "w"]
    state.scanned = True
    state.mobs = ["klymacks"]
    state.followers = ["klymacks"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["e"]
    assert "n" not in sent
    _see_tile(b, state)
    b._last_step = "w"
    b._step_room = "River Street, Eastern End"
    state.prompt_seq += 1
    sent.clear()
    b.tick(state, sent.append, pending=False)
    assert sent == ["w"]


def test_bridge_walks_northeast_not_north_gate() -> None:
    b = Brain(allowed=True, klass="paladin", me="matt", party_leader="matt")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    b._invited = True
    state = WorldState()
    state.in_realm = True
    state.hp = 67
    state.max_hp = 67
    state.max_hp_known = True
    state.blessed = True
    state.level = 4
    state.prompt_seq = 733
    state.room = "Bridge"
    state.exits = ["ne", "s", "sw"]
    state.scanned = True
    state.mobs = ["klymacks"]
    state.followers = ["klymacks"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["ne"]
    assert "n" not in sent


def test_river_bridge_intersection_opens_north_gate() -> None:
    b = Brain(allowed=True, klass="paladin", me="matt", party_leader="matt")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    b._invited = True
    state = WorldState()
    state.in_realm = True
    state.hp = 67
    state.max_hp = 67
    state.max_hp_known = True
    state.blessed = True
    state.level = 4
    state.prompt_seq = 734
    state.room = "Intersection of River St. & Bridge St."
    state.exits = ["e", "s", "w"]
    state.closed_exits = ["n"]
    state.scanned = True
    state.mobs = ["klymacks"]
    state.followers = ["klymacks"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["bash north"]
    assert "s" not in sent
    assert "e" not in sent
    assert "w" not in sent
    assert "open north" not in sent
    assert "aa north" not in sent


def test_river_bridge_intersection_ninja_picklocks() -> None:
    b = Brain(allowed=True, klass="ninja", me="klymacks", stealth="walk")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    b._invited = True
    state = WorldState()
    state.in_realm = True
    state.hp = 36
    state.max_hp = 36
    state.max_hp_known = True
    state.blessed = True
    state.level = 4
    state.prompt_seq = 735
    state.room = "Intersection of River St. & Bridge St."
    state.exits = ["e", "s", "w"]
    state.closed_exits = ["n"]
    state.scanned = True
    state.mobs = ["klymacks"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["picklock north"]
    assert "bash north" not in sent


def test_river_eastern_end_no_exit_looks() -> None:
    b = Brain(
        allowed=True,
        klass="paladin",
        me="matt",
        party_leader="matt",
        alts="klymacks",
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    b._invited = True
    b._last_step = "e"
    state = WorldState()
    state.in_realm = True
    state.hp = 67
    state.max_hp = 67
    state.max_hp_known = True
    state.blessed = True
    state.level = 4
    state.prompt_seq = 731
    state.room = "River Street, Eastern End"
    state.exits = ["e", "w"]
    state.scanned = True
    state.blocked = True
    state.mobs = ["klymacks"]
    state.followers = ["klymacks"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["look"]
    assert "e" not in sent


def test_torch_run_leaves_river_east_end_west() -> None:
    """Giovanni is west of the locked tower. Farm e/w must not steal the shop walk."""
    b = Brain(
        allowed=True,
        klass="paladin",
        me="matt",
        party_leader="matt",
        alts="klymacks",
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    b._invited = True
    b._torch_shopping = True
    b._last_step = "e"
    state = WorldState()
    state.in_realm = True
    state.hp = 67
    state.max_hp = 67
    state.max_hp_known = True
    state.blessed = True
    state.level = 4
    state.prompt_seq = 740
    state.room = "River Street, Eastern End"
    state.exits = ["s", "w"]
    state.scanned = True
    state.mobs = ["klymacks"]
    state.followers = ["klymacks"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["w"]
    assert "e" not in sent
    _see_tile(b, state)
    mid = WorldState()
    mid.in_realm = True
    mid.hp = 67
    mid.max_hp = 67
    mid.max_hp_known = True
    mid.blessed = True
    mid.level = 4
    mid.prompt_seq = state.prompt_seq + 1
    mid.room = "River Street"
    mid.exits = ["e", "n", "s", "w"]
    mid.scanned = True
    mid.mobs = ["klymacks"]
    mid.followers = ["klymacks"]
    sent.clear()
    b.tick(mid, sent.append, pending=False)
    assert sent == ["w"]
    assert "e" not in sent


def test_hunt_maps_unknown_hall_and_walks() -> None:
    atlas = Atlas()
    b = Brain(
        allowed=True,
        klass="paladin",
        me="matt",
        party_leader="matt",
        atlas=atlas,
    )
    b.gear_done = True
    b.mode = "hunt"
    b._asked_health = True
    state = WorldState()
    state.in_realm = True
    state.hp = 67
    state.max_hp = 67
    state.max_hp_known = True
    state.blessed = True
    state.level = 4
    state.prompt_seq = 710
    state.room = "Hidden Gallery"
    state.exits = ["e", "w"]
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert atlas.known("Hidden Gallery")
    assert sent and sent[0] in {"e", "w"}
    assert "look" not in sent


def test_manual_maps_unknown_room() -> None:
    atlas = Atlas()
    b = Brain(allowed=True, atlas=atlas)
    b.mode = "manual"
    state = WorldState()
    state.in_realm = True
    state.room = "Hidden Gallery"
    state.exits = ["e", "w"]
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert atlas.known("Hidden Gallery")
    assert sent == []


def test_note_send_maps_edge_into_new_room() -> None:
    atlas = Atlas()
    b = Brain(
        allowed=True,
        klass="paladin",
        me="matt",
        party_leader="matt",
        atlas=atlas,
    )
    b.gear_done = True
    b.mode = "hunt"
    b._asked_health = True
    b.note_send("n", "Graveyard")
    state = WorldState()
    state.in_realm = True
    state.hp = 67
    state.max_hp = 67
    state.max_hp_known = True
    state.blessed = True
    state.level = 4
    state.prompt_seq = 711
    state.room = "Secret Passage"
    state.exits = ["n"]
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert atlas.known("Secret Passage")
    assert atlas.path("Graveyard", "Secret Passage") == ["n"]
    assert sent == ["n"]


def test_compass_holds_second_step_until_room_confirms() -> None:
    b = Brain(allowed=True, klass="paladin", me="matt", party_leader="matt")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    state = WorldState()
    state.in_realm = True
    state.hp = 67
    state.max_hp = 67
    state.max_hp_known = True
    state.level = 4
    state.prompt_seq = 413
    state.room = "Town Square"
    state.exits = ["n", "s", "e", "w"]
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["n"]
    sent.clear()
    state.room = "Guild Street, Southern End"
    state.apply({"kind": "exits", "exits": ["n", "s"]})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["n"]
    assert "look" not in sent


def test_graveyard_gate_walks_east() -> None:
    b = Brain(allowed=True, klass="paladin", me="matt", party_leader="matt")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    state = WorldState()
    state.in_realm = True
    state.hp = 67
    state.max_hp = 67
    state.max_hp_known = True
    state.level = 4
    state.prompt_seq = 412
    state.room = "Graveyard Entrance"
    state.exits = ["e", "w"]
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["e"]
    assert "n" not in sent
    assert "s" not in sent
    assert b.next_action == "graveyard"


def test_graveyard_paladin_bashes_fierce_zombie() -> None:
    b = Brain(
        allowed=True,
        klass="paladin",
        me="matt",
        party_leader="matt",
        alts="klymacks",
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    b._invited = True
    state = WorldState()
    state.in_realm = True
    state.hp = 67
    state.max_hp = 67
    state.max_hp_known = True
    state.blessed = True
    state.level = 4
    state.prompt_seq = 415
    state.room = "Graveyard"
    state.exits = ["e", "w"]
    state.scanned = True
    state.saw_here = True
    state.mobs = ["klymacks", "fierce zombie"]
    state.followers = ["klymacks"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["aa zombie"]
    assert "e" not in sent
    assert "w" not in sent


def test_graveyard_heals_on_heal_me_before_walk() -> None:
    """GY is Silvermere, so not `_in_pit` — still cast before ping-pong."""
    b = Brain(
        allowed=True,
        klass="paladin",
        me="matt",
        party_leader="matt",
        alts="klymacks",
        spell_list=["minor healing", "harm"],
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    b._invited = True
    state = WorldState()
    state.in_realm = True
    state.hp = 74
    state.max_hp = 74
    state.max_hp_known = True
    state.ma = 16
    state.max_ma = 16
    state.blessed = True
    state.level = 5
    state.prompt_seq = 416
    state.room = "Graveyard, Southern Edge"
    state.exits = ["n", "e", "w"]
    state.scanned = True
    state.saw_here = True
    state.mobs = ["Klymacks"]
    state.followers = ["klymacks"]
    ev = parse_line('Klymacks says "heal me"')
    assert ev and ev["kind"] == "heal_ask"
    state.apply(ev)
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["cast minor healing klymacks"]
    assert "e" not in sent
    assert "w" not in sent


def test_graveyard_ping_pongs_west_after_five_east() -> None:
    b = Brain(allowed=True, klass="paladin", me="matt", party_leader="matt")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    b._grave_i = 5
    state = WorldState()
    state.in_realm = True
    state.hp = 67
    state.max_hp = 67
    state.max_hp_known = True
    state.level = 4
    state.prompt_seq = 413
    state.room = "Graveyard"
    state.exits = ["e", "w"]
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["w"]
    assert "n" not in sent
    assert "s" not in sent


def test_graveyard_low_hp_flees_west() -> None:
    b = Brain(allowed=True, klass="paladin", me="matt", party_leader="matt")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    b._attacking = "skeleton"
    state = WorldState()
    state.in_realm = True
    state.hp = 20
    state.max_hp = 67
    state.max_hp_known = True
    state.blessed = True
    state.level = 4
    state.prompt_seq = 414
    state.room = "Graveyard"
    state.exits = ["e", "w"]
    state.scanned = True
    state.in_combat = True
    state.mobs = ["skeleton"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["w"]
    assert b.next_action == "flee"
    assert "e" not in sent


def test_graveyard_leader_flees_when_ninja_gasps() -> None:
    b = Brain(allowed=True, klass="paladin", me="matt", party_leader="matt")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    b._attacking = "skeleton"
    state = WorldState()
    state.in_realm = True
    state.hp = 67
    state.max_hp = 67
    state.max_hp_known = True
    state.blessed = True
    state.level = 4
    state.prompt_seq = 415
    state.room = "Graveyard"
    state.exits = ["e", "w"]
    state.scanned = True
    state.in_combat = True
    state.mobs = ["klymacks", "skeleton"]
    state.self_names = {"matt"}
    state.apply(
        {
            "kind": "wounded",
            "name": "Klymacks",
        }
    )
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["w"]
    assert b.next_action == "flee"


def test_ninja_follows_flee_when_not_following() -> None:
    b = Brain(
        allowed=True,
        klass="ninja",
        me="klymacks",
        party_leader="matt",
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    state = WorldState()
    state.in_realm = True
    state.hp = 40
    state.max_hp = 40
    state.max_hp_known = True
    state.level = 4
    state.prompt_seq = 416
    state.room = "Graveyard"
    state.exits = ["e", "w"]
    state.scanned = True
    state.self_names = {"klymacks"}
    state.apply({"kind": "flee", "name": "Matt", "dir": "w"})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["w"]
    assert b.next_action == "flee"


def test_ninja_does_not_flee_twice_while_following() -> None:
    b = Brain(
        allowed=True,
        klass="ninja",
        me="klymacks",
        party_leader="matt",
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    b._followed = True
    b._joined = True
    b._ranked = True
    state = WorldState()
    state.in_realm = True
    state.hp = 40
    state.max_hp = 40
    state.max_hp_known = True
    state.level = 4
    state.prompt_seq = 417
    state.room = "Graveyard"
    state.exits = ["e", "w"]
    state.scanned = True
    state.following = "Matt"
    state.self_names = {"klymacks"}
    state.apply({"kind": "flee", "name": "Matt", "dir": "w"})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert "w" not in sent
    assert "e" not in sent


def test_ninja_rests_when_matt_sits() -> None:
    b = Brain(
        allowed=True,
        klass="ninja",
        me="klymacks",
        party_leader="matt",
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    b._followed = True
    b._joined = True
    b._ranked = True
    state = WorldState()
    state.in_realm = True
    state.hp = 40
    state.max_hp = 40
    state.max_hp_known = True
    state.level = 4
    state.prompt_seq = 418
    state.room = "Graveyard Entrance"
    state.exits = ["e", "w"]
    state.scanned = True
    state.following = "Matt"
    state.mobs = ["Matt"]
    state.self_names = {"klymacks"}
    state.apply({"kind": "rest", "actor": "Matt"})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["rest"]


def test_ninja_breaks_when_matt_stands() -> None:
    b = Brain(
        allowed=True,
        klass="ninja",
        me="klymacks",
        party_leader="matt",
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    b._followed = True
    b._joined = True
    b._ranked = True
    b._sitting = True
    state = WorldState()
    state.in_realm = True
    state.hp = 40
    state.max_hp = 40
    state.max_hp_known = True
    state.level = 4
    state.prompt_seq = 419
    state.room = "Graveyard Entrance"
    state.exits = ["e", "w"]
    state.scanned = True
    state.following = "Matt"
    state.resting = True
    state.mobs = ["Matt"]
    state.self_names = {"klymacks"}
    state.apply({"kind": "stand", "actor": "Matt"})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["break"]


def test_square_skips_light_when_torch_already_lit() -> None:
    """Graveyard farm does not light/dive at TS — walks north."""
    b = Brain(allowed=True, klass="paladin", me="matt", party_leader="matt")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.level = 4
    state.prompt_seq = 412
    state.room = "Town Square"
    state.exits = ["n", "s", "e", "w"]
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert f"light {STARTER_LIGHT}" not in sent
    assert sent == ["n"]
    assert "go manhole" not in sent


def test_square_i_without_lit_marker_keeps_light() -> None:
    """Fresh `i` often omits (lit) — do not clear lit and wander temple."""
    b = Brain(allowed=True, klass="paladin", me="matt", party_leader="matt")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    b._torch_bought = True
    b._torch_checked = True
    b._torch_est = 2
    b._torch_bag_ready = True
    b._torch_lit = True
    b._sewer_stocked = True
    b._torch_inv_synced = 0
    state = WorldState()
    state.in_realm = True
    state.hp = 67
    state.max_hp = 67
    state.max_hp_known = True
    state.level = 4
    state.prompt_seq = 420
    state.room = "Town Square"
    state.exits = ["n", "s", "e", "w"]
    state.scanned = True
    state.torch_lit = True
    state.apply(
        {
            "kind": "inventory",
            "items": [STARTER_LIGHT, STARTER_LIGHT],
            "worn": [],
            "extras": [],
        }
    )
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert "w" not in sent
    assert f"light {STARTER_LIGHT}" not in sent
    assert sent == ["n"]
    assert b._torch_lit


def test_already_lit_reply_stops_reliight() -> None:
    b = Brain(allowed=True, klass="paladin", me="matt", party_leader="matt")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    b._torch_bought = True
    b._torch_checked = True
    b._torch_est = 2
    b._torch_bag_ready = True
    b._torch_inv_synced = 0  # prevent empty sync wiping est
    state = WorldState()
    state.in_realm = True
    state.hp = 67
    state.max_hp = 67
    state.max_hp_known = True
    state.level = 4
    state.prompt_seq = 421
    state.room = "Town Square"
    state.exits = ["n", "s", "e", "w"]
    state.scanned = True
    state.inventory = [STARTER_LIGHT, STARTER_LIGHT]
    state.apply({"kind": "torch_lit"})  # "You already have something lit!"
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert f"light {STARTER_LIGHT}" not in sent
    assert "w" not in sent
    assert sent == ["n"]
    assert b._torch_lit


def test_bag_ready_on_temple_street_walks_east() -> None:
    """If we drifted west of TS with a full bag, walk east — never further west."""
    b = Brain(allowed=True, klass="paladin", me="matt", party_leader="matt")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    b._torch_bought = True
    b._torch_bag_ready = True
    b._torch_est = 2
    b._torch_checked = True
    state = WorldState()
    state.in_realm = True
    state.hp = 67
    state.max_hp = 67
    state.max_hp_known = True
    state.level = 4
    state.prompt_seq = 422
    state.room = "Temple Street, Eastern End"
    state.exits = ["n", "s", "e", "w"]
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["e"]
    assert "w" not in sent
    assert "go manhole" not in sent


def test_matt_following_at_square_leaves_to_stock() -> None:
    """Follower does not stock — only the loop leader without night vision does."""
    b = Brain(
        allowed=True,
        klass="paladin",
        me="matt",
        party_leader="klymacks",
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    b._followed = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.level = 4
    state.prompt_seq = 500
    state.room = "Town Square"
    state.exits = ["n", "s", "e", "w"]
    state.scanned = True
    state.following = "klymacks"
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert "i" not in sent
    assert "go manhole" not in sent
    assert "leave" not in sent


def test_matt_leading_at_square_checks_inventory() -> None:
    """Human loop leader at TS: `i` then torch count before manhole."""
    b = Brain(
        allowed=True,
        klass="paladin",
        me="matt",
        party_leader="matt",
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.level = 4
    state.prompt_seq = 510
    state.room = "Town Square"
    state.exits = ["n", "s", "e", "w"]
    state.scanned = True
    state.inventory = [STARTER_LIGHT, STARTER_LIGHT]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["n"]
    assert "i" not in sent
    assert "go manhole" not in sent


def test_sewer_walks_loop_when_clear() -> None:
    """Empty sewer junction follows the klymacks/Winterhawk tape (starts north)."""
    b = Brain(allowed=True, klass="paladin")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    state = WorldState()
    state.in_realm = True
    state.hp = 38
    state.max_hp = 38
    state.max_hp_known = True
    state.level = 4
    state.prompt_seq = 420
    state.room = "Sewer Tunnel, Junction"
    state.exits = ["u", "n", "e", "s", "w"]
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["n"]
    assert "rest" not in sent
    assert "u" not in sent
    assert b.next_action == "sewer loop"
    assert b._sewer_i == 1
    _see_tile(b, state)
    # Next tape beat while exits still allow it.
    b._last_step = "n"
    b._step_room = "Sewer Tunnel, Junction"
    state.room = "Sewer Tunnel"
    state.exits = ["n", "s"]
    state.prompt_seq += 1
    sent.clear()
    b.tick(state, sent.append, pending=False)
    assert sent == ["n"]
    _see_tile(b, state)
    # Dead-end arm: compass fallback when tape step is closed.
    b._last_step = "n"
    b._step_room = "Sewer Tunnel"
    state.room = "Sewer Tunnel, Dead End"
    state.exits = ["s"]
    state.prompt_seq += 1
    sent.clear()
    b.tick(state, sent.append, pending=False)
    assert sent == ["s"]


def test_sewer_swings_before_loop() -> None:
    b = Brain(allowed=True, klass="paladin")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    state = WorldState()
    state.in_realm = True
    state.hp = 38
    state.max_hp = 38
    state.max_hp_known = True
    state.level = 4
    state.prompt_seq = 421
    state.room = "Sewer Tunnel, Junction"
    state.exits = ["u", "n", "e", "s", "w"]
    state.scanned = True
    state.apply({"kind": "arrive", "name": "a giant rat"})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    # Paladin bash verb is client `aa`.
    assert sent == ["aa giant rat"]
    assert "n" not in sent
    assert "u" not in sent


def test_sewer_dark_uses_torch_then_buys() -> None:
    """Human in dark pipes: use torch once, then climb out to the store."""
    b = Brain(allowed=True, klass="paladin")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    state = WorldState()
    state.in_realm = True
    state.hp = 38
    state.max_hp = 38
    state.max_hp_known = True
    state.level = 4
    state.prompt_seq = 430
    state.room = "Sewer Tunnel"
    state.exits = ["u", "n", "s"]
    state.scanned = True
    state.apply({"kind": "dark"})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == [f"light {STARTER_LIGHT}"]
    assert not state.dark
    assert b._tried_torch
    assert b._torch_lit
    # Still dark after a failed/missing torch — walk up toward the store.
    state.apply({"kind": "dark"})
    state.prompt_seq += 1
    sent.clear()
    b.tick(state, sent.append, pending=False)
    assert sent == ["u"]
    assert b.next_action == "torch"
    # Town Square on a torch run: east toward Giovanni, not north to the guild.
    b2 = Brain(allowed=True, klass="paladin", me="matt", party_leader="matt")
    b2.gear_done = True
    b2.mode = "hunt"
    b2._in_camp = True
    b2._asked_health = True
    b2._torch_shopping = True
    square = WorldState()
    square.in_realm = True
    square.hp = 28
    square.max_hp = 28
    square.max_hp_known = True
    square.level = 4
    square.prompt_seq = 431
    square.room = "Town Square"
    square.exits = ["n", "s", "e", "w"]
    square.scanned = True
    sent2: list[str] = []
    b2.tick(square, sent2.append, pending=False)
    assert sent2 == ["e"]
    assert "go manhole" not in sent2
    assert "n" not in sent2
    assert "w" not in sent2
    assert b2.next_action == "torch"
    _see_tile(b2, square)
    # Western End is one e of TS; two mid-Silver tiles then s into Giovanni.
    west = WorldState()
    west.in_realm = True
    west.hp = 28
    west.max_hp = 28
    west.max_hp_known = True
    west.level = 4
    west.prompt_seq = square.prompt_seq + 1
    west.room = "Silver Street, Western End"
    west.exits = ["n", "s", "e", "w"]
    west.scanned = True
    sent2.clear()
    b2.tick(west, sent2.append, pending=False)
    assert sent2 == ["e"]
    assert b2._torch_shopping
    _see_tile(b2, west)
    mid = WorldState()
    mid.in_realm = True
    mid.hp = 28
    mid.max_hp = 28
    mid.max_hp_known = True
    mid.level = 4
    mid.prompt_seq = west.prompt_seq + 1
    mid.room = "Silver Street"
    mid.exits = ["n", "s", "e", "w"]
    mid.scanned = True
    sent2.clear()
    b2.tick(mid, sent2.append, pending=False)
    assert sent2 == ["e"]
    assert b2._store_silver_east == 1
    _see_tile(b2, mid)
    shops = WorldState()
    shops.in_realm = True
    shops.hp = 28
    shops.max_hp = 28
    shops.max_hp_known = True
    shops.level = 4
    shops.prompt_seq = mid.prompt_seq + 1
    shops.room = "Silver Street"
    shops.exits = ["n", "s", "e", "w"]
    shops.scanned = True
    shops.travel_seq = mid.travel_seq + 1
    sent2.clear()
    b2.tick(shops, sent2.append, pending=False)
    assert sent2 == ["s"]
    assert b2._store_silver_east == 2
    assert b2.next_action == "torch"
    _see_tile(b2, shops)
    # Farm must not yank west while the shop run is sticky; retry door ok.
    shops.prompt_seq += 1
    shops.travel_seq += 1
    sent2.clear()
    b2.next_action = "looking"
    b2.tick(shops, sent2.append, pending=False)
    assert sent2 == ["s"]
    assert "w" not in sent2
    assert b2._torch_shopping


def test_matt_invites_klymacks_at_square_before_torch() -> None:
    """Loop leader picks up the alt at TS before stocking / diving."""
    b = Brain(
        allowed=True,
        klass="paladin",
        me="matt",
        party_leader="matt",
        alts="klymacks",
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    state = WorldState()
    state.in_realm = True
    state.hp = 67
    state.max_hp = 67
    state.max_hp_known = True
    state.level = 4
    state.prompt_seq = 440
    state.room = "Town Square"
    state.exits = ["n", "s", "e", "w"]
    state.scanned = True
    state.mobs = ["Klymacks"]
    state.saw_here = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["invite Klymacks"]
    assert "i" not in sent
    assert "go manhole" not in sent


def test_silver_western_end_walks_west_to_square() -> None:
    b = Brain(allowed=True, klass="paladin", me="matt", party_leader="matt")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    b._sewer_stocked = True
    b._torch_est = 2
    state = WorldState()
    state.in_realm = True
    state.hp = 67
    state.max_hp = 67
    state.max_hp_known = True
    state.level = 4
    state.prompt_seq = 441
    state.room = "Silver Street, Western End"
    state.exits = ["n", "s", "e", "w"]
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["w"]
    assert "look" not in sent
    # Store buy when use already failed.
    store = WorldState()
    store.in_realm = True
    store.hp = 28
    store.max_hp = 28
    store.max_hp_known = True
    store.level = 4
    store.prompt_seq = 432
    store.room = "General Store"
    store.exits = ["n"]
    store.scanned = True
    store.apply({"kind": "dark"})
    b3 = Brain(allowed=True, klass="paladin")
    b3.gear_done = True
    b3.mode = "hunt"
    b3._in_camp = True
    b3._asked_health = True
    b3._tried_torch = True
    sent3: list[str] = []
    b3.tick(store, sent3.append, pending=False)
    assert sent3 == [f"buy {STARTER_LIGHT}", "i"]
    assert b3._torch_bought
    assert b3._torch_wait_inv
    # Stale bag must not trigger another buy on the next prompt.
    store.prompt_seq += 1
    store.inv_seq = 0
    sent3.clear()
    b3.tick(store, sent3.append, pending=False)
    assert sent3 == []
    assert "buy" not in " ".join(sent3)
    # Fresh `i` with one torch → buy once more, then stop at two.
    store.apply(
        {
            "kind": "inventory",
            "items": ["torch"],
            "worn": [],
            "extras": [],
        }
    )
    store.prompt_seq += 1
    sent3.clear()
    b3.tick(store, sent3.append, pending=False)
    assert sent3 == [f"buy {STARTER_LIGHT}", "i"]
    assert b3._torch_est == 2
    store.apply(
        {
            "kind": "inventory",
            "items": ["torch", "torch"],
            "worn": [],
            "extras": [],
        }
    )
    store.prompt_seq += 1
    sent3.clear()
    b3.tick(store, sent3.append, pending=False)
    assert f"buy {STARTER_LIGHT}" not in sent3
    assert "n" in sent3  # leave shop toward the square
    assert b3._torch_bag_ready
    assert not b3._sewer_stocked  # still need TS light before dive
    assert not b3._torch_shopping


def test_store_keeps_two_torches_from_sell() -> None:
    from client.paths import extra_starter

    assert extra_starter(["torch", "torch"], worn=[]) is None
    assert extra_starter(["torch", "torch", "torch"], worn=[]) == "torch"
    # Readied (lit) + spare must not look like a surplus to dump.
    assert (
        extra_starter(
            ["torch (lit)", "torch"],
            extras=["torch (lit)", "torch"],
            worn=[],
        )
        is None
    )


def test_stale_in_shop_does_not_sell_on_street() -> None:
    """Leaving Giovanni must clear in_shop — never sell torch on Silver Street."""
    b = Brain(allowed=True, klass="paladin", me="matt", party_leader="matt")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    b._torch_bought = True
    b._torch_est = 2
    b._torch_bag_ready = True
    b._torch_lit = True
    b._sewer_stocked = True
    b._torch_checked = True
    b._torch_inv_synced = 1
    state = WorldState()
    state.in_realm = True
    state.in_shop = True  # stale from the store
    state.hp = 67
    state.max_hp = 67
    state.max_hp_known = True
    state.level = 4
    state.prompt_seq = 430
    state.room = "Silver Street"
    state.exits = ["s", "e", "w"]
    state.scanned = True
    state.inventory = ["torch (lit)", "torch"]
    state.extras = ["torch (lit)", "torch"]
    state.worn = []
    state.inv_seq = 1
    # Room change clears the stale shop flag.
    state.apply({"kind": "room", "title": "Silver Street, Western End"})
    assert not state.in_shop
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert "sell torch" not in sent
    assert not any(c.startswith("sell ") for c in sent)


def test_readied_torch_counts_as_lit() -> None:
    b = Brain(allowed=True, klass="paladin", me="matt", party_leader="matt")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    state = WorldState()
    state.in_realm = True
    state.hp = 67
    state.max_hp = 67
    state.max_hp_known = True
    state.level = 4
    state.prompt_seq = 431
    state.room = "Town Square"
    state.exits = ["n", "s", "e", "w"]
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["n"]
    assert "i" not in sent
    assert "go manhole" not in sent
    assert f"light {STARTER_LIGHT}" not in sent
    assert "sell torch" not in sent


def test_temple_street_walks_east_not_west() -> None:
    """Casino stretch shares the Temple Street title — always east to the square."""
    b = Brain(allowed=True, klass="paladin", me="matt", party_leader="matt")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    b._sewer_stocked = True
    b._torch_lit = True
    b._torch_bought = True
    b._torch_est = 2
    b._last_step = "n"
    state = WorldState()
    state.in_realm = True
    state.hp = 67
    state.max_hp = 67
    state.max_hp_known = True
    state.level = 4
    state.prompt_seq = 600
    state.room = "Temple Street"
    state.exits = ["s", "e", "w"]
    state.scanned = True
    state.torch_lit = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["e"]
    assert "w" not in sent


def test_temple_street_clears_last_step_across_tiles() -> None:
    """Same title corridor must not stall on last_step=e forever."""
    b = Brain(allowed=True, klass="paladin", me="matt", party_leader="matt")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    b._sewer_stocked = True
    b._torch_lit = True
    b._torch_bought = True
    b._torch_est = 2
    b._last_step = "e"
    b._step_room = "Temple Street"
    b._step_prompt = 600
    state = WorldState()
    state.in_realm = True
    state.hp = 67
    state.max_hp = 67
    state.max_hp_known = True
    state.level = 4
    state.prompt_seq = 601
    state.room = "Temple Street"
    state.exits = ["s", "e", "w"]
    state.scanned = True
    state.torch_lit = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["e"]


def test_casino_walks_north_to_temple_street() -> None:
    b = Brain(allowed=True, klass="paladin", me="matt", party_leader="matt")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    b._sewer_stocked = True
    b._torch_lit = True
    b._torch_bought = True
    b._torch_est = 2
    state = WorldState()
    state.in_realm = True
    state.hp = 67
    state.max_hp = 67
    state.max_hp_known = True
    state.level = 4
    state.prompt_seq = 602
    state.room = "Lucky Strike Casino"
    state.exits = ["n", "s"]
    state.scanned = True
    state.torch_lit = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["n"]
    assert "w" not in sent


def test_atlas_temple_street_east_to_square() -> None:
    from pathlib import Path

    from client.realm_map import Atlas

    a = Atlas(Path("data/realm-map.json"))
    assert a.edges.get(("temple street", "e")) in {
        "temple street eastern end",
        "town square",
    }
    assert '"' not in (a.edges.get(("temple street", "e")) or "")
    route = a.way_home("Temple Street", ["s", "e", "w"])
    assert route
    assert route[0] == "e"


def test_temple_street_rewrites_west_to_east() -> None:
    """Even if atlas asks west, send east — never walk into the hall."""
    b = Brain(allowed=True, klass="paladin", me="matt", party_leader="matt")
    b.gear_done = True
    b.mode = "hunt"
    state = WorldState()
    state.in_realm = True
    state.room = "Temple Street"
    state.exits = ["s", "e", "w"]
    sent: list[str] = []
    assert b._go(sent.append, "w", state)
    assert sent == ["e"]


def test_cmd_cannot_send_west_on_temple_street() -> None:
    b = Brain(allowed=True, klass="paladin", me="matt", party_leader="matt")
    state = WorldState()
    state.room = "Temple Street"
    state.exits = ["s", "e", "w"]
    sent: list[str] = []
    b._cmd(sent.append, "w", state)
    assert sent == ["e"]


def test_sovereign_northern_end_walks_north_to_square() -> None:
    """One step south of TS is Sovereign — do not look-loop; walk north."""
    b = Brain(allowed=True, klass="paladin", me="matt", party_leader="matt")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    b._sewer_stocked = True
    b._torch_est = 2
    state = WorldState()
    state.in_realm = True
    state.hp = 67
    state.max_hp = 67
    state.max_hp_known = True
    state.level = 4
    state.prompt_seq = 520
    state.room = "Sovereign Street, Northern End"
    state.exits = ["n", "s", "e", "w"]
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["n"]
    assert "look" not in sent


def test_skali_front_walks_south_not_look() -> None:
    """Skali sits on Temple Street, Eastern End. Hunt walks out south."""
    b = Brain(allowed=True, klass="paladin")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.level = 4
    state.prompt_seq = 411
    state.room = "Skali's Fine Armour, Front Room"
    state.exits = ["e", "s", "w"]
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["s"]
    assert "look" not in sent


def test_skali_look_scan_still_walks_south() -> None:
    b = Brain(allowed=True, klass="ninja", stealth="walk")
    b.gear_done = True
    b.mode = "hunt"
    b._asked_health = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.level = 4
    state.prompt_seq = 412
    state.room = "Skali's Fine Armour, Showroom"
    state.exits = ["e", "n", "s"]
    state.scanned = False
    state.look_scan = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["s"]
    assert "look" not in sent


def test_level3_road_still_drops_arena() -> None:
    b, state = _matt_hunt()
    state.level = 3
    state.room = "Newhaven, Narrow Road"
    state.exits = ["n", "e", "w", "d"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["d"]


def test_level4_road_walks_skiff_not_pit() -> None:
    b, state = _matt_hunt()
    state.level = 4
    state.room = "Newhaven, Narrow Road"
    state.exits = ["n", "e", "w", "d"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["e"]
    _see_tile(b, state)
    sent.clear()
    state.room = "Newhaven, Village Entrance"
    state.exits = ["n", "s", "w"]
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["se"]
    _see_tile(b, state)
    sent.clear()
    state.room = "Newhaven, Docks"
    state.exits = ["n"]
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["borrow skiff"]


def test_level4_skiff_run_is_not_yanked_to_pit() -> None:
    b, state = _matt_hunt()
    state.level = 4
    state.room = "Newhaven, Forest Path"
    state.exits = ["nw", "s"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["s"]
    assert sent != ["nw"]


def test_level4_leaves_arena_for_skiff() -> None:
    b, state = _matt_hunt()
    state.level = 4
    state.room = "Newhaven, Arena"
    state.exits = ["u"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["u"]


def test_arena_gate_message_walks_skiff() -> None:
    b, state = _matt_hunt()
    state.room = "Newhaven, Narrow Road"
    state.exits = ["n", "e", "w", "d"]
    state.apply(parse_line("You may not enter the arena."))
    assert state.arena_gated
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["e"]
    assert sent != ["d"]


def test_silvermere_docks_walks_to_square() -> None:
    b = Brain(allowed=True, klass="paladin")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 420
    state.room = "Docks"
    state.exits = ["e", "n", "s"]
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["e"]


def test_gear_offer_beats_spell_at_ten() -> None:
    b, state = _matt_bless()
    b.mode = "manual"
    b._memorized = {"minor healing", "harm", "bless"}
    b._seen_level = 9
    state.level = 10
    state.blessed = True
    state.apply({"kind": "trained", "level": 10})
    state.room = "Newhaven, Guild"
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert b.gear_offer() == "shimmering greatsword"
    assert b.spell_offer() == ""


def test_learned_file_skips_known_scrolls() -> None:
    with tempfile.TemporaryDirectory() as raw:
        path = Path(raw) / "learned-spells.json"
        path.write_text(
            '{"matt": ["minor healing", "harm", "bless"]}\n',
            encoding="utf-8",
        )
        b = Brain(
            allowed=True,
            me="matt Matt",
            klass="paladin",
            spell_list=["minor healing", "harm", "bless"],
            learned_path=str(path),
        )
        assert b._spells_shopped
        assert b._knows("bless")
        b.mode = "gear"
        b._looked = True
        b._armour_i = len(ARMOUR_ITEMS)
        b._weapon_worn = True
        b._torch_bought = True
        state = WorldState()
        state.in_realm = True
        state.hp = 22
        state.max_hp = 22
        state.room = "Newhaven, Spell Shop"
        state.exits = ["s"]
        state.prompt_seq = 9
        sent: list[str] = []
        b.tick(state, sent.append, pending=False)
        assert sent == ["s"]
        assert not any(cmd.startswith("buy") for cmd in sent)


def test_ninja_gear_skips_spell_shop() -> None:
    b = Brain(allowed=True, klass="ninja")
    b.mode = "gear"
    b._looked = True
    b._armour_i = len(ARMOUR_ITEMS)
    b._weapon_worn = True
    b._weapon_bought = True
    b._torch_bought = True
    state = WorldState()
    state.in_realm = True
    state.hp = 22
    state.max_hp = 22
    state.room = "Newhaven, Village Entrance"
    state.exits = ["n", "s", "w"]
    state.prompt_seq = 9
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert b.mode == "hunt"
    assert sent == []


def test_manual_asks_health_once() -> None:
    b = Brain(allowed=True)
    state = WorldState()
    state.in_realm = True
    state.hp = 24
    state.max_hp = 24
    state.prompt_seq = 1
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["health"]
    assert b.mode == "manual"
    state.apply({"kind": "hits", "hp": 24, "max_hp": 28})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["health"]
    assert state.hp_label() == "HP 24/28"
    b.tick(state, sent.append, pending=False)
    assert sent == ["health"]


def test_manual_no_health_until_prompt() -> None:
    b, state = _klymacks_manual()
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == []


def test_health_again_after_train() -> None:
    b, state = _road_hunt("ninja", "always")
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["sn"]
    state.apply({"kind": "trained"})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "health"
    state.apply({"kind": "hits", "hp": 32, "max_hp": 32})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent.count("health") == 1
    assert state.hp_label() == "HP 32/32"


def test_hunt_asks_health_once() -> None:
    b = Brain(allowed=True)
    b.gear_done = True
    b.mode = "hunt"
    b._asked_health = False
    state = WorldState()
    state.in_realm = True
    state.hp = 17
    state.max_hp = 17
    state.prompt_seq = 2
    state.room = "Newhaven, Arena"
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["health"]
    state.apply({"kind": "hits", "hp": 17, "max_hp": 28})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert "health" not in sent[1:]
    assert state.hp_label() == "HP 17/28"


def test_rest_and_lop() -> None:
    assert STARTER_WEAPON == "club"
    assert lop_in(["a town guard", "a large rat"]) == "rat"
    b = Brain(allowed=True)
    b.gear_done = True
    b.mode = "hunt"
    state = WorldState()
    state.in_realm = True
    state.hp = 8
    state.max_hp = 22
    state.prompt_seq = 3
    state.room = "Newhaven Arena"
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["u"]
    assert b.mode == "rest"
    _see_tile(b, state)
    state.room = "Newhaven, Narrow Road"
    state.hp = 17
    state.max_hp = 28
    state.max_hp_known = True
    state.in_combat = False
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "rest"
    state.hp = 20
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "rest"
    assert b.mode == "rest"
    assert b.next_action == "healing"
    state.hp = 28
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert b.mode == "hunt"
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "d"


def test_rest_between_fights_then_break() -> None:
    b = Brain(allowed=True)
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 35
    state.max_hp_known = True
    state.prompt_seq = 60
    state.room = "Newhaven, Arena"
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["rest"]
    assert b._sitting
    state.resting = True
    state.apply({"kind": "arrive", "name": "nasty kobold thief"})
    state.apply({"kind": "combat", "name": "nasty kobold thief"})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "att kobold thief"
    assert "break" not in sent


def test_rest_after_fight_when_room_empty() -> None:
    b = Brain(allowed=True)
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 62
    state.room = "Newhaven, Arena"
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == []
    assert "rest" not in sent
    follower = Brain(
        allowed=True,
        me="klymacks Klymacks",
        party_leader="Matt",
        rank="back",
    )
    follower.gear_done = True
    follower.mode = "hunt"
    follower._in_camp = True
    follower._joined = True
    follower._followed = True
    follower._ranked = True
    state.prompt_seq += 1
    sent = []
    follower.tick(state, sent.append, pending=False)
    assert sent == []
    assert "rest" not in sent


def test_ninja_sneaks_then_backstabs() -> None:
    b, state = _road_hunt("ninja", "always")
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["sn"]
    _sneak_try_wait(b, state, sent)
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "d"
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.mobs = ["nasty lashworm"]
    state.apply({"kind": "sneak_ok"})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "bs lashworm"
    assert sent.count("sn") == 1


def test_ninja_breaks_then_sneaks() -> None:
    b = Brain(allowed=True, klass="ninja")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._sitting = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 64
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.resting = True
    state.mobs = ["nasty lashworm"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["att lashworm"]
    assert "u" not in sent
    assert "sn" not in sent


def test_ninja_sitting_hidden_backstabs() -> None:
    b = Brain(allowed=True, klass="ninja")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._sitting = True
    b._hidden = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 66
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.resting = True
    state.mobs = ["nasty lashworm"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["bs lashworm"]
    assert "break" not in sent
    assert "sn" not in sent


def test_following_empty_pit_full_hp_sneaks_not_rest() -> None:
    """klymacks following Matt: full HP Newhaven pit is sn, not rest spam."""
    b, state = _following_klymacks()
    b.mode = "hunt"
    b._asked_health = True
    state.hp = 36
    state.max_hp = 36
    state.max_hp_known = True
    state.mobs = ["Matt"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["sn"]
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent.count("rest") == 0
    assert "rest" not in sent


def test_ninja_empty_pit_full_hp_sneaks() -> None:
    b = Brain(allowed=True, klass="ninja", me="klymacks")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 300
    state.room = "Newhaven, Arena"
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["sn"]
    assert "u" not in sent
    assert "rest" not in sent
    assert not b._sitting


def test_ninja_empty_pit_full_hp_sitting_breaks_then_sneaks() -> None:
    b = Brain(allowed=True, klass="ninja", me="klymacks")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._sitting = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 303
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.resting = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["break"]
    assert "sn" not in sent
    assert not b._sitting
    state.resting = False
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "sn"
    assert "u" not in sent
    assert "rest" not in sent


def test_ninja_empty_pit_wounded_rests_then_break_sneak() -> None:
    b = Brain(allowed=True, klass="ninja", me="klymacks")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 20
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 301
    state.room = "Newhaven, Arena"
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["sn"]
    assert not b._sitting
    state.mobs = ["nasty lashworm"]
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "att lashworm"
    assert "break" not in sent
    assert "rest" not in sent


def test_paladin_empty_pit_full_hp_does_not_rest() -> None:
    b = Brain(allowed=True, klass="paladin", me="sysop Matt")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 302
    state.room = "Newhaven, Arena"
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == []
    assert "rest" not in sent
    assert "sn" not in sent
    assert "u" not in sent
    assert "w" not in sent


def test_ninja_attacks_if_already_in_combat() -> None:
    b = Brain(allowed=True, klass="ninja")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 65
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.in_combat = True
    state.mobs = ["nasty lashworm"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["att lashworm"]


def test_ninja_attacks_when_sneak_fails() -> None:
    b = Brain(allowed=True, klass="ninja")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 66
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.mobs = ["nasty giant rat"]
    b._last_step = "d"
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["att giant rat"]
    assert "sn" not in sent
    assert "bs " not in sent[0]


def test_ninja_sound_on_enter_attacks() -> None:
    b, state = _road_hunt("ninja", "always")
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["sn"]
    state.prompt_seq += 1
    n = len(sent)
    b.tick(state, sent.append, pending=False)
    assert sent[n:] == []
    assert "d" not in sent
    _sneak_try_wait(b, state, sent)
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "d"
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.mobs = ["nasty giant rat"]
    for ev in parse_events("Sneaking...You make a sound when entering the room!"):
        state.apply(ev)
    assert state.sneak_ok and state.sneak_fail
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "att giant rat"
    assert "bs " not in sent[-1]
    assert not b._hidden
    assert not b._sneaking


def test_ninja_bs_after_sneaking_line() -> None:
    b, state = _road_hunt("ninja", "always")
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["sn"]
    _sneak_try_wait(b, state, sent)
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "d"
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.mobs = ["nasty giant rat"]
    state.apply({"kind": "sneak_ok"})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "bs giant rat"
    assert "sn" not in sent[1:]


def test_ninja_inout_sneaks_down_then_leaves() -> None:
    b = Brain(allowed=True, klass="ninja", ambush="inout")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 68
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.mobs = ["nasty giant rat"]
    b._last_step = "d"
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["att giant rat"]
    assert "sn" not in sent
    state.apply({"kind": "killed", "name": "The giant rat"})
    state.mobs = []
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "sn"
    assert "u" not in sent
    assert "road" not in state.room.lower()


def test_ninja_inout_no_sneaking_line_leaves() -> None:
    b = Brain(allowed=True, klass="ninja", ambush="inout")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 69
    state.room = "Newhaven, Narrow Road"
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["sn"]
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["sn"]
    assert "d" not in sent
    _sneak_try_wait(b, state, sent)
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "d"
    state.room = "Newhaven, Arena"
    state.mobs = ["nasty giant rat"]
    state.scanned = True
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "att giant rat"
    assert sent.count("d") == 1
    assert "bs " not in sent[-1]


def test_arena_hunt_road_goes_down_not_healer() -> None:
    b = Brain(allowed=True, klass="paladin", me="klymacks", hunt="arena")
    b.gear_done = True
    b.mode = "hunt"
    state = WorldState()
    state.in_realm = True
    state.hp = 22
    state.max_hp = 22
    state.max_hp_known = True
    state.level = 1
    state.prompt_seq = 500
    state.room = "Newhaven, Narrow Road"
    state.exits = ["n", "e", "w", "d"]
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert "w" not in sent
    assert "buy healing" not in " ".join(sent)
    assert sent[-1] == "d"


def test_arena_hunt_wounded_rests_in_pit() -> None:
    b = Brain(allowed=True, klass="ninja", me="klymacks", hunt="arena")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 10
    state.max_hp = 35
    state.max_hp_known = True
    state.prompt_seq = 501
    state.room = "Newhaven, Arena"
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["rest"]
    assert b.mode == "rest"
    assert "u" not in sent
    assert "w" not in sent


def test_flee_rest_goes_up_not_sit_in_pit() -> None:
    b = Brain(allowed=True)
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 10
    state.max_hp = 35
    state.max_hp_known = True
    state.prompt_seq = 61
    state.room = "Newhaven, Arena"
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["u"]
    assert b.mode == "rest"
    assert "rest" not in sent


def test_two_arrives_stays_then_switches_after_kill() -> None:
    b = Brain(allowed=True, klass="paladin", spell_list=["minor healing", "harm"])
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.ma = 0
    state.prompt_seq = 200
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.apply({"kind": "arrive", "name": "a filthbug"})
    sent: list[str] = []
    cancelled: list[bool] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["aa filthbug"]
    state.in_combat = True
    state.prompt_seq += 1
    state.apply({"kind": "arrive", "name": "a large rat"})
    b.tick(state, sent.append, pending=True, cancel=lambda: cancelled.append(True))
    assert cancelled == []
    assert sent == ["aa filthbug"]
    b.tick(state, sent.append, pending=False)
    assert sent == ["aa filthbug"]
    assert b.next_action == "fighting filthbug"
    assert b._attacking == "filthbug"
    state.mobs = ["a large rat", "a filthbug"]
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["aa filthbug"]
    assert b.next_action == "fighting filthbug"
    state.apply({"kind": "killed", "name": "The filthbug"})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "aa rat"
    assert b._attacking == "rat"


def test_ninja_two_mobs_in_combat_stays_on_first() -> None:
    b = Brain(allowed=True, klass="ninja", me="klymacks")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 201
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.in_combat = True
    state.mobs = ["a filthbug"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["att filthbug"]
    state.prompt_seq += 1
    state.apply({"kind": "arrive", "name": "a large rat"})
    b.tick(state, sent.append, pending=False)
    assert sent == ["att filthbug"]
    assert "sn" not in sent
    assert "bs " not in " ".join(sent)
    assert b.next_action == "fighting filthbug"
    state.apply({"kind": "killed", "name": "The filthbug"})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "att rat"
    assert "sn" not in sent
    assert "bs " not in sent[-1]


def test_hunt_stays_in_fight() -> None:
    b = Brain(allowed=True)
    b.gear_done = True
    b.mode = "hunt"
    state = WorldState()
    state.in_realm = True
    state.hp = 22
    state.max_hp = 22
    state.prompt_seq = 4
    state.room = "Newhaven, Arena"
    state.exits = ["n", "u"]
    state.mobs = ["a filthbug"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["att filthbug"]
    state.prompt_seq += 1
    state.in_combat = True
    state.mobs = []
    state.room = "The filthbug moves to attack you!"
    b.tick(state, sent.append, pending=False)
    assert sent[0] == "att filthbug"
    assert sent.count("att filthbug") == 1
    assert "get all" not in sent
    assert not any(c.startswith("attack ") for c in sent[1:])


def test_camp_chills_then_returns() -> None:
    b = Brain(allowed=True)
    b.gear_done = True
    b.mode = "hunt"
    state = WorldState()
    state.in_realm = True
    state.hp = 22
    state.max_hp = 22
    state.prompt_seq = 5
    state.room = "Newhaven, Arena"
    state.exits = ["u"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["look"]
    assert b._in_camp
    state.apply({"kind": "room", "title": "Newhaven, Arena"})
    state.apply({"kind": "exits", "exits": ["u"]})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "look"
    assert "rest" not in sent
    state.apply({"kind": "arrive", "name": "a filthbug"})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "att filthbug"
    assert "break" not in sent
    b._attacking = ""
    state.in_combat = False
    state.mobs = []
    state.hp = 22
    state.room = "Newhaven, Narrow Road"
    state.exits = ["n", "e", "w", "d"]
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "d"


def test_pit_off_then_rat_attacks_not_look() -> None:
    """Filthbug dies, rat creeps in — swing, do not look and eat a free lunge."""
    b = Brain(allowed=True)
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._pit_fight = True
    b._attacking = "filthbug"
    state = WorldState()
    state.in_realm = True
    state.hp = 22
    state.max_hp = 22
    state.prompt_seq = 14
    state.room = "Newhaven, Arena"
    state.exits = ["u"]
    state.scanned = False
    state.apply({"kind": "killed", "name": "The filthbug"})
    state.apply({"kind": "combat_off"})
    state.apply({"kind": "arrive", "name": "giant rat"})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["att giant rat"]
    assert "look" not in sent


def test_kill_attacks_next_not_get_all() -> None:
    assert attack_name("The nasty giant rat") == "giant rat"
    assert attack_name("A small giant rat") == "giant rat"
    assert attack_name("a filthbug") == "filthbug"
    assert attack_name("giant rat Klymacks") == "giant rat"
    assert attack_name("Klymacks") == "Klymacks"
    b = Brain(allowed=True)
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 22
    state.max_hp = 22
    state.prompt_seq = 8
    state.room = "Newhaven, Arena"
    state.last_kill = "The giant rat"
    state.mobs = ["The giant rat", "A small giant rat"]
    state.in_combat = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["att giant rat"]
    assert "get all" not in sent


def test_arena_kill_gets_coins() -> None:
    assert coins_in(["12 copper", "a torch"]) == ["copper"]
    b = Brain(allowed=True)
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 22
    state.max_hp = 22
    state.prompt_seq = 9
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.last_kill = "The giant rat"
    state.things = ["12 copper", "3 silver"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["get copper"]
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "get silver"
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert "rest" not in sent
    assert sent[-1] == "get silver"
    assert "get gold" not in sent
    assert "get all" not in sent


def _pit_combat_then_road(*, followed: str = "") -> tuple[Brain, WorldState]:
    """In the pit, fighting, then Narrow Road + *Combat Off*."""
    b = Brain(allowed=True, klass="ninja", me="klymacks", ambush="stand")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._attacking = "lashworm"
    b._pit_fight = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 600
    state.room = "Newhaven, Arena"
    state.exits = ["n", "u"]
    state.scanned = True
    state.in_combat = True
    state.mobs = ["nasty lashworm"]
    state.apply({"kind": "room", "title": "Newhaven, Narrow Road"})
    state.apply({"kind": "combat_off"})
    if followed:
        state.apply({"kind": "arrive", "name": followed})
    return b, state


def test_combat_off_on_road_empty_sneaks() -> None:
    """*Combat Off* outside the pit — `break`, look, then `sn` if still empty."""
    b, state = _pit_combat_then_road()
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["break"]
    assert "sn" not in sent
    assert "rest" not in sent
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "look"
    assert "sn" not in sent
    assert "rest" not in sent
    state.apply({"kind": "also_here", "mobs": []})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "sn"
    assert "attack" not in " ".join(sent)
    assert sent.count("sn") == 1


def test_combat_off_on_road_followed_rat_attacks() -> None:
    """A lop that followed onto the road is not sneak setup."""
    b, state = _pit_combat_then_road(followed="giant rat")
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["break"]
    assert "sn" not in sent
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert "sn" not in sent
    assert sent[-1] == "att giant rat"
    assert b.mode == "hunt"


def test_leave_combat_room_breaks_before_sn() -> None:
    """`u` while combat is on — next command is `break`, not sneak."""
    b = Brain(allowed=True, klass="ninja", me="klymacks", ambush="stand")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._attacking = "lashworm"
    b._pit_fight = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 610
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.in_combat = True
    state.mobs = ["nasty lashworm"]
    sent: list[str] = []
    b._cmd(sent.append, "u", state)
    assert sent == ["u"]
    assert b._need_break
    assert "road" in state.room.lower()
    state.prompt_seq += 1
    state.in_combat = True
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "break"
    assert "sn" not in sent
    state.in_combat = False
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "sn"


def test_empty_pit_up_breaks_then_sn() -> None:
    """Solo `u` from the pit — stop attacking, `break`, then `sn`."""
    b = Brain(allowed=True, klass="ninja", me="klymacks", ambush="stand")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 611
    state.room = "Newhaven, Arena"
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["sn"]
    assert "u" not in sent


def test_may_not_sneak_breaks_then_walks() -> None:
    """Busy-fail is this tile, not a combat leftover. Break once, then walk, never re-sn."""
    b, state = _road_hunt("ninja", "always")
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["sn"]
    state.apply({"kind": "sneak_fail", "reason": "busy"})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "break"
    assert b._sneak_wait is False
    state.prompt_seq += 1
    state.in_combat = False
    b.tick(state, sent.append, pending=False)
    assert sent[-1] != "sn"
    assert "sn" not in sent[2:]
    assert sent[-1] == "d"


def test_party_may_not_sneak_paste_then_rat_attacks() -> None:
    """Live klymacks paste: busy-fail must not storm `sn`; rat → attack, not bs."""
    b, state = _following_klymacks()
    b.mode = "hunt"
    state.hp = 22
    state.max_hp = 22
    state.mobs = ["Matt"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["sn"]
    for ev in parse_events("You may not sneak right now!"):
        state.apply(ev)
    state.apply({"kind": "prompt", "hp": 22})
    n = len(sent)
    b.tick(state, sent.append, pending=False)
    assert sent[n:] == ["break"]
    assert sent.count("sn") == 1
    # Same prompt / KEY_GAP: do not stack sn.
    b.tick(state, sent.append, pending=False)
    assert sent[n:] == ["break"]
    state.apply({"kind": "prompt", "hp": 22})
    n = len(sent)
    b.tick(state, sent.append, pending=False)
    assert "sn" not in sent[n:]
    assert sent.count("sn") == 1
    for ev in parse_events(
        "You may not sneak right now!\n"
        "A giant rat creeps into the room from nowhere."
    ):
        state.apply(ev)
    state.apply({"kind": "prompt", "hp": 22})
    n = len(sent)
    b.tick(state, sent.append, pending=False)
    assert sent[n:] == ["att giant rat"]
    assert "sn" not in sent[n:]
    assert "bs " not in " ".join(sent[n:])
    for ev in parse_events("The giant rat lunges at Matt!"):
        state.apply(ev)
    state.apply({"kind": "prompt", "hp": 22})
    n = len(sent)
    b.tick(state, sent.append, pending=False)
    assert "sn" not in sent[n:]
    assert not any(c.startswith("bs ") for c in sent[n:])


def test_party_hidden_then_rat_backstabs() -> None:
    """Sneak succeeded before the rat — `bs`, not a regular attack."""
    b, state = _following_klymacks(hidden=True)
    b.mode = "hunt"
    state.mobs = ["Matt"]
    for ev in parse_events("A giant rat creeps into the room from nowhere."):
        state.apply(ev)
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["bs giant rat"]
    assert "attack" not in " ".join(sent)
    assert "sn" not in sent


def test_pit_kill_combat_off_breaks_then_sn() -> None:
    """Live paste: empty Arena after carrion kill — `break` then `sn`, not look."""
    b = Brain(allowed=True, klass="ninja", me="klymacks")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._attacking = "carrion beast"
    b._pit_fight = True
    state = WorldState()
    state.in_realm = True
    state.hp = 30
    state.max_hp = 30
    state.max_hp_known = True
    state.prompt_seq = 80
    state.room = "Newhaven, Arena"
    state.exits = ["n", "u"]
    state.scanned = True
    state.in_combat = True
    state.mobs = ["carrion beast"]
    state.apply({"kind": "killed", "name": "carrion beast"})
    state.apply({"kind": "combat_off"})
    state.mobs = []
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["break"]
    assert "look" not in sent
    assert "rest" not in sent
    assert "sn" not in sent
    state.prompt_seq += 1
    state.in_combat = False
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "sn"
    assert "look" not in sent
    assert "rest" not in sent


def test_ninja_combat_off_looks_then_attacks_not_bs_loop() -> None:
    """*Combat Off* + leftover mobs: break, sn if empty, then attack. No bs loop."""
    b, state = _following_klymacks()
    b.mode = "hunt"
    b._attacking = "filthbug"
    b._pit_fight = True
    state.in_combat = True
    state.mobs = ["Matt", "filthbug"]
    for ev in parse_events("*Combat Off*"):
        state.apply(ev)
    assert lop_in(state.mobs) is None
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["break"]
    assert "bs " not in " ".join(sent)
    state.prompt_seq += 1
    state.in_combat = False
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "sn"
    assert "look" not in sent
    state.apply({"kind": "also_here", "mobs": ["Matt", "giant rat"]})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "att giant rat"
    assert not any(c.startswith("bs ") for c in sent)
    state.prompt_seq += 1
    n = len(sent)
    b.tick(state, sent.append, pending=False)
    assert not any(c.startswith("bs ") for c in sent[n:])
    assert sent[n:] == []


def test_klymacks_at_17_asks_heal_me() -> None:
    """Live arena paste: HP 17 is at/below HEAL_RATIO — speak heal me, never say."""
    b, state = _following_klymacks()
    b.mode = "hunt"
    state.hp = 17
    state.max_hp = 28
    state.max_hp_known = True
    state.mobs = ["Matt", "kobold thief"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["heal me"]
    assert "say" not in sent[0]


def test_klymacks_gy_asks_heal_me_before_sneak() -> None:
    """Live GY paste: HP 36 while following — `heal me`, not sn after Matt."""
    b, state = _following_klymacks()
    b.mode = "hunt"
    state.hp = 36
    state.max_hp = 50
    state.max_hp_known = True
    state.room = "Graveyard, Southern Edge"
    state.exits = ["n", "e", "w"]
    state.mobs = ["Matt"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["heal me"]
    assert "sn" not in sent


def test_klymacks_asks_heal_me_while_sneak_armed() -> None:
    """Attempting to sneak must not eat the `heal me` tick."""
    b, state = _following_klymacks()
    b.mode = "hunt"
    b._sneak_armed = True
    b._sneak_wait = True
    b._sneak_ready_at = time.monotonic() + 30
    state.hp = 36
    state.max_hp = 50
    state.max_hp_known = True
    state.room = "Graveyard, Southern Edge"
    state.exits = ["n", "e", "w"]
    state.mobs = ["Matt"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["heal me"]
    assert "sn" not in sent


def test_party_combat_off_leftover_kobold_no_sn() -> None:
    """*Combat Off* then leftover kobold lunge: no sn, look/attack."""
    b, state = _following_klymacks()
    b.mode = "hunt"
    b._attacking = "giant rat"
    b._pit_fight = True
    b._last_verb = "attack"
    state.in_combat = True
    state.hp = 17
    state.max_hp = 28
    state.max_hp_known = True
    state.mobs = ["Matt", "giant rat", "kobold thief"]
    for ev in parse_events("*Combat Off*"):
        state.apply(ev)
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert "sn" not in sent
    assert sent[0] == "heal me"
    state.prompt_seq += 1
    n = len(sent)
    b.tick(state, sent.append, pending=False)
    assert "sn" not in sent[n:]
    for ev in parse_events("The fat kobold thief lunges at you!"):
        state.apply(ev)
    state.prompt_seq += 1
    n = len(sent)
    b.tick(state, sent.append, pending=False)
    assert "sn" not in sent[n:]
    assert sent[n:][-1] == "att kobold thief"


def test_matt_swings_leftover_kobold_after_combat_off() -> None:
    """aa/hunt paladin: leftover lop after *Combat Off* — swing, do not idle."""
    b, state = _matt_bless()
    state.apply({"kind": "buff", "name": "bless", "on": True})
    b._attacking = "giant rat"
    state.in_combat = True
    state.mobs = ["Klymacks", "giant rat", "kobold thief"]
    for ev in parse_events("*Combat Off*"):
        state.apply(ev)
    for ev in parse_events("The fat kobold thief lunges at Klymacks!"):
        state.apply(ev)
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "aa kobold thief"
    assert "sn" not in sent
    assert "cast bless" not in sent


def test_sneak_wait_without_reply_retries() -> None:
    """No Attempting and no fail — wait. Do not dump `sn` every prompt."""
    b, state = _road_hunt("ninja", "always")
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["sn"]
    assert b._sneak_wait
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["sn"]
    assert "d" not in sent
    assert b._sneak_wait


def test_road_kill_breaks_before_sn() -> None:
    b, state = _road_hunt("ninja", "always")
    b._attacking = "giant rat"
    state.in_combat = True
    state.mobs = ["nasty giant rat"]
    sent: list[str] = []
    state.apply({"kind": "killed", "name": "giant rat"})
    state.mobs = []
    state.scanned = True
    b.tick(state, sent.append, pending=False)
    assert sent == ["break"]
    assert "sn" not in sent
    state.prompt_seq += 1
    state.in_combat = False
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "sn"


def test_following_leader_mortal_may_sneak() -> None:
    b, state = _road_hunt(
        "ninja", "always", party_leader="Matt", me="klymacks"
    )
    b._followed = True
    b._joined = True
    b._ranked = True
    b._ranked = True
    state.following = "Matt"
    state.ally_mortal = "Matt"
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["leave"]
    assert "follow" not in (b.next_action or "")


def test_combat_off_looks_then_engages() -> None:
    b = Brain(allowed=True)
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._attacking = "kobold thief"
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.prompt_seq = 11
    state.room = "Newhaven, Arena"
    state.needs_scan = True
    state.mobs = ["large kobold thief"]
    state.last_kill = "The kobold thief"
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["look"]
    state.needs_scan = False
    state.last_kill = ""
    state.apply({"kind": "arrive", "name": "giant rat"})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "att giant rat"


def test_creep_in_attacks_without_look() -> None:
    b = Brain(allowed=True)
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._want_look = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.prompt_seq = 12
    state.room = "Newhaven, Arena"
    state.needs_scan = True
    state.scanned = True
    state.mobs = ["giant rat"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["att giant rat"]


def test_same_type_respawn_attacks() -> None:
    b = Brain(allowed=True)
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._attacking = "giant rat"
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.prompt_seq = 14
    state.room = "Newhaven, Arena"
    state.last_kill = "The giant rat"
    state.scanned = True
    state.mobs = ["giant rat"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["att giant rat"]


def test_no_second_look_when_already_scanned() -> None:
    b = Brain(allowed=True)
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._want_look = True
    b._sent_at = 0.0
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.prompt_seq = 13
    state.room = "Newhaven, Arena"
    state.needs_scan = True
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == []
    assert "rest" not in sent


def test_creep_breaks_look_wait() -> None:
    b = Brain(allowed=True)
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._want_look = True
    b._wait_prompt = 99
    b._sent_at = time.monotonic()
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.prompt_seq = 12
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.mobs = ["giant rat"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["att giant rat"]


def test_empty_look_waits_then_creep() -> None:
    b = Brain(allowed=True)
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.prompt_seq = 15
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.mobs = []
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == []
    assert "rest" not in sent
    state.mobs = ["giant rat"]
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "att giant rat"
    assert "break" not in sent


def test_say_attack_does_not_retry() -> None:
    b = Brain(allowed=True)
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._attacking = "giant rat"
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.prompt_seq = 16
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.whiff = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == []
    assert "rest" not in sent
    assert b._attacking == ""
    state.mobs = ["giant rat"]
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "att giant rat"
    assert "break" not in sent


def test_look_scan_listed_lop_engages() -> None:
    """A look in flight must not hide lops already listed in the room."""
    b = Brain(allowed=True)
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.prompt_seq = 17
    state.room = "Newhaven, Arena"
    state.look_scan = True
    state.saw_here = False
    state.scanned = True
    state.mobs = ["acid slime"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["att acid slime"]
    state.apply({"kind": "exits", "exits": ["u"]})
    state.apply({"kind": "arrive", "name": "small carrion beast"})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["att acid slime", "att carrion beast"]


def test_no_double_down_then_attack() -> None:
    b = Brain(allowed=True)
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 25
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 20
    state.room = "Newhaven, Narrow Road"
    state.exits = ["n", "e", "w", "d"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["d"]
    assert "arena" in state.room.lower()
    assert "d" not in state.exits
    state.room = "Newhaven, Narrow Road"
    state.exits = ["n", "e", "w", "d"]
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["d", "look"]
    _see_tile(b, state)
    state.blocked = True
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "look"
    assert "arena" in state.room.lower()
    state.room = "Newhaven, Arena"
    state.exits = ["u"]
    state.apply({"kind": "also_here", "mobs": ["nasty acid slime"]})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "att acid slime"


def test_arena_look_after_down_attacks() -> None:
    b = Brain(allowed=True)
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 25
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 21
    state.room = "Newhaven, Narrow Road"
    state.exits = ["n", "e", "w", "d"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["d"]
    look = (
        "Newhaven, Arena\n"
        "    This huge room is has been carved into the earth.\n"
        "Also here: nasty acid slime.\n"
        "Obvious exits: closed door north, up\n"
        "[HP=25]:\n"
    )
    kinds: set[str] = set()
    for ev in parse_events(look):
        kinds.add(str(ev["kind"]))
        state.apply(ev)
    for ev in harvest_screen(look, set()):
        state.apply(ev)
    state.empty_if_look_missed(kinds, look)
    b.tick(state, sent.append, pending=False)
    assert sent == ["d", "att acid slime"]


def _walk_in_two_lops(klass: str, **kwargs) -> tuple[Brain, WorldState, list[str]]:
    """`d` from the road, then Arena with slime+lashworm on the next tick."""
    stealth = kwargs.pop("stealth", "always" if klass == "ninja" else "")
    b = Brain(allowed=True, klass=klass, stealth=stealth, **kwargs)
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 88
    state.room = "Newhaven, Narrow Road"
    state.exits = ["n", "e", "w", "d"]
    state.scanned = True
    sent: list[str] = []
    if klass == "ninja":
        b.tick(state, sent.append, pending=False)
        assert sent == ["sn"]
        _sneak_try_wait(b, state, sent)
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "d"
    assert b._last_step == "d"
    state.room = "Newhaven, Arena"
    state.mobs = ["nasty acid slime", "nasty lashworm"]
    state.prompt_seq += 1
    return b, state, sent


def test_walk_in_slime_lashworm_no_rest_paladin() -> None:
    """Just `d` into Arena with two lops — never sit."""
    b, state, sent = _walk_in_two_lops("paladin", me="sysop Matt")
    b.tick(state, sent.append, pending=False)
    assert "rest" not in sent
    assert sent[-1] in ("aa acid slime", "aa lashworm")


def test_walk_in_slime_lashworm_no_rest_ninja() -> None:
    """Just `d` into Arena with two lops — hidden `bs`, never sit."""
    b, state, sent = _walk_in_two_lops("ninja", me="klymacks")
    state.apply({"kind": "sneak_ok"})
    b.tick(state, sent.append, pending=False)
    assert "rest" not in sent
    assert sent[-1] in ("bs acid slime", "bs lashworm")
    assert "sn" not in sent[1:]


def test_walk_in_slime_lashworm_visible_ninja_attacks() -> None:
    """Walk-in, ambush on, not hidden — regular attack (just-`d`), never rest."""
    b, state, sent = _walk_in_two_lops("ninja", me="klymacks")
    b.tick(state, sent.append, pending=False)
    assert "rest" not in sent
    assert sent[-1] in ("att acid slime", "att lashworm")
    assert not any(cmd.startswith("bs ") for cmd in sent)


def test_walk_in_look_scan_hides_lops_no_rest() -> None:
    """Tick after `d` before Also here — look_scan hid mobs; do not sit."""
    for klass, me in (("paladin", "sysop Matt"), ("ninja", "klymacks")):
        b = Brain(allowed=True, klass=klass, me=me)
        b.gear_done = True
        b.mode = "hunt"
        b._in_camp = True
        b._last_step = "d"
        b._drop_scan = True
        state = WorldState()
        state.in_realm = True
        state.hp = 28
        state.max_hp = 28
        state.max_hp_known = True
        state.prompt_seq = 89
        state.room = "Newhaven, Arena"
        state.look_scan = True
        state.saw_here = False
        state.scanned = True
        state.mobs = []
        sent: list[str] = []
        b.tick(state, sent.append, pending=False)
        assert "rest" not in sent, (klass, sent)
        state.apply(
            {"kind": "also_here", "mobs": ["nasty acid slime", "nasty lashworm"]}
        )
        state.prompt_seq += 1
        b.tick(state, sent.append, pending=False)
        assert "rest" not in sent, (klass, sent)
        assert sent[-1] in (
            "att acid slime",
            "att lashworm",
            "aa acid slime",
            "aa lashworm",
            "bs acid slime",
            "bs lashworm",
            "u",
        ), (klass, sent)


def test_just_d_stale_road_scan_no_rest() -> None:
    """Road `scanned` must not count as an empty pit after `d`."""
    b = Brain(allowed=True, klass="paladin", me="sysop Matt")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 90
    state.room = "Newhaven, Narrow Road"
    state.exits = ["n", "e", "w", "d"]
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["d"]
    assert not state.scanned
    assert state.look_scan
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert "rest" not in sent
    assert sent[-1] in ("look", "d") or b.next_action == "looking"


def test_no_look_while_engaged() -> None:
    b = Brain(allowed=True)
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._attacking = "acid slime"
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.prompt_seq = 18
    state.room = "Newhaven, Arena"
    state.in_combat = True
    state.scanned = True
    state.mobs = ["acid slime"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == []
    assert "look" not in sent


def test_no_look_on_prompt_after_attack() -> None:
    b = Brain(allowed=True)
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 49
    state.max_hp = 49
    state.max_hp_known = True
    state.prompt_seq = 40
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.saw_here = True
    state.apply({"kind": "arrive", "name": "nasty acid slime"})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["att acid slime"]
    state.prompt_seq += 1
    state.in_combat = True
    b.tick(state, sent.append, pending=False)
    assert sent == ["att acid slime"]
    assert "look" not in sent


def test_arrive_lashworm_one_attack_then_still() -> None:
    b = Brain(allowed=True)
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 41
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.apply({"kind": "arrive", "name": "nasty lashworm"})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["att lashworm"]
    for _ in range(3):
        state.prompt_seq += 1
        state.in_combat = True
        b.tick(state, sent.append, pending=False)
    assert sent == ["att lashworm"]
    assert "look" not in sent


def test_combat_off_empty_no_attack() -> None:
    b = Brain(allowed=True)
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._attacking = "acid slime"
    b._last_verb = "attack"
    b._last_aim = "acid slime"
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 42
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.saw_here = True
    state.in_combat = True
    state.mobs = ["nasty acid slime"]
    state.apply({"kind": "combat_off"})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["look"]
    assert not any(c.startswith(("att ", "aa ", "attack ")) for c in sent)
    assert "bs " not in " ".join(sent)


def test_combat_off_echo_does_not_loop_look_attack() -> None:
    """Matt's paste: Off/Engaged + swing echo must not look-then-attack forever."""
    b, state = _following_klymacks()
    b.mode = "hunt"
    b._attacking = "acid slime"
    b._last_verb = "attack"
    b._last_aim = "acid slime"
    state.hp = 49
    state.max_hp = 49
    state.max_hp_known = True
    state.in_combat = True
    state.scanned = True
    state.saw_here = True
    state.mobs = ["Matt", "nasty acid slime"]
    sent: list[str] = []
    for _ in range(3):
        for ev in parse_events(
            "Klymacks moves to attack nasty acid slime.\n"
            "*Combat Off*\n"
            "*Combat Engaged*\n"
        ):
            state.apply(ev)
        b.tick(state, sent.append, pending=False)
        state.prompt_seq += 1
    assert "look" not in sent
    assert sent.count("att acid slime") == 0
    assert not any(c.startswith(("att ", "aa ", "attack ")) for c in sent)


def test_sense_and_engage() -> None:
    b = Brain(allowed=True)
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 22
    state.max_hp = 22
    state.prompt_seq = 10
    state.room = "Newhaven, Arena"
    state.things = ["5 gold"]
    state.mobs = ["A small giant rat"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["get gold"]
    state.prompt_seq += 1
    sent.clear()
    b.tick(state, sent.append, pending=False)
    assert sent == ["att giant rat"]


def test_sneak_in_after_loot_attacks() -> None:
    b = Brain(allowed=True)
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 41
    state.max_hp = 41
    state.max_hp_known = True
    state.prompt_seq = 40
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.things = ["5 silver nobles"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["get silver"]
    state.things = []
    state.apply({"kind": "arrive", "name": "small kobold thief"})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "att kobold thief"


def test_get_coins_breaks_combat_must_attack_again() -> None:
    """`get` cancels auto-combat. Same species after loot must be swung again."""
    b = Brain(allowed=True)
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._last_aim = "giant rat"
    b._last_verb = "attack"
    state = WorldState()
    state.in_realm = True
    state.hp = 50
    state.max_hp = 50
    state.max_hp_known = True
    state.prompt_seq = 80
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.things = ["12 copper"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["get copper"]
    assert b._need_swing
    assert b._last_aim == ""
    state.things = []
    state.mobs = ["thin giant rat"]
    state.in_combat = False
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "att giant rat"
    assert not b._need_swing


def _road_hunt(klass: str, stealth: str, **kwargs) -> tuple[Brain, WorldState]:
    b = Brain(allowed=True, klass=klass, stealth=stealth, **kwargs)
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 90
    state.room = "Newhaven, Narrow Road"
    state.exits = ["n", "e", "w", "d"]
    state.scanned = True
    return b, state


def _boot_look_done(
    state: WorldState, *, mobs: list[str] | None = None, exits: list[str] | None = None
) -> None:
    """Finish the ambush-boot look so the next tick can decide."""
    state.prompt_seq += 1
    if mobs is not None:
        state.apply({"kind": "also_here", "mobs": mobs})
        return
    state.apply({"kind": "exits", "exits": exits or list(state.exits) or ["n", "e", "w", "d"]})


def _solo_ambush_boot(
    *, room: str = "Newhaven, Arena", exits: list[str] | None = None, **kwargs
) -> tuple[Brain, WorldState]:
    b = Brain(
        allowed=True, klass="ninja", me="klymacks", ambush="stand", **kwargs
    )
    b.gear_done = True
    b._in_camp = "arena" in room.lower()
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 800
    state.room = room
    state.exits = exits or (["u"] if "arena" in room.lower() else ["n", "e", "w", "d"])
    state.scanned = True
    return b, state


def test_ninja_always_sneaks_before_move() -> None:
    b, state = _road_hunt("ninja", "always")
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["sn"]
    state.prompt_seq += 1
    n = len(sent)
    b.tick(state, sent.append, pending=False)
    assert sent[n:] == []
    assert "d" not in sent
    _sneak_try_wait(b, state, sent)
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "d"
    b2, state2 = _road_hunt("ninja", "always")
    sent2: list[str] = []
    b2.tick(state2, sent2.append, pending=False)
    assert sent2 == ["sn"]
    state2.apply({"kind": "sneak_fail"})
    state2.prompt_seq += 1
    b2.tick(state2, sent2.append, pending=False)
    assert sent2[-1] == "d"
    assert sent2[-1] != "sn"


def test_ninja_road_sneak_retries_then_down() -> None:
    b, state = _road_hunt("ninja", "auto")
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["sn"]
    sent.clear()
    state.apply({"kind": "sneak_fail"})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["d"]
    assert "sn" not in sent


def test_ninja_road_sneak_try_fail_same_tick_retries() -> None:
    b, state = _road_hunt("ninja", "always")
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["sn"]
    for ev in parse_events("Attempting to sneak...You don't think you're sneaking."):
        state.apply(ev)
    assert state.sneak_try and state.sneak_fail
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "d"
    assert not b._sneak_armed
    assert not b._sneaking
    assert not b._hidden


def _arena_ninja_lops(*, sneaking: bool = False, **kwargs) -> tuple[Brain, WorldState]:
    b = Brain(allowed=True, klass="ninja", me="klymacks", stealth="auto", **kwargs)
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._sneaking = sneaking
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 420
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.mobs = ["acid slime", "nasty lashworm"]
    return b, state


def _one_lop_swing(sent: list[str], *, hidden: bool) -> None:
    assert sent, sent
    verb = "bs"
    assert sent[0] in {f"{verb} acid slime", f"{verb} lashworm"}, sent
    assert "sn" not in sent
    assert not any(cmd.startswith("attack ") for cmd in sent)


def test_ninja_pit_slime_lashworm_fights_when_visible() -> None:
    b, state = _arena_ninja_lops()
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent[0] in {"att acid slime", "att lashworm"}
    assert "u" not in sent
    assert "sn" not in sent


def test_ninja_pit_slime_lashworm_bs_when_sneaking() -> None:
    b, state = _arena_ninja_lops(sneaking=True)
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    _one_lop_swing(sent, hidden=True)


def test_ninja_pit_lops_engage_during_look_scan() -> None:
    """Room-title reprint sets look_scan; listed lops must still be swung."""
    b, state = _arena_ninja_lops(sneaking=True)
    state.look_scan = True
    state.saw_here = False
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    _one_lop_swing(sent, hidden=True)


def test_ninja_pit_lops_fight_while_sneak_wait() -> None:
    b, state = _arena_ninja_lops()
    b._sneak_wait = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent[0] in {"att acid slime", "att lashworm"}
    assert "u" not in sent
    assert "sn" not in sent


def test_ninja_pit_lops_solo_with_matt_leader() -> None:
    """party_leader is Matt but he is not here — solo still swings."""
    b, state = _arena_ninja_lops(
        sneaking=True, party_leader="Matt", rank="back", alts="matt sysop"
    )
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    _one_lop_swing(sent, hidden=True)
    assert "join" not in " ".join(sent).lower()


def test_ninja_road_empty_sneaks() -> None:
    b, state = _road_hunt("ninja", "always")
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["sn"]
    assert "attack" not in " ".join(sent)
    assert "bs" not in " ".join(sent)


def test_ninja_road_health_then_sn_or_d() -> None:
    b, state = _road_hunt("ninja", "always")
    state.max_hp_known = False
    b._asked_health = False
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["health"]
    state.apply({"kind": "hits", "hp": 28, "max_hp": 28})
    assert state.max_hp_known
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "sn"
    assert sent != ["health"]
    assert "d" not in sent
    _sneak_try_wait(b, state, sent)
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "d"


def test_paladin_asks_health_for_max_ma() -> None:
    b, state = _road_hunt("paladin", "")
    state.max_hp_known = True
    state.ma = 8
    state.max_ma = None
    b._asked_health = False
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["health"]
    state.apply({"kind": "mana", "ma": 8, "max_ma": 8})
    assert state.max_ma == 8
    assert state.hp_label() == "HP 28/28  MA 8/8"
    state.blessed = True
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "d"


def test_ninja_road_health_hits_no_prompt_still_moves() -> None:
    """`health` then Hits — do not idle on _wait_prompt / sent==[]."""
    b, state = _road_hunt("ninja", "always")
    state.max_hp_known = False
    b._asked_health = False
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["health"]
    state.apply({"kind": "hits", "hp": 28, "max_hp": 28})
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "sn"
    assert "d" not in sent


def test_ninja_road_sneak_try_goes_down() -> None:
    b, state = _road_hunt("ninja", "always")
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["sn"]
    _sneak_try_wait(b, state, sent)
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "d"


def test_ninja_road_sneak_try_waits_before_down() -> None:
    """`d` on the Attempting prompt breaks sneak — wait ~2s, not a full round."""
    b, state = _road_hunt("ninja", "always")
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["sn"]
    state.apply({"kind": "sneak_try"})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent == ["sn"]
    assert "d" not in sent
    assert b._sneak_armed
    assert b.next_action == "ambush"
    b._sneak_ready_at = 0.0
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "d"


def test_ninja_road_sneak_fail_during_settle_retries() -> None:
    b, state = _road_hunt("ninja", "always")
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["sn"]
    state.apply({"kind": "sneak_try"})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert "d" not in sent
    state.apply({"kind": "sneak_fail"})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "d"
    assert not b._sneak_armed


def test_ninja_road_matt_no_invite_still_goes_down() -> None:
    b, state = _klymacks_near_matt(room="Newhaven, Narrow Road")
    state.exits = ["n", "e", "w", "d"]
    state.max_hp_known = False
    b._asked_health = False
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["health"]
    state.apply({"kind": "hits", "hp": 28, "max_hp": 28})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "sn"
    assert "join" not in " ".join(sent).lower()
    _sneak_try_wait(b, state, sent)
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "d"
    assert "join" not in " ".join(sent).lower()


def test_paladin_road_scanned_empty_goes_down() -> None:
    b, state = _road_hunt("paladin", "always")
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["d"]
    assert "sn" not in sent


def test_ninja_pit_lop_attacks_not_sneak() -> None:
    b = Brain(allowed=True, klass="ninja", me="klymacks")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 310
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.mobs = ["nasty lashworm"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["att lashworm"]
    assert "sn" not in sent
    assert "u" not in sent


def test_ninja_empty_pit_visible_goes_up() -> None:
    b = Brain(allowed=True, klass="ninja", me="klymacks")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 311
    state.room = "Newhaven, Arena"
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["sn"]
    assert "u" not in sent


def test_ninja_empty_pit_hidden_waits() -> None:
    b = Brain(allowed=True, klass="ninja", me="klymacks")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._hidden = True
    b._sneaking = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 312
    state.room = "Newhaven, Arena"
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == []
    assert "sn" not in sent
    assert "u" not in sent
    assert b.next_action == "ambush"


def test_ninja_always_breaks_before_move_sneak() -> None:
    b, state = _road_hunt("ninja", "always")
    b._sitting = True
    state.resting = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["break"]
    assert "sn" not in sent
    state.resting = False
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "sn"


def test_ninja_walk_does_not_sneak_before_move() -> None:
    b, state = _road_hunt("ninja", "walk")
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["d"]
    assert "sn" not in sent


def test_ninja_toggle_stealth_flips() -> None:
    b, state = _road_hunt("ninja", "always")
    assert b.toggle_stealth() == "walk"
    assert b.next_action == "ambush walk"
    assert b.stealth_label() == "walk"
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["d"]
    assert b.toggle_stealth() == "always"
    assert b.next_action == "ambush always"
    assert b.stealth_label() == "ambush"
    state.prompt_seq += 1
    state.room = "Newhaven, Narrow Road"
    state.exits = ["n", "e", "w", "d"]
    state.scanned = True
    state.look_scan = False
    b._last_step = ""
    b._drop_scan = False
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "sn"


def test_on_ambush_on_empty_road_sns() -> None:
    b, state = _road_hunt("ninja", "walk")
    assert b.toggle_stealth() == "always"
    assert b.on_ambush_on(state) == ["look"]
    assert b._ambush_boot
    assert not b._sneak_wait
    _boot_look_done(state)
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["sn"]
    assert b._sneak_wait
    assert b.next_action == "sn"
    _sneak_try_wait(b, state, sent)
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "d"


def test_on_ambush_on_empty_road_fail_retries() -> None:
    b, state = _road_hunt("ninja", "walk")
    b.toggle_stealth()
    assert b.on_ambush_on(state) == ["look"]
    _boot_look_done(state)
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "sn"
    state.apply({"kind": "sneak_fail"})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "d"
    assert not b._sneak_armed


def test_on_ambush_on_pit_slime_fights() -> None:
    b, state = _arena_ninja_lops()
    b.stealth = "walk"
    assert b.toggle_stealth() == "always"
    sent = b.on_ambush_on(state)
    assert sent == ["look"]
    assert "sn" not in sent
    assert "u" not in sent
    assert not b._sneak_wait
    _boot_look_done(state, mobs=["acid slime", "nasty lashworm"])
    later: list[str] = []
    b.tick(state, later.append, pending=False)
    assert later[0] in {"att acid slime", "att lashworm"}
    assert "u" not in later
    assert "s" not in later
    assert "sn" not in later


def test_on_ambush_on_walk_is_empty() -> None:
    b, state = _road_hunt("ninja", "always")
    assert b.toggle_stealth() == "walk"
    assert b.on_ambush_on(state) == []
    assert not b._sneak_wait
    assert not b._ambush_boot


def test_on_ambush_on_sitting_breaks_then_sn() -> None:
    b, state = _road_hunt("ninja", "walk")
    b.toggle_stealth()
    b._sitting = True
    state.resting = True
    assert b.on_ambush_on(state) == ["look"]
    _boot_look_done(state)
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["break"]
    assert not b._sitting
    state.resting = False
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "sn"
    assert b._sneak_wait
    assert b.next_action == "sn"


def test_f7_ambush_boot_looks_before_sn_or_swing() -> None:
    """F7 own ambush: look first even if a stale Also here lists a rat."""
    b, state = _solo_ambush_boot()
    state.mobs = ["giant rat"]
    b.toggle_hunt()
    assert b.mode == "hunt"
    assert b._ambush_boot
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["look"]
    assert "sn" not in sent
    assert "u" not in sent
    assert "s" not in sent
    assert "break" not in sent
    assert "attack" not in " ".join(sent)
    assert not any(cmd.startswith("bs ") for cmd in sent)


def test_f7_ambush_boot_arena_rat_fights() -> None:
    """Occupied arena after the boot look: swing, do not leave to sn."""
    b, state = _solo_ambush_boot()
    state.mobs = ["giant rat"]
    b.toggle_hunt()
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["look"]
    _boot_look_done(state, mobs=["giant rat"])
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "att giant rat"
    assert "u" not in sent
    assert "s" not in sent
    assert "sn" not in sent


def test_f7_ambush_boot_empty_looks_then_sn_once() -> None:
    b, state = _solo_ambush_boot(room="Newhaven, Narrow Road", exits=["n", "e", "w", "d"])
    b.toggle_hunt()
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["look"]
    _boot_look_done(state)
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "sn"
    assert sent.count("sn") == 1
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent.count("sn") == 1
    assert "attack" not in " ".join(sent)


def test_f7_ambush_boot_hidden_looks_then_bs() -> None:
    b, state = _solo_ambush_boot()
    b._hidden = True
    b._sneaking = True
    state.mobs = ["nasty lashworm"]
    b.toggle_hunt()
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["look"]
    _boot_look_done(state, mobs=["nasty lashworm"])
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "bs lashworm"
    assert "u" not in sent
    assert "s" not in sent
    assert "sn" not in sent


def test_f7_ambush_boot_visible_lop_on_road_attacks() -> None:
    b, state = _solo_ambush_boot(
        room="Newhaven, Narrow Road", exits=["n", "e", "w", "d"]
    )
    state.mobs = ["giant rat"]
    b.toggle_hunt()
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["look"]
    _boot_look_done(state, mobs=["giant rat"])
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "att giant rat"
    assert "u" not in sent
    assert "s" not in sent
    assert "sn" not in sent
    assert not any(cmd.startswith("bs ") for cmd in sent)


def test_f7_following_ambush_boot_does_not_leave() -> None:
    """Following Matt: F7 still swings in the pit — no own u/s."""
    b, state = _following_klymacks()
    b.toggle_hunt()
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["att acid slime"]
    assert "u" not in sent
    assert "s" not in sent
    assert "look" not in sent
    assert not b._ambush_boot


def test_ambush_key_aliases_stealth() -> None:
    always = Brain(allowed=True, klass="ninja", ambush="always")
    assert always.ambush == "stand"
    assert always.stealth == "always"
    assert always.stealth_label() == "ambush"
    off = Brain(allowed=True, klass="ninja", ambush="off")
    assert off.ambush == "stand"
    assert off.stealth == "walk"
    assert off.stealth_label() == "walk"
    leftover = Brain(allowed=True, klass="ninja", ambush="inout", stealth="always")
    assert leftover.ambush == "inout"
    assert leftover.stealth == "always"
    stand = Brain(allowed=True, klass="ninja", ambush="stand")
    assert stand.ambush == "stand"
    assert stand.stealth == "always"
    assert stand.stealth_label() == "ambush"
    inout = Brain(allowed=True, klass="ninja", ambush="inout")
    assert inout.stealth == "always"
    assert inout.stealth_label() == "ambush"


def test_ninja_ambush_stand_road_sns_before_d() -> None:
    """ambush: stand (player.json) — Narrow Road empty, max HP known → sn then d."""
    b, state = _road_hunt("ninja", "", ambush="stand")
    assert b.stealth == "always"
    assert b.stealth_label() == "ambush"
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["sn"]
    assert "d" not in sent
    assert "rest" not in sent
    _sneak_try_wait(b, state, sent)
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "d"


def test_ninja_ambush_empty_road_full_hp_sns() -> None:
    b, state = _road_hunt("ninja", "", ambush="stand")
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["sn"]
    assert "rest" not in sent


def test_ninja_ambush_following_empty_pit_stays() -> None:
    b = Brain(
        allowed=True,
        klass="ninja",
        me="klymacks",
        party_leader="Matt",
        ambush="stand",
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._followed = True
    b._joined = True
    b._ranked = True
    b._ranked = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 313
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.following = "Matt"
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["sn"]
    assert "u" not in sent
    assert "d" not in sent
    assert "attack" not in " ".join(sent)


def test_ninja_ambush_pit_lops_fights() -> None:
    b, state = _arena_ninja_lops(ambush="stand")
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent[0] in {"att acid slime", "att lashworm"}
    assert "u" not in sent
    assert "sn" not in sent


def test_corwyn_blocks_sneak() -> None:
    b, state = _road_hunt("ninja", "always")
    state.apply({"kind": "also_here", "mobs": ["Corwyn"]})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert "sn" not in sent
    assert sent == ["d"]


def test_coorwyn_blocks_sneak() -> None:
    b, state = _road_hunt("ninja", "always")
    state.apply({"kind": "also_here", "mobs": ["Coorwyn"]})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert "sn" not in sent
    assert sent == ["d"]


def test_party_empty_with_matt_still_sneaks() -> None:
    """Empty pit with Matt — sneak. Party mates are not strangers."""
    b, state = _following_klymacks()
    b.mode = "hunt"
    b._asked_health = True
    state.hp = 36
    state.max_hp = 36
    state.mobs = ["Matt"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["sn"]


def test_busy_sneak_empty_room_does_not_retry() -> None:
    """No room title yet — busy-fail still locks sn until we walk."""
    b, state = _following_klymacks()
    b.mode = "hunt"
    b._asked_health = True
    state.room = ""
    state.mobs = ["Matt"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert "sn" not in sent
    state.apply({"kind": "sneak_fail", "reason": "busy"})
    state.prompt_seq += 1
    n = len(sent)
    b.tick(state, sent.append, pending=False)
    assert "sn" not in sent[n:]
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert "sn" not in sent[n:]


def test_join_arena_slime_fights_not_i_or_look() -> None:
    """Drop into the pit from Enter the Realm: swing, do not i/look/health."""
    for klass, me, swing in (
        ("ninja", "klymacks", "att acid slime"),
        ("paladin", "matt", "aa acid slime"),
    ):
        b = Brain(allowed=True, klass=klass, me=me, stealth="walk")
        state = WorldState()
        state.in_realm = True
        state.hp = 28
        state.max_hp = 28
        state.max_hp_known = True
        state.prompt_seq = 12
        state.room = "Newhaven, Arena"
        state.exits = ["u"]
        state.scanned = True
        state.apply({"kind": "also_here", "mobs": ["acid slime"]})
        b.toggle_hunt()
        assert b.open_gear_inv(state) is None
        sent: list[str] = []
        b.tick(state, sent.append, pending=False)
        assert sent == [swing], (klass, sent)
        assert "i" not in sent
        assert "look" not in sent
        assert "health" not in sent


def test_join_arena_slime_fights_before_health() -> None:
    b = Brain(allowed=True, klass="ninja", me="klymacks")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b._asked_health = False
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = False
    state.prompt_seq = 13
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.apply({"kind": "also_here", "mobs": ["acid slime"]})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["att acid slime"]
    assert "health" not in sent


def test_ninja_ambush_pit_lops_hidden_backstabs() -> None:
    b, state = _arena_ninja_lops(sneaking=True, ambush="stand")
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    _one_lop_swing(sent, hidden=True)


def test_ninja_ambush_following_does_not_move() -> None:
    """Follow Matt: sn in place, never own u/d."""
    b, state = _road_hunt(
        "ninja", "always", party_leader="Matt", me="klymacks"
    )
    b._followed = True
    b._joined = True
    b._ranked = True
    b._ranked = True
    state.following = "Matt"
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["sn"]
    assert "d" not in sent
    assert "u" not in sent
    assert "attack" not in " ".join(sent)


def test_ninja_auto_following_sneaks() -> None:
    b, state = _road_hunt("ninja", "auto", party_leader="Matt", me="klymacks")
    b._followed = True
    b._joined = True
    b._ranked = True
    b._ranked = True
    state.following = "Matt"
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["sn"]
    assert "d" not in sent
    assert "attack" not in " ".join(sent)


def test_paladin_never_sneaks() -> None:
    b, state = _road_hunt("paladin", "always")
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["d"]
    assert "sn" not in sent
    _see_tile(b, state)
    assert b.toggle_stealth() == "walk"
    state.room = "Newhaven, Narrow Road"
    state.exits = ["n", "e", "w", "d"]
    state.prompt_seq += 1
    b._last_step = ""
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "d"
    assert "sn" not in sent


def test_map_pathfind_when_lost() -> None:
    atlas = Atlas()
    atlas.observe("Room A", ["d"])
    atlas.observe("Newhaven, Arena", ["u"], via="d", prev="Room A")
    assert atlas.path("Room A", "Newhaven, Arena") == ["d"]
    b = Brain(allowed=True, atlas=atlas)
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = False
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 91
    state.room = "Room A"
    state.exits = ["d"]
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["d"]
    assert b.next_action in {"d", "path: d"} or "path:" in b.next_action


def test_unknown_room_does_not_crash() -> None:
    b = Brain(allowed=True)
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = False
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 92
    state.room = ""
    state.exits = []
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)


def test_following_skips_map_walk() -> None:
    atlas = Atlas()
    atlas.observe("Mystery Cave", ["n", "s"])
    atlas.observe("Newhaven, Arena", ["u"], via="s", prev="Mystery Cave")
    b = Brain(
        allowed=True,
        klass="ninja",
        stealth="always",
        party_leader="Matt",
        atlas=atlas,
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = False
    b._joined = True
    b._followed = True
    b._ranked = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 93
    state.room = "Mystery Cave"
    state.exits = ["n", "s"]
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["sn"]
    assert "n" not in sent[1:] and "s" not in sent


def _klymacks_arena() -> tuple[Brain, WorldState]:
    b = Brain(
        allowed=True,
        me="klymacks Klymacks",
        alts="matt sysop",
        party_leader="Matt",
        rank="back",
        klass="ninja",
        stealth="walk",
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 400
    state.room = "Newhaven, Arena"
    state.exits = ["u"]
    state.scanned = True
    state.mobs = ["Matt"]
    return b, state


def _matt_road() -> tuple[Brain, WorldState]:
    b = Brain(
        allowed=True,
        me="sysop Matt",
        alts="klymacks",
        party_leader="Matt",
        klass="paladin",
        spell_list=["minor healing", "harm"],
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = False
    state = WorldState()
    state.in_realm = True
    state.hp = -95
    state.max_hp = 28
    state.max_hp_known = True
    state.ma = 0
    state.prompt_seq = 410
    state.room = "Newhaven, Narrow Road"
    state.exits = ["n", "e", "w", "d"]
    state.scanned = True
    return b, state


def test_ally_mortal_break_then_leave_drag() -> None:
    b, state = _klymacks_arena()
    state.following = "Matt"
    b._joined = True
    b._followed = True
    b._ranked = True
    state.in_combat = True
    state.apply({"kind": "mortal", "name": "Matt"})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["break"]
    state.in_combat = False
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "leave"
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "drag Matt u"


def test_ally_mortal_arena_leave_drag_aid() -> None:
    b, state = _klymacks_arena()
    state.following = "Matt"
    b._joined = True
    b._followed = True
    b._ranked = True
    state.apply({"kind": "mortal", "name": "Matt"})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["leave"]
    assert not b._followed
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "drag Matt u"
    assert "road" in state.room.lower()
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "aid Matt"
    blob = " ".join(sent).lower()
    assert "buy healing" not in blob
    assert " w" not in f" {blob} "
    assert not any(cmd == "w" for cmd in sent)


def test_panic_matt_mortal_starts_rescue() -> None:
    b, state = _klymacks_arena()
    state.following = "Matt"
    b._joined = True
    b._followed = True
    b._ranked = True
    state.apply({"kind": "mortal", "name": "Matt"})
    sent: list[str] = []
    b.panic(state, sent.append)
    assert sent == ["break"]
    assert b._rescue == "out"
    assert b._rescue_who == "Matt"
    assert b._panic_until == 0.0
    assert b.mode == "hunt"
    state.in_combat = False
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "leave"
    assert not b._followed
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "drag Matt u"


def test_after_aid_homie_returns_to_pit() -> None:
    b, state = _klymacks_arena()
    state.apply({"kind": "mortal", "name": "Matt"})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["drag Matt u"]
    _see_tile(b, state)
    state.apply({"kind": "aided", "name": "Matt"})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "drag"
    _see_tile(b, state)
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "d"
    assert "arena" in state.room.lower()
    state.mobs = []
    state.scanned = True
    state.look_scan = False
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] in ("look", "rest", "sn")
    assert "buy healing" not in " ".join(sent)


def test_after_aid_road_monster_fights() -> None:
    """After aid, do not freeze on the road — fight, then solo."""
    b, state = _klymacks_arena()
    state.apply({"kind": "mortal", "name": "Matt"})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["drag Matt u"]
    state.apply({"kind": "aided", "name": "Matt"})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "drag"
    state.mobs = ["giant rat"]
    state.scanned = True
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "att giant rat"
    assert b.next_action != "aid"
    assert "join" not in " ".join(sent).lower()


def test_after_aid_solos_until_invite() -> None:
    """After aid: solo ambush. Regroup only on a real invite."""
    b = Brain(
        allowed=True,
        me="klymacks",
        alts="matt sysop",
        party_leader="Matt",
        rank="back",
        klass="ninja",
        stealth="always",
    )
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 420
    state.room = "Newhaven, Arena"
    state.exits = ["u"]
    state.scanned = True
    state.apply({"kind": "mortal", "name": "Matt"})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["drag Matt u"]
    state.apply({"kind": "aided", "name": "Matt"})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    if sent[-1] == "break":
        state.in_combat = False
        state.prompt_seq += 1
        b.tick(state, sent.append, pending=False)
    assert sent[-1] == "drag"
    state.mobs = ["Matt"]
    state.scanned = True
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "sn"
    assert "join" not in " ".join(sent).lower()
    assert not b._followed
    _sneak_try_wait(b, state, sent)
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "d"
    assert "join" not in " ".join(sent).lower()
    state.apply({"kind": "invited", "name": "Matt"})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "follow Matt"


def test_wounded_rests_on_road_no_healer() -> None:
    b, state = _matt_road()
    state.apply({"kind": "aided", "name": "you"})
    state.hp = -95
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent
    assert sent[-1] == "rest"
    assert "buy healing" not in " ".join(sent)
    assert "w" not in sent
    state.resting = True
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "rest"
    assert b.next_action == "healing"
    assert "w" not in sent
    assert "buy healing" not in " ".join(sent)
    state.ma = 8
    state.hp = 10
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "cast minor healing"
    assert "w" not in sent
    state.hp = 8
    state.mortal = True
    state.bleeding = True
    state.prompt_seq += 1
    b._last_cast = ""
    b._cast_at = 0.0
    b.tick(state, sent.append, pending=False)
    assert "d" not in sent
    assert "w" not in sent


def test_ready_invite_join_only_after_invite() -> None:
    b, state = _klymacks_arena()
    state.mobs = ["Matt", "nasty acid slime"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert "join" not in " ".join(sent).lower()
    state.apply({"kind": "invited", "name": "Matt"})
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "follow Matt"


def test_matt_ready_goes_down_not_healer() -> None:
    b, state = _matt_road()
    state.apply({"kind": "aided", "name": "you"})
    state.hp = 20
    state.ma = 0
    b._recovering = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent
    assert sent[-1] == "d"
    assert "w" not in sent
    assert "buy healing" not in " ".join(sent)
    state.room = "Newhaven, Arena"
    state.mobs = ["Klymacks"]
    state.scanned = True
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "invite Klymacks"


def test_go_train_walks_to_guild() -> None:
    b, state = _road_hunt("paladin", "walk")
    b.request_train()
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["n"]
    assert b._want_train
    assert b.next_action == "train"
    state.room = "Newhaven, Guild"
    state.exits = ["s"]
    state.prompt_seq += 1
    b._last_step = ""
    b.tick(state, sent.append, pending=False)
    assert sent == ["n"]
    assert "train" not in sent
    assert b.train_holding()
    assert b.mode == "manual"
    assert b.next_action == "train hold"
    assert not b._want_train


def test_go_train_silvermere_three_north_then_east() -> None:
    """Town Square: n n n, then Northern End e into the Adventurer's Guild."""
    b = Brain(allowed=True, klass="paladin", me="matt", party_leader="matt")
    b.gear_done = True
    b._asked_health = True
    b.request_train()
    state = WorldState()
    state.in_realm = True
    state.hp = 67
    state.max_hp = 67
    state.max_hp_known = True
    state.level = 4
    state.klass = "paladin"
    state.prompt_seq = 500
    state.room = "Town Square"
    state.exits = ["n", "s", "e", "w"]
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["n"]
    assert "borrow skiff" not in sent
    state.room = "Guild Street, Southern End"
    state.exits = ["n", "s", "w"]
    state.prompt_seq += 1
    b._last_step = "n"
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "n"
    state.room = "Guild Street"
    state.exits = ["n", "s", "e", "w"]
    state.prompt_seq += 1
    b._last_step = "n"
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "n"
    state.room = "Guild Street, Northern End"
    state.exits = ["e", "n", "s"]
    state.prompt_seq += 1
    b._last_step = "n"
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "e"
    state.room = "Adventurer's Guild, Foyer"
    state.exits = ["e", "w"]
    state.prompt_seq += 1
    b._last_step = "e"
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "e"
    state.room = "Adventurer's Guild, Universal Trainer"
    state.exits = ["w"]
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert "train" not in sent[1:]
    assert b.train_holding()


def test_go_train_from_graveyard_does_not_skiff() -> None:
    b = Brain(allowed=True, klass="ninja", me="klymacks", party_leader="klymacks")
    b.gear_done = True
    b._asked_health = True
    b.request_train()
    state = WorldState()
    state.in_realm = True
    state.hp = 40
    state.max_hp = 40
    state.max_hp_known = True
    state.level = 4
    state.klass = "ninja"
    state.prompt_seq = 501
    state.room = "Graveyard Entrance"
    state.exits = ["e", "w"]
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent
    assert sent[0] != "borrow skiff"
    assert "skiff" not in " ".join(sent)


def test_goto_ts_walks_then_stops() -> None:
    b = Brain(allowed=True, klass="paladin", me="matt")
    b.gear_done = True
    b.start_goto("ts")
    state = WorldState()
    state.in_realm = True
    state.hp = 40
    state.max_hp = 40
    state.max_hp_known = True
    state.prompt_seq = 40
    state.room = "Guild Street, Southern End"
    state.exits = ["n", "s", "e", "w"]
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["s"]
    assert b.mode == "goto"
    state.room = "Town Square"
    state.prompt_seq += 1
    b._last_step = ""
    sent.clear()
    b.tick(state, sent.append, pending=False)
    assert sent == []
    assert b.mode == "manual"
    assert b.next_action == "at ts"
    assert not b.goto_goal


def test_goto_gy_from_square_walks_north() -> None:
    b = Brain(allowed=True, klass="paladin", me="matt")
    b.gear_done = True
    b.start_goto("gy")
    state = WorldState()
    state.in_realm = True
    state.hp = 40
    state.max_hp = 40
    state.max_hp_known = True
    state.prompt_seq = 40
    state.room = "Town Square"
    state.exits = ["n", "s", "e", "w"]
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["n"]
    assert b.mode == "goto"
    state.room = "Graveyard"
    state.exits = ["e", "w"]
    state.prompt_seq += 1
    b._last_step = ""
    sent.clear()
    b.tick(state, sent.append, pending=False)
    assert sent == []
    assert b.mode == "manual"
    assert b.next_action == "at gy"


def test_run_gy_bashes_gate_instead_of_south() -> None:
    b = Brain(allowed=True, klass="paladin", me="matt", party_leader="matt")
    b.gear_done = True
    b.start_run("gy")
    state = WorldState()
    state.in_realm = True
    state.hp = 67
    state.max_hp = 67
    state.max_hp_known = True
    state.level = 4
    state.klass = "paladin"
    state.prompt_seq = 80
    state.room = "Intersection of River St. & Bridge St."
    state.exits = ["e", "s", "w"]
    state.closed_exits = ["n"]
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["bash north"]
    assert "s" not in sent
    state.prompt_seq += 1
    state.closed_exits = ["n"]
    sent.clear()
    b.tick(state, sent.append, pending=False)
    assert sent == ["bash north"]
    state.room = "Bridge Street"
    state.exits = ["e", "s", "w"]
    state.closed_exits = []
    state.prompt_seq += 1
    b.compass.reset()
    b._last_step = "s"
    sent.clear()
    b.tick(state, sent.append, pending=False)
    assert sent == ["bash north"]
    assert "s" not in sent
    # Open north: walk through. Closed: keep bashing until the look lists n.
    state.room = "Bridge Street"
    state.exits = ["n", "s"]
    state.closed_exits = []
    state.prompt_seq += 1
    b.compass.reset()
    b._last_step = ""
    sent.clear()
    b.tick(state, sent.append, pending=False)
    assert sent == ["n"]
    assert "s" not in sent
    assert "bash north" not in sent
    state.exits = ["s"]
    state.closed_exits = ["n"]
    state.prompt_seq += 1
    b.compass.reset()
    sent.clear()
    b.tick(state, sent.append, pending=False)
    assert sent == ["bash north"]
    state.prompt_seq += 1
    state.closed_exits = ["n"]
    sent.clear()
    b.tick(state, sent.append, pending=False)
    assert sent == ["bash north"]
    state.exits = ["n", "s"]
    state.closed_exits = []
    state.prompt_seq += 1
    b.compass.reset()
    sent.clear()
    b.tick(state, sent.append, pending=False)
    assert sent == ["n"]


def test_goto_swings_lops_then_walks() -> None:
    b = Brain(allowed=True, klass="paladin", me="matt")
    b.gear_done = True
    b.aa = True
    b.start_goto("ts")
    state = WorldState()
    state.in_realm = True
    state.hp = 40
    state.max_hp = 40
    state.max_hp_known = True
    state.prompt_seq = 40
    state.room = "Guild Street, Southern End"
    state.exits = ["n", "s", "e", "w"]
    state.scanned = True
    state.apply({"kind": "also_here", "mobs": ["large rat"]})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["aa rat"]
    assert b.mode == "goto"
    state.mobs = []
    state.in_combat = False
    state.last_kill = "rat"
    state.prompt_seq += 1
    sent.clear()
    b.tick(state, sent.append, pending=False)
    assert sent == ["s"]
    assert b.mode == "goto"


def test_coins_beat_the_next_swing() -> None:
    """Both toons must swoop copper even if a lop is still listed."""
    b = Brain(allowed=True, klass="paladin", me="matt")
    b.gear_done = True
    b.aa = True
    b.mode = "hunt"
    b._in_camp = True
    state = WorldState()
    state.in_realm = True
    state.hp = 40
    state.max_hp = 40
    state.max_hp_known = True
    state.prompt_seq = 50
    state.room = "Newhaven, Arena"
    state.exits = ["u"]
    state.scanned = True
    state.things = ["12 copper"]
    state.apply({"kind": "also_here", "mobs": ["filthbug"]})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["get copper"]
    state.prompt_seq += 1
    sent.clear()
    b.tick(state, sent.append, pending=False)
    assert sent == ["aa filthbug"]


def test_follower_swoops_coins() -> None:
    b, state = _road_hunt("ninja", "walk", party_leader="Matt", me="klymacks")
    b._followed = True
    b._joined = True
    b._ranked = True
    state.following = "Matt"
    state.things = ["7 silver nobles"]
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["get silver"]


def test_run_skips_lops() -> None:
    b = Brain(allowed=True, klass="paladin", me="matt")
    b.gear_done = True
    b.aa = True
    b.start_run("ts")
    state = WorldState()
    state.in_realm = True
    state.hp = 40
    state.max_hp = 40
    state.max_hp_known = True
    state.prompt_seq = 40
    state.room = "Guild Street, Southern End"
    state.exits = ["n", "s", "e", "w"]
    state.scanned = True
    state.apply({"kind": "also_here", "mobs": ["large rat"]})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["s"]
    assert "aa" not in " ".join(sent)
    assert b.goto_skip


def test_deathpile_run_back_without_fighting() -> None:
    b = Brain(allowed=True, klass="paladin", me="matt")
    b.gear_done = True
    b.aa = True
    state = WorldState()
    state.in_realm = True
    state.hp = 0
    state.max_hp = 40
    state.max_hp_known = True
    state.prompt_seq = 80
    state.room = "Graveyard"
    state.exits = ["e", "w"]
    state.scanned = True
    state.apply({"kind": "death"})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert b.deathpile == "Graveyard"
    assert not sent
    state.hp = 36
    state.room = "Temple Healer"
    state.exits = ["s"]
    state.prompt_seq += 1
    b.tick(state, sent.append, pending=False)
    assert b.mode == "goto"
    assert b.goto_goal == "_pile"
    assert b.goto_skip
    assert sent == ["s"]
    state.room = "Graveyard"
    state.exits = ["e", "w"]
    state.mobs = ["skeleton"]
    state.prompt_seq += 1
    sent.clear()
    b.tick(state, sent.append, pending=False)
    assert sent == ["get all"]
    assert "aa" not in " ".join(sent)


def test_go_train_from_arena() -> None:
    b = Brain(allowed=True, klass="paladin")
    b.gear_done = True
    b.mode = "hunt"
    b._in_camp = True
    b.request_train()
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.prompt_seq = 200
    state.room = "Newhaven, Arena"
    state.exits = ["u"]
    state.scanned = True
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["u"]
    assert b.next_action == "train"


def test_go_train_manual_one_shot() -> None:
    b, state = _klymacks_manual()
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.room = "Newhaven, Narrow Road"
    state.exits = ["n", "e", "w", "d"]
    state.scanned = True
    b._asked_health = True
    b._realm_maxes = True
    b.request_train()
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == ["n"]
    assert b.mode == "manual"


def test_go_train_following_does_not_move() -> None:
    b, state = _road_hunt("ninja", "always", party_leader="Matt")
    b._followed = True
    b._joined = True
    b._ranked = True
    state.following = "Matt"
    b.request_train()
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert "n" not in sent
    assert "train" not in sent
    assert b._want_train


def test_go_train_fights_first() -> None:
    b, state = _road_hunt("paladin", "walk")
    state.room = "Newhaven, Arena"
    state.exits = ["u"]
    state.mobs = ["giant rat"]
    state.scanned = True
    b._in_camp = True
    b.request_train()
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent[-1] == "aa giant rat"
    assert b._want_train


def test_request_train_at_guild_pauses_without_sending() -> None:
    b, state = _road_hunt("paladin", "walk")
    state.room = "Newhaven, Guild"
    state.exits = ["s"]
    b.request_train(state)
    assert b.train_holding()
    assert b.mode == "manual"
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert sent == []
    assert b.next_action == "train hold"


def test_train_hold_pauses_hunt_heal_look_party() -> None:
    b, state = _road_hunt("ninja", "always", party_leader="Matt")
    b._followed = True
    b._joined = True
    b._ranked = True
    state.following = "Matt"
    state.room = "Newhaven, Guild"
    state.exits = ["s"]
    state.hp = 10
    state.mobs = ["giant rat"]
    state.apply({"kind": "invited", "name": "Matt"})
    b.begin_train_hold()
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    blob = " ".join(sent)
    assert sent == []
    assert "sn" not in blob
    assert "bs" not in blob
    assert "attack" not in blob
    assert "look" not in blob
    assert "follow" not in blob
    assert "heal" not in blob
    assert b.train_holding()
    b.cancel_train()
    assert not b.train_holding()
    assert b.mode == "manual"
    assert b.next_action == "manual"


def test_train_hold_clears_after_trained_or_leave() -> None:
    b, state = _road_hunt("paladin", "walk")
    state.room = "Newhaven, Guild"
    state.exits = ["s"]
    b.begin_train_hold()
    state.apply({"kind": "trained"})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert not b.train_holding()
    b.begin_train_hold()
    state.room = "Newhaven, Narrow Road"
    state.exits = ["n", "e", "w", "d"]
    state.trained = False
    b.tick(state, sent.append, pending=False)
    assert not b.train_holding()


def test_does_not_aid_self() -> None:
    b, state = _klymacks_arena()
    state.apply({"kind": "mortal", "name": "Klymacks"})
    sent: list[str] = []
    b.tick(state, sent.append, pending=False)
    assert not any(cmd.startswith("aid ") for cmd in sent)
    assert not any(cmd.startswith("drag ") for cmd in sent)


if __name__ == "__main__":
    test_lawful_does_not_attack_players()
    test_switches_off_dead_filthbug_to_kobold()
    test_sysop_login_follows_matt_invite()
    test_sysop_hunts_past_matt()
    test_given_name_matt_is_not_pvp()
    test_lashworm_is_not_a_player()
    test_named_lunge_is_not_pvp()
    test_lawful_bails_on_stranger()
    test_arena_slime_lashworm_does_not_logoff()
    test_matt_does_not_attack_klymacks_on_the_rat()
    test_matt_peels_exit_off_attack()
    test_paladin_heals_and_saves_harm()
    test_harm_desperate_living_not_slime()
    test_matt_casts_bless_when_not_fighting()
    test_matt_skips_bless_when_already_lucky()
    test_matt_recasts_bless_after_combat_off()
    test_matt_heal_beats_bless()
    test_matt_skips_bless_at_level_1()
    test_matt_skips_bless_until_level_known()
    test_matt_keeps_bless_when_board_says_too_low()
    test_matt_keeps_bless_when_board_says_unknown()
    test_second_slime_after_kill_is_live()
    test_ooze_arrive_engages_acid_slime()
    test_klymacks_never_casts()
    test_matt_heals_klymacks_after_hit()
    test_matt_self_heals_before_klymacks()
    test_matt_skips_heal_at_27_of_28()
    test_matt_heals_at_80_percent_not_above()
    test_klymacks_asks_heal_once_when_following()
    test_matt_heals_on_heal_me()
    test_matt_still_heals_on_old_say_heal()
    test_klymacks_at_17_asks_heal_me()
    test_klymacks_gy_asks_heal_me_before_sneak()
    test_klymacks_asks_heal_me_while_sneak_armed()
    test_party_combat_off_leftover_kobold_no_sn()
    test_matt_swings_leftover_kobold_after_combat_off()
    test_matt_skips_party_heal_on_small_hit()
    test_matt_party_heals_after_chips()
    test_matt_skips_heal_when_max_unknown()
    test_klymacks_never_heals_party()
    test_matt_does_not_heal_klymacks_after_leave()
    test_matt_does_not_heal_or_attack_klymacks_corpse()
    test_harm_still_living_only_with_klymacks_here()
    test_friendly_fire_logs_off()
    test_f7_following_swings()
    test_following_at_village_gates_does_not_sneak()
    test_following_on_forest_path_sneaks()
    test_following_matt_on_guild_street_sneaks()
    test_following_busy_then_new_room_retries_sneak()
    test_follow_f7_also_here_lashworm_engages()
    test_follow_unscanned_looks_then_engages()
    test_empty_scanned_room_no_attack()
    test_sneak_try_no_fail_assumes_hidden()
    test_party_empty_sns_occupied_bs()
    test_matt_peels_attack_tack()
    test_klymacks_peels_attack_tack()
    test_no_ghost_lashworm_after_leave()
    test_falls_dead_combat_off_no_ghost_swing()
    test_rat_dies_beast_snaps_swings_beast()
    test_pending_rat_swing_dropped_after_kill()
    test_pending_rat_swing_kept_while_rat_lives()
    test_say_whiff_does_not_retry_gone_name()
    test_combat_off_arrive_lashworm_not_ghost_slime()
    test_say_whiff_slime_then_attack_lashworm()
    test_say_a_carrion_beast_then_attack_live()
    test_matt_strike_peels_size_adjectives()
    test_following_hidden_backstabs()
    test_klymacks_follows_matt()
    test_manual_auto_join_matt_invite()
    test_manual_tick_follows_him_invite()
    test_manual_tick_backranks_after_typed_follow()
    test_ninja_follow_sends_backr_without_rank_flag()
    test_paladin_follow_does_not_backr()
    test_manual_join_backrank_starts_hunt()
    test_hunt_on_backrank_stays_hunt()
    test_join_off_follow_does_not_start_hunt()
    test_auto_join_off_skips_invite()
    test_toggle_auto_join_flips_without_takeover()
    test_auto_join_skips_when_already_following()
    test_auto_join_skips_stranger_when_leader_set()
    test_auto_join_no_leader_joins_inviter()
    test_auto_join_skips_self()
    test_auto_join_skips_outside_realm()
    test_hunt_invite_no_double_join()
    test_klymacks_joins_before_swinging()
    test_matt_waits_for_join_before_swinging()
    test_matt_invites_klymacks()
    test_matt_road_after_follow_goes_down()
    test_matt_invites_glued_klymacks_before_slime()
    test_matt_invites_when_klymacks_arrives()
    test_matt_invites_when_klymacks_swings()
    test_matt_attacks_slime_when_klymacks_absent()
    test_matt_aa_default_on()
    test_matt_aa_off_holds_swing()
    test_matt_aa_off_breaks_live_bash()
    test_matt_aa_bashes_not_attack()
    test_ninja_hunts_without_aa()
    test_klymacks_no_attack_before_follow()
    test_no_join_without_invite_hunt()
    test_no_join_without_invite_manual()
    test_rest_look_do_not_join_without_invite()
    test_stale_invite_does_not_rejoin()
    test_pvp_fights_back_if_attacked()
    test_localhost_guard()
    test_nathaniel_never_sends_north()
    test_gear_north_after_betram_north()
    test_stale_village_title_honors_south_only()
    test_sell_duplicate_club()
    test_manual_sells_extra_padded()
    test_sell_extra_padded_from_i()
    test_sell_stacked_helm_until_i_is_clean()
    test_stale_screen_inv_does_not_skip_i()
    test_sell_ignored_sells_same_extra_again()
    test_already_worn_skips_wear_retry()
    test_village_extras_walk_to_armour()
    test_shop_vague_does_not_rebuy()
    test_gear_then_any_key()
    test_gear_weapon_alone_still_gets_torch()
    test_gear_buys_torch_at_store()
    test_gear_skips_buy_when_i_shows_torch()
    test_narrow_path_without_torch_walks_south()
    test_narrow_road_without_torch_walks_east()
    test_shop_vague_at_nathaniel_does_not_skip_torch()
    test_gear_assesses_i_then_walks_north()
    test_paladin_gear_village_walks_west_for_spells()
    test_paladin_gear_path_walks_north_to_spell_shop()
    test_paladin_gear_buys_minor_healing_first()
    test_paladin_reads_held_scroll_not_missing_bless()
    test_hunt_reads_held_scroll_not_missing_bless()
    test_paladin_gear_reads_scroll_already_in_i()
    test_paladin_geared_still_gets_spells()
    test_paladin_spell_vague_retries_short_name()
    test_paladin_skips_buy_when_i_lists_known_spells()
    test_paladin_still_buys_bless_scroll_at_level_1()
    test_paladin_already_knows_spell_buys_harm()
    test_paladin_does_not_rebuy_after_read()
    test_paladin_skips_shop_for_memorized_spells()
    test_paladin_cast_marks_known_no_rebuy()
    test_learned_file_skips_known_scrolls()
    test_due_spells_follow_level()
    test_spell_offer_after_train()
    test_spell_offer_yes_walks_from_guild()
    test_spell_offer_no_stays_put()
    test_class_weapons_name_the_uniques()
    test_shop_weapons_are_value_picks()
    test_ninja_buys_stiletto()
    test_paladin_buys_battle_axe()
    test_gear_offer_after_train()
    test_gear_offer_yes_walks_skiff_from_guild()
    test_gear_offer_yes_stops_at_town_square()
    test_gear_offer_yes_keeps_hunting()
    test_gear_offer_no_stays_put()
    test_gear_offer_skipped_if_holding_weapon()
    test_silvermere_square_walks_north_to_guild()
    test_sewer_run_ninja_dives_at_square()
    test_sewer_run_paladin_stocks_at_square()
    test_guild_street_keeps_walking_north()
    test_guild_southern_end_walks_north_not_into_helfgrim()
    test_helfgrim_walks_east_back_to_guild()
    test_secret_passage_walks_north_not_look()
    test_secret_passage_continues_southeast_not_camp()
    test_secret_passage_no_exit_looks_not_west()
    test_river_eastern_end_walks_west_not_locked_east()
    test_silver_eastern_end_walks_west_not_atlas_east()
    test_river_street_walks_east_toward_bridge()
    test_bridge_walks_northeast_not_north_gate()
    test_river_bridge_intersection_opens_north_gate()
    test_river_bridge_intersection_ninja_picklocks()
    test_river_eastern_end_no_exit_looks()
    test_torch_run_leaves_river_east_end_west()
    test_hunt_maps_unknown_hall_and_walks()
    test_manual_maps_unknown_room()
    test_note_send_maps_edge_into_new_room()
    test_compass_holds_second_step_until_room_confirms()
    test_graveyard_gate_walks_east()
    test_graveyard_paladin_bashes_fierce_zombie()
    test_graveyard_heals_on_heal_me_before_walk()
    test_graveyard_ping_pongs_west_after_five_east()
    test_graveyard_low_hp_flees_west()
    test_graveyard_leader_flees_when_ninja_gasps()
    test_ninja_follows_flee_when_not_following()
    test_ninja_does_not_flee_twice_while_following()
    test_ninja_rests_when_matt_sits()
    test_ninja_breaks_when_matt_stands()
    test_square_skips_light_when_torch_already_lit()
    test_square_i_without_lit_marker_keeps_light()
    test_already_lit_reply_stops_reliight()
    test_bag_ready_on_temple_street_walks_east()
    test_matt_following_at_square_leaves_to_stock()
    test_matt_leading_at_square_checks_inventory()
    test_sewer_walks_loop_when_clear()
    test_sewer_swings_before_loop()
    test_sewer_dark_uses_torch_then_buys()
    test_matt_invites_klymacks_at_square_before_torch()
    test_store_keeps_two_torches_from_sell()
    test_stale_in_shop_does_not_sell_on_street()
    test_readied_torch_counts_as_lit()
    test_silver_western_end_walks_west_to_square()
    test_temple_street_walks_east_not_west()
    test_temple_street_clears_last_step_across_tiles()
    test_casino_walks_north_to_temple_street()
    test_atlas_temple_street_east_to_square()
    test_temple_street_rewrites_west_to_east()
    test_cmd_cannot_send_west_on_temple_street()
    test_sovereign_northern_end_walks_north_to_square()
    test_skali_front_walks_south_not_look()
    test_skali_look_scan_still_walks_south()
    test_level3_road_still_drops_arena()
    test_level4_road_walks_skiff_not_pit()
    test_level4_skiff_run_is_not_yanked_to_pit()
    test_level4_leaves_arena_for_skiff()
    test_arena_gate_message_walks_skiff()
    test_silvermere_docks_walks_to_square()
    test_gear_offer_beats_spell_at_ten()
    test_ninja_gear_skips_spell_shop()
    test_manual_asks_health_once()
    test_manual_no_health_until_prompt()
    test_health_again_after_train()
    test_hunt_asks_health_once()
    test_rest_and_lop()
    test_rest_between_fights_then_break()
    test_rest_after_fight_when_room_empty()
    test_ninja_sneaks_then_backstabs()
    test_ninja_breaks_then_sneaks()
    test_ninja_sitting_hidden_backstabs()
    test_following_empty_pit_full_hp_sneaks_not_rest()
    test_ninja_empty_pit_full_hp_sneaks()
    test_ninja_empty_pit_full_hp_sitting_breaks_then_sneaks()
    test_ninja_empty_pit_wounded_rests_then_break_sneak()
    test_paladin_empty_pit_full_hp_does_not_rest()
    test_ninja_attacks_if_already_in_combat()
    test_ninja_attacks_when_sneak_fails()
    test_ninja_sound_on_enter_attacks()
    test_ninja_bs_after_sneaking_line()
    test_ninja_inout_sneaks_down_then_leaves()
    test_ninja_inout_no_sneaking_line_leaves()
    test_arena_hunt_road_goes_down_not_healer()
    test_arena_hunt_wounded_rests_in_pit()
    test_flee_rest_goes_up_not_sit_in_pit()
    test_two_arrives_stays_then_switches_after_kill()
    test_ninja_two_mobs_in_combat_stays_on_first()
    test_hunt_stays_in_fight()
    test_camp_chills_then_returns()
    test_pit_off_then_rat_attacks_not_look()
    test_kill_attacks_next_not_get_all()
    test_arena_kill_gets_coins()
    test_sense_and_engage()
    test_combat_off_on_road_empty_sneaks()
    test_combat_off_on_road_followed_rat_attacks()
    test_leave_combat_room_breaks_before_sn()
    test_empty_pit_up_breaks_then_sn()
    test_may_not_sneak_breaks_then_walks()
    test_party_may_not_sneak_paste_then_rat_attacks()
    test_party_hidden_then_rat_backstabs()
    test_pit_kill_combat_off_breaks_then_sn()
    test_ninja_combat_off_looks_then_attacks_not_bs_loop()
    test_klymacks_at_17_asks_heal_me()
    test_klymacks_gy_asks_heal_me_before_sneak()
    test_klymacks_asks_heal_me_while_sneak_armed()
    test_party_combat_off_leftover_kobold_no_sn()
    test_matt_swings_leftover_kobold_after_combat_off()
    test_sneak_wait_without_reply_retries()
    test_road_kill_breaks_before_sn()
    test_following_leader_mortal_may_sneak()
    test_combat_off_looks_then_engages()
    test_creep_in_attacks_without_look()
    test_same_type_respawn_attacks()
    test_no_second_look_when_already_scanned()
    test_creep_breaks_look_wait()
    test_empty_look_waits_then_creep()
    test_say_attack_does_not_retry()
    test_look_scan_listed_lop_engages()
    test_no_look_while_engaged()
    test_no_look_on_prompt_after_attack()
    test_arrive_lashworm_one_attack_then_still()
    test_combat_off_empty_no_attack()
    test_combat_off_echo_does_not_loop_look_attack()
    test_no_double_down_then_attack()
    test_arena_look_after_down_attacks()
    test_walk_in_slime_lashworm_no_rest_paladin()
    test_walk_in_slime_lashworm_no_rest_ninja()
    test_walk_in_slime_lashworm_visible_ninja_attacks()
    test_walk_in_look_scan_hides_lops_no_rest()
    test_just_d_stale_road_scan_no_rest()
    test_sneak_in_after_loot_attacks()
    test_get_coins_breaks_combat_must_attack_again()
    test_ninja_always_sneaks_before_move()
    test_ninja_road_sneak_retries_then_down()
    test_ninja_road_sneak_try_fail_same_tick_retries()
    test_ninja_pit_lop_attacks_not_sneak()
    test_ninja_pit_slime_lashworm_fights_when_visible()
    test_ninja_pit_slime_lashworm_bs_when_sneaking()
    test_ninja_pit_lops_engage_during_look_scan()
    test_ninja_pit_lops_fight_while_sneak_wait()
    test_ninja_pit_lops_solo_with_matt_leader()
    test_ninja_ambush_stand_road_sns_before_d()
    test_ninja_ambush_empty_road_full_hp_sns()
    test_ninja_ambush_following_empty_pit_stays()
    test_ninja_ambush_pit_lops_fights()
    test_corwyn_blocks_sneak()
    test_coorwyn_blocks_sneak()
    test_party_empty_with_matt_still_sneaks()
    test_busy_sneak_empty_room_does_not_retry()
    test_join_arena_slime_fights_not_i_or_look()
    test_join_arena_slime_fights_before_health()
    test_ninja_ambush_pit_lops_hidden_backstabs()
    test_ninja_ambush_following_does_not_move()
    test_ninja_auto_following_sneaks()
    test_ninja_road_empty_sneaks()
    test_ninja_road_health_then_sn_or_d()
    test_paladin_asks_health_for_max_ma()
    test_ninja_road_health_hits_no_prompt_still_moves()
    test_ninja_road_sneak_try_goes_down()
    test_ninja_road_sneak_try_waits_before_down()
    test_ninja_road_sneak_fail_during_settle_retries()
    test_ninja_road_matt_no_invite_still_goes_down()
    test_paladin_road_scanned_empty_goes_down()
    test_ninja_empty_pit_visible_goes_up()
    test_ninja_empty_pit_hidden_waits()
    test_ninja_always_breaks_before_move_sneak()
    test_ninja_walk_does_not_sneak_before_move()
    test_ninja_toggle_stealth_flips()
    test_on_ambush_on_empty_road_sns()
    test_on_ambush_on_empty_road_fail_retries()
    test_on_ambush_on_pit_slime_fights()
    test_on_ambush_on_walk_is_empty()
    test_on_ambush_on_sitting_breaks_then_sn()
    test_f7_ambush_boot_looks_before_sn_or_swing()
    test_f7_ambush_boot_arena_rat_fights()
    test_f7_ambush_boot_empty_looks_then_sn_once()
    test_f7_ambush_boot_hidden_looks_then_bs()
    test_f7_ambush_boot_visible_lop_on_road_attacks()
    test_f7_following_ambush_boot_does_not_leave()
    test_ambush_key_aliases_stealth()
    test_paladin_never_sneaks()
    test_map_pathfind_when_lost()
    test_unknown_room_does_not_crash()
    test_following_skips_map_walk()
    test_ally_mortal_break_then_leave_drag()
    test_ally_mortal_arena_leave_drag_aid()
    test_panic_matt_mortal_starts_rescue()
    test_after_aid_homie_returns_to_pit()
    test_after_aid_road_monster_fights()
    test_after_aid_solos_until_invite()
    test_wounded_rests_on_road_no_healer()
    test_ready_invite_join_only_after_invite()
    test_matt_ready_goes_down_not_healer()
    test_does_not_aid_self()
    test_go_train_walks_to_guild()
    test_go_train_silvermere_three_north_then_east()
    test_go_train_from_graveyard_does_not_skiff()
    test_goto_ts_walks_then_stops()
    test_goto_gy_from_square_walks_north()
    test_run_gy_bashes_gate_instead_of_south()
    test_goto_swings_lops_then_walks()
    test_coins_beat_the_next_swing()
    test_follower_swoops_coins()
    test_run_skips_lops()
    test_deathpile_run_back_without_fighting()
    test_go_train_from_arena()
    test_go_train_manual_one_shot()
    test_go_train_following_does_not_move()
    test_go_train_fights_first()
    test_request_train_at_guild_pauses_without_sending()
    test_train_hold_pauses_hunt_heal_look_party()
    test_train_hold_clears_after_trained_or_leave()
    print("ok")
