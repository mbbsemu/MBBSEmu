from __future__ import annotations

import importlib.util
import os
import tempfile
import time
from pathlib import Path

from client.brain import Brain
from client.parse import keep_party_lf, parse_line
from client.state import WorldState

_ROOT = Path(__file__).resolve().parent.parent
_SPEC = importlib.util.spec_from_file_location("bbs_client", _ROOT / "scripts" / "bbs_client.py")
assert _SPEC and _SPEC.loader
_CLIENT = importlib.util.module_from_spec(_SPEC)
_SPEC.loader.exec_module(_CLIENT)


def _read(seq: bytes) -> bytes | None:
    pending = bytearray(seq)
    key = _CLIENT.read_key(-1, pending)
    assert not pending, seq
    return key


def test_f2_sequences() -> None:
    for seq in (b"\x1bOQ", b"\x1b[12~", b"\x1b[OQ", b"\x1b[[B", b"\x8fQ", b"\x1b[12;2~"):
        pending = bytearray(seq)
        key = _CLIENT.read_key(-1, pending)
        assert key == _CLIENT.KEY_F2, seq
        assert not pending


def test_f1_sequences() -> None:
    for seq in (b"\x1bOP", b"\x1b[11~", b"\x1b[[A", b"\x8fP"):
        pending = bytearray(seq)
        key = _CLIENT.read_key(-1, pending)
        assert key == _CLIENT.KEY_F1, seq


def test_f3_to_f8_sequences() -> None:
    cases = (
        (b"\x1bOR", _CLIENT.KEY_F3),
        (b"\x1b[13~", _CLIENT.KEY_F3),
        (b"\x1b[OR", _CLIENT.KEY_F3),
        (b"\x1b[[C", _CLIENT.KEY_F3),
        (b"\x8fR", _CLIENT.KEY_F3),
        (b"\x1b[13;2~", _CLIENT.KEY_F3),
        (b"\x1bOS", _CLIENT.KEY_F4),
        (b"\x1b[14~", _CLIENT.KEY_F4),
        (b"\x1b[[D", _CLIENT.KEY_F4),
        (b"\x8fS", _CLIENT.KEY_F4),
        (b"\x1b[15~", _CLIENT.KEY_F5),
        (b"\x1b[[E", _CLIENT.KEY_F5),
        (b"\x8fT", _CLIENT.KEY_F5),
        (b"\x1b[17~", _CLIENT.KEY_F6),
        (b"\x1b[17;2~", _CLIENT.KEY_F6),
        (b"\x8fU", _CLIENT.KEY_F6),
        (b"\x1b[18~", _CLIENT.KEY_F7),
        (b"\x8fV", _CLIENT.KEY_F7),
        (b"\x1b[19~", _CLIENT.KEY_F8),
        (b"\x1b[19;2~", _CLIENT.KEY_F8),
        (b"\x1bOW", _CLIENT.KEY_F8),
        (b"\x8fW", _CLIENT.KEY_F8),
        (b"\x1b[20~", _CLIENT.KEY_F9),
        (b"\x1b[20;2~", _CLIENT.KEY_F9),
        (b"\x1bOX", _CLIENT.KEY_F9),
        (b"\x8fX", _CLIENT.KEY_F9),
        (b"\x1b[21~", _CLIENT.KEY_F10),
        (b"\x1b[21;2~", _CLIENT.KEY_F10),
        (b"\x1bOY", _CLIENT.KEY_F10),
        (b"\x8fY", _CLIENT.KEY_F10),
        (b"\x1b[23~", _CLIENT.KEY_F11),
        (b"\x1b[23;2~", _CLIENT.KEY_F11),
        (b"\x1b[24~", _CLIENT.KEY_F12),
        (b"\x1b[24;2~", _CLIENT.KEY_F12),
    )
    for seq, want in cases:
        assert _read(seq) == want, seq


def test_esc_o_waits() -> None:
    pending = bytearray(b"\x1bO")
    assert _CLIENT.read_key(-1, pending) is None
    assert pending == bytearray(b"\x1bO")
    pending.extend(b"Q")
    assert _CLIENT.read_key(-1, pending) == _CLIENT.KEY_F2


def test_peek_commands() -> None:
    assert _CLIENT.PEEK_COMMANDS[_CLIENT.KEY_F2] == "look"
    assert _CLIENT.PEEK_COMMANDS[_CLIENT.KEY_F3] == "health"
    assert _CLIENT.PEEK_COMMANDS[_CLIENT.KEY_F4] == "i"
    assert _CLIENT.PEEK_COMMANDS[_CLIENT.KEY_F5] == "exp"
    assert _CLIENT.PEEK_COMMANDS[_CLIENT.KEY_F6] == "party"
    assert _CLIENT.KEY_F7 not in _CLIENT.PEEK_COMMANDS
    assert _CLIENT.KEY_F8 not in _CLIENT.PEEK_COMMANDS
    assert _CLIENT.KEY_F9 not in _CLIENT.PEEK_COMMANDS
    assert _CLIENT.KEY_F10 not in _CLIENT.PEEK_COMMANDS
    assert _CLIENT.KEY_F11 not in _CLIENT.PEEK_COMMANDS
    assert _CLIENT.KEY_F12 not in _CLIENT.PEEK_COMMANDS
    assert _CLIENT.KEY_F1 not in _CLIENT.PEEK_COMMANDS


def _special(
    key: bytes,
    *,
    in_realm: bool,
    hunting: bool = True,
    brain: Brain | None = None,
    state: WorldState | None = None,
):
    brain = brain or Brain(allowed=True)
    if hunting:
        brain.mode = "hunt"
        brain.next_action = "lop"
    pacer = _CLIENT.KeyPacer()
    world = state if state is not None else WorldState()
    kind = _CLIENT.handle_special_key(
        key, brain, pacer, in_realm=in_realm, state=world
    )
    return kind, brain, pacer, world


def _drain(pacer: _CLIENT.KeyPacer, now: float = 1.0) -> list[bytes]:
    out: list[bytes] = []
    t = now
    while True:
        line = pacer.take(t)
        if line is None:
            break
        out.append(line)
        t += _CLIENT.WALK_GAP
    return out


def _next_cmd(pacer: _CLIENT.KeyPacer, now: float = 1.0) -> bytes | None:
    """Skip prompt-wipe packets; return the next CR-terminated line."""
    t = now
    while True:
        item = pacer.take(t)
        t += _CLIENT.WALK_GAP
        if item is None:
            return None
        if item.endswith(b"\r"):
            return item


def _cmds(pacer: _CLIENT.KeyPacer, now: float = 1.0) -> list[bytes]:
    return [ln for ln in _drain(pacer, now) if ln.endswith(b"\r")]


def _arena(*, following: str = "") -> WorldState:
    state = WorldState()
    state.in_realm = True
    state.hp = 20
    state.max_hp = 28
    state.max_hp_known = True
    state.room = "Newhaven, Arena"
    state.exits = ["u"]
    state.scanned = True
    state.in_combat = True
    state.following = following
    return state


def test_peek_does_not_stop_hunter() -> None:
    kind, brain, pacer, _ = _special(_CLIENT.KEY_F2, in_realm=True)
    assert kind == "peek"
    assert brain.mode == "hunt"
    assert brain.next_action == "lop"
    assert pacer.pending()
    line = _next_cmd(pacer)
    assert line is not None
    assert line.endswith(b"look\r")


def test_peek_all_keys_queue() -> None:
    for key, cmd in _CLIENT.PEEK_COMMANDS.items():
        kind, brain, pacer, _ = _special(key, in_realm=True)
        assert kind == "peek"
        assert brain.mode == "hunt"
        line = _next_cmd(pacer)
        assert line is not None
        assert line.endswith(f"{cmd}\r".encode("ascii"))


def test_peek_skipped_outside_realm() -> None:
    kind, brain, pacer, _ = _special(_CLIENT.KEY_F3, in_realm=False)
    assert kind == "peek"
    assert brain.mode == "hunt"
    assert not pacer.pending()


def test_peek_queues_behind_pending() -> None:
    kind, brain, pacer = _special(_CLIENT.KEY_F2, in_realm=True)[:3]
    assert kind == "peek"
    pacer_first = _CLIENT.KeyPacer()
    pacer_first.push_text("attack rat")
    again = _CLIENT.handle_special_key(
        _CLIENT.KEY_F5, brain, pacer_first, in_realm=True, state=WorldState()
    )
    assert again == "peek"
    assert brain.mode == "hunt"
    first = _next_cmd(pacer_first, 1.0)
    second = _next_cmd(pacer_first, 3.0)
    assert first is not None and first.endswith(b"att rat\r")
    assert second is not None and second.endswith(b"exp\r")


def test_realm_line_keeps_attack() -> None:
    """`att filthbug` engages. `k` / `a` / `at` are speech or collide."""
    assert _CLIENT.realm_line("attack acid slime") == "att acid slime"
    assert _CLIENT.realm_line("att acid slime") == "att acid slime"
    assert _CLIENT.realm_line("att tt giant rat") == "att giant rat"
    assert _CLIENT.realm_line("k kobold thief") == "att kobold thief"
    assert _CLIENT.realm_line("kill filthbug") == "att filthbug"
    assert _CLIENT.realm_line("attack") == "att"
    assert _CLIENT.realm_line("look") == "look"
    assert _CLIENT.realm_line("bs giant rat") == "bs giant rat"
    assert _CLIENT.realm_line("bash kobold thief") == "aa kobold thief"
    assert _CLIENT.realm_line("aa kobold thief") == "aa kobold thief"
    assert _CLIENT.realm_line("bash north") == "bash north"
    assert _CLIENT.realm_line("bash north", paladin=True) == "bash north"
    assert _CLIENT.realm_line("picklock north", paladin=True) == "picklock north"
    assert _CLIENT.realm_line("attack giant rat", paladin=True) == "aa giant rat"
    assert _CLIENT.realm_line("att giant rat", paladin=True) == "aa giant rat"
    assert _CLIENT.realm_line("bash giant rat", paladin=True) == "aa giant rat"
    assert _CLIENT.realm_line("aa off", paladin=True) == "aa off"
    assert _CLIENT.realm_line("aa on", paladin=True) == "aa on"
    # Live WG: split [HP=]+SGR left `37m/MA=16]:` on the combat name.
    assert _CLIENT.realm_line("aa 37m/MA=16]:The zombie", paladin=True) == "aa zombie"
    assert _CLIENT.realm_line("aa 37m/MA=16]:The zombie") == "aa zombie"
    assert _CLIENT.realm_line("att /MA=16]:The zombie") == "att zombie"
    pal = _CLIENT.KeyPacer(paladin=True)
    pal.push_text("attack giant rat")
    assert _next_cmd(pal, 1.0) == b"aa giant rat\r"
    pal.push_text("aa 37m/MA=16]:The zombie")
    assert _next_cmd(pal, 3.0) == b"aa zombie\r"
    pacer = _CLIENT.KeyPacer()
    pacer.push_text("attack acid slime")
    assert _next_cmd(pacer, 1.0) == b"att acid slime\r"
    pacer.push_text("M", wipe=False)
    assert pacer.take(3.0) == b"M\r"
    mystic = _CLIENT.KeyPacer(paladin=_CLIENT.uses_bash_aa("mystic", True))
    mystic.push_text("att giant rat")
    assert _next_cmd(mystic, 1.0) == b"att giant rat\r"


def test_f7_toggles_hunt() -> None:
    kind, brain, pacer, _ = _special(_CLIENT.KEY_F7, in_realm=True, hunting=False)
    assert kind == "hunt"
    assert brain.mode in ("gear", "hunt")
    if brain.mode == "gear":
        first = _next_cmd(pacer, 1.0)
        assert first is not None and first.endswith(b"i\r")
    else:
        assert not pacer.pending()


def test_f2_is_look_not_hunt() -> None:
    kind, brain, pacer, _ = _special(_CLIENT.KEY_F2, in_realm=True, hunting=False)
    assert kind == "peek"
    assert brain.mode == "manual"
    assert _next_cmd(pacer).endswith(b"look\r")


def test_f1_panic_party_break_only() -> None:
    brain = Brain(allowed=True, me="klymacks", party_leader="Matt")
    brain.mode = "hunt"
    brain.gear_done = True
    brain._followed = True
    brain._attacking = "filthbug"
    brain._last_cast = "heal:minor healing"
    brain._cast_at = 99.0
    state = _arena(following="Matt")
    kind, brain, pacer, state = _special(
        _CLIENT.KEY_F1, in_realm=True, brain=brain, state=state
    )
    assert kind == "panic"
    assert brain.mode == "hunt"
    assert brain._attacking == ""
    assert brain._last_cast == ""
    assert brain._cast_at == 0.0
    assert not state.in_combat
    lines = _cmds(pacer)
    assert [ln.endswith(b"break\r") for ln in lines] == [True]
    blob = b" ".join(lines)
    assert b"\r u\r" not in blob and not any(ln.endswith(b"u\r") for ln in lines)
    assert b"follow" not in blob


def test_f1_panic_leader_with_followers_stays() -> None:
    brain = Brain(allowed=True, me="Matt", party_leader="Matt", klass="paladin")
    brain.mode = "hunt"
    brain.gear_done = True
    brain._in_camp = True
    brain._attacking = "acid slime"
    state = _arena()
    state.followers = ["klymacks"]
    kind, brain, pacer, _ = _special(
        _CLIENT.KEY_F1, in_realm=True, brain=brain, state=state
    )
    assert kind == "panic"
    assert brain.mode == "hunt"
    lines = _cmds(pacer)
    assert len(lines) == 1 and lines[0].endswith(b"break\r")


def test_f1_panic_solo_pit_break_then_u() -> None:
    brain = Brain(allowed=True)
    brain.mode = "hunt"
    brain.gear_done = True
    brain._in_camp = True
    brain._attacking = "filthbug"
    state = _arena()
    kind, brain, pacer, state = _special(
        _CLIENT.KEY_F1, in_realm=True, brain=brain, state=state
    )
    assert kind == "panic"
    assert brain.mode == "hunt"
    assert not state.in_combat
    lines = _cmds(pacer)
    assert len(lines) == 2
    assert lines[0].endswith(b"break\r")
    assert lines[1].endswith(b"u\r")


def test_f1_panic_does_not_takeover_or_reaggro() -> None:
    brain = Brain(allowed=True)
    brain.mode = "hunt"
    brain.gear_done = True
    brain._in_camp = True
    brain._attacking = "filthbug"
    brain._asked_health = True
    state = _arena()
    state.hp = 28
    state.max_hp = 28
    state.mobs = ["filthbug"]
    state.prompt_seq = 10
    kind, brain, pacer, state = _special(
        _CLIENT.KEY_F1, in_realm=True, brain=brain, state=state
    )
    assert kind == "panic"
    assert brain.mode == "hunt"
    _drain(pacer)
    state.prompt_seq += 1
    sent: list[str] = []
    brain.tick(state, sent.append, pending=False)
    assert brain.mode == "hunt"
    assert not any(item.startswith("attack") or item.startswith("bs ") for item in sent)


def test_esc_stops_hunt_and_clears_follow() -> None:
    brain = Brain(allowed=True, me="rita Rita", party_leader="Curtis")
    brain.mode = "hunt"
    brain.next_action = "follow Curtis"
    brain._follow_sent_to = "Curtis"
    brain._resume_follow = "Curtis"
    pacer = _CLIENT.KeyPacer()
    pacer.push_text("look")
    hint, typed = _CLIENT.handle_escape_key(brain, pacer, typed="follow x")
    assert hint == "stopped"
    assert typed == ""
    assert brain.mode == "manual"
    assert brain.next_action == "manual"
    assert brain._follow_sent_to == ""
    assert brain._resume_follow == ""
    assert pacer.take(time.monotonic()) is None


def test_f1_leader_hurt_does_not_pit_flee() -> None:
    brain = Brain(allowed=True, me="Matt", party_leader="Matt", klass="paladin")
    brain.mode = "hunt"
    brain.gear_done = True
    brain._in_camp = True
    brain._asked_health = True
    state = _arena()
    state.followers = ["klymacks"]
    state.hp = 10
    state.mobs = []
    state.in_combat = False
    state.prompt_seq = 20
    pacer = _CLIENT.KeyPacer()
    _CLIENT.handle_special_key(
        _CLIENT.KEY_F1, brain, pacer, in_realm=True, state=state
    )
    _drain(pacer)
    state.prompt_seq += 1
    sent: list[str] = []
    brain.tick(state, sent.append, pending=False)
    assert "u" not in sent
    assert "follow off" not in sent


def test_f1_panic_matt_mortal_rescues() -> None:
    brain = Brain(
        allowed=True, me="klymacks", party_leader="Matt", klass="ninja"
    )
    brain.mode = "hunt"
    brain.gear_done = True
    brain._followed = True
    brain._ranked = True
    brain._in_camp = True
    state = _arena(following="Matt")
    state.apply({"kind": "mortal", "name": "Matt"})
    kind, brain, pacer, state = _special(
        _CLIENT.KEY_F1, in_realm=True, brain=brain, state=state
    )
    assert kind == "panic"
    assert brain.mode == "hunt"
    assert brain._rescue == "out"
    assert brain._panic_until == 0.0
    lines = _cmds(pacer)
    assert lines and lines[0].endswith(b"break\r")
    assert not any(ln.endswith(b"u\r") for ln in lines)
    state.in_combat = False
    state.prompt_seq += 1
    sent: list[str] = []
    brain.tick(state, sent.append, pending=False)
    assert sent[-1] == "leave"
    assert "u" not in sent


def test_f1_party_tick_does_not_flee() -> None:
    brain = Brain(
        allowed=True, me="klymacks", party_leader="Matt", klass="ninja"
    )
    brain.mode = "hunt"
    brain.gear_done = True
    brain._followed = True
    brain._ranked = True
    brain._in_camp = True
    brain._asked_health = True
    state = _arena(following="Matt")
    state.hp = 10
    state.mobs = ["filthbug"]
    state.prompt_seq = 20
    _CLIENT.handle_special_key(
        _CLIENT.KEY_F1, brain, _CLIENT.KeyPacer(), in_realm=True, state=state
    )
    state.prompt_seq += 1
    sent: list[str] = []
    brain.tick(state, sent.append, pending=False)
    assert "u" not in sent
    assert "follow off" not in sent
    assert brain.mode == "hunt"


def test_f1_skipped_outside_realm() -> None:
    kind, brain, pacer, _ = _special(_CLIENT.KEY_F1, in_realm=False)
    assert kind == "panic"
    assert brain.mode == "hunt"
    assert not pacer.pending()


def _road(*, resting: bool = False) -> WorldState:
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.room = "Newhaven, Narrow Road"
    state.exits = ["n", "e", "w", "d"]
    state.scanned = True
    state.resting = resting
    return state


def _pit_slime() -> WorldState:
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.room = "Newhaven, Arena"
    state.exits = ["u"]
    state.scanned = True
    state.mobs = ["acid slime"]
    return state


def test_f8_toggles_ninja_stealth() -> None:
    brain = Brain(allowed=True, klass="ninja", stealth="always")
    kind, brain, pacer, _ = _special(
        _CLIENT.KEY_F8, in_realm=True, hunting=True, brain=brain
    )
    assert kind == "ambush"
    assert brain.stealth == "walk"
    assert brain.stealth_label() == "walk"
    assert brain.next_action == "ambush walk"
    assert brain.mode == "hunt"
    assert not pacer.pending()
    again, brain, _, _ = _special(
        _CLIENT.KEY_F8, in_realm=True, hunting=True, brain=brain
    )
    assert again == "ambush"
    assert brain.stealth == "always"
    assert brain.stealth_label() == "ambush"
    assert brain.next_action == "ambush always"


def test_f8_walk_to_ambush_looks_on_empty_road() -> None:
    brain = Brain(allowed=True, klass="ninja", stealth="walk", me="klymacks")
    kind, brain, pacer, _ = _special(
        _CLIENT.KEY_F8,
        in_realm=True,
        hunting=True,
        brain=brain,
        state=_road(),
    )
    assert kind == "ambush"
    assert brain.stealth == "always"
    assert brain.stealth_label() == "ambush"
    assert brain.mode == "hunt"
    lines = _drain(pacer)
    assert any(ln.endswith(b"look\r") for ln in lines)
    assert not any(ln.endswith(b"sn\r") for ln in lines)
    assert not any(ln.endswith(b"u\r") for ln in lines)
    assert brain._ambush_boot
    assert brain._boot_asked
    assert not brain._sneak_wait
    assert brain.next_action == "look"


def test_f8_walk_to_ambush_pit_slime_looks() -> None:
    brain = Brain(allowed=True, klass="ninja", stealth="walk", me="klymacks")
    brain._in_camp = True
    kind, brain, pacer, _ = _special(
        _CLIENT.KEY_F8,
        in_realm=True,
        hunting=True,
        brain=brain,
        state=_pit_slime(),
    )
    assert kind == "ambush"
    assert brain.stealth == "always"
    assert brain.mode == "hunt"
    lines = _drain(pacer)
    assert any(ln.endswith(b"look\r") for ln in lines)
    assert not any(ln.endswith(b"sn\r") for ln in lines)
    assert not any(ln.endswith(b"u\r") for ln in lines)
    assert brain._ambush_boot
    assert not brain._sneak_wait


def test_f8_ambush_to_walk_no_sn() -> None:
    brain = Brain(allowed=True, klass="ninja", stealth="always", me="klymacks")
    kind, brain, pacer, _ = _special(
        _CLIENT.KEY_F8,
        in_realm=True,
        hunting=True,
        brain=brain,
        state=_road(),
    )
    assert kind == "ambush"
    assert brain.stealth == "walk"
    assert brain.stealth_label() == "walk"
    assert brain.mode == "hunt"
    assert not pacer.pending()
    assert not brain._sneak_wait
    assert not any(ln.endswith(b"sn\r") for ln in _drain(pacer))


def test_f8_walk_to_ambush_manual_looks() -> None:
    brain = Brain(allowed=True, klass="ninja", stealth="walk", me="klymacks")
    kind, brain, pacer, _ = _special(
        _CLIENT.KEY_F8,
        in_realm=True,
        hunting=False,
        brain=brain,
        state=_road(),
    )
    assert kind == "ambush"
    assert brain.mode == "manual"
    lines = _drain(pacer)
    assert any(ln.endswith(b"look\r") for ln in lines)
    assert not any(ln.endswith(b"sn\r") for ln in lines)
    assert brain._ambush_boot
    assert not brain._sneak_wait


def test_f8_walk_to_ambush_sitting_looks_first() -> None:
    brain = Brain(allowed=True, klass="ninja", stealth="walk", me="klymacks")
    brain._sitting = True
    kind, brain, pacer, _ = _special(
        _CLIENT.KEY_F8,
        in_realm=True,
        hunting=True,
        brain=brain,
        state=_road(resting=True),
    )
    assert kind == "ambush"
    assert brain.mode == "hunt"
    lines = _cmds(pacer)
    assert len(lines) == 1
    assert lines[0].endswith(b"look\r")
    assert brain._ambush_boot
    assert not brain._sneak_wait


def test_f9_toggles_auto_join() -> None:
    brain = Brain(allowed=True, me="klymacks", party_leader="Matt")
    brain.mode = "hunt"
    brain.next_action = "lop"
    kind, brain, pacer, _ = _special(
        _CLIENT.KEY_F9, in_realm=True, hunting=True, brain=brain
    )
    assert kind == "join"
    assert brain.auto_join is False
    assert brain.join_label() == "join off"
    assert brain.mode == "hunt"
    assert not pacer.pending()
    again, brain, _, _ = _special(
        _CLIENT.KEY_F9, in_realm=True, hunting=True, brain=brain
    )
    assert again == "join"
    assert brain.auto_join is True
    assert brain.join_label() == "join"
    assert brain.mode == "hunt"


def test_maybe_auto_party_manual_join() -> None:
    brain = Brain(
        allowed=True, me="klymacks", party_leader="Matt", rank="back"
    )
    brain.mode = "manual"
    brain.gear_done = True
    state = WorldState()
    state.in_realm = True
    state.apply({"kind": "invited", "name": "Matt"})
    sent: list[str] = []
    _CLIENT.maybe_auto_party(
        state, brain, sent.append, invited=True, followed=False
    )
    assert sent == ["follow Matthew"]
    state.apply({"kind": "following", "name": "Matt"})
    _CLIENT.maybe_auto_party(
        state, brain, sent.append, invited=False, followed=True
    )
    assert sent == ["follow Matthew", "backr"]
    assert brain.mode == "hunt"
    state.apply({"kind": "backrank"})
    sent.clear()
    _CLIENT.maybe_auto_party(
        state, brain, sent.append, invited=False, followed=True
    )
    assert sent == []


def test_maybe_auto_party_mystic_midr_once() -> None:
    """Robald: `maybe_auto_party` while following must not stack `midr`."""
    brain = Brain(
        allowed=True, me="robald", party_leader="Matt", klass="mystic"
    )
    brain.mode = "manual"
    state = WorldState()
    state.in_realm = True
    state.mobs = ["Matt"]
    state.apply({"kind": "following", "name": "Matt"})
    sent: list[str] = []
    _CLIENT.maybe_auto_party(
        state, brain, sent.append, invited=False, followed=True
    )
    assert sent == ["midr"]
    state.apply({"kind": "rank", "row": "mid"})
    sent.clear()
    _CLIENT.maybe_auto_party(
        state, brain, sent.append, invited=False, followed=True
    )
    assert sent == []
    state.apply({"kind": "rank", "row": "front"})
    _CLIENT.maybe_auto_party(
        state, brain, sent.append, invited=False, followed=True
    )
    assert sent == []


def test_maybe_auto_party_join_off_no_hunt() -> None:
    brain = Brain(
        allowed=True, me="klymacks", party_leader="Matt", rank="back"
    )
    brain.mode = "manual"
    brain.auto_join = False
    state = WorldState()
    state.in_realm = True
    state.apply({"kind": "following", "name": "Matt"})
    sent: list[str] = []
    _CLIENT.maybe_auto_party(
        state, brain, sent.append, invited=False, followed=True
    )
    assert sent == ["backr"]
    assert brain.mode == "manual"


def test_maybe_auto_party_no_join_without_invite() -> None:
    brain = Brain(
        allowed=True, me="klymacks", party_leader="Matt", rank="back"
    )
    brain.mode = "manual"
    assert brain.auto_join
    state = WorldState()
    state.in_realm = True
    state.mobs = ["Matt"]
    sent: list[str] = []
    _CLIENT.maybe_auto_party(
        state, brain, sent.append, invited=False, followed=False
    )
    assert sent == []
    state.apply({"kind": "sneak_try"})
    _CLIENT.maybe_auto_party(
        state, brain, sent.append, invited=False, followed=False
    )
    assert sent == []


def test_maybe_auto_party_join_call() -> None:
    brain = Brain(
        allowed=True, me="klymacks", party_leader="Matt", rank="back"
    )
    brain.mode = "manual"
    brain.gear_done = True
    state = WorldState()
    state.in_realm = True
    state.apply({"kind": "join_call", "name": "Matt"})
    sent: list[str] = []
    _CLIENT.maybe_auto_party(
        state,
        brain,
        sent.append,
        invited=False,
        followed=False,
        join_called=True,
    )
    assert sent == ["follow Matthew"]
    state.apply({"kind": "following", "name": "Matt"})
    _CLIENT.maybe_auto_party(
        state, brain, sent.append, invited=False, followed=True
    )
    assert sent == ["follow Matthew", "backr"]
    assert brain.mode == "hunt"


