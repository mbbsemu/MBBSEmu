from __future__ import annotations

import importlib.util
import tempfile
import time
from pathlib import Path

from client.brain import Brain
from client.parse import parse_line
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
    pal = _CLIENT.KeyPacer(paladin=True)
    pal.push_text("attack giant rat")
    assert _next_cmd(pal, 1.0) == b"aa giant rat\r"
    pacer = _CLIENT.KeyPacer()
    pacer.push_text("attack acid slime")
    assert _next_cmd(pacer, 1.0) == b"att acid slime\r"
    pacer.push_text("M", wipe=False)
    assert pacer.take(3.0) == b"M\r"


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
    state = WorldState()
    state.in_realm = True
    state.apply({"kind": "invited", "name": "Matt"})
    sent: list[str] = []
    _CLIENT.maybe_auto_party(
        state, brain, sent.append, invited=True, followed=False
    )
    assert sent == ["follow Matt"]
    state.apply({"kind": "following", "name": "Matt"})
    _CLIENT.maybe_auto_party(
        state, brain, sent.append, invited=False, followed=True
    )
    assert sent == ["follow Matt", "backr"]
    assert brain.mode == "hunt"


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
    assert not pacer.pending()
    assert walk.done
    assert walk.hint == _CLIENT.LogoffWalk.FAREWELL
    assert "Y" not in walk.hint
    assert "thanks for calling" not in walk.hint.lower()


def test_logoff_walk_already_on_bbs_menu_just_closes() -> None:
    brain = Brain(allowed=True)
    pacer = _CLIENT.KeyPacer()
    walk = _CLIENT.LogoffWalk()
    walk.start(brain, 1.0)
    walk.tick("Make your selection: ", in_realm=False, pacer=pacer, now=1.1)
    assert not pacer.pending()
    assert walk.done


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
    empty_given = _CLIENT.window_title({"given": "", "username": "klymacks"})
    assert kly == "Finn's Realm — klymacks"
    assert sysop == "Finn's Realm — klymacks"
    assert matt == "Finn's Realm — Matt"
    assert empty_given == "Finn's Realm — klymacks"
    assert sysop != matt
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
    screen = _CLIENT.AnsiScreen()
    _CLIENT.paint_signoff(screen)
    text = screen.text()
    assert screen.rows == 25
    assert screen.cols == 80
    assert "klymacks" in text
    assert "KLYMACKS" not in text
    assert "Klymacks" not in text
    assert "sysop" in text
    assert "yours" in text and "truly" in text
    assert "the gate closes." in text
    assert "still here." in text
    assert "thanks for calling" not in text.lower()
    ice = [c for row in screen.buf for c in row if c.ch in "█▄▀▓▒░■"]
    assert len(ice) >= 40
    assert {c.fg for c in ice} <= {0, 4, 6, 7}
    assert 2 not in {c.fg for row in screen.buf for c in row}


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


def test_key_gap_is_slow_enough_for_majormud() -> None:
    assert 0.4 <= _CLIENT.KEY_GAP <= 0.8
    assert 1.5 <= _CLIENT.WALK_GAP <= 3.0
    assert _CLIENT.FLOOD_PAUSE >= 4.0
    assert _CLIENT.REALM_SETTLE >= 5.0
    pacer = _CLIENT.KeyPacer()
    pacer.push_text("e")
    assert pacer.take(10.0) == b"e\r"
    assert pacer._ready_at == 10.0 + _CLIENT.WALK_GAP
    pacer.push_text("att rat")
    assert pacer.take(10.0 + _CLIENT.WALK_GAP) == b"att rat\r"
    assert pacer._ready_at == 10.0 + _CLIENT.WALK_GAP + _CLIENT.KEY_GAP


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