def test_maybe_auto_party_sherry_invite_once() -> None:
    """Live loop: payload + tick retries must not queue five follow Sherry."""
    brain = Brain(
        allowed=True,
        me="rhiannon Rhiannon",
        party_leader="Matt",
        klass="mystic",
        auto_join=True,
    )
    brain.mode = "manual"
    state = WorldState()
    state.in_realm = True
    state.mobs = ["Sherry"]
    state.saw_here = True
    state.apply(parse_line("Sherry has invited you to follow her."))
    sent: list[str] = []
    for _ in range(5):
        _CLIENT.maybe_auto_party(
            state, brain, sent.append, invited=True, followed=False
        )
        brain.tick(state, sent.append, pending=False)
    follows = [cmd for cmd in sent if cmd.lower().startswith("follow ")]
    assert follows == ["follow Sherry"]


def test_maybe_auto_party_skips_goto() -> None:
    """Goto pile/gy must walk, not re-follow Matt and stall on backr."""
    brain = Brain(
        allowed=True, me="ryan", party_leader="Matt", klass="thief", rank="back"
    )
    brain.mode = "goto"
    brain.goto_goal = "_pile"
    state = WorldState()
    state.in_realm = True
    state.following = "Matt"
    sent: list[str] = []
    _CLIENT.maybe_auto_party(
        state, brain, sent.append, invited=False, followed=True
    )
    assert sent == []
    assert brain.mode == "goto"
    assert brain.goto_goal == "_pile"
    assert not brain._followed


def test_f10_is_hold_does_not_takeover() -> None:
    kind, brain, pacer, _ = _special(
        _CLIENT.KEY_F10, in_realm=True, hunting=True
    )
    assert kind == "hold"
    assert brain.mode == "hunt"
    assert brain.next_action == "lop"
    assert not pacer.pending()
    again, brain, pacer, _ = _special(
        _CLIENT.KEY_F10, in_realm=True, hunting=True, brain=brain
    )
    assert again == "hold"
    assert brain.mode == "hunt"
    assert brain.next_action == "lop"
    assert not pacer.pending()


def test_f10_hold_outside_realm() -> None:
    kind, brain, pacer, _ = _special(_CLIENT.KEY_F10, in_realm=False)
    assert kind == "hold"
    assert brain.mode == "hunt"
    assert not pacer.pending()


def test_f11_is_sheet_does_not_takeover() -> None:
    kind, brain, pacer, _ = _special(
        _CLIENT.KEY_F11, in_realm=True, hunting=True
    )
    assert kind == "sheet"
    assert brain.mode == "hunt"
    assert not pacer.pending()


def test_f12_starts_logoff_without_dumping_keys() -> None:
    kind, brain, pacer, _ = _special(_CLIENT.KEY_F12, in_realm=True, hunting=True)
    assert kind == "logoff"
    assert brain.mode == "manual"
    assert not pacer.pending()
    kind, brain, pacer, _ = _special(
        _CLIENT.KEY_F12, in_realm=False, hunting=False
    )
    assert kind == "logoff"
    assert not pacer.pending()
    brain = Brain(allowed=True)
    brain.mode = "hunt"
    pacer = _CLIENT.KeyPacer()
    pacer.push_text("att acid slime")
    assert pacer.pending()
    kind = _CLIENT.handle_special_key(
        _CLIENT.KEY_F12, brain, pacer, in_realm=True, state=WorldState()
    )
    # F12 must drop a queued swing so x is next, not a leftover attack.
    assert kind == "logoff"
    assert not pacer.pending()


def test_logoff_walk_realm_then_mud_then_close() -> None:
    brain = Brain(allowed=True)
    brain.mode = "hunt"
    pacer = _CLIENT.KeyPacer()
    walk = _CLIENT.LogoffWalk()
    walk.start(brain, 1.0)
    assert brain.mode == "manual"
    walk.tick("[HP=20/28]:", in_realm=True, pacer=pacer, now=1.1)
    assert _cmds(pacer, now=1.1) == [b"x\r"]
    assert walk.hint == "leaving the realm..."
    assert not walk.done
    walk.tick("[HP=20/28]:", in_realm=True, pacer=pacer, now=2.0)
    assert not pacer.pending()
    walk.tick(
        "[MAJORMUD]  (C) 2002 West Coast Creations\nEnter the Realm",
        in_realm=False,
        pacer=pacer,
        now=3.0,
    )
    assert _cmds(pacer, now=3.0) == [b"x\r"]
    assert walk.hint == "leaving MajorMUD..."
    walk.tick(
        "[HP=20/28]: leftover\n[MAJORMUD]  Enter the Realm",
        in_realm=True,
        pacer=pacer,
        now=3.5,
    )
    assert not pacer.pending()
    walk.tick(
        "Make your selection: ",
        in_realm=False,
        pacer=pacer,
        now=4.0,
    )
    assert _cmds(pacer, now=4.0) == [b"x\r"]
    assert not walk.done
    walk.tick(
        "Are you sure you want to log off?",
        in_realm=False,
        pacer=pacer,
        now=5.0,
    )
    assert _cmds(pacer, now=5.0) == [b"y\r"]
    assert not walk.done
    walk.tick(
        "Thanks for calling!\nPlease hang up now.",
        in_realm=False,
        pacer=pacer,
        now=6.0,
    )
    assert walk.done
    assert walk.hint == _CLIENT.LogoffWalk.FAREWELL
    assert "Y" not in walk.hint


def test_logoff_walk_confirm_is_y_not_x() -> None:
    """TOP X → Are you sure (Y/N, or R)? must answer y — leftover HP must not re-x."""
    brain = Brain(allowed=True)
    pacer = _CLIENT.KeyPacer()
    walk = _CLIENT.LogoffWalk()
    walk.start(brain, 1.0)
    walk.tick("[HP=20/28]:", in_realm=True, pacer=pacer, now=1.0)
    assert _cmds(pacer, now=1.0) == [b"x\r"]
    walk.tick(
        "[HP=20/28]: leftover\n[MAJORMUD]:\nEnter the Realm",
        in_realm=False,
        pacer=pacer,
        now=2.0,
    )
    assert _cmds(pacer, now=2.0) == [b"x\r"]
    walk.tick(
        "[HP=20/28]: leftover\nMake your selection (X to exit):",
        in_realm=False,
        pacer=pacer,
        now=3.0,
    )
    assert _cmds(pacer, now=3.0) == [b"x\r"]
    walk.tick(
        "[HP=20/28]: leftover\nAre you sure (Y/N, or R to re-logon)?",
        in_realm=False,
        pacer=pacer,
        now=4.0,
    )
    assert _cmds(pacer, now=4.0) == [b"y\r"]
    assert walk._sent == "yes"
    assert not walk.done


def test_logoff_walk_already_on_bbs_menu_sends_x() -> None:
    brain = Brain(allowed=True)
    pacer = _CLIENT.KeyPacer()
    walk = _CLIENT.LogoffWalk()
    walk.start(brain, 1.0)
    walk.tick("Make your selection: ", in_realm=False, pacer=pacer, now=1.1)
    assert _cmds(pacer, now=1.1) == [b"x\r"]
    assert not walk.done


def test_logoff_walk_stale_bbs_menu_still_quits_in_realm() -> None:
    """Leftover BBS text must not hang up while the toon is still in-game."""
    brain = Brain(allowed=True)
    pacer = _CLIENT.KeyPacer()
    walk = _CLIENT.LogoffWalk()
    walk.start(brain, 1.0)
    walk.tick(
        "Make your selection (X to exit):\nAlso here: Corwyn.\n[HP=20/28]:",
        in_realm=True,
        pacer=pacer,
        now=1.1,
    )
    assert _cmds(pacer, now=1.1) == [b"x\r"]
    assert walk._sent == "quit"
    assert not walk.done


def test_logoff_walk_combat_breaks_then_quits() -> None:
    brain = Brain(allowed=True)
    pacer = _CLIENT.KeyPacer()
    walk = _CLIENT.LogoffWalk()
    walk.start(brain, 1.0)
    walk.tick(
        "[HP=20/28]:",
        in_realm=True,
        in_combat=True,
        pacer=pacer,
        now=1.1,
    )
    assert _cmds(pacer, now=1.1) == [b"break\r"]
    assert not walk.done
    walk.tick(
        "[HP=20/28]:",
        in_realm=True,
        in_combat=False,
        pacer=pacer,
        now=2.0,
    )
    assert _cmds(pacer, now=2.0) == [b"x\r"]


def test_logoff_walk_matt_aa_sticky_combat_still_leaves() -> None:
    """Paladin aa can leave in_combat sticky after break — still send x."""
    brain = Brain(allowed=True, klass="paladin", me="matt")
    brain.aa = True
    brain._attacking = "zombie"
    pacer = _CLIENT.KeyPacer()
    walk = _CLIENT.LogoffWalk()
    walk.start(brain, 1.0)
    assert not brain.aa
    walk.tick(
        "[HP=84/MA=16]:",
        in_realm=True,
        in_combat=True,
        pacer=pacer,
        now=1.1,
    )
    assert _cmds(pacer, now=1.1) == [b"break\r"]
    # Sticky combat: do not break-loop; leave the realm.
    walk.tick(
        "[HP=84/MA=16]:",
        in_realm=True,
        in_combat=True,
        pacer=pacer,
        now=2.0,
    )
    assert _cmds(pacer, now=2.0) == [b"x\r"]
    assert walk.hint == "leaving the realm..."


def test_startup_splash_does_not_arm_logoff_or_cleanup() -> None:
    """Connect graffiti has 1.11p — must not look like hangup / start LogoffWalk."""
    screen = _CLIENT.AnsiScreen()
    _CLIENT.paint_splash(screen, "127.0.0.1", 2323)
    blob = screen.text()
    assert "1.11p" in blob
    assert "finn's realm" in blob.lower()
    assert not _CLIENT.board_logoff_screen(blob)
    assert not _CLIENT.note_cleanup_text(blob)
    assert not _CLIENT.LogoffWalk().active
    walk = _CLIENT.LogoffWalk()
    assert not walk.active
    # Cold start: walk stays idle until F12 calls start().
    pacer = _CLIENT.KeyPacer()
    walk.tick(blob, in_realm=False, pacer=pacer, now=1.0)
    assert not pacer.pending()
    assert not walk.active
    assert walk._sent == ""
    assert not walk.done


def test_toggle_sheet_pauses_at_guild() -> None:
    state = WorldState()
    state.in_realm = True
    state.room = "Newhaven, Guild"
    brain = Brain(allowed=True, klass="ninja")
    brain.mode = "hunt"
    action, mud = _CLIENT.toggle_sheet(
        brain, state, locked=False, on_form=False
    )
    assert action == "pause"
    assert mud is None
    assert brain.train_holding()
    assert brain.mode == "manual"
    assert not brain._want_train


def test_toggle_sheet_walks_when_not_at_guild() -> None:
    state = WorldState()
    state.in_realm = True
    state.room = "Newhaven, Narrow Road"
    brain = Brain(allowed=True, klass="ninja")
    action, mud = _CLIENT.toggle_sheet(
        brain, state, locked=False, on_form=False
    )
    assert action == "walk"
    assert mud is None
    assert brain._want_train


def test_toggle_sheet_unlocks_and_cancels_walk() -> None:
    state = WorldState()
    state.in_realm = True
    state.room = "Newhaven, Narrow Road"
    brain = Brain(allowed=True, klass="ninja")
    brain.request_train()
    action, mud = _CLIENT.toggle_sheet(
        brain, state, locked=True, on_form=False
    )
    assert action == "unlock"
    assert mud is None
    assert not brain._want_train
    action, mud = _CLIENT.toggle_sheet(
        brain, state, locked=False, on_form=True
    )
    assert action == "unlock"


def test_toggle_sheet_cancels_walk_and_hold() -> None:
    state = WorldState()
    state.in_realm = True
    state.room = "Newhaven, Narrow Road"
    brain = Brain(allowed=True, klass="ninja")
    brain.request_train()
    action, mud = _CLIENT.toggle_sheet(
        brain, state, locked=False, on_form=False
    )
    assert action == "idle"
    assert mud is None
    assert not brain._want_train
    state.room = "Newhaven, Guild"
    brain.begin_train_hold()
    action, mud = _CLIENT.toggle_sheet(
        brain, state, locked=False, on_form=False
    )
    assert action == "idle"
    assert not brain.train_holding()
    assert brain.mode == "manual"


def test_sheet_lock_keeps_fsd_keys_with_leftover_hp() -> None:
    """Leftover [HP=] on TRAIN STATS is still the form. F11 lock owns keys if the title is gone."""
    screen = _CLIENT.AnsiScreen()
    screen.feed(b"\x1b[1;1HTRAIN STATS\r\nGiven Name\r\n")
    screen.feed(b"\x1b[25;1H[HP=28]: ")
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    assert screen.looks_like_creation()
    assert not _CLIENT.use_local_input(screen, state)
    assert not _CLIENT.use_local_input(screen, state, sheet_lock=True)
    assert _CLIENT.form_frozen(screen)
    assert not _CLIENT.realm_thaws_sheet(screen)
    asked: list[str] = []
    assert not _CLIENT.maybe_ask_exp(state, asked.append, frozen=True)
    assert asked == []
    sent: list[str] = []
    _CLIENT.maybe_auto_party(
        state,
        Brain(allowed=True),
        sent.append,
        invited=True,
        followed=False,
        frozen=True,
    )
    assert sent == []

    hidden = _CLIENT.AnsiScreen()
    hidden.feed(b"\x1b[1;1HGiven Name   klymacks\r\n")
    hidden.feed(b"\x1b[25;1H[HP=28]: ")
    assert not hidden.looks_like_creation()
    assert _CLIENT.use_local_input(hidden, state)
    assert not _CLIENT.use_local_input(hidden, state, sheet_lock=True)
    assert _CLIENT.form_frozen(hidden, True)


def test_hold_snapshot_writes_utf8_grid() -> None:
    screen = _CLIENT.AnsiScreen()
    screen.feed("Hello".encode("ascii") + bytes((0xC4,)))
    with tempfile.TemporaryDirectory() as tmp:
        path = Path(tmp) / "screen-hold.txt"
        wrote = _CLIENT.write_hold_snapshot(screen, path)
        assert wrote == path
        text = path.read_text(encoding="utf-8")
    lines = text.splitlines()
    assert len(lines) == 25
    assert lines[0].startswith("Hello─")
    assert all(len(row) == 80 for row in lines)


def test_f8_paladin_toggles_aa() -> None:
    brain = Brain(allowed=True, klass="paladin")
    assert brain.aa
    kind, brain, pacer, _ = _special(
        _CLIENT.KEY_F8, in_realm=True, hunting=True, brain=brain
    )
    assert kind == "aa"
    assert not brain.aa
    assert brain.f8_label() == "aa off"
    assert brain.next_action == "aa off"
    assert not pacer.pending()
    again = _CLIENT.handle_special_key(
        _CLIENT.KEY_F8, brain, pacer, in_realm=True, state=WorldState()
    )
    assert again == "aa"
    assert brain.aa
    assert brain.f8_label() == "aa"


def test_f8_aa_off_breaks_live_fight() -> None:
    brain = Brain(allowed=True, klass="paladin")
    brain.aa = True
    brain._attacking = "acid slime"
    state = WorldState()
    state.in_combat = True
    kind, brain, pacer, _ = _special(
        _CLIENT.KEY_F8, in_realm=True, hunting=True, brain=brain, state=state
    )
    assert kind == "aa"
    assert not brain.aa
    assert _next_cmd(pacer, 1.0) == b"break\r"


def test_letter_is_not_special() -> None:
    kind, brain, pacer, _ = _special(b"x", in_realm=True)
    assert kind is None
    assert brain.mode == "hunt"
    assert not pacer.pending()


def test_help_overlay_lists_keys() -> None:
    raw = _CLIENT.help_overlay().decode("utf-8", "replace")
    assert "F1          panic" in raw
    assert "F2          look" in raw
    assert "F3          health" in raw
    assert "F4          i" in raw
    assert "F5          exp" in raw
    assert "F6          party" in raw
    assert "F7          hunt / hunt off" in raw
    assert "F8          ambush / walk (ninja) · aa" in raw
    assert "F9          join / join off" in raw
    assert "F10         copy / held" in raw
    assert "F11         train / live" in raw
    assert "F12         logoff" in raw
    assert "last commands" in raw
    assert "train hold" in raw
    assert "brain paused" in raw
    assert "freezes status on the sheet" not in raw
    for n in range(1, 13):
        assert _CLIENT.fkey_label(n, style="help") in raw
    assert "peek - hunter stays on" in raw
    assert "same as F11" in raw
    assert "hunt list" in raw
    assert "goto ts" in raw
    assert "bank" in raw
    assert "boost" in raw
    assert "stash" in raw
    assert "invite all" in raw
    assert "!join" in raw
    assert "!rest" in raw
    assert "!heal" in raw
    assert "run ts" in raw
    assert "gy, sewer, arena" in raw
    assert "start / stop" not in raw
    assert "press to" not in raw.lower()
    drawn = []
    for part in raw.split("\x1b["):
        if "H" not in part:
            continue
        body = part.split("H", 1)[1]
        if body:
            drawn.append(body)
    assert drawn
    assert len({len(row) for row in drawn}) == 1, drawn


def test_fkey_table_feeds_tip_and_hold() -> None:
    on8 = _CLIENT.FKEYS[8]["on"]
    on9 = _CLIENT.FKEYS[9]["on"]
    hunt = _CLIENT.realm_fkey_tip(hunting=True, ambush=on8, join=on9)
    idle = _CLIENT.realm_fkey_tip(
        hunting=False, ambush=_CLIENT.FKEYS[8]["off"], join=_CLIENT.FKEYS[9]["off"]
    )
    assert _CLIENT.fkey_label(1) in hunt
    assert _CLIENT.fkey_label(7, active=True) in hunt
    assert _CLIENT.fkey_label(7, active=False) in idle
    assert _CLIENT.fkey_label(10) in hunt
    assert _CLIENT.fkey_label(10) in idle
    assert _CLIENT.fkey_label(10) == "F10 copy"
    assert _CLIENT.fkey_label(10, active=False) == "F10 held"
    frozen = _CLIENT.realm_fkey_tip(
        hunting=True, ambush=on8, join=on9, held=True
    )
    assert "F10 held" in frozen
    assert "F10 copy" not in frozen
    assert _CLIENT.fkey_label(1) in frozen
    assert "hold_tip" not in hunt
    assert "data/screen-hold.txt" not in hunt
    assert "data/screen-hold.txt" not in frozen


def test_window_title_is_distinct() -> None:
    kly = _CLIENT.window_title({"given": "Klymacks", "username": "klymacks"})
    sysop = _CLIENT.window_title({"given": "Klymacks", "username": "sysop"})
    matt = _CLIENT.window_title({"given": "Matt", "username": "matt"})
    robald = _CLIENT.window_title({"given": "Robald", "username": "robald"})
    ryan = _CLIENT.window_title({"given": "Ryan", "username": "ryan"})
    empty_given = _CLIENT.window_title({"given": "", "username": "klymacks"})
    blank = _CLIENT.window_title({"given": "", "username": ""})
    assert kly == "Finn's Realm — klymacks"
    assert sysop == "Finn's Realm — klymacks"
    assert matt == "Finn's Realm — Matt"
    assert robald == "Finn's Realm — Robald"
    assert ryan == "Finn's Realm — Ryan"
    assert empty_given == "Finn's Realm — klymacks"
    assert blank == "Finn's Realm — new"
    assert sysop != matt
    assert robald != ryan
    alex = _CLIENT.window_title({"given": "Alex", "username": "alex"})
    assert alex == "Finn's Realm — Alex"
    assert "Klymacks" not in kly
    assert "sysop" not in sysop
    osc = _CLIENT.osc_set_title(matt)
    assert osc.startswith(b"\x1b]0;")
    assert osc.endswith(b"\x07")
    assert b"Matt" in osc
    assert _CLIENT.osc_set_title("") == b""
    assert _CLIENT.osc_set_title("   ") == b""


def test_chrome_title_shows_who() -> None:
    screen = _CLIENT.AnsiScreen()
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    kly = Brain(allowed=True, me="sysop Klymacks", klass="ninja")
    kly.mode = "hunt"
    state.level = 3
    kly_bar = _plain_bar(_CLIENT.chrome(30, screen, "x", "127.0.0.1", state, kly))
    assert "FINN'S REALM" in kly_bar
    assert kly_bar.index("FINN'S REALM") < kly_bar.index("klymacks")
    assert "klymacks" in kly_bar
    assert "Ninja" in kly_bar
    assert "Lv.3" in kly_bar
    assert "Klymacks" not in kly_bar
    assert "sysop" not in kly_bar
    assert _CLIENT.footer_who(kly) == "klymacks (Ninja)"
    assert _CLIENT.footer_who(kly, 3) == "klymacks (Lv.3 Ninja)"
    matt = Brain(allowed=True, me="matt Matt", klass="paladin")
    matt.mode = "hunt"
    matt_bar = _plain_bar(_CLIENT.chrome(30, screen, "x", "127.0.0.1", state, matt))
    assert "Matt" in matt_bar
    assert "Paladin" in matt_bar
    assert "Lv.3 Paladin" in matt_bar
    assert _CLIENT.footer_who(matt) == "Matt (Paladin)"
    assert _CLIENT.footer_who(matt, 1) == "Matt (Lv.1 Paladin)"
    sheet = _CLIENT.AnsiScreen()
    sheet.feed(b"M A J O R  M U D Character Creation\r\nGiven Name   klymacks\r\n")
    sheet_bar = _plain_bar(
        _CLIENT.chrome(30, sheet, "x", "127.0.0.1", WorldState(), kly)
    )
    assert "new character" in sheet_bar
    assert "Ninja" not in sheet_bar
    assert "character sheet" in sheet_bar


def test_paint_signoff_is_klymacks() -> None:
    from datetime import datetime

    from client.signoff import signed_off_line

    when = datetime(2026, 9, 7, 20, 58)
    screen = _CLIENT.AnsiScreen()
    _CLIENT.paint_signoff(screen, when=when)
    text = screen.text()
    assert screen.rows == 25
    assert screen.cols == 80
    assert "klymacks" in text
    assert "KLYMACKS" not in text
    assert "Klymacks" not in text
    assert "sysop" in text
    assert signed_off_line(when) in text
    assert "the gate closes." not in text
    assert "still here." not in text
    assert "thanks for calling" not in text.lower()
    # TDF "yours truly" is glyphs, not literal letters — expect ink on art rows
    art = "".join("".join(c.ch for c in screen.buf[y]) for y in range(15, 21))
    assert sum(1 for ch in art if ch not in " ") >= 40
    ice = [c for row in screen.buf for c in row if c.ch in "█▄▀▓▒░■"]
    assert len(ice) >= 40
    assert {c.fg for c in ice} <= {0, 4, 6, 7}
    assert 2 not in {c.fg for row in screen.buf for c in row}


def test_signoff_clock_counts_down() -> None:
    assert _CLIENT.signoff_secs(10) == 10
    assert _CLIENT.signoff_secs(9.1) == 10
    assert _CLIENT.signoff_secs(0) == 0
    assert "10s" in _CLIENT.signoff_clock_line(10)
    assert "any key" in _CLIENT.signoff_clock_line(3)
    assert "window closes" in _CLIENT.signoff_clock_line(3)
    assert _CLIENT.signoff_clock_line(0) == "closing..."
    line = _CLIENT.signoff_clock_line(3)
    assert "·" in line
    line.encode("utf-8")  # F12 clock used to crash on ascii encode


def test_await_any_key_hold_zero_returns() -> None:
    r, w = os.pipe()
    try:
        t0 = time.monotonic()
        _CLIENT.await_any_key(r, bytearray(), 0)
        assert time.monotonic() - t0 < 0.4
    finally:
        os.close(r)
        os.close(w)


def test_await_any_key_unblocks_on_space() -> None:
    r, w = os.pipe()
    try:
        os.write(w, b" ")
        t0 = time.monotonic()
        _CLIENT.await_any_key(r, bytearray(), 5)
        assert time.monotonic() - t0 < 1.0
    finally:
        os.close(r)
        os.close(w)


def test_board_logoff_is_goodbye_not_menu() -> None:
    assert _CLIENT.board_logoff_screen(
        "Thanks for calling Finn's Realm\r\nPlease hang up your modem."
    )
    # Connect splash / graffiti must not look like hangup (was auto-logoff).
    assert not _CLIENT.board_logoff_screen(
        "1.11p local  //\nconnecting  127.0.0.1:2323"
    )
    assert not _CLIENT.board_logoff_screen("Make your selection (X to exit): ")
    assert not _CLIENT.board_logoff_screen("Are you sure you want to log off?")
    assert not _CLIENT.board_logoff_screen("Also here: Corwyn.\n[HP=20]:")
    assert _CLIENT.session_on_board("Make your selection: ", in_realm=False)
    assert _CLIENT.session_on_board("", in_realm=True)
    assert not _CLIENT.session_on_board("Username: ", in_realm=False)


def test_paint_splash_is_graffiti() -> None:
    demo = _CLIENT.AnsiScreen()
    _CLIENT.paint_splash(demo, "127.0.0.1", 2323)
    willow = _CLIENT.AnsiScreen()
    _CLIENT.paint_splash(willow, "127.0.0.1", 2324)
    demo_text = demo.text()
    willow_text = willow.text()
    for screen in (demo, willow):
        assert screen.rows == 25
        assert screen.cols == 80
        assert all(len(screen.line(y)) == 80 for y in range(screen.rows))
    ice = [c for row in demo.buf for c in row if c.ch in "█▄▀▓▒░■"]
    assert len(ice) >= 80
    assert "FINN'S REALM" in demo_text
    assert "FINN'S REALM" in willow_text
    assert "F I N N ' S   R E A L M" not in demo_text
    assert ".-----" not in demo_text
    assert "not the desktop" not in demo_text.lower()
    assert "not the desktop" not in willow_text.lower()
    assert "127.0.0.1:2323" in demo_text
    assert "127.0.0.1:2324" in willow_text
    assert "click this window" not in demo_text
    assert "click this window" not in willow_text
    assert "demo clock" not in demo_text.lower()
    assert "demo clock" not in willow_text.lower()
    assert "go on" not in demo_text.lower()
    assert "go on" not in willow_text.lower()
    assert "1.11p" in demo_text
    assert "klymacks" in demo_text
    assert "connecting" in demo.line(21)
    assert "127.0.0.1:2323" in demo.line(21)
    assert "connecting" in willow.line(21)
    assert "f i n n ' s     r e a l m" not in demo_text
    assert "KLYMACKS" not in demo_text
    assert "Klymacks" not in demo_text
    assert {c.fg for c in ice} <= {0, 4, 6, 7}
    assert {c.fg for c in ice} & {4, 6, 7}
    assert 2 not in {c.fg for row in demo.buf for c in row}
    assert any(c.bold for c in ice)


def test_login_ans_replaces_mbbs_banner() -> None:
    screen = _CLIENT.AnsiScreen()
    _CLIENT.paint_splash(screen, "127.0.0.1", 2323, kind="board")
    raw = _CLIENT.render_login_ans(screen)
    text = raw.decode("cp437")
    ice_bytes = raw.count(bytes((0xDB,))) + raw.count(bytes((0xDC,))) + raw.count(
        bytes((0xDF,))
    )
    assert ice_bytes >= 40
    assert b"FINN" in raw and b"REALM" in raw
    assert "FINN'S REALM" in screen.text()
    assert "klymacks" in text
    assert "KLYMACKS" not in text
    assert "connecting" not in text
    assert "click this window" not in text
    assert "MBBSEmu" not in text
    assert "mbbsemu.com" not in text
    assert "The MajorBBS Emulator" not in text
    settings = (_ROOT / "config" / "appsettings.json").read_text()
    assert "ANSI.Login" in settings
    assert "login.ans" in settings


def test_login_ans_homes_to_prompt_dock() -> None:
    screen = _CLIENT.AnsiScreen()
    _CLIENT.paint_splash(screen, "127.0.0.1", 2323, kind="board")
    raw = _CLIENT.render_login_ans(screen)
    assert raw.endswith(b"\x1b[22;1H") or b"\x1b[22;1H" in raw[-20:]
    assert "connecting" not in screen.text()
    assert "Username:" not in screen.text()
    rule = screen.line(20)
    assert any(ch in rule for ch in "░▒▀")


def test_login_text_does_not_scroll_graffiti() -> None:
    screen = _CLIENT.AnsiScreen()
    _CLIENT.paint_splash(screen, "127.0.0.1", 2323)
    plate = screen.line(0)
    assert "FINN'S REALM" in plate
    screen.feed(b"\x1b[22;1H")
    for _ in range(12):
        screen.feed(b"waiting too long at login\r\n")
    assert screen.line(0) == plate
    assert "FINN'S REALM" in screen.line(0)
    blob = "\n".join(screen.line(y) for y in range(21, 24))
    assert "waiting too long" in blob
    assert "waiting too long" not in screen.line(24)


def test_login_notice_stays_under_splash() -> None:
    screen = _CLIENT.AnsiScreen()
    _CLIENT.paint_splash(screen, "127.0.0.1", 2323)
    plate = screen.line(0)
    _CLIENT.paint_login_notice(screen, "disconnected  ·  waited too long at login")
    assert screen.line(0) == plate
    assert "waited too long at login" in screen.line(21)
    assert "waited too long" not in screen.line(24)
    brain = Brain(allowed=True, klass="ninja")
    bar = _plain_bar(
        _CLIENT.chrome(30, screen, "signing in...", "127.0.0.1", WorldState(), brain)
    )
    assert "Type your username" not in bar


def test_chrome_lists_new_map() -> None:
    screen = _CLIENT.AnsiScreen()
    state = WorldState()
    state.in_realm = True
    state.hp = 20
    state.max_hp = 20
    brain = Brain(allowed=True, klass="ninja", stealth="always")
    brain.mode = "hunt"
    bar = _CLIENT.chrome(30, screen, "x", "127.0.0.1", state, brain)
    text = bar.decode("utf-8", "replace")
    assert "F1 panic" in text
    assert "F2 look" in text
    assert "F3 hp" in text or "F3 health" in text
    assert "F4 i" in text
    assert "F5 exp" in text
    assert "F6 party" in text
    assert "F7 hunt" in text
    assert "F7 hunt off" not in text
    assert "F8 ambush" in text
    assert "F9 join" in text
    assert "F10 copy" in text
    assert text.count("F10 copy") == 1
    assert "F10 hold" not in text
    assert "in the realm" not in text
    assert "F7/stop" not in text
    assert "HOLD" not in _plain_bar(bar)
    assert "next:" in text and "ambush" in text
    assert "F2 hunt" not in text
    brain.mode = "manual"
    brain.stealth = "walk"
    brain.auto_join = False
    bar = _CLIENT.chrome(30, screen, "x", "127.0.0.1", state, brain)
    text = bar.decode("utf-8", "replace")
    assert "F7 hunt off" in text
    assert "F8 walk" in text
    assert "F9 join off" in text
    assert "F10 copy" in text
    assert text.count("F10 copy") == 1
    assert "F1 panic" in text
    held_bar = _CLIENT.chrome(30, screen, "x", "127.0.0.1", state, brain, held=True)
    held_text = held_bar.decode("utf-8", "replace")
    held_plain = _plain_bar(held_bar)
    assert "HOLD" not in held_plain
    assert "F10 held" in held_text
    assert held_text.count("F10 held") == 1
    assert "F10 copy" not in held_text
    assert "F10 live" not in held_text
    assert "F10 resume" not in held_text
    assert "F1 panic" in held_text
    assert "F7 hunt off" in held_text
    assert "data/screen-hold.txt" not in held_text
    copied_bar = _CLIENT.chrome(
        30, screen, "x", "127.0.0.1", state, brain, held=True, hold_copied=True
    )
    copied_text = copied_bar.decode("utf-8", "replace")
    assert "Ctrl+V" not in copied_text
    assert "copied —" not in copied_text
    assert "F10 held" in copied_text
    assert copied_text.count("F10 held") == 1


def test_copy_hold_clipboard_returns_bool() -> None:
    ok = _CLIENT.copy_hold_clipboard("finn hold")
    assert ok in {True, False}


def test_validating_name_says_wait() -> None:
    screen = _CLIENT.AnsiScreen()
    screen.feed(b"Please wait - Validating your name.\r\nValidating your name, please wait.\r\n")
    assert screen.looks_like_creation()
    assert _CLIENT.status_line(screen, "127.0.0.1") == "checking name"
    tip = _CLIENT.creation_tip(screen)
    assert "Scanning" in tip
    assert "Wait" in tip
    kly = Brain(allowed=True, me="sysop klymacks", klass="ninja")
    bar = _CLIENT.chrome(30, screen, "x", "127.0.0.1", WorldState(), kly)
    raw = bar.decode("utf-8", "replace")
    assert _CLIENT.chrome_row(screen) == 23
    assert "\x1b[23;1H" in raw
    assert "\x1b[25;1H" in raw
    assert "Scanning" in _plain_bar(bar)
    assert "░" in _plain_bar(bar)


def test_sheet_chrome_lifts_wait_bars() -> None:
    """Parchment ░ throbber sits in the 80×25 band. Realm chrome stays at 26."""
    realm = _CLIENT.AnsiScreen()
    state = WorldState()
    state.in_realm = True
    state.hp = 20
    state.max_hp = 20
    brain = Brain(allowed=True, klass="ninja")
    realm_bar = _CLIENT.chrome(30, realm, "x", "127.0.0.1", state, brain)
    assert _CLIENT.chrome_row(realm) == 26
    assert b"\x1b[26;1H" in realm_bar
    assert b"\x1b[23;1H" not in realm_bar

    sheet = _CLIENT.AnsiScreen()
    sheet.feed(
        b"M A J O R  M U D Character Creation\r\n"
        b"Given Name   klymacks\r\n"
        b"\x1b[20;1H  | >> Exit: SAVE                     << |\r\n"
    )
    assert "Exit: SAVE" in sheet.line(19)
    assert _CLIENT._fsd_parchment(sheet)
    assert _CLIENT.chrome_row(sheet) == 23
    sheet_bar = _CLIENT.chrome(30, sheet, "x", "127.0.0.1", WorldState(), brain)
    raw = sheet_bar.decode("utf-8", "replace")
    assert "\x1b[23;1H" in raw
    assert "\x1b[25;1H" in raw
    assert "\x1b[20;1H" not in raw
    assert b"\x1b[28;1H\x1b[K" in sheet_bar
    assert "Exit: SAVE" in sheet.line(19)

    race = _CLIENT.AnsiScreen()
    race.feed(b"Select a race:\r\n1. Human\r\n")
    assert race.looks_like_creation()
    assert not _CLIENT._fsd_parchment(race)
    assert _CLIENT.chrome_row(race) == 26


def test_name_taken_is_creation() -> None:
    screen = _CLIENT.AnsiScreen()
    screen.feed(b"You may not use Klymacks as your name.\r\nPlease enter a new name:\r\n")
    assert screen.looks_like_creation()
    assert _CLIENT.status_line(screen, "127.0.0.1") == "name taken"
    assert "taken" in _CLIENT.creation_tip(screen)
    kly = Brain(allowed=True, me="sysop Klymacks", klass="ninja")
    bar = _plain_bar(_CLIENT.chrome(30, screen, "x", "127.0.0.1", WorldState(), kly))
    assert "new character" in bar
    assert "That given name is taken" in bar
    assert "Ninja" not in bar


def _park_sheet_row(screen: _CLIENT.AnsiScreen, needle: str) -> None:
    for y in range(screen.rows):
        if needle.lower() in screen.line(y).lower():
            screen.cy = y
            screen.form_cursor = (4, y)
            return
    raise AssertionError(f"no row contains {needle!r}")


def test_sheet_chrome_tracks_stats_then_looks() -> None:
    """Login hint must not stick under FINN'S REALM on the parchment."""
    screen = _CLIENT.AnsiScreen()
    screen.feed(
        b"M A J O R  M U D Character Creation\r\n"
        b"Given Name   Ryan\r\n"
        b"Family Name  Tuck\r\n"
        b"Race         Halfling\r\n"
        b"Class        Thief\r\n"
        b"Strength   (  20 to   60)    50\r\n"
        b"Intellect  (  30 to   90)    30\r\n"
        b"Agility    (  60 to  150)    80\r\n"
        b"Hair Length   none\r\n"
        b"Hair Colour   black\r\n"
        b"Eye Colour    black\r\n"
        b"Exit: SAVE   CP Left:  100\r\n"
    )
    brain = Brain(allowed=True, klass="ninja")
    stale = "Type your username."

    _park_sheet_row(screen, "Strength")
    assert _CLIENT.creation_phase(screen) == "stats"
    assert "stats" in _CLIENT.creation_status(screen)
    assert "Halfling" in _CLIENT.creation_status(screen)
    assert "Thief" in _CLIENT.creation_status(screen)
    assert "STR 50" in _CLIENT.creation_tip(screen)
    assert "AGI 80" in _CLIENT.creation_tip(screen)
    assert "dump" not in _CLIENT.creation_tip(screen).lower()
    assert "floor" not in _CLIENT.creation_tip(screen).lower()
    stats_bar = _plain_bar(
        _CLIENT.chrome(30, screen, stale, "127.0.0.1", WorldState(), brain)
    )
    assert "Type your username" not in stats_bar
    assert "stats" in stats_bar
    assert "Halfling" in stats_bar
    assert "STR 50" in stats_bar
    assert "AGI 80" in stats_bar

    _park_sheet_row(screen, "Hair Length")
    assert _CLIENT.creation_phase(screen) == "looks"
    looks_bar = _plain_bar(
        _CLIENT.chrome(30, screen, stale, "127.0.0.1", WorldState(), brain)
    )
    assert "Type your username" not in looks_bar
    assert "looks" in looks_bar
    assert "Space cycles" in looks_bar

    _park_sheet_row(screen, "Given Name")
    assert _CLIENT.creation_phase(screen) == "names"
    names_bar = _plain_bar(
        _CLIENT.chrome(30, screen, stale, "127.0.0.1", WorldState(), brain)
    )
    assert "names" in names_bar
    assert "Last name required" in names_bar


def test_save_field_footer_is_not_name_entry() -> None:
    """Exit: SAVE must not keep the given-name / last-name tip."""
    screen = _CLIENT.AnsiScreen()
    screen.feed(
        b"M A J O R  M U D Character Creation\r\n"
        b"Given Name   Ron\r\n"
        b"Family Name  Foulston\r\n"
        b"Race         Dwarf\r\n"
        b"Class        Cleric\r\n"
        b"Strength   (  50 to  110)    80\r\n"
        b"Hair Length   Short\r\n"
        b"Hair Colour   Dark-Brown\r\n"
        b"Eye Colour    Hazel\r\n"
        b"Exit: SAVE   CP Left:    0\r\n"
    )
    brain = Brain(allowed=True, klass="cleric")
    _park_sheet_row(screen, "Exit: SAVE")
    assert _CLIENT.creation_phase(screen) == "save"
    assert "Last name" not in _CLIENT.creation_tip(screen)
    assert "Given name, then family" not in _CLIENT.creation_tip(screen)
    bar = _plain_bar(
        _CLIENT.chrome(30, screen, "Type your username.", "127.0.0.1", WorldState(), brain)
    )
    assert "save" in bar
    assert "Last name required" not in bar
    assert "Given name, then family" not in bar
    assert "Type your username" not in bar


def test_save_wait_shows_ice_throbber_not_name_tip() -> None:
    """After Enter on SAVE, wait chrome must be visible and not talk about names."""
    screen = _CLIENT.AnsiScreen()
    screen.feed(
        b"M A J O R  M U D Character Creation\r\n"
        b"Given Name   Ron\r\n"
        b"Family Name  Foulston\r\n"
        b"Race         Dwarf\r\n"
        b"Class        Cleric\r\n"
        b"Hair Length   Short\r\n"
        b"Eye Colour    Hazel\r\n"
        b"Exit: SAVE   CP Left:    0\r\n"
    )
    _park_sheet_row(screen, "Exit: SAVE")
    _CLIENT.note_form_enter(screen, b"\r")
    assert screen.form_saving
    screen.sheet_notice = "To prevent accidental suicide or reroll"
    _CLIENT.remember_sheet_notice(screen, "Validating your name, please wait.")
    assert "validating" in screen.sheet_notice.lower()
    assert _CLIENT.creation_phase(screen) == "saving"
    tip = _CLIENT.creation_tip(screen)
    assert "Saving your character" in tip
    assert "Wait" in tip
    assert "Last name" not in tip
    assert "Given name" not in tip
    assert "Scanning" not in tip
    brain = Brain(allowed=True, klass="cleric")
    bar = _CLIENT.chrome(30, screen, "x", "127.0.0.1", WorldState(), brain)
    plain = _plain_bar(bar)
    raw = bar.decode("utf-8", "replace")
    assert _CLIENT.chrome_row(screen) == 23
    assert "\x1b[23;1H" in raw
    assert "\x1b[25;1H" in raw
    assert "▓" in plain
    assert "▒" in plain
    assert "saving" in plain
    assert "Saving your character" in plain
    assert "Last name required" not in plain
    assert "Given name, then family" not in plain
    assert "Scanning" not in plain


def test_train_stats_noop_save_skips_wait_throbber() -> None:
    """Audrey path: Enter on TRAIN STATS SAVE with no change must not lock wait.

    form_saving stays set so a later [HP=] can still close the sheet; ice chrome
    must stay on the editable tip, not 'saving — wait' forever.
    """
    screen = _CLIENT.AnsiScreen()
    filt = _CLIENT.FormHoldFilter()
    _CLIENT.paint_mud(
        screen,
        b"\x1b[1;1HTRAIN STATS\r\n"
        b"Given Name   Audrey\r\n"
        b"Family Name  Test\r\n"
        b"Race         Elf\r\n"
        b"Class        Warlock\r\n"
        b"Strength   (  30 to   70)    35\r\n"
        b"Intellect  (  50 to  120)    80\r\n"
        b"Exit: SAVE   CP Left:    0\r\n",
        hold=True,
        filt=filt,
    )
    _park_sheet_row(screen, "Exit: SAVE")
    _CLIENT.note_form_enter(screen, b"\r")
    assert screen.form_saving
    # WG may still drip validate onto the foot — filter it, do not arm wait.
    _CLIENT.paint_mud(
        screen,
        b"\x1b[24;1HPlease wait - Validating your name.\r\n",
        hold=True,
        filt=filt,
    )
    assert not _CLIENT._name_wait(screen)
    assert _CLIENT.creation_phase(screen) == "save"
    tip = _CLIENT.creation_tip(screen)
    assert "Saving your character" not in tip
    assert "Wait — do not type" not in tip
    brain = Brain(allowed=True, klass="warlock")
    plain = _plain_bar(_CLIENT.chrome(30, screen, "x", "127.0.0.1", WorldState(), brain))
    assert "saving — wait" not in plain
    assert "Saving your character" not in plain
    assert "Scanning" not in plain
    # Successful leave still works with form_saving armed.
    assert screen.form_saving


def test_stat_reject_shows_in_chrome_tip() -> None:
    """`Strength may not be higher than N` is written under ice chrome — tip it."""
    screen = _CLIENT.AnsiScreen()
    filt = _CLIENT.FormHoldFilter()
    _CLIENT.paint_mud(
        screen,
        b"\x1b[1;1HTRAIN STATS\r\n"
        b"Given Name   Audrey\r\n"
        b"Family Name  Test\r\n"
        b"Race         Elf\r\n"
        b"Class        Warlock\r\n"
        b"Strength   (  30 to   70)    35\r\n"
        b"Exit: SAVE   CP Left:    5\r\n",
        hold=True,
        filt=filt,
    )
    _CLIENT.paint_mud(
        screen,
        b"\x1b[24;1HStrength may not be higher than 70.\r\n",
        hold=True,
        filt=filt,
    )
    assert "may not be higher than 70" in screen.sheet_notice.lower()
    # Under-chrome row is blanked after hoist; tip carries the reject.
    assert "may not be higher than" not in screen.line(23).lower()
    brain = Brain(allowed=True, klass="warlock")
    plain = _plain_bar(_CLIENT.chrome(30, screen, "x", "127.0.0.1", WorldState(), brain))
    assert "Strength may not be higher than 70" in plain
    assert "accidental suicide" not in plain.lower()
    # Suicide overlay filter still drops the prevent banner.
    _CLIENT.paint_mud(
        screen,
        b"\x1b[24;1HTo prevent accidental suicide or reroll\r\n",
        hold=True,
        filt=filt,
    )
    assert "accidental suicide" not in screen.text().lower()
    assert "may not be higher than 70" in screen.sheet_notice.lower()


def test_footer_leak_does_not_stick_save_on_given_name() -> None:
    """Looks are filled; a row-24 leak must home to SAVE, not Given Name."""
    screen = _CLIENT.AnsiScreen()
    filt = _CLIENT.FormHoldFilter()
    _CLIENT.paint_mud(
        screen,
        b"M A J O R  M U D Character Creation\r\n"
        b"Given Name   Ron\r\n"
        b"Family Name  Foulston\r\n"
        b"Race         Dwarf\r\n"
        b"Class        Cleric\r\n"
        b"Hair Length   Short\r\n"
        b"Eye Colour    Hazel\r\n"
        b"Exit: SAVE   CP Left:    0\r\n",
        hold=True,
        filt=filt,
    )
    assert screen.form_snap is not None
    _park_sheet_row(screen, "Given Name")
    assert _CLIENT.creation_phase(screen) == "names"
    screen.cy = 24
    _CLIENT._sync_form_cursor(screen)
    assert _CLIENT.creation_phase(screen) == "save"
    assert "Last name" not in _CLIENT.creation_tip(screen)


def test_creation_tip_unknown_toon_spends_cp() -> None:
    screen = _CLIENT.AnsiScreen()
    screen.feed(
        b"M A J O R  M U D Character Creation\r\n"
        b"Given Name   Bob\r\n"
        b"Family Name  Test\r\n"
        b"Race         Human\r\n"
        b"Class        Warrior\r\n"
        b"Strength   (  40 to  100)    40\r\n"
    )
    _park_sheet_row(screen, "Strength")
    assert _CLIENT.creation_phase(screen) == "stats"
    tip = _CLIENT.creation_tip(screen)
    assert "+/- spends CP" in tip
    assert "dump" not in tip.lower()
    assert "floor" not in tip.lower()
    assert not _CLIENT.creation_start_line(screen)


def test_form_hold_captures_prevent_banner() -> None:
    filt = _CLIENT.FormHoldFilter()
    out = filt.filter(b"\x1b[24;1HTo prevent accidental suicide or reroll\r\n")
    assert b"To prevent" not in out
    assert "To prevent accidental" in filt.notice


def test_creation_sheet_foot_hides_suicide_under_ice_chrome() -> None:
    """Real parchment foot + suicide footer + ice chrome: no peek, throbber visible."""
    foot = "\\_________________________________\\___/"
    curl = " ⌐┴" + "─" * 31 + ".     │"
    filt = _CLIENT.FormHoldFilter()
    sheet = (
        b"\x1b[1;1HM A J O R  M U D Character Creation\r\n"
        b"Given Name   Ryan\r\n"
        b"Family Name  Tuck\r\n"
        b"Race         Halfling\r\n"
        b"Class        Thief\r\n"
        b"Point Cost Chart\r\n"
        b"\x1b[20;1H  | >> Exit: SAVE                     << | SAVE your character or EXIT\r\n"
        + f"\x1b[21;1H{curl}\r\n".encode("cp437")
        + f"\x1b[22;1H {foot}\r\n".encode("ascii")
    )
    screen = _CLIENT.AnsiScreen()
    _CLIENT.paint_mud(screen, sheet, hold=True, filt=filt)
    assert "Exit: SAVE" in screen.text()
    assert foot in screen.line(21)
    _CLIENT.paint_mud(
        screen,
        b"\x1b[24;1HTo prevent accidental suicide or reroll\r\n",
        hold=True,
        filt=filt,
    )
    _CLIENT.paint_mud(
        screen,
        b"\x1b[22;42HTo prevent accidental suicide or reroll",
        hold=True,
        filt=filt,
    )
    blob = screen.text()
    assert "Exit: SAVE" in blob
    assert foot in screen.line(21)
    assert "⌐┴" in screen.text()
    assert "accidental suicide" not in blob.lower()
    assert "reroll" not in blob.lower()
    assert "To prevent" not in blob
    brain = Brain(allowed=True, me="sysop klymacks", klass="thief")
    bar = _CLIENT.chrome(30, screen, "x", "127.0.0.1", WorldState(), brain)
    plain = _plain_bar(bar)
    raw = bar.decode("utf-8", "replace")
    assert _CLIENT.chrome_row(screen) == 23
    assert "\x1b[23;1H" in raw
    assert "\x1b[25;1H" in raw
    assert "░" in plain
    assert "accidental suicide" not in plain.lower()
    assert "reroll" not in plain.lower()
    assert "To prevent" not in plain


def test_prevent_banner_moves_under_sheet() -> None:
    """Sysop footer is dropped from the parchment. Ice chrome must not show it."""
    foot = "\\_________________________________\\___/"
    curl = " ⌐┴" + "─" * 31 + ".     │"
    sheet = (
        b"\x1b[1;1HM A J O R  M U D Character Creation\r\n"
        b"Given Name   Ryan\r\n"
        b"Family Name  Tuck\r\n"
        b"Race         Halfling\r\n"
        b"Class        Thief\r\n"
        b"Point Cost Chart\r\n"
        b"\x1b[20;1H  | >> Exit: SAVE                     << | SAVE your character or EXIT\r\n"
        + f"\x1b[21;1H{curl}\r\n".encode("cp437")
        + f"\x1b[22;1H {foot}\r\n".encode("ascii")
        + b"\x1b[24;1HTo prevent accidental suicide or reroll\r\n"
    )
    screen = _CLIENT.AnsiScreen()
    _CLIENT.paint_mud(screen, sheet, hold=True, filt=_CLIENT.FormHoldFilter())
    blob = screen.text()
    assert "Exit: SAVE" in blob
    assert foot in screen.text()
    assert "⌐┴" in screen.text()
    assert "To prevent accidental" not in blob
    assert "accidental suicide" not in blob.lower()
    assert "reroll" not in blob.lower()
    brain = Brain(allowed=True, me="sysop klymacks", klass="thief")
    bar = _CLIENT.chrome(
        30, screen, "Type your username.", "127.0.0.1", WorldState(), brain
    )
    plain = _plain_bar(bar)
    raw = bar.decode("utf-8", "replace")
    assert "Type your username" not in plain
    assert "To prevent accidental" not in plain
    assert "accidental suicide" not in plain.lower()
    assert "reroll" not in plain.lower()
    assert "░" in plain
    assert _CLIENT.chrome_row(screen) == 23
    assert "\x1b[23;1H" in raw
    assert "\x1b[25;1H" in raw

    parked = _CLIENT.AnsiScreen()
    parked.feed(sheet)
    assert "To prevent accidental" in parked.text()
    _CLIENT._erase_parchment_leak_rows(parked)
    assert "To prevent accidental" not in parked.text()
    assert foot in parked.text()


def test_autopilot_starts_bbs_signup_when_unknown() -> None:
    pacer = _CLIENT.KeyPacer()
    pacer.push_text("matt")
    pilot = _CLIENT.Autopilot({"username": "matt", "password": "matt"}, play=True)
    pilot.phase = "pass"
    pilot.tick("Invalid Credentials\nUsername: ", pacer)
    assert pilot.phase == "signup_new"
    assert not pacer.pending()
    assert "creating one" in pilot.hint()
    now = 10.0
    pilot.tick("Enter Username or enter \"NEW\"\nUsername: ", pacer)
    assert pilot.phase == "signup_user"
    assert pacer.take(now) == b"NEW\r"
    now += _CLIENT.KEY_GAP
    pilot.tick("Please enter a unique Username (Max. 29 Characters):\n", pacer)
    assert pacer.take(now) == b"matt\r"
    now += _CLIENT.KEY_GAP
    pilot.tick("Please enter a strong Password:\n", pacer)
    assert pacer.take(now) == b"matt\r"
    now += _CLIENT.KEY_GAP
    pilot.tick("Please re-enter your password to confirm:\n", pacer)
    assert pacer.take(now) == b"matt\r"
    now += _CLIENT.KEY_GAP
    pilot.tick("Please enter a valid e-Mail Address:\n", pacer)
    assert pacer.take(now) == b"matt@finns.realm\r"
    assert pilot.phase == "signup_gender"
    assert "M or F" in pilot.hint()
    screen = _CLIENT.AnsiScreen()
    screen.feed(b"Please enter your gender 'M' or 'F' (can be changed later):\r\n")
    assert _CLIENT.status_line(screen, "127.0.0.1") == "BBS signup"


def test_autopilot_stops_when_already_logged_in() -> None:
    pacer = _CLIENT.KeyPacer()
    pacer.push_text("sysop")
    pilot = _CLIENT.Autopilot({"username": "sysop", "password": "sysop"}, play=True)
    pilot.phase = "pass"
    text = "sysop is already logged in -- only 1 connection allowed per user.\nUsername: "
    pilot.tick(text, pacer)
    assert pilot.phase == "blocked"
    assert not pacer.pending()
    assert "already logged in" in pilot.hint()


def test_autopilot_does_not_type_sysop_at_bbs_menu() -> None:
    pacer = _CLIENT.KeyPacer()
    pilot = _CLIENT.Autopilot(
        {"username": "sysop", "password": "sysop"}, play=True
    )
    pilot.phase = "bbs"
    text = (
        "/SYS to access SYSOP commands\n"
        "WCCREQUIREDUSERID\n"
        "Please select one of the following:\n"
        "   M ... MajorMUD\n"
        "Make your selection (X to exit): "
    )
    now = 10.0
    pilot.tick(text, pacer)
    assert pacer.take(now) == b"M\r"
    assert not pacer.pending()


def test_autopilot_skips_wg_userid_before_board_m() -> None:
    pacer = _CLIENT.KeyPacer()
    pilot = _CLIENT.Autopilot(
        {"username": "sysop", "password": "sysop"}, play=True
    )
    pilot.phase = "bbs"
    pilot.tick("WCCREQUIREDUSERID\n", pacer)
    assert not pacer.pending()
    assert pilot.phase == "bbs"


def test_autopilot_answers_wg_userid_after_m() -> None:
    pacer = _CLIENT.KeyPacer()
    pilot = _CLIENT.Autopilot(
        {"username": "sysop", "password": "sysop"}, play=True
    )
    pilot.phase = "mud"
    pilot._board_m = True
    leftover = (
        "WCCREQUIREDUSERID\n"
        "Please select one of the following:\n"
        "   M ... MajorMUD\n"
        "Make your selection (X to exit): "
    )
    now = 10.0
    pilot.tick(leftover, pacer)
    assert pacer.take(now) == b"sysop\r"
    assert pilot.phase == "play"
    assert pilot._sent_wg_userid


def test_autopilot_worldgroup_userid_prompt() -> None:
    pacer = _CLIENT.KeyPacer()
    pilot = _CLIENT.Autopilot(
        {"username": "klymacks", "password": "Sysop Urm0m!"}, play=False
    )
    pilot.tick("Please enter your user-ID:\n", pacer)
    assert pilot.phase == "pass"
    now = 10.0
    assert pacer.take(now) == b"klymacks\r"
    now += _CLIENT.KEY_GAP
    pacer.clear()
    # drain cooldown by ticking after _until — use a large monotonic via pause skip
    pilot._until = 0.0
    pilot.tick("Please enter your password:\n", pacer)
    assert pacer.take(now) == b"Sysop Urm0m!\r"