def test_action_pry_shop_vague_blocks_buy() -> None:
    pry = _CLIENT.ActionPry()
    pry.note_send("buy sword", 1)
    pry.note_shop_vague()
    assert pry.blocks("buy sword")
    assert not pry.blocks("look")
    state = WorldState()
    state.in_realm = True
    state.prompt_seq = 2
    sent: list[str] = []
    assert not pry.maybe_send(state, sent.append)
    pry.note_send("buy longsword", 2)
    assert not pry.blocks("buy longsword")
    state.prompt_seq = 3
    assert not pry.maybe_send(state, sent.append)
    assert sent == []


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


def test_chrome_tips_fit() -> None:
    hunt = _CLIENT.realm_fkey_tip(hunting=True, ambush="ambush", join="join")
    idle = _CLIENT.realm_fkey_tip(hunting=False, ambush="walk", join="join")
    paladin = _CLIENT.realm_fkey_tip(hunting=True, ambush="aa", join="join")
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
    assert "F10 copy" in hunt
    assert "F10 copy" in idle_off
    assert "F10 held" in _CLIENT.realm_fkey_tip(
        hunting=False, ambush="walk", join="join off", held=True
    )


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
    assert brain.mode == "manual"
    kind, mud = _CLIENT.handle_client_line("goto ts", brain, state)
    assert kind == "goto" and mud is None
    assert brain.mode == "goto"
    assert brain.goto_goal == "square"
    assert brain.hunting()
    kind, mud = _CLIENT.handle_client_line("goto gy", brain, state)
    assert brain.goto_goal == "graveyard"
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
    locked = _plain_bar(
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
    assert not _CLIENT.form_blocks_line(b"\r", frozen=True)
    assert not _CLIENT.form_blocks_line(b"health\r", frozen=False)


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
    test_maybe_auto_party_join_off_no_hunt()
    test_maybe_auto_party_no_join_without_invite()
    test_f10_is_hold_does_not_takeover()
    test_f10_hold_outside_realm()
    test_f11_is_sheet_does_not_takeover()
    test_f12_starts_logoff_without_dumping_keys()
    test_logoff_walk_realm_then_mud_then_close()
    test_logoff_walk_already_on_bbs_menu_just_closes()
    test_toggle_sheet_pauses_at_guild()
    test_toggle_sheet_walks_when_not_at_guild()
    test_toggle_sheet_unlocks_and_cancels_walk()
    test_toggle_sheet_cancels_walk_and_hold()
    test_sheet_lock_keeps_fsd_keys_with_leftover_hp()
    test_form_hold_drops_hp_keeps_fields()
    test_form_hold_drops_split_ma_prompt()
    test_train_stats_exit_shows_room_not_stars()
    test_leftover_hp_keeps_creation_sheet()
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
    test_paint_splash_is_graffiti()
    test_login_ans_replaces_mbbs_banner()
    test_chrome_lists_new_map()
    test_validating_name_says_wait()
    test_name_taken_is_creation()
    test_autopilot_starts_bbs_signup_when_unknown()
    test_autopilot_stops_when_already_logged_in()
    test_autopilot_queues_m_during_password_cooldown()
    test_autopilot_does_not_type_m_at_majormud_prompt()
    test_autopilot_sends_board_m_once()
    test_key_gap_is_slow_enough_for_majormud()
    test_realm_gate_holds_auto_play_after_first_prompt()
    test_realm_gate_resets_on_character_sheet()
    test_action_pry_skips_follow_and_backrank()
    test_action_pry_skips_buy_sell_wear()
    test_action_pry_skips_settle_and_look()
    test_action_pry_shop_vague_blocks_buy()
    test_action_pry_read_vague_asks_for_full_name()
    test_action_pry_never_inv_after_a_walk()
    test_drop_stray_keys_during_login_not_on_sheet()
    test_f7_does_not_arm_hunt_before_realm()
    test_chrome_tips_fit()
    test_play_paused_is_train_hold_not_copy()
    test_chrome_hp_tone()
    test_handle_client_line_train()
    test_handle_client_line_spell_yn()
    test_handle_client_line_gear_yn()
    test_handle_client_line_aa()
    test_handle_client_line_hunt_run()
    test_handle_client_line_goto()
    test_handle_client_line_boost()
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