def test_ttype_prefers_ansi_bbs() -> None:
    class Sock:
        def __init__(self) -> None:
            self.sent = bytearray()

        def sendall(self, data: bytes) -> None:
            self.sent.extend(data)

    sock = Sock()
    tn = _CLIENT.Telnet(sock)
    tn.feed(bytes((_CLIENT.IAC, _CLIENT.DO, _CLIENT.TTYPE)))
    assert b"ANSI-BBS" in sock.sent
    assert b"xterm-256color" not in sock.sent
    sock.sent.clear()
    tn.feed(bytes((_CLIENT.IAC, _CLIENT.SB, _CLIENT.TTYPE, _CLIENT.TTYPE_SEND, _CLIENT.IAC, _CLIENT.SE)))
    assert b"ANSI" in sock.sent
    sock.sent.clear()
    tn.feed(bytes((_CLIENT.IAC, _CLIENT.SB, _CLIENT.TTYPE, _CLIENT.TTYPE_SEND, _CLIENT.IAC, _CLIENT.SE)))
    assert b"xterm-256color" in sock.sent


def test_ansi_dsr_6n_replies_cursor() -> None:
    screen = _CLIENT.AnsiScreen()
    screen.cx, screen.cy = 3, 5
    screen.feed(b"\x1b[6n")
    assert screen.replies == [b"\x1b[6;4R"]


def test_autopilot_bbs_only_stays_on_menu() -> None:
    pacer = _CLIENT.KeyPacer()
    pilot = _CLIENT.Autopilot(
        {"username": "sysop", "password": "Urm0m!"},
        play=False,
        enter_mud=False,
    )
    pilot.phase = "bbs"
    pilot.tick("Make your selection: ", pacer)
    assert pilot.phase == "play"
    assert "BBS menu" in pilot.hint()
    assert not pacer.pending()
    assert "M" not in (pacer.take(time.monotonic()) or b"").decode()


def test_autopilot_queues_m_during_password_cooldown() -> None:
    """BBS menu often arrives while KEY_GAP is still cooling. Do not skip M."""
    pacer = _CLIENT.KeyPacer()
    pacer.push_text("klymacks1", wipe=False)
    now = time.monotonic()
    assert pacer.take(now) == b"klymacks1\r"
    assert pacer.pending()
    assert not pacer.queued()
    pilot = _CLIENT.Autopilot(
        {"username": "klymacks", "password": "klymacks1"}, play=True
    )
    pilot.phase = "bbs"
    pilot.tick("Make your selection: ", pacer)
    assert pilot.phase == "mud"
    assert "entering the realm" in pilot.hint()
    assert pacer.take(now + _CLIENT.KEY_GAP) == b"M\r"
    banner = "[MAJORMUD]  (C) 2002 West Coast Creations\nEnter the Realm"
    later = now + _CLIENT.KEY_GAP * 2
    pilot.tick(banner, pacer)
    assert pacer.take(later) == b"E\r"
    assert pilot.phase == "play"


def test_autopilot_enters_realm_when_auto_play_is_off() -> None:
    """NT klymacks: auto_play false still sends M then E."""
    pacer = _CLIENT.KeyPacer()
    pilot = _CLIENT.Autopilot(
        {"username": "sysop", "password": "sysop"},
        play=False,
        enter_mud=True,
    )
    pilot.phase = "bbs"
    now = 10.0
    pilot.tick("Make your selection: ", pacer)
    assert pacer.take(now) == b"M\r"
    assert pilot.phase == "mud"
    pilot._until = 0.0
    banner = "[E] . Enter the Realm\n[MAJORMUD]: "
    later = now + _CLIENT.KEY_GAP
    pilot.tick(banner, pacer)
    assert pacer.take(later) == b"E\r"
    assert pilot.phase == "play"


def test_auto_play_cli_distinct_from_no_auto() -> None:
    parser = _CLIENT.build_parser()
    bare = parser.parse_args([])
    assert bare.auto_play_cli is None
    assert not bare.no_auto
    hunt = parser.parse_args(["--auto-play"])
    assert hunt.auto_play_cli is True
    assert not hunt.no_auto
    wait = parser.parse_args(["--no-auto-play"])
    assert wait.auto_play_cli is False
    login = parser.parse_args(["--no-auto"])
    assert login.no_auto
    assert login.auto_play_cli is None
    last = parser.parse_args(["--auto-play", "--no-auto-play"])
    assert last.auto_play_cli is False


def test_auto_play_cli_overrides_json() -> None:
    player: dict[str, object] = {"auto_play": False}
    _CLIENT.apply_auto_play_override(player, True)
    assert player["auto_play"] is True
    player = {"auto_play": True}
    _CLIENT.apply_auto_play_override(player, False)
    assert player["auto_play"] is False
    player = {"auto_play": False}
    _CLIENT.apply_auto_play_override(player, None)
    assert player["auto_play"] is False


def test_autopilot_does_not_type_m_at_majormud_prompt() -> None:
    """Stale BBS 'Make your selection' must not send M at [MAJORMUD]:."""
    pacer = _CLIENT.KeyPacer()
    pilot = _CLIENT.Autopilot(
        {"username": "matt", "password": "matt1"}, play=True
    )
    pilot.phase = "mud"
    pilot._board_m = True
    text = (
        "Make your selection: \n"
        "M A J O R  M U D v1.11p-WG\n"
        "[E] . Enter the Realm\n"
        "[MAJORMUD]: "
    )
    pilot.tick(text, pacer)
    assert pacer.take(10.0) == b"E\r"
    assert not pacer.queued()
    assert pilot.phase == "play"


def test_autopilot_sends_board_m_once() -> None:
    pacer = _CLIENT.KeyPacer()
    now = time.monotonic()
    pilot = _CLIENT.Autopilot(
        {"username": "matt", "password": "matt1"}, play=True
    )
    pilot.phase = "bbs"
    pilot.tick("Make your selection: ", pacer)
    assert pacer.take(now) == b"M\r"
    assert pilot.phase == "mud"
    pilot.tick("Make your selection: ", pacer)
    assert not pacer.queued()


_DISCONNECT_PAGER = (
    "Last time you were on, you disconnected while playing.\n"
    "The gods have punished you appropriately.\n"
    "(N)onstop, (Q)uit, or (C)ontinue?"
)


def test_login_pager_nonstop_matches_disconnect_text() -> None:
    assert _CLIENT.login_pager_nonstop(_DISCONNECT_PAGER)
    hold = (_ROOT / "data" / "screen-hold.txt").read_text(encoding="utf-8")
    if "(N)onstop, (Q)uit, or (C)ontinue?" in hold:
        assert _CLIENT.login_pager_nonstop(hold)
    screen = _CLIENT.AnsiScreen()
    screen.feed(_DISCONNECT_PAGER.replace("\n", "\r\n").encode("ascii"))
    assert _CLIENT.status_line(screen, "127.0.0.1") == "hangup penalty  ·  N nonstop"
    assert not _CLIENT.login_pager_nonstop(
        "Help on LOOK\n(N)onstop, (Q)uit, or (C)ontinue?"
    )
    assert not _CLIENT.login_pager_nonstop(
        "[HP=20/28]:\n"
        "Last time you were on, you disconnected while playing.\n"
        "The gods have punished you appropriately.\n"
        "(N)onstop, (Q)uit, or (C)ontinue?"
    )


def test_autopilot_nonstop_on_disconnect_pager() -> None:
    """After E, the hangup pager gets bare N — not Q, not C, not N+CR."""
    pacer = _CLIENT.KeyPacer()
    pilot = _CLIENT.Autopilot(
        {"username": "klymacks", "password": "klymacks1"},
        play=False,
        enter_mud=True,
    )
    pilot.phase = "play"
    now = 10.0
    pilot.tick(_DISCONNECT_PAGER, pacer)
    assert pacer.take(now) == b"N"
    assert not pacer.queued()
    later = now + _CLIENT.KEY_GAP
    pacer.clear()
    pilot._until = 0.0
    pilot.tick(_DISCONNECT_PAGER, pacer)
    assert pacer.take(later) is None
    assert not _CLIENT.drop_stray_keys(pilot, on_form=False, in_realm=False)


def test_autopilot_e_then_nonstop_from_hold_screen() -> None:
    """[MAJORMUD]: E, then the captured pager, then N into the realm."""
    pacer = _CLIENT.KeyPacer()
    pilot = _CLIENT.Autopilot(
        {"username": "klymacks", "password": "klymacks1"}, play=True
    )
    pilot.phase = "mud"
    now = 10.0
    pilot.tick("[E] . Enter the Realm\n[MAJORMUD]: ", pacer)
    assert pacer.take(now) == b"E\r"
    assert pilot.phase == "play"
    hold = (_ROOT / "data" / "screen-hold.txt").read_text(encoding="utf-8")
    if not _CLIENT.login_pager_nonstop(hold):
        hold = _DISCONNECT_PAGER
    screen = _CLIENT.AnsiScreen()
    screen.feed(hold.replace("\n", "\r\n").encode("utf-8"))
    assert _CLIENT.login_pager_nonstop(screen.text())
    assert _CLIENT.status_line(screen, "127.0.0.1") == "hangup penalty  ·  N nonstop"
    pilot._until = 0.0
    # Menu E looks like a walk to KeyPacer (`e`); wait out WALK_GAP.
    pilot.tick(screen.text(), pacer)
    assert pacer.take(now + _CLIENT.WALK_GAP) == b"N"
    assert not pacer.queued()
    assert not _CLIENT.drop_stray_keys(pilot, on_form=False, in_realm=False)


def test_autopilot_leaves_typed_continue_on_pager() -> None:
    """C or Q from the keyboard stay first; do not freeze the form."""
    pacer = _CLIENT.KeyPacer()
    pacer.push(b"C")
    pilot = _CLIENT.Autopilot(
        {"username": "klymacks", "password": "klymacks1"}, play=True
    )
    pilot.phase = "play"
    pilot.tick(_DISCONNECT_PAGER, pacer)
    assert pacer.take(10.0) == b"C"
    assert not pacer.queued()
    assert not _CLIENT.drop_stray_keys(pilot, on_form=False, in_realm=False)


def test_autopilot_does_not_nonstop_in_game_more() -> None:
    pacer = _CLIENT.KeyPacer()
    pilot = _CLIENT.Autopilot(
        {"username": "klymacks", "password": "klymacks1"}, play=True
    )
    pilot.phase = "play"
    realm_more = (
        "Also here: a rat.\n"
        "[HP=20/28]:\n"
        "(N)onstop, (Q)uit, or (C)ontinue?"
    )
    pilot.tick(realm_more, pacer)
    assert not pacer.queued()
    help_more = "Help on LOOK\n(N)onstop, (Q)uit, or (C)ontinue?"
    pilot._until = 0.0
    pilot.tick(help_more, pacer)
    assert not pacer.queued()


def test_key_gap_is_slow_enough_for_majormud() -> None:
    assert 0.4 <= _CLIENT.KEY_GAP <= 0.8
    assert 0.04 <= _CLIENT.TYPE_GAP <= 0.15
    assert 1.5 <= _CLIENT.WALK_GAP <= 3.0
    assert _CLIENT.FLOOD_PAUSE >= 4.0
    assert _CLIENT.REALM_SETTLE >= 5.0
    assert _CLIENT.PLAY_TICK == 0.4
    pacer = _CLIENT.KeyPacer()
    pacer.push_text("e")
    assert pacer.take(10.0) == b"e\r"
    assert pacer._ready_at == 10.0 + _CLIENT.WALK_GAP
    pacer.push_text("att rat")
    assert pacer.take(10.0 + _CLIENT.WALK_GAP) == b"att rat\r"
    assert pacer._ready_at == 10.0 + _CLIENT.WALK_GAP + _CLIENT.KEY_GAP
    pacer.push(b"K")
    assert pacer.take(20.0) == b"K"
    assert pacer._ready_at == 20.0 + _CLIENT.TYPE_GAP


def test_realm_gate_holds_auto_play_after_first_prompt() -> None:
    gate = _CLIENT.RealmGate()
    gate.note(in_realm=False, frozen=False, now=10.0)
    assert not gate.quiet(10.0)
    gate.note(in_realm=True, frozen=False, now=10.0)
    assert gate.quiet(10.1)
    assert gate.quiet(10.0 + _CLIENT.REALM_SETTLE - 0.05)
    assert not gate.quiet(10.0 + _CLIENT.REALM_SETTLE)
    kind, brain, pacer, _ = _special(_CLIENT.KEY_F2, in_realm=True, hunting=False)
    assert kind == "peek"
    assert _next_cmd(pacer).endswith(b"look\r")
    kind, brain, pacer, _ = _special(_CLIENT.KEY_F7, in_realm=True, hunting=False)
    assert kind == "hunt"
    assert brain.hunting()


def test_realm_gate_resets_on_character_sheet() -> None:
    gate = _CLIENT.RealmGate()
    gate.note(in_realm=True, frozen=False, now=1.0)
    assert gate.quiet(1.1)
    gate.note(in_realm=True, frozen=True, now=2.0)
    assert not gate.quiet(2.0)
    gate.note(in_realm=True, frozen=False, now=20.0)
    assert gate.quiet(20.1)
    assert not gate.quiet(20.0 + _CLIENT.REALM_SETTLE)


def test_play_select_timeout_wakes_realm() -> None:
    """Hunt used to wait forever unless F7 was on. Realm [HP=] must pulse too."""
    assert _CLIENT.play_select_timeout(
        pending=False,
        hunting=False,
        in_realm=True,
        paused=False,
        settling=False,
    ) == _CLIENT.PLAY_TICK
    assert _CLIENT.play_select_timeout(
        pending=False,
        hunting=True,
        in_realm=True,
        paused=False,
        settling=False,
    ) == _CLIENT.PLAY_TICK
    assert (
        _CLIENT.play_select_timeout(
            pending=False,
            hunting=True,
            in_realm=True,
            paused=True,
            settling=False,
        )
        is None
    )
    assert _CLIENT.play_select_timeout(
        pending=False,
        hunting=False,
        in_realm=False,
        paused=False,
        settling=False,
        chrome_pulse=True,
    ) == _CLIENT.CHROME_PULSE
    assert _CLIENT.play_select_timeout(
        pending=False,
        hunting=True,
        in_realm=True,
        paused=False,
        settling=True,
        remain=3.0,
    ) == 0.5
    assert _CLIENT.play_may_tick(
        in_play=True, walk_active=False, typed="", paused=False, settling=False
    )
    assert not _CLIENT.play_may_tick(
        in_play=True, walk_active=False, typed="", paused=True, settling=False
    )


def test_realm_hp_thaws_leftover_sheet_lock() -> None:
    """After SAVE, leftover F11 lock must not pause look/buy/att."""
    screen = _CLIENT.AnsiScreen()
    screen.feed(b"\x1b[2J\x1b[1;1H")
    screen.feed(b"M A J O R  M U D Character Creation\r\n")
    screen.feed(b"Given Name   ryan\r\nExit: SAVE\r\n")
    assert _CLIENT.form_frozen(screen, True)
    assert not _CLIENT.realm_thaws_sheet(screen)
    frozen = _CLIENT.form_frozen(screen, True)
    assert _CLIENT.play_paused(Brain(allowed=True, klass="thief"), frozen=frozen)
    assert (
        _CLIENT.play_select_timeout(
            pending=False,
            hunting=True,
            in_realm=True,
            paused=True,
            settling=False,
        )
        is None
    )

    screen.feed(b"\x1b[16;1HThe Guild Hall.\r\n")
    screen.feed(b"Obvious exits: south\r\n")
    screen.feed(b"Also here: Matt.\r\n")
    screen.feed(b"\x1b[24;1H[HP=22]: ")
    state = WorldState()
    state.in_realm = True
    state.hp = 22
    assert _CLIENT._realm_on_grid(screen)
    assert _CLIENT.realm_thaws_sheet(screen)
    assert not _CLIENT.form_frozen(screen, True)
    assert not _CLIENT.play_paused(
        Brain(allowed=True, klass="thief"),
        frozen=_CLIENT.form_frozen(screen, True),
    )
    assert _CLIENT.use_local_input(screen, state, sheet_lock=True)
    paused = _CLIENT.play_paused(
        Brain(allowed=True, klass="thief"),
        frozen=_CLIENT.form_frozen(screen, True),
    )
    assert _CLIENT.play_may_tick(
        in_play=True, walk_active=False, typed="", paused=paused, settling=False
    )
    assert (
        _CLIENT.play_select_timeout(
            pending=False,
            hunting=True,
            in_realm=True,
            paused=paused,
            settling=False,
        )
        == _CLIENT.PLAY_TICK
    )

    hp_only = _CLIENT.AnsiScreen()
    hp_only.feed(b"Newhaven, Guild\r\n[HP=22]: ")
    assert _CLIENT._realm_on_grid(hp_only)
    assert not _CLIENT.form_frozen(hp_only, True)
    assert _CLIENT.realm_thaws_sheet(hp_only)

    gear = Brain(
        allowed=True,
        klass="thief",
        race="halfling",
        me="ryan",
        party_leader="Matt",
        auto_join=True,
    )
    town = WorldState()
    town.in_realm = True
    town.hp = 22
    town.max_hp = 22
    town.max_hp_known = True
    town.room = "Newhaven, Guild"
    town.exits = ["s"]
    town.prompt_seq = 10
    town.scanned = True
    gear.toggle_hunt()
    assert gear.mode == "gear"
    assert gear.open_gear_inv(town) == "i"
    town.apply(
        {
            "kind": "inventory",
            "items": [],
            "worn": [],
            "extras": [],
            "text": "You are carrying nothing.",
        }
    )
    town.prompt_seq += 1
    bought: list[str] = []
    gear.tick(town, bought.append, pending=False)
    assert bought == ["s"]
    assert gear.mode == "gear"

    hunter = Brain(allowed=True, klass="warrior", me="klymacks")
    hunter.gear_done = True
    hunter.mode = "hunt"
    hunter._asked_health = True
    hunter._in_camp = True
    fight = WorldState()
    fight.in_realm = True
    fight.hp = 28
    fight.max_hp = 28
    fight.max_hp_known = True
    fight.room = "Newhaven, Arena"
    fight.exits = ["u"]
    fight.scanned = True
    fight.saw_here = True
    fight.mobs = ["a giant rat"]
    fight.prompt_seq = 40
    swung: list[str] = []
    hunter.tick(fight, swung.append, pending=False)
    assert swung
    assert any(
        cmd.startswith(("att ", "aa ", "attack ")) or cmd == "look" for cmd in swung
    )


def test_action_pry_skips_follow_and_backrank() -> None:
    pry = _CLIENT.ActionPry()
    state = WorldState()
    state.in_realm = True
    state.prompt_seq = 10
    sent: list[str] = []
    pry.note_send("follow Matt", 10)
    state.prompt_seq = 11
    assert not pry.maybe_send(state, sent.append)
    pry.note_send("fo matt", 11)
    state.prompt_seq = 12
    assert not pry.maybe_send(state, sent.append)
    pry.note_send("backr", 12)
    state.prompt_seq = 13
    assert not pry.maybe_send(state, sent.append)
    pry.note_send("midr", 13)
    state.prompt_seq = 14
    assert not pry.maybe_send(state, sent.append)
    pry.note_send("frontr", 14)
    state.prompt_seq = 15
    assert not pry.maybe_send(state, sent.append)
    assert sent == []


def test_action_pry_skips_buy_sell_wear() -> None:
    """Brain sends `i` after buy/sell/wear — pry must not double it."""
    pry = _CLIENT.ActionPry()
    state = WorldState()
    state.in_realm = True
    state.prompt_seq = 3
    sent: list[str] = []
    pry.note_send("buy club", 3)
    state.prompt_seq = 4
    assert not pry.maybe_send(state, sent.append)
    pry.note_send("sell padded helm", 4)
    state.prompt_seq = 5
    assert not pry.maybe_send(state, sent.append)
    pry.note_send("wear padded vest", 5)
    state.prompt_seq = 6
    assert not pry.maybe_send(state, sent.append)
    assert sent == []


def test_action_pry_skips_settle_and_look() -> None:
    pry = _CLIENT.ActionPry()
    state = WorldState()
    state.in_realm = True
    state.prompt_seq = 1
    sent: list[str] = []
    pry.note_send("look", 1)
    state.prompt_seq = 2
    assert not pry.maybe_send(state, sent.append)
    pry.note_send("l", 2)
    state.prompt_seq = 3
    assert not pry.maybe_send(state, sent.append)
    pry.note_send("list", 3)
    state.prompt_seq = 4
    assert not pry.maybe_send(state, sent.append)
    pry.note_send("list armour", 4)
    state.prompt_seq = 5
    assert not pry.maybe_send(state, sent.append)
    pry.note_send("s", 5, gearing=True)
    state.prompt_seq = 6
    assert not pry.maybe_send(state, sent.append, settling=True)
    assert not pry.maybe_send(state, sent.append, frozen=True)
    assert not pry.maybe_send(state, sent.append)
    assert sent == []


def test_action_pry_shop_vague_does_not_eat_buy() -> None:
    """Helgrim: scimitar vs serrated scimitar — the next buy must reach the shop."""
    pry = _CLIENT.ActionPry()
    pry.note_send("buy scimitar", 1)
    pry.note_shop_vague()
    assert pry.stuck
    assert not pry.blocks("buy scimitar")
    assert not pry.blocks("buy serrated scimitar")
    assert not pry.blocks("look")
    state = WorldState()
    state.in_realm = True
    state.prompt_seq = 2
    sent: list[str] = []
    assert not pry.maybe_send(state, sent.append)

    def push(text: str) -> None:
        if pry.blocks(text):
            return
        pry.note_send(text, 2)
        sent.append(text)

    push("buy scimitar")
    assert sent == ["buy scimitar"]
    assert not pry.stuck
    state.prompt_seq = 3
    assert not pry.maybe_send(state, sent.append)
    assert sent == ["buy scimitar"]

    pry.note_send("buy scimitar", 4)
    pry.note_shop_vague()
    push("buy serrated scimitar")
    assert sent == ["buy scimitar", "buy serrated scimitar"]
    assert not pry.stuck


def test_action_pry_read_vague_asks_for_full_name() -> None:
    pry = _CLIENT.ActionPry()
    pry.note_send("read scroll of bless", 1)
    pry.note_shop_vague()
    assert pry.stuck
    state = WorldState()
    state.in_realm = True
    state.prompt_seq = 2
    sent: list[str] = []
    assert not pry.maybe_send(state, sent.append)
    assert sent == []


def test_action_pry_never_inv_after_a_walk() -> None:
    """A step is followed by `look`, never an inventory pry."""
    pry = _CLIENT.ActionPry()
    state = WorldState()
    state.in_realm = True
    state.prompt_seq = 8
    sent: list[str] = []
    pry.note_send("s", 8, gearing=True)
    state.prompt_seq = 9
    assert not pry.maybe_send(state, sent.append)
    pry.note_send("n", 9, gearing=False)
    state.prompt_seq = 10
    assert not pry.maybe_send(state, sent.append)
    pry.note_send("go manhole", 10)
    state.prompt_seq = 11
    assert not pry.maybe_send(state, sent.append)
    assert sent == []


def test_drop_stray_keys_during_login_not_on_sheet() -> None:
    pilot = _CLIENT.Autopilot({"username": "klymacks", "password": "klymacks1"}, play=True)
    assert _CLIENT.drop_stray_keys(pilot, on_form=False, in_realm=False)
    pilot.phase = "mud"
    assert _CLIENT.drop_stray_keys(pilot, on_form=False, in_realm=False)
    pilot.phase = "play"
    assert not _CLIENT.drop_stray_keys(pilot, on_form=False, in_realm=False)
    assert not _CLIENT.drop_stray_keys(pilot, on_form=True, in_realm=False)
    assert not _CLIENT.drop_stray_keys(pilot, on_form=False, in_realm=True)
    assert not _CLIENT.drop_stray_keys(None, on_form=False, in_realm=False)
    menu = _CLIENT.Autopilot({"username": "klymacks", "password": "klymacks1"}, play=False)
    menu.phase = "play"
    assert not _CLIENT.drop_stray_keys(menu, on_form=False, in_realm=False)


def test_f7_does_not_arm_hunt_before_realm() -> None:
    kind, brain, pacer, _ = _special(_CLIENT.KEY_F7, in_realm=False, hunting=False)
    assert kind == "hunt"
    assert brain.mode == "manual"
    assert not pacer.pending()


# Canonical Worldgroup Main System Menu (TOP) — doors (D) + email (E) kept.
_TOP_MENU = (
    "As a Sysop, you may use the 'ENABLE' and 'DISABLE' commands\r\n"
    "Please select one of the following:\r\n"
    "T ... Teleconference\r\n"
    "I ... Information Center\r\n"
    "F ... Forums (Public Message Bases)\r\n"
    "E ... Electronic Mail\r\n"
    "L ... File Libraries\r\n"
    "A ... Account Display/Edit\r\n"
    "P ... Polls and Questionnaires\r\n"
    "D ... Doors\r\n"
    "R ... Registry of Users\r\n"
    "Q ... QWK-mail\r\n"
    "N ... Networking Connectivity\r\n"
    "M ... MajorMUD\r\n"
    "W ... WorldLink Scheduler\r\n"
    "S ... System Management\r\n"
    "X ... Exit System (Logoff)\r\n"
    "Main System Menu (TOP)\r\n"
    "Make your selection (T,I,F,E,L,A,P,D,R,Q,N,M,W,S,? for help, or X to exit):"
)


def test_board_menu_screen_top_and_majormud() -> None:
    assert _CLIENT.board_menu_screen(_TOP_MENU)
    assert _CLIENT.board_menu_screen(
        "Make your selection (T,I,F,E,L,A,P,D,R,Q,N,M,W,S,? for help, or X to exit):"
    )
    assert _CLIENT.board_menu_screen(
        "[MAJORMUD]  (C) 2002 West Coast Creations\nEnter the Realm"
    )
    assert not _CLIENT.board_menu_screen("Username: ")
    assert not _CLIENT.board_menu_screen("Also here: Corwyn.\n[HP=20/28]:")
    # Leftover module chrome beside a live room is still the realm.
    assert not _CLIENT.board_menu_screen(
        "[E] . Enter the Realm\nAlso here: Ryan.\nObvious exits: east\n[HP=40]:"
    )
    assert not _CLIENT.board_menu_screen(
        "Make your selection (X to exit):\nAlso here: Corwyn.\n[HP=20/28]:"
    )


def test_top_menu_clears_stale_in_realm() -> None:
    state = WorldState()
    state.in_realm = True
    state.hp = 20
    assert _CLIENT.leave_realm_on_board(state, _TOP_MENU)
    assert not state.in_realm
    assert not _CLIENT.use_local_input(_CLIENT.AnsiScreen(), state)


def test_leftover_board_chrome_keeps_in_realm() -> None:
    """Scrolled Enter the Realm / [MAJORMUD] must not drop Enter→l or hold ticks."""
    state = WorldState()
    state.in_realm = True
    state.hp = 40
    blob = (
        "[E] . Enter the Realm\n"
        "Newhaven, Narrow Path\n"
        "Also here: Ryan.\n"
        "Obvious exits: east, west\n"
        "[HP=40]: "
    )
    assert not _CLIENT.leave_realm_on_board(state, blob)
    assert state.in_realm
    kind, mud = _CLIENT.handle_client_line("", Brain(allowed=True), state)
    assert kind == "enter"
    assert mud == ""
    screen = _CLIENT.AnsiScreen()
    screen.feed(blob.encode("ascii"))
    assert _CLIENT.use_local_input(screen, state)
    assert not _CLIENT.form_hold_now(
        screen,
        b"\x1b[24;1H\x1b[KAlso here: Ryan, Robald.\r\n",
        sheet_lock=True,
        in_realm=state.in_realm,
    )


def test_top_menu_peeks_and_f7_do_not_push_mud() -> None:
    state = WorldState()
    state.in_realm = True
    _CLIENT.leave_realm_on_board(state, _TOP_MENU)
    for key in (
        _CLIENT.KEY_F2,
        _CLIENT.KEY_F3,
        _CLIENT.KEY_F4,
        _CLIENT.KEY_F5,
        _CLIENT.KEY_F6,
        _CLIENT.KEY_F7,
    ):
        kind, brain, pacer, _ = _special(key, in_realm=False, hunting=True, state=state)
        assert kind in {"peek", "hunt"}
        assert not pacer.pending()
        assert brain.mode == "hunt" or kind == "hunt"


def test_top_menu_enter_is_not_look() -> None:
    state = WorldState()
    state.in_realm = True
    _CLIENT.leave_realm_on_board(state, _TOP_MENU)
    brain = Brain(allowed=True)
    kind, mud = _CLIENT.handle_client_line("", brain, state)
    assert kind == "enter"
    assert mud == ""
    state.in_realm = True
    kind, mud = _CLIENT.handle_client_line("", brain, state)
    assert kind == "enter"
    assert mud == ""


def test_key_pacer_empty_text_is_bare_enter() -> None:
    """Brain soft room refresh uses push_text(\"\") → bare CR."""
    pacer = _CLIENT.KeyPacer()
    pacer.push_text("")
    assert pacer.take(time.monotonic()) == b"\r"


def test_top_menu_brain_tick_sends_nothing() -> None:
    state = WorldState()
    state.in_realm = True
    state.hp = 20
    state.max_hp = 28
    _CLIENT.leave_realm_on_board(state, _TOP_MENU)
    brain = Brain(allowed=True, me="klymacks", klass="ninja")
    brain.mode = "hunt"
    brain.next_action = "lop"
    sent: list[str] = []
    brain.tick(state, sent.append, pending=False)
    assert sent == []


def test_top_menu_chrome_keeps_mail_and_doors() -> None:
    state = WorldState()
    state.in_realm = True
    _CLIENT.leave_realm_on_board(state, _TOP_MENU)
    screen = _CLIENT.AnsiScreen()
    screen.feed(_TOP_MENU.encode("ascii"))
    brain = Brain(allowed=True, me="klymacks")
    plain = _plain_bar(
        _CLIENT.chrome(30, screen, "board", "127.0.0.1", state, brain)
    )
    assert "M MajorMUD" in plain
    assert "E mail" in plain
    assert "D doors" in plain
    assert "F7 hunt" not in plain
    assert not _CLIENT.use_local_input(screen, state)


def test_f8_skipped_outside_realm() -> None:
    ninja = Brain(allowed=True, klass="ninja", stealth="walk")
    kind, brain, pacer, _ = _special(
        _CLIENT.KEY_F8, in_realm=False, hunting=True, brain=ninja
    )
    assert kind == "ambush"
    assert brain.stealth == "walk"
    assert not pacer.pending()
    paladin = Brain(allowed=True, klass="paladin", aa=False)
    kind, brain, pacer, _ = _special(
        _CLIENT.KEY_F8, in_realm=False, hunting=True, brain=paladin
    )
    assert kind == "aa"
    assert brain.aa is False
    assert not pacer.pending()


def test_chrome_tips_fit() -> None:
    hunt = _CLIENT.realm_fkey_tip(hunting=True, ambush="ambush", join="join")
    idle = _CLIENT.realm_fkey_tip(hunting=False, ambush="walk", join="join")
    paladin = _CLIENT.realm_fkey_tip(hunting=True, ambush="aa", join="join")
    paladin_off = _CLIENT.realm_fkey_tip(hunting=True, ambush="a", join="join")
    idle_off = _CLIENT.realm_fkey_tip(
        hunting=False, ambush="walk", join="join off"
    )
    hunt_off = _CLIENT.realm_fkey_tip(
        hunting=True, ambush="ambush", join="join off"
    )
    assert len(hunt) <= 80, hunt
    assert len(idle) <= 80, idle
    assert len(paladin) <= 80, paladin
    assert len(idle_off) <= 80, idle_off
    assert len(hunt_off) <= 80, hunt_off
    assert "F6 party" in hunt
    assert "F7 hunt" in hunt
    assert "F7 hunt off" not in hunt
    assert "F9 join" in hunt
    assert "F7 hunt off" in idle
    assert "F9 join off" in idle_off
    assert "F7 hunt off" in idle_off
    assert "F7 hunt" in paladin
    assert "F7 hunt off" not in paladin
    assert "F8 aa" in paladin
    assert "F8 a" in paladin_off
    assert "F8 aa" not in paladin_off
    assert "F8 aa off" not in paladin_off
    assert "F10 copy" in hunt
    assert "F10 copy" in idle_off
    assert "F10 held" in _CLIENT.realm_fkey_tip(
        hunting=False, ambush="walk", join="join off", held=True
    )


def test_chrome_invite_tip() -> None:
    state = WorldState()
    state.in_realm = True
    state.hp = 21
    state.max_hp = 21
    state.ma = 20
    state.max_ma = 20
    state.invited_by = "Ron"
    brain = Brain(allowed=True, me="emily", klass="bard", party_leader="Matt")
    brain.mode = "manual"
    bar = _CLIENT.chrome(30, _CLIENT.AnsiScreen(), "x", "127.0.0.1", state, brain)
    plain = _plain_bar(bar)
    assert "Ron invited you" in plain
    assert "fo ron" in plain
    state.following = "Ron"
    after = _plain_bar(
        _CLIENT.chrome(30, _CLIENT.AnsiScreen(), "x", "127.0.0.1", state, brain)
    )
    assert "Ron invited you" not in after
    wiped = _CLIENT.AnsiScreen()
    wiped.feed(b"Ron has invited you to join him.\r[HP=21/MA=20]:")
    assert "invited" not in wiped.text().lower()
    shown = _CLIENT.AnsiScreen()
    shown.feed(keep_party_lf(b"Ron has invited you to join him.\r[HP=21/MA=20]:"))
    blob = shown.text()
    assert "Ron has invited you to join him." in blob
    assert "[HP=21/MA=20]:" in blob


def test_paint_mud_keeps_disband_party_lines() -> None:
    """Bare-CR leave/disband status must survive paint_mud (held or free)."""
    screen = _CLIENT.AnsiScreen()
    screen.feed(b"Newhaven\r\nAlso here: Matt.\r\nObvious exits: east\r\n[HP=40]: ")
    filt = _CLIENT.FormHoldFilter()
    payload = b"You are no longer following Matt.\r[HP=40]: "
    _CLIENT.paint_mud(screen, payload, hold=True, filt=filt, in_realm=True)
    blob = screen.text()
    assert "You are no longer following Matt." in blob
    assert "[HP=40]:" in blob
    # Stuck hold filter path: leave line is not an HP/Name status drop.
    stuck = _CLIENT.AnsiScreen()
    stuck.feed(b"Newhaven\r\nAlso here: Ryan.\r\nObvious exits: west\r\n")
    filt2 = _CLIENT.FormHoldFilter()
    shown = _CLIENT.paint_mud(
        stuck,
        b"klymacks has been removed from your followers.\r[HP=40]:",
        hold=True,
        filt=filt2,
        in_realm=True,
    )
    assert b"removed from your followers." in shown
    assert "removed from your followers." in stuck.text()
    sure = _CLIENT.AnsiScreen()
    _CLIENT.paint_mud(
        sure,
        b"Are you sure you want to disband party? ",
        hold=False,
        filt=_CLIENT.FormHoldFilter(),
        in_realm=True,
    )
    assert "Are you sure you want to disband party?" in sure.text()


def test_play_paused_is_train_hold_not_copy() -> None:
    brain = Brain(allowed=True, klass="ninja")
    assert not _CLIENT.play_paused(brain)
    assert _CLIENT.play_paused(brain, frozen=True)
    brain.begin_train_hold()
    assert _CLIENT.play_paused(brain)
    state = WorldState()
    state.in_realm = True
    screen = _CLIENT.AnsiScreen()
    assert _CLIENT.use_local_input(screen, state)
    asked: list[str] = []
    assert not _CLIENT.maybe_ask_exp(state, asked.append, frozen=True)
    sent: list[str] = []
    state.apply({"kind": "invited", "name": "Matt"})
    _CLIENT.maybe_auto_party(
        state,
        brain,
        sent.append,
        invited=True,
        followed=False,
        frozen=True,
    )
    assert sent == []


def test_lone_esc_is_key_esc() -> None:
    assert _read(b"\x1b") == _CLIENT.KEY_ESC


def _plain_bar(bar: bytes) -> str:
    return _CLIENT._SGR_RE.sub("", bar.decode("utf-8", "replace"))


def _chrome_for(state: WorldState, klass: str = "ninja") -> bytes:
    screen = _CLIENT.AnsiScreen()
    brain = Brain(allowed=True, klass=klass, stealth="always")
    brain.mode = "hunt"
    return _CLIENT.chrome(30, screen, "x", "127.0.0.1", state, brain)


def test_chrome_hp_tone() -> None:
    ok = WorldState()
    ok.in_realm = True
    ok.hp = 20
    ok.max_hp = 20
    ok.max_hp_known = True
    bar = _chrome_for(ok)
    text = bar.decode("utf-8", "replace")
    plain = _plain_bar(bar)
    assert "HP " in plain
    assert "▓" in plain
    assert "HP 20/20" not in plain
    assert _CLIENT.HP_RED_SGR not in text
    assert _CLIENT.HP_YELLOW_SGR not in text
    assert _CLIENT.hp_chrome_sgr(ok) == ""

    edge = WorldState()
    edge.in_realm = True
    edge.hp = 7
    edge.max_hp = 28
    edge.max_hp_known = True
    assert edge.hp_ratio() == 0.25
    assert _CLIENT.hp_chrome_sgr(edge) == ""

    low = WorldState()
    low.in_realm = True
    low.hp = 6
    low.max_hp = 28
    low.max_hp_known = True
    low.ma = 3
    low.max_ma = 8
    assert low.hp_ratio() is not None and low.hp_ratio() < 0.25
    assert _CLIENT.hp_chrome_sgr(low) == _CLIENT.HP_YELLOW_SGR
    yellow = _chrome_for(low).decode("utf-8", "replace")
    yellow_plain = _CLIENT._SGR_RE.sub("", yellow)
    assert f"{_CLIENT.HP_YELLOW_SGR}HP " in yellow
    assert "HP 6/28" not in yellow_plain
    assert "MA " in yellow_plain
    assert "MA 3/8" not in yellow_plain
    assert f"{_CLIENT.HP_YELLOW_SGR}HP 6/28  MA" not in yellow
    body = _CLIENT.color_footer_hp(low.hp_label() + "  room", low)
    assert _CLIENT.visible_len(_CLIENT.pad_visible(body, 80)) == 80

    dead = WorldState()
    dead.in_realm = True
    dead.hp = -95
    dead.max_hp = 28
    dead.max_hp_known = True
    dead.ma = 8
    dead.max_ma = 8
    assert dead.hp_label() == "HP -95/28  MA 8/8"
    assert _CLIENT.hp_chrome_sgr(dead) == _CLIENT.HP_RED_SGR
    red = _chrome_for(dead).decode("utf-8", "replace")
    red_plain = _CLIENT._SGR_RE.sub("", red)
    assert f"{_CLIENT.HP_RED_SGR}HP " in red
    assert "HP -95/28" not in red_plain
    assert "MA " in red_plain
    assert "MA 8/8" not in red_plain
    assert f"{_CLIENT.HP_RED_SGR}HP -95/28  MA" not in red
    padded = _CLIENT.pad_visible(_CLIENT.color_footer_hp(dead.hp_label(), dead), 80)
    assert _CLIENT.visible_len(padded) == 80
    assert len(padded) > 80

    zero = WorldState()
    zero.hp = 0
    zero.max_hp = 28
    zero.max_hp_known = True
    assert _CLIENT.hp_chrome_sgr(zero) == _CLIENT.HP_YELLOW_SGR


def test_chrome_hp_bar_uses_hits_not_health_stat() -> None:
    """Rolled Health 30 must not shrink a 22/22 Hits bar."""
    stale = WorldState()
    stale.in_realm = True
    stale.apply({"kind": "prompt", "hp": 22, "max_hp": None})
    stale.apply({"kind": "prompt", "hp": 30, "max_hp": None})
    stale.hp = 22
    assert not stale.max_hp_known
    assert stale.hp_meter_ratio() == 1.0
    stale.apply({"kind": "hits", "hp": 22, "max_hp": 22})
    assert stale.max_hp == 22
    assert stale.max_hp_known
    assert stale.hp_ratio() == 1.0
    assert stale.hp_meter_ratio() == 1.0
    full = _plain_bar(_chrome_for(stale))
    assert "▓▓▓▓▓▓▓▓" in full


def test_handle_client_line_train() -> None:
    state = WorldState()
    state.in_realm = True
    state.room = "Newhaven, Narrow Road"
    brain = Brain(allowed=True, klass="ninja")
    kind, mud = _CLIENT.handle_client_line("train", brain, state)
    assert kind == "train" and mud is None
    assert brain._want_train
    kind, mud = _CLIENT.handle_client_line("look", brain, state)
    assert kind == "game" and mud == "look"
    assert not brain._want_train
    kind, mud = _CLIENT.handle_client_line("train", brain, state)
    assert kind == "train" and brain._want_train
    kind, mud = _CLIENT.handle_client_line("train", brain, state)
    assert kind == "train" and mud is None
    assert not brain._want_train
    state.room = "Newhaven, Guild"
    kind, mud = _CLIENT.handle_client_line("train", brain, state)
    assert kind == "train" and mud is None
    assert brain.train_holding()
    assert brain.mode == "manual"
    kind, mud = _CLIENT.handle_client_line("train", brain, state)
    assert kind == "game" and mud == "train"
    assert brain.train_holding()
    kind, mud = _CLIENT.handle_client_line("str", brain, state)
    assert kind == "game" and mud == "str"
    assert brain.train_holding()
    kind, mud = _CLIENT.handle_client_line("go train", brain, state)
    assert kind == "train" and mud is None
    assert not brain.train_holding()


def test_handle_client_line_spell_yn() -> None:
    state = WorldState()
    state.in_realm = True
    state.level = 2
    state.room = "Newhaven, Guild"
    brain = Brain(allowed=True, klass="paladin")
    brain._spell_offer = "bless"
    brain._spell_offer_at = 2
    kind, mud = _CLIENT.handle_client_line("y", brain, state)
    assert kind == "spell" and mud is None
    assert brain._want_spell == "bless"
    brain._spell_offer = "bless"
    kind, mud = _CLIENT.handle_client_line("n", brain, state)
    assert kind == "spell" and mud is None
    assert not brain._want_spell
    kind, mud = _CLIENT.handle_client_line("look", brain, state)
    assert kind == "game" and mud == "look"


def test_handle_client_line_gear_yn() -> None:
    state = WorldState()
    state.in_realm = True
    state.level = 10
    state.room = "Newhaven, Guild"
    brain = Brain(allowed=True, klass="ninja", me="klymacks")
    brain._gear_offer = "ebony ninjato"
    brain._gear_offer_key = "class-weapon"
    brain._gear_offer_at = 10
    kind, mud = _CLIENT.handle_client_line("y", brain, state)
    assert kind == "gear" and mud is None
    assert brain._want_gear == "ebony ninjato"
    tip = brain.offer_tip(10)
    assert "ebony ninjato" in tip
    assert len(tip) <= 80
    brain._gear_offer = "ebony ninjato"
    brain._gear_offer_key = "class-weapon"
    kind, mud = _CLIENT.handle_client_line("n", brain, state)
    assert kind == "gear" and mud is None
    assert not brain._want_gear
    kind, mud = _CLIENT.handle_client_line("look", brain, state)
    assert kind == "game" and mud == "look"


def test_handle_client_line_aa() -> None:
    state = WorldState()
    state.in_realm = True
    paladin = Brain(allowed=True, klass="paladin")
    assert paladin.aa
    kind, mud = _CLIENT.handle_client_line("aa", paladin, state)
    assert kind == "aa" and mud is None
    assert not paladin.aa
    kind, mud = _CLIENT.handle_client_line("aa on", paladin, state)
    assert kind == "aa" and paladin.aa
    kind, mud = _CLIENT.handle_client_line("aa off", paladin, state)
    assert kind == "aa" and mud is None and not paladin.aa
    paladin.aa = True
    paladin._attacking = "acid slime"
    state.in_combat = True
    kind, mud = _CLIENT.handle_client_line("aa off", paladin, state)
    assert kind == "aa" and mud == "break" and not paladin.aa
    ninja = Brain(allowed=True, klass="ninja")
    assert not ninja.aa
    kind, mud = _CLIENT.handle_client_line("aa", ninja, state)
    assert kind == "aa" and mud is None
    assert not ninja.aa


def test_handle_client_line_hunt_run() -> None:
    state = WorldState()
    state.in_realm = True
    brain = Brain(allowed=True, klass="paladin", me="matt")
    kind, mud = _CLIENT.handle_client_line("hunt list", brain, state)
    assert kind == "hunt" and mud is None
    assert "gy" in brain.next_action
    assert brain.mode == "manual"
    kind, mud = _CLIENT.handle_client_line("hunt sewer", brain, state)
    assert kind == "hunt"
    assert brain.hunt_run.id == "sewer-east"
    assert brain.hunting()
    kind, mud = _CLIENT.handle_client_line("hunt gy", brain, state)
    assert brain.hunt_run.id == "gy"
    assert brain.hunting()
    kind, mud = _CLIENT.handle_client_line("hunt off", brain, state)
    assert kind == "hunt" and not brain.hunting()
    kind, mud = _CLIENT.handle_client_line("hunt bog", brain, state)
    assert kind == "hunt" and not brain.hunting()
    assert "unknown run" in brain.next_action


def test_handle_client_line_goto() -> None:
    state = WorldState()
    state.in_realm = True
    brain = Brain(allowed=True, klass="paladin", me="matt")
    kind, mud = _CLIENT.handle_client_line("goto list", brain, state)
    assert kind == "goto" and mud is None
    assert "ts" in brain.next_action and "gy" in brain.next_action
    assert "bank" in brain.next_action
    assert brain.mode == "manual"
    kind, mud = _CLIENT.handle_client_line("goto ts", brain, state)
    assert kind == "goto" and mud is None
    assert brain.mode == "goto"
    assert brain.goto_goal == "square"
    assert brain.hunting()
    kind, mud = _CLIENT.handle_client_line("goto gy", brain, state)
    assert brain.goto_goal == "graveyard"
    assert brain.mode == "goto"
    kind, mud = _CLIENT.handle_client_line("goto rest", brain, state)
    assert brain.goto_goal == "restpark"
    assert brain.mode == "goto"
    kind, mud = _CLIENT.handle_client_line("goto bank", brain, state)
    assert brain.goto_goal == "bank"
    assert brain.mode == "goto"
    kind, mud = _CLIENT.handle_client_line("goto bog", brain, state)
    assert kind == "goto" and brain.mode == "goto"
    assert "unknown goto" in brain.next_action
    kind, mud = _CLIENT.handle_client_line("goto stop", brain, state)
    assert brain.mode == "manual"
    assert not brain.hunting()
    brain.start_goto("ts")
    kind, mud = _CLIENT.handle_client_line("hunt gy", brain, state)
    assert brain.mode in {"hunt", "gear"}
    assert brain.hunt_run.id == "gy"
    assert not brain.goto_goal


def test_handle_client_line_boost() -> None:
    brain = Brain(allowed=True, pvp=False, me="sysop klymacks")
    state = WorldState()
    kind, mud = _CLIENT.handle_client_line("boost", brain, state)
    assert kind == "boost"
    assert mud is None
    state.in_realm = True
    kind, mud = _CLIENT.handle_client_line("boost", brain, state)
    assert kind == "boost"
    assert mud is not None
    assert "SYS TWEAK EXPERIENCE" in mud
    assert "SYS TWEAK LEVEL 10" in mud
    assert "LEVEL 15" not in mud


def test_handle_client_line_stash() -> None:
    brain = Brain(allowed=True, pvp=False, me="sysop klymacks")
    state = WorldState()
    kind, mud = _CLIENT.handle_client_line("stash", brain, state)
    assert kind == "stash"
    assert mud is None
    state.in_realm = True
    kind, mud = _CLIENT.handle_client_line("stash", brain, state)
    assert kind == "stash"
    assert mud == "SYS TWEAK GOLD 500"
    kind, mud = _CLIENT.handle_client_line("stash 80", brain, state)
    assert mud == "SYS TWEAK GOLD 80"
    kind, mud = _CLIENT.handle_client_line("stash 9999", brain, state)
    assert mud == "SYS TWEAK GOLD 2000"
    kind, mud = _CLIENT.handle_client_line("stash gold", brain, state)
    assert mud is None
    assert "stash" in brain.next_action


def test_handle_client_line_invite_all() -> None:
    brain = Brain(allowed=True, me="sysop Matt", alts="klymacks ryan alex")
    state = WorldState()
    kind, mud = _CLIENT.handle_client_line("invite all", brain, state)
    assert kind == "invite"
    assert mud is None
    assert brain.next_action == "invite in the realm"
    state.in_realm = True
    state.mobs = ["Klymacks", "Ryan", "acid slime", "Betram"]
    kind, mud = _CLIENT.handle_client_line("invite all", brain, state)
    assert kind == "invite"
    assert mud == "invite Klymacks\ninvite Ryan"
    state.mobs = ["Klymacks"]
    state.followers = ["Klymacks"]
    kind, mud = _CLIENT.handle_client_line("inv all", brain, state)
    assert kind == "invite"
    assert mud is None
    assert brain.next_action == "already grouped"
    empty = WorldState()
    empty.in_realm = True
    kind, mud = _CLIENT.handle_client_line("invite all", brain, empty)
    assert kind == "invite"
    assert mud == "look"
    assert brain.next_action == "look"
    kind, mud = _CLIENT.handle_client_line("invite Klymacks", brain, state)
    assert kind == "game"
    assert mud == "invite Klymacks"
    kind, mud = _CLIENT.handle_client_line("", brain, state)
    assert kind == "enter"
    assert mud == "l"


def test_handle_client_line_join_call() -> None:
    matt = Brain(
        allowed=True, me="sysop Matt", alts="klymacks ryan", party_leader="Matt"
    )
    state = WorldState()
    kind, mud = _CLIENT.handle_client_line("!join", matt, state)
    assert kind == "join"
    assert mud is None
    assert matt.next_action == "join in the realm"
    state.in_realm = True
    state.mobs = ["Klymacks", "Ryan"]
    kind, mud = _CLIENT.handle_client_line("!join", matt, state)
    assert kind == "join"
    assert mud == "invite Klymacks\ninvite Ryan\n!join"
    kind, mud = _CLIENT.handle_client_line("say !join", matt, state)
    assert kind == "join"
    assert mud is not None and mud.endswith("!join")
    kly = Brain(
        allowed=True, me="klymacks", party_leader="Matt", klass="ninja"
    )
    here = WorldState()
    here.in_realm = True
    # Solo ninja may type !join (rally). Once following, no echo.
    kind, mud = _CLIENT.handle_client_line("!join", kly, here)
    assert kind == "join"
    assert mud == "!join"
    live = Brain(
        allowed=True,
        me="klymacks Klymacks",
        alts="matt matthew sysop",
        party_leader="Matt",
        klass="ninja",
    )
    kind, mud = _CLIENT.handle_client_line("!join", live, here)
    assert kind == "join"
    assert mud == "!join"
    sherry = Brain(
        allowed=True, me="sherry Sherry", party_leader="Sherry", klass="druid"
    )
    kind, mud = _CLIENT.handle_client_line("!join", sherry, here)
    assert kind == "join"
    assert mud == "!join"
    assert "Matthew" not in (mud or "")
    follower = Brain(
        allowed=True, me="audrey", party_leader="Sherry", klass="warlock"
    )
    follower.auto_join = True
    following = WorldState()
    following.in_realm = True
    following.apply({"kind": "following", "name": "Sherry"})
    kind, mud = _CLIENT.handle_client_line("!join", follower, following)
    assert kind == "join"
    assert mud is None
    assert "leader shouts" in follower.next_action
    kly.auto_join = False
    kind, mud = _CLIENT.handle_client_line("join up", kly, here)
    assert kind == "join"
    assert mud == "!join"


def test_handle_client_line_rest_heal_call() -> None:
    matt = Brain(
        allowed=True, me="sysop Matt", alts="klymacks ryan", party_leader="Matt"
    )
    state = WorldState()
    kind, mud = _CLIENT.handle_client_line("!rest", matt, state)
    assert kind == "rest"
    assert mud is None
    assert matt.next_action == "rest in the realm"
    state.in_realm = True
    state.room = "Graveyard, Southern Edge"
    state.exits = ["n", "e", "w"]
    state.mobs = ["Klymacks", "Ryan"]
    kind, mud = _CLIENT.handle_client_line("!rest", matt, state)
    assert kind == "rest"
    assert mud == "!rest"
    assert matt.mode == "goto"
    assert matt.goto_goal == "restpark"
    kind, mud = _CLIENT.handle_client_line("!heal", matt, state)
    assert kind == "heal"
    assert mud == "!heal"
    kly = Brain(
        allowed=True, me="klymacks", party_leader="Matt", klass="ninja"
    )
    here = WorldState()
    here.in_realm = True
    here.room = "Graveyard, Southern Edge"
    here.exits = ["n", "e", "w"]
    kind, mud = _CLIENT.handle_client_line("say !rest", kly, here)
    assert kind == "rest"
    assert mud == "!rest"
    assert kly.mode == "goto"
    assert kly.goto_goal == "restpark"


def test_handle_client_line_run() -> None:
    state = WorldState()
    state.in_realm = True
    brain = Brain(allowed=True, klass="paladin", me="matt")
    kind, mud = _CLIENT.handle_client_line("run ts", brain, state)
    assert kind == "goto" and mud is None
    assert brain.mode == "goto"
    assert brain.goto_skip
    assert brain.goto_goal == "square"
    kind, mud = _CLIENT.handle_client_line("run pile", brain, state)
    assert "no deathpile" in brain.next_action
    brain.deathpile = "Graveyard"
    kind, mud = _CLIENT.handle_client_line("run pile", brain, state)
    assert brain.goto_goal == "_pile"
    assert brain.goto_skip


def test_maybe_ask_exp_once() -> None:
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    sent: list[str] = []
    assert _CLIENT.maybe_ask_exp(state, sent.append)
    assert sent == ["exp"]
    assert state.exp_asked
    assert not _CLIENT.maybe_ask_exp(state, sent.append)
    assert sent == ["exp"]
    state.apply(
        {
            "kind": "level",
            "level": 1,
            "exp": 762,
            "needed": 1538,
            "next": 2300,
            "pct": 33,
        }
    )
    assert not state.needs_exp()
    state.apply({"kind": "experience", "amount": 40})
    state.apply({"kind": "killed", "name": "rat"})
    assert not state.needs_exp()
    sent.clear()
    assert not _CLIENT.maybe_ask_exp(state, sent.append)
    assert sent == []
    state.apply({"kind": "killed", "name": "rat"})
    assert not state.needs_exp()
    assert not _CLIENT.maybe_ask_exp(state, sent.append)
    assert sent == []
    state.in_combat = True
    state.exp_asked = False
    assert not _CLIENT.maybe_ask_exp(state, sent.append)


def test_hunt_ticks_do_not_resend_exp() -> None:
    """After a full Exp: line, hunt ticks and kills do not send `exp`."""
    state = WorldState()
    state.in_realm = True
    state.hp = 38
    state.max_hp = 38
    state.max_hp_known = True
    state.room = "Newhaven, Arena"
    state.scanned = True
    state.apply(
        parse_line("Exp: 4610 Level: 3 Exp needed for next level: 3823 (8433) [54%]")
    )
    assert state.has_exp_reading()
    assert not state.needs_exp()
    brain = Brain(allowed=True, me="klymacks", klass="ninja")
    brain.gear_done = True
    brain.mode = "hunt"
    brain._in_camp = True
    brain._asked_health = True
    sent: list[str] = []
    for _ in range(4):
        brain.tick(state, sent.append, pending=False)
        assert not _CLIENT.maybe_ask_exp(state, sent.append)
        state.prompt_seq += 1
    assert "exp" not in sent
    state.apply({"kind": "killed", "name": "giant rat"})
    assert not state.needs_exp()
    brain.tick(state, sent.append, pending=False)
    assert not _CLIENT.maybe_ask_exp(state, sent.append)
    assert "exp" not in sent
    assert not _CLIENT.maybe_ask_exp(state, sent.append)
    assert _CLIENT.maybe_ask_stat(state, sent.append)
    assert sent[-1] == "stat"
    assert not _CLIENT.maybe_ask_stat(state, sent.append)


def test_maybe_ask_stat_once() -> None:
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    sent: list[str] = []
    assert _CLIENT.maybe_ask_stat(state, sent.append)
    assert sent == ["stat"]
    assert state.stat_asked
    assert not _CLIENT.maybe_ask_stat(state, sent.append)
    assert sent == ["stat"]
    state.apply({"kind": "stats", "strength": 70, "agility": 80})
    assert not state.needs_stat()
    sent.clear()
    assert not _CLIENT.maybe_ask_stat(state, sent.append)
    state.in_combat = True
    state.stat_asked = False
    state.stat_known = False
    assert not _CLIENT.maybe_ask_stat(state, sent.append)
    state.in_combat = False
    assert not _CLIENT.maybe_ask_exp(state, sent.append, frozen=True)
    assert not _CLIENT.maybe_ask_stat(state, sent.append, frozen=True)


def test_chrome_shows_exp_and_train() -> None:
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    state.apply(
        {
            "kind": "level",
            "level": 1,
            "exp": 762,
            "needed": 1538,
            "next": 2300,
            "pct": 33,
        }
    )
    text = _plain_bar(_chrome_for(state))
    assert "HP " in text
    assert "HP 28/28" not in text
    assert "EXP 33% 762/2300" in text
    assert text.count("EXP ") == 1
    assert text.index("33%") < text.index("762/2300")
    assert text.index("EXP 33%") < text.index("HP ")
    assert "▓" in text and "░" in text
    assert "TRAIN" not in text
    state.apply(
        {
            "kind": "level",
            "level": 1,
            "exp": 2300,
            "needed": 0,
            "next": 2300,
            "pct": 100,
        }
    )
    bar = _chrome_for(state)
    text = _plain_bar(bar)
    assert "TRAIN 100%" in text
    raw = bar.decode("utf-8", "replace")
    assert _CLIENT.HP_YELLOW_SGR in raw
    assert "F11 train" in text
    # Leftover F11 lock on a blank grid is not the parchment — no SHEET freeze.
    blank_lock = _plain_bar(
        _CLIENT.chrome(
            30,
            _CLIENT.AnsiScreen(),
            "x",
            "127.0.0.1",
            state,
            Brain(allowed=True, klass="ninja"),
            sheet_lock=True,
        )
    )
    assert "SHEET" not in blank_lock
    # Title scrolled off; Given Name + lock still owns keys and chrome.
    named = _CLIENT.AnsiScreen()
    named.feed(b"Given Name   klymacks\r\n\x1b[25;1H[HP=28]: ")
    locked = _plain_bar(
        _CLIENT.chrome(
            30,
            named,
            "x",
            "127.0.0.1",
            state,
            Brain(allowed=True, klass="ninja"),
            sheet_lock=True,
        )
    )
    assert "SHEET" in locked
    assert "F11 live" in locked
    assert "no health/exp/hunt" in locked
    hold_brain = Brain(allowed=True, klass="ninja")
    hold_brain.begin_train_hold()
    held = _plain_bar(
        _CLIENT.chrome(
            30,
            _CLIENT.AnsiScreen(),
            "x",
            "127.0.0.1",
            state,
            hold_brain,
        )
    )
    assert "TRAIN HOLD" in held
    assert "brain paused" in held
    assert "you type" in held
    assert "F10 copy" not in held
    assert "F10 held" not in held
    assert "SHEET" not in held
    assert "> " in held


def test_chrome_hides_hp_on_train_stats() -> None:
    """TRAIN STATS (and F10 on that sheet) must not keep the realm HP strip."""
    state = WorldState()
    state.in_realm = True
    state.hp = 67
    state.max_hp = 67
    state.max_hp_known = True
    state.level = 5
    screen = _CLIENT.AnsiScreen()
    screen.feed(b"\x1b[1;1HTRAIN STATS\r\nGiven Name   Matt\r\n")
    assert screen.looks_like_creation()
    text = _plain_bar(
        _CLIENT.chrome(30, screen, "x", "127.0.0.1", state, Brain(allowed=True, klass="paladin"))
    )
    assert "HP " not in text
    held = _plain_bar(
        _CLIENT.chrome(
            30,
            screen,
            "x",
            "127.0.0.1",
            state,
            Brain(allowed=True, klass="paladin"),
            held=True,
        )
    )
    assert "HP " not in held


def test_chrome_splits_status_and_stats() -> None:
    state = WorldState()
    state.in_realm = True
    state.hp = 24
    state.max_hp = 28
    state.max_hp_known = True
    state.ma = 3
    state.max_ma = 8
    state.room = "Newhaven, Arena"
    state.apply(
        {
            "kind": "level",
            "level": 1,
            "exp": 762,
            "needed": 1538,
            "next": 2300,
            "pct": 33,
        }
    )
    state.apply({"kind": "combat", "dealt": 23})
    brain = Brain(allowed=True, klass="ninja", stealth="always")
    brain.mode = "hunt"
    brain.next_action = "fighting"
    bar = _CLIENT.chrome(30, _CLIENT.AnsiScreen(), "x", "127.0.0.1", state, brain)
    text = _plain_bar(bar)
    raw = bar.decode("utf-8", "replace")
    assert "FINN'S REALM" in text
    assert "░▒▓" in text
    assert "Newhaven Arena" in text
    assert "hunt" in text
    assert "next: fighting" in text
    assert "HP " in text
    assert "MA " in text
    assert "HP 24/28" not in text
    assert "MA 3/8" not in text
    assert "EXP 33% 762/2300" in text
    assert "DPS 23" in text
    assert text.index("Newhaven Arena") < text.index("DPS 23")
    assert text.index("DPS 23") < text.index("EXP 33%")
    assert text.index("33%") < text.index("762/2300")
    assert text.index("EXP 33%") < text.index("HP ")
    assert text.index("HP ") < text.index("MA ")
    row = _CLIENT.format_stats_row(state)
    assert row.startswith("DPS 23")
    assert "MA " in row
    assert row.rstrip().endswith(("░", "▒", "▓"))
    assert _CLIENT.visible_len(row) == 80
    assert _CLIENT.ICE_CYAN_SGR in raw
    assert _CLIENT.ICE_DIM_SGR in raw
    assert _CLIENT.visible_len(_CLIENT.ice_title_line("klymacks (Lv.3 Ninja)")) <= 80
    assert _CLIENT.visible_len(_CLIENT.color_status_line(state, brain)) <= 80
    assert _CLIENT.visible_len(_CLIENT.paint_stats_row(state)) <= 80


def test_chrome_shows_hp_and_ma_over_max() -> None:
    state = WorldState()
    state.in_realm = True
    state.hp = 24
    state.max_hp = 28
    state.max_hp_known = True
    state.ma = 3
    state.max_ma = 8
    state.room = "Newhaven, Narrow Road"
    brain = Brain(allowed=True, klass="paladin")
    brain.mode = "hunt"
    brain.next_action = "fighting acid slime  ambush always"
    bar = _CLIENT.chrome(30, _CLIENT.AnsiScreen(), "x", "127.0.0.1", state, brain)
    text = _plain_bar(bar)
    assert "HP " in text
    assert "MA " in text
    assert "HP 24/28" not in text
    assert "MA 3/8" not in text
    assert "F8 aa" in text
    state.apply(
        {
            "kind": "level",
            "level": 1,
            "exp": 762,
            "needed": 1538,
            "next": 2300,
            "pct": 33,
        }
    )
    crowded = _plain_bar(
        _CLIENT.chrome(30, _CLIENT.AnsiScreen(), "x", "127.0.0.1", state, brain)
    )
    assert crowded.index("EXP 33% 762/2300") < crowded.index("HP ")
    assert crowded.count("EXP ") == 1
    assert crowded.index("DPS") < crowded.index("EXP 33%")
    assert crowded.index("33%") < crowded.index("762/2300")


def test_chrome_splits_attack_defense() -> None:
    """AT/DF sit under who on the status line (2nd footer row), far right."""
    ninja = WorldState()
    ninja.in_realm = True
    ninja.hp = 24
    ninja.max_hp = 28
    ninja.max_hp_known = True
    ninja.klass = "ninja"
    ninja.level = 1
    ninja.strength = 70
    ninja.agility = 80
    ninja.attack = 35
    ninja.ac = 14
    ninja.room = "Newhaven, Arena"
    ninja.worn = [
        "stiletto",
        "padded vest",
        "padded helm",
        "padded pants",
        "padded boots",
        "padded gloves",
    ]
    ninja.apply({"kind": "combat", "dealt": 4})
    brain = Brain(allowed=True, me="sysop klymacks", klass="ninja")
    brain.mode = "hunt"
    brain.next_action = "fighting"
    status = _CLIENT._SGR_RE.sub("", _CLIENT.color_status_line(ninja, brain))
    assert "AT 35+0" in status
    assert "DF 4+10" in status
    assert "1-4" in status
    assert status.rstrip().endswith(("1-4", "DF 4+10", "AT 35+0"))
    assert status.index("Newhaven") < status.index("AT 35+0")
    assert _CLIENT.visible_len(_CLIENT.color_status_line(ninja, brain)) <= 80
    stats = _CLIENT.format_stats_row(ninja, klass="ninja")
    assert "AT 35+0" not in stats
    assert stats.startswith("DPS")
    ninja_bar = _plain_bar(
        _CLIENT.chrome(
            30,
            _CLIENT.AnsiScreen(),
            "x",
            "127.0.0.1",
            ninja,
            brain,
        )
    )
    assert "AT 35+0" in ninja_bar
    assert "DF 4+10" in ninja_bar
    assert ninja_bar.index("FINN'S REALM") < ninja_bar.index("AT 35+0")
    assert ninja_bar.index("AT 35+0") < ninja_bar.index("DPS")

    paladin = WorldState()
    paladin.in_realm = True
    paladin.hp = 38
    paladin.max_hp = 38
    paladin.max_hp_known = True
    paladin.klass = "paladin"
    paladin.level = 3
    paladin.strength = 80
    paladin.agility = 50
    paladin.attack = 42
    paladin.ac = 16
    paladin.room = "Sewer Tunnel, Junction"
    paladin.worn = [
        "battle axe",
        "padded vest",
        "padded helm",
        "padded pants",
        "padded boots",
        "padded gloves",
    ]
    p_brain = Brain(allowed=True, me="matt Matt", klass="paladin")
    p_brain.mode = "hunt"
    p_status = _CLIENT._SGR_RE.sub("", _CLIENT.color_status_line(paladin, p_brain))
    assert "AT 42+0" in p_status
    assert "DF 6+10" in p_status
    assert "4-15" in p_status
    unique = WorldState()
    unique.worn = ["ebony ninjato"]
    unique.klass = "ninja"
    unique.level = 1
    unique.strength = 70
    unique.agility = 80
    assert "5-18" in unique.combat_label()


def test_form_hold_drops_hp_keeps_fields() -> None:
    """F11 lock: delayed [HP=] / health must not overwrite Intellect."""
    raw = (
        b"\x1b[9;5H\x1b[1;37mIntellect  (40   to 100 )  66\x1b[0m"
        b"[HP=42/MA=10]:                   66"
        b"\r\nHealth:    42/42    [100%]  Mana:   10/10\r\n"
        b"\x1b[10;5HWillpower  (40   to 100 )  40"
    )
    held = _CLIENT.form_hold_payload(raw)
    assert b"[HP=" not in held
    assert b"Health:" not in held
    assert b"Intellect" in held
    assert b"Willpower" in held
    assert b"66" in held
    screen = _CLIENT.AnsiScreen()
    screen.feed(b"\x1b[2J\x1b[1;1HM A J O R  M U D Character Creation\r\n")
    screen.feed(b"Given Name   Matt\r\n")
    screen.feed(held)
    text = screen.text()
    assert "Character Creation" in text
    assert "Given Name" in text
    assert "[HP=" not in text
    assert _CLIENT.form_blocks_line(b"health\r", frozen=True)
    assert _CLIENT.form_blocks_line(b"look\r", frozen=True)
    assert not _CLIENT.form_blocks_line(b"follow Matthew\r", frozen=True)
    assert not _CLIENT.form_blocks_line(b"invite klymacks\r", frozen=True)
    assert not _CLIENT.form_blocks_line(b"\r", frozen=True)
    assert not _CLIENT.form_blocks_line(b"health\r", frozen=False)


def test_form_hold_drops_bottom_erase_then_hp() -> None:
    """WG idle prompt: CUP to row 25, erase line, then [HP=]. Must not blank the scroll foot."""
    screen = _CLIENT.AnsiScreen()
    foot = b"\\_________________________________\\___/"
    screen.feed(b"\x1b[1;1HTRAIN STATS\r\nGiven Name   Matt\r\n")
    screen.feed(b"\x1b[25;1H" + foot)
    assert "TRAIN STATS" in screen.text()
    assert foot.decode() in screen.line(24)
    filt = _CLIENT.FormHoldFilter()
    shown = _CLIENT.paint_mud(
        screen,
        b"\x1b[25;1H\x1b[K[HP=67/MA=16]: ",
        hold=True,
        filt=filt,
    )
    assert b"[HP=" not in shown
    assert "[HP=" not in screen.text()
    assert foot.decode() in screen.line(24)
    # split: CUP+EL this packet, prompt the next
    screen2 = _CLIENT.AnsiScreen()
    screen2.feed(b"\x1b[1;1HTRAIN STATS\r\n")
    screen2.feed(b"\x1b[25;1H" + foot)
    filt2 = _CLIENT.FormHoldFilter()
    _CLIENT.paint_mud(screen2, b"\x1b[25;1H\x1b[K", hold=True, filt=filt2)
    _CLIENT.paint_mud(screen2, b"[HP=28]: \r\n", hold=True, filt=filt2)
    assert "[HP=" not in screen2.text()
    assert foot.decode() in screen2.line(24)


def test_form_hold_drops_name_status_after_save() -> None:
    """SAVE writes `Name: Given Family` on the last row. Keep the parchment foot."""
    screen = _CLIENT.AnsiScreen()
    foot = b"\\_________________________________\\___/"
    screen.feed(b"\x1b[1;1HM A J O R  M U D Character Creation\r\n")
    screen.feed(b"Given Name   Klymacks\r\n")
    screen.feed(b"Family Name  Urmom\r\n")
    screen.feed(b"Exit: SAVE\r\n")
    screen.feed(b"\x1b[25;1H" + foot)
    filt = _CLIENT.FormHoldFilter()
    shown = _CLIENT.paint_mud(
        screen,
        b"\x1b[25;1H\x1b[KName: Klymacks Urmom",
        hold=True,
        filt=filt,
    )
    assert b"Name: Klymacks" not in shown
    blob = screen.text()
    assert "Given Name   Klymacks" in blob
    assert "Name: Klymacks Urmom" not in blob
    assert foot.decode() in screen.line(24)
    field = filt.filter(b"\x1b[4;5HGiven Name   Klymacks")
    assert b"Given Name   Klymacks" in field


def test_form_hold_scrubs_name_status_drawn_per_letter() -> None:
    """WG FSD often SGR-wraps the SAVE status; the assembled line still goes."""
    screen = _CLIENT.AnsiScreen()
    foot = "\\_________________________________\\___/"
    filt = _CLIENT.FormHoldFilter()
    _CLIENT.paint_mud(
        screen,
        b"\x1b[1;1HM A J O R  M U D Character Creation\r\n"
        b"Given Name   Klymacks\r\nExit: SAVE\r\n",
        hold=True,
        filt=filt,
    )
    _CLIENT.paint_mud(screen, b"\x1b[25;1H" + foot.encode(), hold=True, filt=filt)
    assert foot in screen.line(24)
    bits = bytearray(b"\x1b[25;1H")
    for ch in "NAME: Klymacks Urmom":
        bits += b"\x1b[0m" + ch.encode()
    _CLIENT.paint_mud(screen, bytes(bits), hold=True, filt=filt)
    assert "NAME:" not in screen.text()
    assert "Name: Klymacks Urmom" not in screen.text()
    assert "Given Name   Klymacks" in screen.text()
    assert foot in screen.line(24)


def test_form_hold_scrubs_bare_given_family_after_save() -> None:
    """After SAVE, WG writes Given Family on row 25 with no `Name:` label.

    Same split as Health (stat field) vs Health: (status): keep the two
    name fields, drop the leftover full name on the last row.
    """
    screen = _CLIENT.AnsiScreen()
    foot = "\\_________________________________\\___/"
    filt = _CLIENT.FormHoldFilter()
    _CLIENT.paint_mud(
        screen,
        b"\x1b[1;1HM A J O R  M U D Character Creation\r\n"
        b"Given Name   Klymacks\r\n"
        b"Family Name  Urmom\r\n"
        b"Exit: SAVE\r\n",
        hold=True,
        filt=filt,
    )
    _CLIENT.paint_mud(screen, b"\x1b[24;1H" + foot.encode(), hold=True, filt=filt)
    bits = bytearray(b"\x1b[25;1H\x1b[K")
    for ch in "Klymacks Urmom":
        bits += b"\x1b[0m" + ch.encode()
    _CLIENT.paint_mud(screen, bytes(bits), hold=True, filt=filt)
    blob = screen.text()
    assert "Given Name   Klymacks" in blob
    assert "Family Name  Urmom" in blob
    assert screen.line(24).strip() != "Klymacks Urmom"
    assert "Klymacks Urmom" not in screen.line(24)
    # Combined name must not sit under the parchment; fields stay.
    last = screen.line(24).strip()
    assert last == "" or last == foot or foot in screen.line(23) + screen.line(24)


def test_repair_bare_sgr_after_lost_esc() -> None:
    screen = _CLIENT.AnsiScreen()
    screen.feed(b"l[0;37;40mThis nicely crafted room")
    assert "[0;37;40m" not in screen.text()
    assert "This nicely crafted room" in screen.text()
    screen2 = _CLIENT.AnsiScreen()
    screen2.feed(b"Also here: [0;37mCorwyn.")
    assert "[0;37m" not in screen2.text()
    assert "Corwyn" in screen2.text()


def test_echoed_l_stays_on_userid() -> None:
    """WG echoes each user-ID letter. A lone `l` is not a look."""
    screen = _CLIENT.AnsiScreen()
    screen.feed(b"Please enter your user-ID: pa")
    screen.feed(b"l")
    screen.feed(b"adin")
    assert "Please enter your user-ID: paladin" in screen.text()


def test_close_form_wipes_sheet_on_room() -> None:
    """SAVE must close FSD. Room text is not an overlay on the parchment."""
    screen = _CLIENT.AnsiScreen()
    filt = _CLIENT.FormHoldFilter()
    _CLIENT.paint_mud(
        screen,
        b"\x1b[1;1HM A J O R  M U D Character Creation\r\n"
        b"Given Name   Klymacks\r\n"
        b"Exit: SAVE\r\n",
        hold=True,
        filt=filt,
    )
    assert "Character Creation" in screen.text()
    room = (
        b"l[0;37;40m    This nicely crafted room is well furnished.\r\n"
        b"Also here: Corwyn.\r\n"
        b"Obvious exits: south\r\n"
        b"[HP=33]: "
    )
    _CLIENT.paint_mud(screen, room, hold=True, filt=filt)
    blob = screen.text()
    assert "Character Creation" not in blob
    assert "Exit: SAVE" not in blob
    assert "[0;37;40m" not in blob
    assert "This nicely crafted room" in blob
    assert "Obvious exits: south" in blob
    assert not screen.looks_like_creation()


def test_split_sgr_does_not_print_bare_codes() -> None:
    """WG often splits `ESC` and `[0;37m` across packets on Also here."""
    screen = _CLIENT.AnsiScreen()
    screen.feed(b"Also here: \x1b")
    screen.feed(b"[0;37mCorwyn.\r\nObvious exits: south")
    blob = screen.text()
    assert "[0;37m" not in blob
    assert "Corwyn" in blob
    assert "Obvious exits: south" in blob
    assert screen.buf[0][11].ch == "C" or "Corwyn" in screen.line(0)


def test_idle_also_here_does_not_wipe_room() -> None:
    """Standing still: WG's Also here refresh must not close_form the guild."""
    screen = _CLIENT.AnsiScreen()
    filt = _CLIENT.FormHoldFilter()
    _CLIENT.paint_mud(
        screen,
        b"Newhaven, Adventurer's Guild\r\n"
        b"    This nicely crafted room is well furnished.\r\n"
        b"Also here: Corwyn.\r\n"
        b"Obvious exits: south\r\n"
        b"[HP=33]: ",
        hold=False,
        filt=filt,
    )
    assert "This nicely crafted room" in screen.text()
    _CLIENT.paint_mud(
        screen,
        b"Also here: Corwyn.\r\nObvious exits: south\r\n[HP=33]: ",
        hold=True,
        filt=filt,
    )
    blob = screen.text()
    assert "This nicely crafted room" in blob
    assert "Adventurer's Guild" in blob
    assert "Obvious exits: south" in blob


def test_leftover_parchment_titles_do_not_rearm_form_snap() -> None:
    """Bare 6n with leftover TRAIN STATS must not become a live FSD session.

    That re-arm made the next idle CPR a form row (e.g. 3;7R) and FormHold
    ate the standing pulse — room ticks died for the rest of the session.
    """
    screen = _CLIENT.AnsiScreen()
    filt = _CLIENT.FormHoldFilter()
    screen.feed(b"TRAIN STATS\r\nGiven Name   Rita\r\nExit: SAVE\r\n")
    assert screen.form_snap is None
    assert not _CLIENT._live_fsd_session(screen)
    _CLIENT.paint_mud(screen, b"\x1b[6n", hold=False, filt=filt, in_realm=True)
    assert screen.form_snap is None
    assert not _CLIENT._live_fsd_session(screen)
    pulse = b"\x1b[25;1H\x1b[K[HP=40]: \x1b[6n"
    assert not _CLIENT.form_hold_now(screen, pulse, in_realm=True)
    screen.replies.clear()
    _CLIENT.paint_mud(screen, pulse, hold=False, filt=filt, in_realm=True)
    _CLIENT.ensure_realm_dsr(screen, pulse, in_realm=True)
    assert screen.form_snap is None
    assert not _CLIENT._live_fsd_session(screen)
    dsr = [r for r in screen.replies if r.endswith(b"R")]
    assert dsr
    # Real caret after ``[HP=40]: `` on row 25 — not forced ``25;1R``.
    assert dsr[-1].startswith(b"\x1b[25;")
    assert dsr[-1] != b"\x1b[25;1R" or screen.cx == 0



def test_form_hold_only_while_live_fsd() -> None:
    """FormHold is F11/parchment-only — leftover titles and sheet_lock never arm it."""
    screen = _CLIENT.AnsiScreen()
    screen.feed(b"Exit: SAVE your character\r\nGiven Name   Rita\r\n")
    assert screen.looks_like_creation()
    assert not _CLIENT._live_fsd_session(screen)
    assert not _CLIENT.form_hold_now(
        screen,
        b"\x1b[25;1H\x1b[K[HP=40]: \x1b[6n",
        sheet_lock=True,
        in_realm=False,
        train_hold=True,
    )
    # Live TRAIN STATS snap — hold.
    filt = _CLIENT.FormHoldFilter()
    _CLIENT.paint_mud(
        screen,
        b"TRAIN STATS\r\nGiven Name   Rita\r\nExit: SAVE\r\n",
        hold=True,
        filt=filt,
    )
    assert _CLIENT._live_fsd_session(screen)
    assert _CLIENT.form_hold_now(
        screen,
        b"\x1b[25;1H\x1b[K[HP=40]: \x1b[6n",
        sheet_lock=False,
        in_realm=True,
        train_hold=True,
    )


def test_train_from_realm_holds_hp_and_keeps_form_cpr() -> None:
    """F11 train leaves in_realm True — must still FormHold and CPR the field."""
    screen = _CLIENT.AnsiScreen()
    filt = _CLIENT.FormHoldFilter()
    _CLIENT.paint_mud(
        screen,
        b"TRAIN STATS\r\nGiven Name   Matt\r\nExit: SAVE\r\n",
        hold=True,
        filt=filt,
    )
    assert screen.form_snap is not None
    assert _CLIENT._live_fsd_session(screen)
    # in_realm still set from before F11 — must not disable hold.
    assert _CLIENT.form_hold_now(
        screen,
        b"\x1b[25;1H\x1b[K[HP=67/MA=16]: \x1b[6n",
        sheet_lock=True,
        in_realm=True,
    )
    screen.replies.clear()
    screen.cx, screen.cy = 4, 10
    punch = b"\x1b[11;1H\x1b[K[HP=67/MA=16]: \x1b[6n"
    _CLIENT.paint_mud(screen, punch, hold=True, filt=filt, in_realm=True)
    _CLIENT.ensure_realm_dsr(screen, punch, in_realm=True)
    assert "[HP=" not in screen.text()
    assert "TRAIN STATS" in screen.text()
    dsr = [r for r in screen.replies if r.endswith(b"R")]
    assert dsr
    # Must NOT be forced to row 25 while the live sheet is open.
    row = int(dsr[-1].decode().split(";")[0].lstrip("\x1b["))
    assert row != 25


def test_form_hold_keeps_room_tick_cup_with_hp() -> None:
    """Sheet hold must drop [HP=] text, not the CUP that homes the idle look.

    Eating CUP left bare 6n → form-row CPR → WG stopped sending Also here.
    """
    filt = _CLIENT.FormHoldFilter()
    out = filt.filter(b"\x1b[25;1H\x1b[K[HP=40]: \x1b[6n")
    assert b"[HP=" not in out
    assert b"\x1b[25;1H" in out
    assert b"\x1b[6n" in out
    # Full tick under hold still paints Also here / exits.
    look = filt.filter(
        b"\x1b[24;1H\x1b[KAlso here: Curtis.\r\nObvious exits: west\r\n"
    )
    assert b"Also here: Curtis." in look
    assert b"Obvious exits: west" in look


def test_form_hold_filter_does_not_eat_short_look_after_hp() -> None:
    """WG splits idle [HP=] then Also here. skip_prompt must not swallow the look."""
    filt = _CLIENT.FormHoldFilter()
    dropped = filt.filter(b"[HP=40]: ")
    assert b"[HP=" not in dropped
    look = filt.filter(b"Also here: Robald.\r\nObvious exits: north\r\n")
    assert b"Also here: Robald." in look
    assert b"Obvious exits: north" in look


def test_realm_idle_short_look_paints_when_hold_stuck() -> None:
    """Sheet-tick hold must not hide the standing room pulse or lie about DSR."""
    screen = _CLIENT.AnsiScreen()
    filt = _CLIENT.FormHoldFilter()
    room = (
        b"Newhaven, Narrow Path\r\n"
        b"    This narrow path is fairly plain.\r\n"
        b"Also here: Robald.\r\n"
        b"Obvious exits: east, west\r\n"
        b"[HP=40]: "
    )
    _CLIENT.paint_mud(screen, room, hold=False, filt=filt)
    screen.form_saving = True
    screen.form_cursor = (4, 19)
    assert _CLIENT.chrome_row(screen) == 26
    assert not _CLIENT.form_hold_now(screen, b"\x1b[24;1H\x1b[K", sheet_lock=True)
    tick = (
        b"\r\n"
        b"Also here: Robald, Ryan.\r\n"
        b"You notice a lantern here.\r\n"
        b"Obvious exits: east, west\r\n"
        b"[HP=40]: \x1b[6n"
    )
    _CLIENT.paint_mud(screen, tick, hold=True, filt=filt)
    blob = screen.text()
    assert "This narrow path" in blob
    assert "Also here: Robald, Ryan." in blob
    assert "You notice a lantern here." in blob
    assert "Obvious exits: east, west" in blob
    assert "[HP=40]" in blob
    dsr = [r for r in screen.replies if r.endswith(b"R")]
    assert dsr
    assert not dsr[-1].startswith(b"\x1b[20;")
    assert not screen.form_saving


def test_sheet_lock_alone_does_not_suppress_short_look() -> None:
    """Leftover F11 lock on a blank / post-SAVE grid must let `l` and ticks through."""
    screen = _CLIENT.AnsiScreen()
    screen.close_form()
    assert screen.needs_resume
    assert not _CLIENT.form_frozen(screen, sheet_lock=True)
    assert not _CLIENT.form_blocks_line(b"l\r", frozen=_CLIENT.form_frozen(screen, True))
    assert not _CLIENT.form_hold_now(
        screen, b"\x1b[24;1H\x1b[K", sheet_lock=True, in_realm=False
    )
    filt = _CLIENT.FormHoldFilter()
    # Simulate a prior stuck hold that parked CUP/EL, then a free realm paint.
    filt.filter(b"\x1b[24;1H\x1b[K")
    assert filt._pending
    tick = (
        b"Also here: Robald.\r\n"
        b"Obvious exits: east\r\n"
        b"[HP=40]: \x1b[6n"
    )
    _CLIENT.paint_mud(screen, tick, hold=False, filt=filt, in_realm=True)
    blob = screen.text()
    assert "Also here: Robald." in blob
    assert "Obvious exits: east" in blob
    assert "[HP=40]" in blob
    assert not filt._pending
    # Given Name + lock still owns the form (title scrolled off).
    named = _CLIENT.AnsiScreen()
    named.feed(b"Given Name   klymacks\r\n\x1b[25;1H[HP=28]: ")
    assert _CLIENT.form_frozen(named, True)
    assert _CLIENT.form_blocks_line(b"l\r", frozen=True)


def test_leftover_sheet_titles_do_not_suppress_room_tick() -> None:
    """Exit:SAVE leftover without a live FSD snap must still paint Also here."""
    screen = _CLIENT.AnsiScreen()
    filt = _CLIENT.FormHoldFilter()
    # Simulate titles left on the grid after a bad close (no form_snap).
    screen.feed(b"Exit: SAVE your character\r\nGiven Name   Rita\r\n")
    assert screen.form_snap is None
    assert not _CLIENT._live_fsd_session(screen)
    assert not _CLIENT.form_hold_now(
        screen,
        b"\x1b[25;1H\x1b[K[HP=40]: \x1b[6n",
        sheet_lock=True,
        in_realm=True,
    )
    tick = (
        b"\x1b[24;1H\x1b[KAlso here: Curtis.\r\n"
        b"Obvious exits: west\r\n"
        b"[HP=40]: \x1b[6n"
    )
    screen.replies.clear()
    _CLIENT.paint_mud(screen, tick, hold=True, filt=filt, in_realm=True)
    _CLIENT.ensure_realm_dsr(screen, tick, in_realm=True)
    blob = screen.text()
    assert "Also here: Curtis." in blob
    assert "Obvious exits: west" in blob
    dsr = [r for r in screen.replies if r.endswith(b"R")]
    assert dsr
    assert dsr[-1] == f"\x1b[{screen.cy + 1};{screen.cx + 1}R".encode("ascii")


def test_hp_prompt_alone_is_realm_not_sheet() -> None:
    """Exits can scroll off. [HP=] on the grid is still the mud, not FSD."""
    screen = _CLIENT.AnsiScreen()
    screen.feed(b"Newhaven, Narrow Path\r\n    A path.\r\n[HP=40]: ")
    assert _CLIENT._realm_on_grid(screen)
    assert not _CLIENT.form_frozen(screen, True)
    assert _CLIENT.realm_thaws_sheet(screen)
    assert not _CLIENT.form_hold_now(
        screen,
        b"\x1b[25;1H\x1b[K[HP=40]: \x1b[6n",
        sheet_lock=True,
        in_realm=True,
    )
    sheet = _CLIENT.AnsiScreen()
    sheet.feed(
        b"TRAIN STATS\r\nGiven Name   Robald\r\nExit: SAVE\r\n[HP=40]: "
    )
    assert not _CLIENT._realm_on_grid(sheet)
    assert _CLIENT.form_frozen(sheet)
    assert not _CLIENT.realm_thaws_sheet(sheet)
    assert not _CLIENT.form_hold_now(
        sheet, b"[HP=40]: \x1b[6n", sheet_lock=False, in_realm=True
    )


def test_idle_hp_dsr_reports_real_caret_after_tick() -> None:
    """After Also here + prompt, CSI 6n must CPR the real caret — not ``25;1R``."""
    screen = _CLIENT.AnsiScreen()
    filt = _CLIENT.FormHoldFilter()
    screen.feed(b"Newhaven, Narrow Path\r\n    A path.\r\n[HP=40]: ")
    screen.form_cursor = (4, 19)
    screen.cx, screen.cy = 4, 19
    tick = (
        b"\x1b[24;1H\x1b[KAlso here: Ryan.\r\n"
        b"Obvious exits: west\r\n"
        b"[HP=40]: \x1b[6n"
    )
    _CLIENT.paint_mud(screen, tick, hold=True, filt=filt, in_realm=True)
    _CLIENT.ensure_realm_dsr(screen, tick, in_realm=True)
    assert "Also here: Ryan." in screen.text()
    dsr = [r for r in screen.replies if r.endswith(b"R")]
    assert dsr
    # Must not stay on the leftover form caret row 20; must not force col 1.
    assert not dsr[-1].startswith(b"\x1b[20;")
    row = int(dsr[-1].decode().split(";")[0].lstrip("\x1b["))
    col = int(dsr[-1].decode().rstrip("R").split(";")[1])
    assert row == screen.cy + 1
    assert col == screen.cx + 1


def test_realm_dsr_reports_mid_screen_hp_caret() -> None:
    """Combat mid-screen [HP=]+6n reports that caret — no forced ``25;1R``."""
    screen = _CLIENT.AnsiScreen()
    filt = _CLIENT.FormHoldFilter()
    screen.feed(b"\x1b[12;1H[HP=40]: ")
    screen.cx, screen.cy = 8, 11
    tick = b"\x1b[12;1H[HP=40]: \x1b[6n"
    _CLIENT.paint_mud(screen, tick, hold=False, filt=filt, in_realm=True)
    _CLIENT.ensure_realm_dsr(screen, tick, in_realm=True)
    dsr = [r for r in screen.replies if r.endswith(b"R")]
    assert dsr
    assert dsr[-1] == f"\x1b[{screen.cy + 1};{screen.cx + 1}R".encode("ascii")
    assert dsr[-1] != b"\x1b[25;1R"


def test_ensure_realm_dsr_answers_6n_even_without_feed_reply() -> None:
    screen = _CLIENT.AnsiScreen()
    screen.cx, screen.cy = 9, 24
    payload = b"[HP=50/MA=12]: \x1b[6n"
    _CLIENT.ensure_realm_dsr(screen, payload, in_realm=True)
    assert screen.replies == [b"\x1b[25;10R"]


def test_split_idle_look_after_hp_prompt() -> None:
    """CUP+[HP=] then a later Also here packet must still reprint the room."""
    screen = _CLIENT.AnsiScreen()
    filt = _CLIENT.FormHoldFilter()
    _CLIENT.paint_mud(
        screen,
        b"Newhaven, Narrow Path\r\n"
        b"Also here: Robald.\r\n"
        b"Obvious exits: west\r\n"
        b"[HP=40]: ",
        hold=False,
        filt=filt,
    )
    _CLIENT.paint_mud(screen, b"\x1b[25;1H\x1b[K[HP=40]: ", hold=True, filt=filt)
    _CLIENT.paint_mud(
        screen,
        b"Also here: Robald, Ryan.\r\nObvious exits: west\r\n",
        hold=True,
        filt=filt,
    )
    assert "Robald, Ryan" in screen.text()
    assert "Obvious exits: west" in screen.text()


def test_creation_save_hp_closes_sheet() -> None:
    screen = _CLIENT.AnsiScreen()
    filt = _CLIENT.FormHoldFilter()
    _CLIENT.paint_mud(
        screen,
        b"\x1b[1;1HM A J O R  M U D Character Creation\r\n"
        b"Given Name   Klymacks\r\nExit: SAVE\r\n",
        hold=True,
        filt=filt,
    )
    _CLIENT.paint_mud(
        screen,
        b"\x1b[25;1H\x1b[K[HP=25]: \x1b[6n",
        hold=True,
        filt=filt,
    )
    blob = screen.text()
    assert "Character Creation" not in blob
    assert "Given Name" not in blob
    assert "[HP=" not in blob
    assert any(r.endswith(b"R") for r in screen.replies)
    assert screen.needs_resume
    assert _CLIENT.sheet_needs_resume(True, False, b"\x1b[25;1H[HP=25]: ")
    assert not _CLIENT.sheet_needs_resume(
        True, False, b"Obvious exits: south\r\n"
    )
    assert _CLIENT.realm_needs_look(screen)
    state = WorldState()
    state.in_realm = True
    state.hp = 25
    bar = _CLIENT.chrome(
        30, screen, "x", "127.0.0.1", state, Brain(allowed=True, klass="ninja")
    )
    assert b"Enter" in bar
    assert b"refreshing the room" in bar


def test_note_form_enter_marks_save() -> None:
    screen = _CLIENT.AnsiScreen()
    screen.feed(
        b"\x1b[1;1HTRAIN STATS\r\nGiven Name   klymacks\r\n"
        b"Strength     (40   to 100 )  50\r\nExit: SAVE\r\n"
    )
    for y in range(screen.rows):
        if "Strength" in screen.line(y):
            screen.cy = y
            break
    _CLIENT.note_form_enter(screen, b"\r")
    assert not screen.form_saving
    for y in range(screen.rows):
        if "SAVE" in screen.line(y):
            screen.cy = y
            break
    _CLIENT.note_form_enter(screen, b"\r")
    assert screen.form_saving


def test_train_stats_save_hp_closes_sheet() -> None:
    """Enter on SAVE, then idle HP — wipe TRAIN STATS and drop train hold."""
    screen = _CLIENT.AnsiScreen()
    filt = _CLIENT.FormHoldFilter()
    _CLIENT.paint_mud(
        screen,
        b"\x1b[1;1HTRAIN STATS\r\n"
        b"Given Name   klymacks\r\nExit: SAVE\r\n",
        hold=True,
        filt=filt,
    )
    for y in range(screen.rows):
        if "SAVE" in screen.line(y):
            screen.cy = y
            break
    brain = Brain(allowed=True, klass="ninja")
    brain.begin_train_hold()
    assert _CLIENT.play_paused(brain)
    _CLIENT.note_form_enter(screen, b"\r")
    assert screen.form_saving
    brain.cancel_train()
    assert not brain.train_holding()
    assert not _CLIENT.play_paused(brain, frozen=False)
    _CLIENT.paint_mud(
        screen,
        b"\x1b[25;1H\x1b[K[HP=25]: \x1b[6n",
        hold=True,
        filt=filt,
    )
    blob = screen.text()
    assert "TRAIN STATS" not in blob
    assert "Given Name" not in blob
    assert "[HP=" not in blob
    assert screen.needs_resume
    assert not screen.looks_like_creation()
    assert _CLIENT.realm_needs_look(screen)
    _CLIENT.release_after_sheet(brain, was_sheet=True, now_sheet=False)
    assert not brain.train_holding()
    state = WorldState()
    state.in_realm = True
    state.hp = 25
    bar = _CLIENT.chrome(30, screen, "x", "127.0.0.1", state, brain)
    assert b"Enter" in bar
    assert b"refreshing the room" in bar
    assert b"TRAIN HOLD" not in bar


def test_release_after_sheet_keeps_hold_while_form_up() -> None:
    brain = Brain(allowed=True, klass="ninja")
    brain.begin_train_hold()
    _CLIENT.release_after_sheet(brain, was_sheet=True, now_sheet=True)
    assert brain.train_holding()
    _CLIENT.release_after_sheet(brain, was_sheet=False, now_sheet=False)
    assert brain.train_holding()


def test_form_hold_scrubs_hp_prompt_tail_after_save() -> None:
    """`[HP=25]:` with SGR in the middle used to leave `]:` under the parchment."""
    screen = _CLIENT.AnsiScreen()
    foot = "\\_________________________________\\___/"
    filt = _CLIENT.FormHoldFilter()
    _CLIENT.paint_mud(
        screen,
        b"\x1b[1;1HTRAIN STATS\r\n"
        b"Given Name   Klymacks\r\nExit: SAVE\r\n",
        hold=True,
        filt=filt,
    )
    _CLIENT.paint_mud(screen, b"\x1b[24;1H" + foot.encode(), hold=True, filt=filt)
    _CLIENT.paint_mud(
        screen,
        b"\x1b[25;1H\x1b[K[HP=25\x1b[0m]:",
        hold=True,
        filt=filt,
    )
    blob = screen.text()
    assert "Given Name   Klymacks" in blob
    assert "[HP=" not in blob
    assert screen.line(24).strip() not in {"]:", "25]:"}
    assert "]: " not in screen.line(24) and screen.line(24).strip() != "]:"


def test_form_hold_keeps_health_stat_field() -> None:
    raw = b"\x1b[12;5HHealth     (40   to 100 )  50"
    held = _CLIENT.form_hold_payload(raw)
    assert b"Health" in held
    assert b"(40" in held


def test_form_hold_drops_split_ma_prompt() -> None:
    """Idle [HP=/MA=] often splits; leftover `/MA=15]:` used to land on Intellect."""
    filt = _CLIENT.FormHoldFilter()
    first = filt.filter(b"\x1b[9;1H[HP=67")
    assert b"[HP=" not in first
    assert b"\x1b[9;1H" not in first
    second = filt.filter(b"/MA=15]:                   72\r\n")
    assert b"/MA=" not in second
    assert b"72" not in second
    field = filt.filter(b"\x1b[10;5HWillpower  (40   to 100 )  40")
    assert b"Willpower" in field
    glued = _CLIENT.form_hold_payload(b"\x1b[12;1H/MA=16]:             PALE-BLUE")
    assert b"/MA=" not in glued
    assert b"PALE-BLUE" not in glued


def test_train_stats_exit_shows_room_not_stars() -> None:
    """SAVE off TRAIN STATS: room reprint must paint letters, not stay filtered."""
    screen = _CLIENT.AnsiScreen()
    screen.feed(b"\x1b[1;1HTRAIN STATS\r\nGiven Name   Matt\r\n20 CP Left\r\n")
    screen.fg, screen.bold, screen.rev = 1, True, True
    assert screen.looks_like_creation()
    filt = _CLIENT.FormHoldFilter()
    room = (
        b"Graveyard, Tomb Entrance\r\n"
        b"You notice gravestone here.\r\n"
        b"Obvious exits: south, east, west\r\n"
        b"[HP=74/MA=16]: "
    )
    assert _CLIENT.form_returns_to_realm(room)
    shown = _CLIENT.paint_mud(screen, room, hold=True, filt=filt)
    assert b"Obvious exits:" in shown
    assert "Obvious exits" in screen.text()
    assert "Tomb Entrance" in screen.text()
    assert "TRAIN STATS" not in screen.text()
    assert "Given Name" not in screen.text()
    assert not screen.looks_like_creation()
    assert (screen.fg, screen.bg, screen.bold, screen.rev) == (7, 0, False, False)
    look = _CLIENT.paint_mud(
        screen,
        b"Also here: small zombie.\r\n",
        hold=False,
        filt=filt,
    )
    assert b"small zombie" in look
    assert "small zombie" in screen.text()


def test_ice_meter_dither() -> None:
    assert _CLIENT.ice_meter(None, 8) == "░░░░░░░░"
    assert _CLIENT.ice_meter(0.0, 8) == "░░░░░░░░"
    assert _CLIENT.ice_meter(1.0, 8) == "▓▓▓▓▓▓▓▓"
    mid = _CLIENT.ice_meter(0.5, 8)
    assert "▓" in mid and "░" in mid
    assert len(mid) == 8
    assert _CLIENT.visible_len(_CLIENT.ice_wash(12)) == 12
    assert _CLIENT.ice_wash(5, 0) != _CLIENT.ice_wash(5, 1)
    assert _CLIENT.visible_len(_CLIENT.ice_wedge()) == 3


def test_line_history_up_down() -> None:
    hist = _CLIENT.LineHistory()
    assert hist.up("") == ""
    hist.remember("look")
    hist.remember("")
    hist.remember("look")
    hist.remember("health")
    assert hist.up("draft") == "health"
    assert hist.up("health") == "look"
    assert hist.up("look") == "look"
    assert hist.down("look") == "health"
    assert hist.down("health") == "draft"
    assert hist.down("draft") == "draft"
    hist.remember("exp")
    assert hist.up("") == "exp"
    assert hist.up("exp") == "health"


def test_leftover_hp_keeps_creation_sheet() -> None:
    """Mid-sheet [HP=] leak must not drop FSD keys or paint the realm > bar."""
    screen = _CLIENT.AnsiScreen()
    screen.feed(b"\x1b[2J\x1b[1;1H")
    screen.feed(b"M A J O R  M U D Character Creation\r\n")
    screen.feed(b"Given Name   klymacks\r\n")
    screen.feed(b"Family Name  Urmom\r\n")
    screen.feed(b"Point Cost Chart\r\n")
    screen.feed(b"\x1b[10;1H[HP=30]:                         40")
    screen.feed(b"\x1b[20;1H20 CP Left\r\n")
    screen.feed(b"Exit: SAVE\r\n")
    state = WorldState()
    state.in_realm = True
    state.hp = 30
    state.max_hp = 30
    state.max_hp_known = True
    blob = screen.text()
    assert "Character Creation" in blob
    assert "Point Cost Chart" in blob
    assert "CP Left" in blob
    assert "Exit: SAVE" in blob
    assert "[HP=" in blob
    assert screen.looks_like_creation()
    assert not _CLIENT.use_local_input(screen, state)
    assert _CLIENT.form_frozen(screen)
    assert not _CLIENT.realm_thaws_sheet(screen)
    assert _CLIENT.status_line(screen, "127.0.0.1") == "character sheet"
    asked: list[str] = []
    assert not _CLIENT.maybe_ask_exp(
        state, asked.append, frozen=_CLIENT.form_frozen(screen)
    )
    assert asked == []
    sent: list[str] = []
    _CLIENT.maybe_auto_party(
        state,
        Brain(allowed=True, klass="ninja"),
        sent.append,
        invited=True,
        followed=False,
        frozen=_CLIENT.play_paused(
            Brain(allowed=True, klass="ninja"),
            frozen=_CLIENT.form_frozen(screen),
        ),
    )
    assert sent == []
    brain = Brain(allowed=True, klass="ninja")
    brain.mode = "hunt"
    brain.next_action = "att slime"
    bar = _CLIENT.chrome(30, screen, "x", "127.0.0.1", state, brain, typed="att")
    plain = _plain_bar(bar)
    assert "> att" not in plain
    assert _CLIENT.status_line(screen, "127.0.0.1") == "character sheet"


def test_realm_prompt_drops_sheet_mask() -> None:
    """Leftover TRAIN STATS / creation text must not star-mask the > bar."""
    screen = _CLIENT.AnsiScreen()
    screen.feed(b"\x1b[2J\x1b[1;1H")
    screen.feed(b"M A J O R  M U D Character Creation\r\n")
    screen.feed(b"Given Name   klymacks\r\n")
    screen.feed(b"Point Cost Chart\r\n")
    state = WorldState()
    state.in_realm = True
    state.hp = 28
    state.max_hp = 28
    state.max_hp_known = True
    assert screen.looks_like_creation()
    assert not _CLIENT.use_local_input(screen, state)
    assert _CLIENT.status_line(screen, "127.0.0.1") == "character sheet"

    screen.fg, screen.bold, screen.rev = 1, True, True
    # Leftover [HP=] on the live form is not EXIT.
    screen.feed(b"\x1b[24;1H[HP=28]: ")
    blob = screen.text()
    assert "Character Creation" in blob
    assert "Given Name" in blob
    assert "[HP=" in blob
    assert screen.looks_like_creation()
    assert not _CLIENT.use_local_input(screen, state)
    assert _CLIENT.status_line(screen, "127.0.0.1") == "character sheet"

    # Live EXIT: room chrome plus [HP=], even if form leftovers remain.
    screen.feed(b"\x1b[18;1HObvious exits: north, south\r\n")
    screen.feed(b"Also here: a giant rat\r\n")
    screen.feed(b"You notice a club.\r\n")
    screen.feed(b"\x1b[24;1H[HP=28]: ")
    assert not screen.looks_like_creation()
    assert _CLIENT.use_local_input(screen, state)
    screen.leave_form()
    assert (screen.fg, screen.bg, screen.bold, screen.rev) == (7, 0, False, False)
    assert _CLIENT.status_line(screen, "127.0.0.1") == ""

    brain = Brain(allowed=True, klass="ninja")
    bar = _CLIENT.chrome(30, screen, "", "127.0.0.1", state, brain, typed="north")
    plain = _plain_bar(bar)
    assert "> north" in plain
    assert "*****" not in plain
    assert "in the realm" not in plain
    assert _CLIENT.realm_bar_text("north") == "north"

    train = _CLIENT.AnsiScreen()
    train.feed(b"\x1b[1;1HTRAIN STATS\r\nGiven Name\r\n")
    assert train.looks_like_creation()
    assert not _CLIENT.use_local_input(train, state)
    train.feed(b"\x1b[25;1H[HP=28]: ")
    assert train.looks_like_creation()
    assert not _CLIENT.use_local_input(train, state)
    train.feed(b"\x1b[22;1HObvious exits: west\r\n")
    assert not train.looks_like_creation()
    assert _CLIENT.use_local_input(train, state)

    race = _CLIENT.AnsiScreen()
    race.feed(b"\x1b[1;1HChoose a race:\r\n  1. Human\r\n  2. Dwarf\r\n")
    assert race.looks_like_creation()
    assert not _CLIENT.use_local_input(race, WorldState())
    race.feed(b"\x1b[25;1H[HP=28]: ")
    assert not race.looks_like_creation()


def test_race_pick_keeps_cursor_on_prompt() -> None:
    """Digits for [13] Gaunt One stay on the help prompt, not the list."""
    screen = _CLIENT.AnsiScreen()
    filt = _CLIENT.FormHoldFilter()
    names = (
        "Human",
        "Dwarf",
        "Gnome",
        "Halfling",
        "Elf",
        "Half-Elf",
        "Dark-Elf",
        "Half-Orc",
        "Goblin",
        "Half-Ogre",
        "Kang",
        "Nekojin",
        "Gaunt One",
    )
    blob = bytearray(b"\x1b[1;1HPlease choose a race from the following list\r\n")
    for i, name in enumerate(names, 1):
        blob += f"[{i}] {name}\r\n".encode()
    _CLIENT.paint_mud(screen, bytes(blob), hold=True, filt=filt)
    _CLIENT.paint_mud(
        screen,
        b"\x1b[24;1HPlease choose your race [ ? for help ] : ",
        hold=True,
        filt=filt,
    )
    assert screen.looks_like_creation()
    assert screen.cy >= 23
    _CLIENT.paint_mud(screen, b"1\x1b[6n", hold=True, filt=filt)
    assert screen.cy >= 23
    assert "1" in screen.line(23)
    kang = next(y for y in range(screen.rows) if "Kang" in screen.line(y))
    assert screen.cy != kang
    dsr = [r for r in screen.replies if r.endswith(b"R")]
    assert dsr
    assert dsr[-1].startswith(b"\x1b[24;") or dsr[-1].startswith(b"\x1b[25;")
    _CLIENT.paint_mud(screen, b"3", hold=True, filt=filt)
    assert "13" in screen.line(23)
    assert "Kang" in screen.line(kang)
    assert screen.line(kang).strip().startswith("[11]")


def test_race_enter_does_not_restore_list() -> None:
    """Enter on the race prompt must show class, not snap the race list back."""
    screen = _CLIENT.AnsiScreen()
    filt = _CLIENT.FormHoldFilter()
    _CLIENT.paint_mud(
        screen,
        b"\x1b[1;1HPlease choose a race from the following list\r\n"
        b"[1] Human\r\n[13] Gaunt One\r\n"
        b"\x1b[24;1HPlease choose your race [ ? for help ] : 13",
        hold=True,
        filt=filt,
    )
    assert "Gaunt One" in screen.text()
    assert not _CLIENT.freeze_on_parchment(screen)
    _CLIENT.paint_mud(
        screen,
        b"\x1b[2J\x1b[1;1HPlease choose a class from the following list\r\n"
        b"[1] Warrior\r\n"
        b"\x1b[24;1HPlease choose your class [ ? for help ] : ",
        hold=True,
        filt=filt,
    )
    blob = screen.text()
    assert "Warrior" in blob
    assert "choose a class" in blob.lower()
    assert "Gaunt One" not in blob


def test_train_stats_still_freezes_parchment() -> None:
    screen = _CLIENT.AnsiScreen()
    screen.feed(b"\x1b[1;1HTRAIN STATS\r\nGiven Name   klymacks\r\nExit: SAVE\r\n")
    assert _CLIENT.freeze_on_parchment(screen)
    race = _CLIENT.AnsiScreen()
    race.feed(
        b"Please choose a race from the following list\r\n"
        b"[13] Gaunt One\r\n"
        b"Please choose your race [ ? for help ] : 13"
    )
    assert race.looks_like_creation()
    assert not _CLIENT.freeze_on_parchment(race)


def test_creation_sheet_keeps_save_through_suicide_banner() -> None:
    """Sysop suicide / name-check lines must not squash Exit: SAVE."""
    screen = _CLIENT.AnsiScreen()
    filt = _CLIENT.FormHoldFilter()
    sheet = (
        b"\x1b[1;1HM A J O R  M U D Character Creation\r\n"
        b"Given Name   Robald\r\n"
        b"Family Name  Rare\r\n"
        b"Race         Gaunt One\r\n"
        b"Class        Mystic\r\n"
        b"Point Cost Chart\r\n"
        b"Exit: SAVE\r\n"
    )
    assert _CLIENT._payload_is_parchment(sheet)
    _CLIENT.paint_mud(screen, sheet, hold=True, filt=filt)
    assert "Exit: SAVE" in screen.text()
    leak = (
        b"these commands have been password protected.  You have not yet entered\r\n"
        b"your suicide password, so please do so soon using the\r\n"
        b"SET SUICIDE command.\r\n"
        b"Please wait - Validating your name.\r\n"
    )
    _CLIENT.paint_mud(screen, leak, hold=True, filt=filt)
    blob = screen.text()
    assert "Exit: SAVE" in blob
    assert "Given Name   Robald" in blob
    assert "Gaunt One" in blob
    assert "suicide" not in blob.lower()
    assert "Validating your name" not in blob
    assert "password protected" not in blob
    assert "these commands" not in blob
    mid = _CLIENT.AnsiScreen()
    filt2 = _CLIENT.FormHoldFilter()
    _CLIENT.paint_mud(mid, sheet, hold=True, filt=filt2)
    _CLIENT.paint_mud(
        mid,
        b"these commands have been password protected.",
        hold=True,
        filt=filt2,
    )
    assert "Exit: SAVE" in mid.text()
    assert "password protected" not in mid.text()


def test_creation_sheet_matches_held_suicide_wrap() -> None:
    """F10 grab: leak started on Exit: SAVE and wrapped the last rows."""
    screen = _CLIENT.AnsiScreen()
    filt = _CLIENT.FormHoldFilter()
    sheet = (
        b"\x1b[1;1HM A J O R  M U D Character Creation\r\n"
        b"Point Cost Chart\r\n"
        b"\x1b[21;1H  | >> Exit: SAVE                     << |\n"
    )
    _CLIENT.paint_mud(screen, sheet, hold=True, filt=filt)
    assert "Exit: SAVE" in screen.line(20)
    leak = (
        b"\x1b[21;18Hthese commands  0\r\n"
        b"have been password protected.  You have not yet entered\r\n"
        b"your suicide password, so please do so soon using the\r\n"
        b"SET SUICIDE command.\r\n"
        b"Please wait - Validating your name.roll\r\n"
    )
    _CLIENT.paint_mud(screen, leak, hold=True, filt=filt)
    blob = screen.text()
    assert "Exit: SAVE" in screen.line(20)
    assert "these commands" not in screen.line(20)
    assert "suicide" not in blob.lower()
    assert "Validating your name" not in blob
    assert ".roll" not in blob
    assert "password protected" not in blob
    sav = _CLIENT.AnsiScreen()
    filt3 = _CLIENT.FormHoldFilter()
    _CLIENT.paint_mud(sav, sheet, hold=True, filt=filt3)
    _CLIENT.paint_mud(sav, b"\x1b[21;16HSAV\\, these commands  0", hold=True, filt=filt3)
    assert "Exit: SAVE" in sav.line(20)
    assert "these commands" not in sav.line(20)


def test_creation_sheet_save_survives_legend_on_exit_row() -> None:
    """Live FSD puts `SAVE your character or EXIT` on the Exit row.

    Mid-field `SAV\\, these commands` is a wound even when the legend still
    contains SAVE. Heal must use the field between `Exit:` and `«`, not the
    legend. The legend may stay on that row.
    """
    legend = (
        "  | >> Exit: SAVE                     << | ------- SAVE your character or EXIT"
    )
    wounded = (
        "  | >> Exit: SAV\\,                     << | ------- SAVE your character or EXIT"
    )
    assert _CLIENT._exit_row_wounded(legend, wounded)
    assert _CLIENT._exit_field_wounded(wounded)
    assert not _CLIENT._exit_field_wounded(legend)
    toggled = legend.replace("Exit: SAVE", "Exit: EXIT", 1)
    assert not _CLIENT._exit_row_wounded(legend, toggled)

    screen = _CLIENT.AnsiScreen()
    filt = _CLIENT.FormHoldFilter()
    sheet = (
        b"\x1b[1;1HM A J O R  M U D Character Creation\r\n"
        b"Point Cost Chart\r\n"
        b"\x1b[21;1H" + legend.encode("ascii") + b"\n"
    )
    _CLIENT.paint_mud(screen, sheet, hold=True, filt=filt)
    assert "Exit: SAVE" in screen.line(20)
    _CLIENT.paint_mud(
        screen,
        b"\x1b[21;16HSAV\\, these commands  0\r\n"
        b"have been password protected.  You have not yet entered\r\n"
        b"your suicide password, so please do so soon using the\r\n"
        b"SET SUICIDE command.\r\n"
        b"Please wait - Validating your name.roll\r\n",
        hold=True,
        filt=filt,
    )
    blob = screen.text()
    field = _CLIENT._exit_field_value(screen.line(20))
    assert "SAVE" in field.upper()
    assert "SAV\\" not in screen.line(20)
    assert "these commands" not in screen.line(20)
    assert "suicide" not in blob.lower()
    assert "Validating your name" not in blob
    assert ".roll" not in blob


def _hold_dump_rows() -> list[str]:
    rows = (_ROOT / "data" / "screen-hold.txt").read_text(encoding="utf-8").splitlines()
    assert len(rows) >= 24
    return [(row + " " * 80)[:80] for row in rows[:25]]


def _parchment_fixture_rows() -> list[str]:
    """Exit: SAVE row when the live F10 grab is a room, not the sheet."""
    rows = [" " * 80 for _ in range(25)]
    rows[0] = "M A J O R  M U D Character Creation".ljust(80)[:80]
    rows[2] = "Given Name   klymacks".ljust(80)[:80]
    rows[3] = "Point Cost Chart".ljust(80)[:80]
    rows[19] = (
        "  | >> Exit: SAVE                  « | ------- SAVE your character or EXIT"
    ).ljust(80)[:80]
    return rows


def _sheet_dump_rows() -> list[str]:
    rows = _hold_dump_rows()
    if any("Exit:" in r for r in rows):
        return rows
    return _parchment_fixture_rows()


def _cup_grid(rows: list[str]) -> bytes:
    out = bytearray()
    for i, row in enumerate(rows[:25], start=1):
        out += f"\x1b[{i};1H".encode("ascii") + row.encode("cp437")
    return bytes(out)


def _healthy_hold_rows(rows: list[str]) -> list[str]:
    out = list(rows)
    line = out[19]
    exit_at = line.find("Exit:")
    stop = line.find("«")
    assert exit_at >= 0 and stop > exit_at
    start = exit_at + 6
    cells = list(line)
    for i, ch in enumerate("SAVE"):
        cells[start + i] = ch
    for i in range(start + 4, stop):
        cells[i] = " "
    out[19] = "".join(cells)
    for i, row in enumerate(out):
        if i == 19 or "____" in row or "⌐┴" in row:
            continue
        low = row.lower()
        if (
            "to prevent accidental" in low
            or "accidental suicide" in low
            or "these commands" in low
            or "password protected" in low
            or "suicide password" in low
            or "validating your name" in low
        ):
            out[i] = " " * 80
    return out


def _assert_exit_save_intact(screen: object, *, want_legend: bool | None = None) -> None:
    row = next(screen.line(y) for y in range(screen.rows) if "Exit:" in screen.line(y))
    field = _CLIENT._exit_field_value(row)
    assert _CLIENT._exit_field_choice(row) == "SAVE", repr(field)
    assert "SAV\\" not in row
    assert "these commands" not in row
    blob = screen.text()
    assert "suicide" not in blob.lower()
    assert "Validating your name" not in blob
    assert ".roll" not in blob
    assert "password protected" not in blob
    assert "have been password" not in blob
    if want_legend is True:
        assert "SAVE your character or EXIT" in row
    elif want_legend is False:
        assert "SAVE your character or EXIT" not in row


def test_creation_sheet_replays_f10_hold_dump() -> None:
    """Replay the F10 grab. A wounded dump still heals SAVE; a live SAVE dump
    must not keep the sysop prevent-banner on the parchment.
    """
    dump = _hold_dump_rows()
    if not any("Exit:" in r for r in dump):
        return
    exit_row = next(r for r in dump if "Exit:" in r)
    overlay = (
        b"\x1b[20;14HSAV\\, these commands  0\r\n"
        b"have been password protected.  You have not yet entered\r\n"
        b"your suicide password, so please do so soon using the\r\n"
        b"SET SUICIDE command.\r\n"
        b"Please wait - Validating your name.roll\r\n"
    )
    if "SAV\\" in exit_row:
        wounded = dump[19]
        assert "these commands" in wounded
        assert "«" in wounded
        assert "SAVE your character or EXIT" in wounded
        assert _CLIENT._exit_field_wounded(wounded)
        assert "suicide" not in _CLIENT._exit_field_value(wounded).lower()
        assert _CLIENT._exit_field_choice(wounded) != "SAVE"
        healthy = _healthy_hold_rows(dump)
        exact = _CLIENT.AnsiScreen()
        _CLIENT.paint_mud(exact, _cup_grid(dump), hold=True, filt=_CLIENT.FormHoldFilter())
        _assert_exit_save_intact(exact, want_legend=True)
        first = _CLIENT.AnsiScreen()
        filt = _CLIENT.FormHoldFilter()
        _CLIENT.paint_mud(first, _cup_grid(dump), hold=False, filt=filt)
        _assert_exit_save_intact(first, want_legend=True)
        _CLIENT.paint_mud(first, overlay, hold=True, filt=filt)
        _assert_exit_save_intact(first, want_legend=True)
    else:
        live_dump = _CLIENT.AnsiScreen()
        _CLIENT.paint_mud(
            live_dump, _cup_grid(dump), hold=True, filt=_CLIENT.FormHoldFilter()
        )
        _assert_exit_save_intact(live_dump, want_legend=True)
        assert "To prevent accidental" not in live_dump.text()
        assert "\\_________________________________\\___/" in live_dump.text()
        assert "accidental suicide" not in live_dump.text().lower()
    healthy = _healthy_hold_rows(dump)

    live = _CLIENT.AnsiScreen()
    filt = _CLIENT.FormHoldFilter()
    _CLIENT.paint_mud(live, _cup_grid(healthy), hold=True, filt=filt)
    _assert_exit_save_intact(live, want_legend=True)
    _CLIENT.paint_mud(live, overlay, hold=True, filt=filt)
    _assert_exit_save_intact(live, want_legend=True)

    split = _CLIENT.AnsiScreen()
    filt = _CLIENT.FormHoldFilter()
    _CLIENT.paint_mud(split, _cup_grid(healthy), hold=True, filt=filt)
    _CLIENT.paint_mud(split, b"\x1b", hold=True, filt=filt)
    _CLIENT.paint_mud(split, b"[20;14H" + overlay[8:], hold=True, filt=filt)
    _assert_exit_save_intact(split, want_legend=True)

    mid = _CLIENT.AnsiScreen()
    filt = _CLIENT.FormHoldFilter()
    _CLIENT.paint_mud(mid, _cup_grid(healthy), hold=True, filt=filt)
    _CLIENT.paint_mud(mid, b"\x1b[20;14HSAV\\, th", hold=True, filt=filt)
    _CLIENT.paint_mud(
        mid,
        b"ese commands  0\r\n"
        b"have been password protected.  You have not yet entered\r\n"
        b"your suicide password, so please do so soon using the\r\n"
        b"Please wait - Validating your name.roll\r\n",
        hold=True,
        filt=filt,
    )
    _assert_exit_save_intact(mid, want_legend=True)

    roll = _CLIENT.AnsiScreen()
    filt = _CLIENT.FormHoldFilter()
    _CLIENT.paint_mud(roll, _cup_grid(healthy), hold=True, filt=filt)
    _CLIENT.paint_mud(roll, b"\x1b[24;1H.roll", hold=True, filt=filt)
    _assert_exit_save_intact(roll, want_legend=True)

    together = _CLIENT.AnsiScreen()
    _CLIENT.paint_mud(
        together,
        _cup_grid(healthy) + overlay,
        hold=True,
        filt=_CLIENT.FormHoldFilter(),
    )
    _assert_exit_save_intact(together, want_legend=True)


def test_creation_sheet_assembles_split_leak_heads() -> None:
    """TCP can split a wrap line so neither chunk starts with a leak head."""
    filt = _CLIENT.FormHoldFilter()
    assert filt.filter(b"have been pass") == b""
    assert filt.filter(b"word protected.\r\n") == b""
    assert filt.filter(b"your suicide pass") == b""
    assert filt.filter(b"word, so please do so soon\r\n") == b""

    screen = _CLIENT.AnsiScreen()
    filt = _CLIENT.FormHoldFilter()
    _CLIENT.paint_mud(
        screen, _cup_grid(_healthy_hold_rows(_sheet_dump_rows())), hold=True, filt=filt
    )
    _assert_exit_save_intact(screen, want_legend=True)
    shown = _CLIENT.paint_mud(
        screen, b"\x1b[22;1Hhave been pass", hold=True, filt=filt
    )
    assert b"have been pass" not in shown
    assert "have been pass" not in screen.text()
    _CLIENT.paint_mud(
        screen,
        b"word protected.  You have not yet entered\r\n"
        b"your suicide password, so please do so soon using the\r\n",
        hold=True,
        filt=filt,
    )
    _assert_exit_save_intact(screen, want_legend=True)


def test_creation_sheet_keeps_legend_when_leak_shares_exit_row() -> None:
    """One CUP packet writes `these commands` and the rest of the Exit row."""
    blank = (
        "  | >> Exit:                      << | ------- SAVE your character or EXIT"
    )
    assert _CLIENT._exit_field_wounded(blank)

    payload = (
        b"\x1b[1;1HM A J O R  M U D Character Creation\r\n"
        b"Point Cost Chart\r\n"
        b"\x1b[21;1H  | >> Exit: SAVE\n"
        b"\x1b[21;16Hthese commands  0  << | ------- SAVE your character or EXIT\n"
    )
    screen = _CLIENT.AnsiScreen()
    _CLIENT.paint_mud(screen, payload, hold=True, filt=_CLIENT.FormHoldFilter())
    _assert_exit_save_intact(screen, want_legend=True)
    assert "<<" in screen.line(20)

    raw = _CLIENT.AnsiScreen()
    _CLIENT.paint_mud(raw, payload, hold=False, filt=_CLIENT.FormHoldFilter())
    _assert_exit_save_intact(raw, want_legend=True)

    wounded_row = _sheet_dump_rows()[19]
    leak_at = wounded_row.find("these commands")
    stop = wounded_row.find("«")
    if leak_at < 0 or stop < 0 or leak_at >= stop:
        wounded_row = (
            "  | >> Exit: SAV\\, these commands  0  « | ------- SAVE your character or EXIT"
        )
        leak_at = wounded_row.find("these commands")
        stop = wounded_row.find("«")
    assert 0 <= leak_at < stop
    overlay = (
        f"\x1b[20;{leak_at + 1}H".encode("ascii")
        + wounded_row[leak_at:].encode("cp437")
    )
    healthy = _healthy_hold_rows(_sheet_dump_rows())
    live = _CLIENT.AnsiScreen()
    filt = _CLIENT.FormHoldFilter()
    _CLIENT.paint_mud(live, _cup_grid(healthy), hold=True, filt=filt)
    _CLIENT.paint_mud(live, overlay, hold=True, filt=filt)
    _assert_exit_save_intact(live, want_legend=True)

    split = _CLIENT.AnsiScreen()
    filt = _CLIENT.FormHoldFilter()
    _CLIENT.paint_mud(split, _cup_grid(healthy), hold=True, filt=filt)
    _CLIENT.paint_mud(
        split,
        f"\x1b[20;{leak_at + 1}Hthese comm".encode("ascii"),
        hold=True,
        filt=filt,
    )
    _CLIENT.paint_mud(
        split,
        b"ands  0  " + wounded_row[stop:].encode("cp437"),
        hold=True,
        filt=filt,
    )
    _assert_exit_save_intact(split, want_legend=True)


def test_first_parchment_packet_heals_save_without_snap() -> None:
    """Class list is still up; the first FSD chunk can include the suicide wrap."""
    screen = _CLIENT.AnsiScreen()
    filt = _CLIENT.FormHoldFilter()
    _CLIENT.paint_mud(
        screen,
        b"\x1b[1;1HPlease choose a class from the following list\r\n[1] Warrior\r\n",
        hold=False,
        filt=filt,
    )
    legend = (
        b"  | >> Exit: SAVE                     << | ------- SAVE your character or EXIT"
    )
    payload = (
        b"\x1b[1;1HM A J O R  M U D Character Creation\r\nPoint Cost Chart\r\n"
        b"\x1b[21;1H" + legend + b"\n"
        b"\x1b[21;16HSAV\\, these commands  0\r\n"
        b"your suicide password, so please do so soon using the\r\n"
        b"Please wait - Validating your name.roll\r\n"
    )
    assert _CLIENT._payload_is_parchment(payload)
    _CLIENT.paint_mud(screen, payload, hold=True, filt=filt)
    field = _CLIENT._exit_field_value(screen.line(20))
    blob = screen.text()
    assert "SAVE" in field.upper()
    assert "SAV\\" not in screen.line(20)
    assert "suicide" not in blob.lower()
    assert "Validating your name" not in blob


def test_creation_sheet_survives_clear() -> None:
    """A wipe must not drop the parchment snap — that was the squash."""
    screen = _CLIENT.AnsiScreen()
    filt = _CLIENT.FormHoldFilter()
    sheet = (
        b"\x1b[1;1HM A J O R  M U D Character Creation\r\n"
        b"Given Name   Robald\r\n"
        b"Point Cost Chart\r\n"
        b"\x1b[21;1HExit: SAVE\n"
    )
    _CLIENT.paint_mud(screen, sheet, hold=True, filt=filt)
    assert screen.form_snap is not None
    _CLIENT.paint_mud(screen, b"\x1b[2J\x1b[1;1H", hold=True, filt=filt)
    blob = screen.text()
    assert "Character Creation" in blob
    assert "Exit: SAVE" in blob
    assert "Given Name   Robald" in blob


if __name__ == "__main__":
    test_f2_sequences()
    test_f1_sequences()
    test_f3_to_f8_sequences()
    test_esc_o_waits()
    test_lone_esc_is_key_esc()
    test_peek_commands()
    test_peek_does_not_stop_hunter()
    test_peek_all_keys_queue()
    test_peek_skipped_outside_realm()
    test_peek_queues_behind_pending()
    test_realm_line_keeps_attack()
    test_f7_toggles_hunt()
    test_f2_is_look_not_hunt()
    test_f1_panic_party_break_only()
    test_f1_panic_leader_with_followers_stays()
    test_f1_panic_solo_pit_break_then_u()
    test_f1_panic_does_not_takeover_or_reaggro()
    test_esc_stops_hunt_and_clears_follow()
    test_f1_leader_hurt_does_not_pit_flee()
    test_f1_panic_matt_mortal_rescues()
    test_f1_party_tick_does_not_flee()
    test_f1_skipped_outside_realm()
    test_f8_toggles_ninja_stealth()
    test_f8_walk_to_ambush_looks_on_empty_road()
    test_f8_walk_to_ambush_pit_slime_looks()
    test_f8_ambush_to_walk_no_sn()
    test_f8_walk_to_ambush_manual_looks()
    test_f8_walk_to_ambush_sitting_looks_first()
    test_f9_toggles_auto_join()
    test_maybe_auto_party_manual_join()
    test_maybe_auto_party_mystic_midr_once()
    test_maybe_auto_party_join_off_no_hunt()
    test_maybe_auto_party_no_join_without_invite()
    test_maybe_auto_party_join_call()
    test_maybe_auto_party_sherry_invite_once()
    test_maybe_auto_party_skips_goto()
    test_f10_is_hold_does_not_takeover()
    test_f10_hold_outside_realm()
    test_f11_is_sheet_does_not_takeover()
    test_f12_starts_logoff_without_dumping_keys()
    test_logoff_walk_realm_then_mud_then_close()
    test_logoff_walk_confirm_is_y_not_x()
    test_logoff_walk_already_on_bbs_menu_sends_x()
    test_logoff_walk_stale_bbs_menu_still_quits_in_realm()
    test_logoff_walk_combat_breaks_then_quits()
    test_logoff_walk_matt_aa_sticky_combat_still_leaves()
    test_startup_splash_does_not_arm_logoff_or_cleanup()
    test_toggle_sheet_pauses_at_guild()
    test_toggle_sheet_walks_when_not_at_guild()
    test_toggle_sheet_unlocks_and_cancels_walk()
    test_toggle_sheet_cancels_walk_and_hold()
    test_sheet_lock_keeps_fsd_keys_with_leftover_hp()
    test_form_hold_drops_hp_keeps_fields()
    test_form_hold_drops_bottom_erase_then_hp()
    test_form_hold_drops_name_status_after_save()
    test_form_hold_scrubs_name_status_drawn_per_letter()
    test_form_hold_scrubs_bare_given_family_after_save()
    test_repair_bare_sgr_after_lost_esc()
    test_echoed_l_stays_on_userid()
    test_close_form_wipes_sheet_on_room()
    test_split_sgr_does_not_print_bare_codes()
    test_idle_also_here_does_not_wipe_room()
    test_form_hold_filter_does_not_eat_short_look_after_hp()
    test_form_hold_keeps_room_tick_cup_with_hp()
    test_form_hold_only_while_live_fsd()
    test_leftover_parchment_titles_do_not_rearm_form_snap()
    test_train_from_realm_holds_hp_and_keeps_form_cpr()
    test_realm_idle_short_look_paints_when_hold_stuck()
    test_sheet_lock_alone_does_not_suppress_short_look()
    test_leftover_sheet_titles_do_not_suppress_room_tick()
    test_hp_prompt_alone_is_realm_not_sheet()
    test_idle_hp_dsr_reports_real_caret_after_tick()
    test_realm_dsr_reports_mid_screen_hp_caret()
    test_ensure_realm_dsr_answers_6n_even_without_feed_reply()
    test_split_idle_look_after_hp_prompt()
    test_creation_save_hp_closes_sheet()
    test_note_form_enter_marks_save()
    test_train_stats_save_hp_closes_sheet()
    test_release_after_sheet_keeps_hold_while_form_up()
    test_form_hold_scrubs_hp_prompt_tail_after_save()
    test_form_hold_drops_split_ma_prompt()
    test_train_stats_exit_shows_room_not_stars()
    test_leftover_hp_keeps_creation_sheet()
    test_race_pick_keeps_cursor_on_prompt()
    test_race_enter_does_not_restore_list()
    test_train_stats_still_freezes_parchment()
    test_creation_sheet_keeps_save_through_suicide_banner()
    test_creation_sheet_matches_held_suicide_wrap()
    test_creation_sheet_save_survives_legend_on_exit_row()
    test_creation_sheet_replays_f10_hold_dump()
    test_creation_sheet_assembles_split_leak_heads()
    test_creation_sheet_keeps_legend_when_leak_shares_exit_row()
    test_first_parchment_packet_heals_save_without_snap()
    test_creation_sheet_survives_clear()
    test_creation_sheet_foot_hides_suicide_under_ice_chrome()
    test_hold_snapshot_writes_utf8_grid()
    test_copy_hold_clipboard_returns_bool()
    test_f8_paladin_toggles_aa()
    test_f8_aa_off_breaks_live_fight()
    test_letter_is_not_special()
    test_help_overlay_lists_keys()
    test_fkey_table_feeds_tip_and_hold()
    test_window_title_is_distinct()
    test_chrome_title_shows_who()
    test_paint_signoff_is_klymacks()
    test_signoff_clock_counts_down()
    test_await_any_key_hold_zero_returns()
    test_await_any_key_unblocks_on_space()
    test_board_logoff_is_goodbye_not_menu()
    test_paint_splash_is_graffiti()
    test_login_ans_replaces_mbbs_banner()
    test_login_ans_homes_to_prompt_dock()
    test_login_text_does_not_scroll_graffiti()
    test_login_notice_stays_under_splash()
    test_chrome_lists_new_map()
    test_validating_name_says_wait()
    test_sheet_chrome_lifts_wait_bars()
    test_name_taken_is_creation()
    test_sheet_chrome_tracks_stats_then_looks()
    test_save_field_footer_is_not_name_entry()
    test_save_wait_shows_ice_throbber_not_name_tip()
    test_train_stats_noop_save_skips_wait_throbber()
    test_stat_reject_shows_in_chrome_tip()
    test_footer_leak_does_not_stick_save_on_given_name()
    test_creation_tip_unknown_toon_spends_cp()
    test_form_hold_captures_prevent_banner()
    test_prevent_banner_moves_under_sheet()
    test_autopilot_starts_bbs_signup_when_unknown()
    test_autopilot_stops_when_already_logged_in()
    test_autopilot_does_not_type_sysop_at_bbs_menu()
    test_autopilot_skips_wg_userid_before_board_m()
    test_autopilot_answers_wg_userid_after_m()
    test_autopilot_worldgroup_userid_prompt()
    test_ttype_prefers_ansi_bbs()
    test_ansi_dsr_6n_replies_cursor()
    test_autopilot_bbs_only_stays_on_menu()
    test_autopilot_queues_m_during_password_cooldown()
    test_autopilot_enters_realm_when_auto_play_is_off()
    test_auto_play_cli_distinct_from_no_auto()
    test_auto_play_cli_overrides_json()
    test_autopilot_does_not_type_m_at_majormud_prompt()
    test_autopilot_sends_board_m_once()
    test_login_pager_nonstop_matches_disconnect_text()
    test_autopilot_nonstop_on_disconnect_pager()
    test_autopilot_e_then_nonstop_from_hold_screen()
    test_autopilot_leaves_typed_continue_on_pager()
    test_autopilot_does_not_nonstop_in_game_more()
    test_key_gap_is_slow_enough_for_majormud()
    test_realm_gate_holds_auto_play_after_first_prompt()
    test_realm_gate_resets_on_character_sheet()
    test_play_select_timeout_wakes_realm()
    test_realm_hp_thaws_leftover_sheet_lock()
    test_action_pry_skips_follow_and_backrank()
    test_action_pry_skips_buy_sell_wear()
    test_action_pry_skips_settle_and_look()
    test_action_pry_shop_vague_does_not_eat_buy()
    test_action_pry_read_vague_asks_for_full_name()
    test_action_pry_never_inv_after_a_walk()
    test_drop_stray_keys_during_login_not_on_sheet()
    test_f7_does_not_arm_hunt_before_realm()
    test_board_menu_screen_top_and_majormud()
    test_top_menu_clears_stale_in_realm()
    test_leftover_board_chrome_keeps_in_realm()
    test_top_menu_peeks_and_f7_do_not_push_mud()
    test_top_menu_enter_is_not_look()
    test_top_menu_brain_tick_sends_nothing()
    test_top_menu_chrome_keeps_mail_and_doors()
    test_f8_skipped_outside_realm()
    test_chrome_tips_fit()
    test_chrome_invite_tip()
    test_paint_mud_keeps_disband_party_lines()
    test_play_paused_is_train_hold_not_copy()
    test_chrome_hp_tone()
    test_chrome_hp_bar_uses_hits_not_health_stat()
    test_handle_client_line_train()
    test_handle_client_line_spell_yn()
    test_handle_client_line_gear_yn()
    test_handle_client_line_aa()
    test_handle_client_line_hunt_run()
    test_handle_client_line_goto()
    test_handle_client_line_boost()
    test_handle_client_line_stash()
    test_handle_client_line_invite_all()
    test_handle_client_line_join_call()
    test_handle_client_line_rest_heal_call()
    test_handle_client_line_run()
    test_maybe_ask_exp_once()
    test_hunt_ticks_do_not_resend_exp()
    test_maybe_ask_stat_once()
    test_chrome_shows_exp_and_train()
    test_chrome_hides_hp_on_train_stats()
    test_chrome_splits_status_and_stats()
    test_chrome_shows_hp_and_ma_over_max()
    test_chrome_splits_attack_defense()
    test_ice_meter_dither()
    test_line_history_up_down()
    test_realm_prompt_drops_sheet_mask()
    print("ok")
