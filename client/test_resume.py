"""Cleanup resume: serialize, decide, restore."""

from __future__ import annotations

import tempfile
from pathlib import Path

from client import resume as resume_mod
from client.brain import Brain
from client.state import WorldState


def _brain(**kwargs) -> Brain:
    defaults = dict(
        allowed=True,
        me="klymacks",
        party_leader="Matt",
        klass="ninja",
        race="dark-elf",
    )
    defaults.update(kwargs)
    return Brain(**defaults)


def test_capture_serialize_roundtrip() -> None:
    state = WorldState()
    state.in_realm = True
    state.room = "Graveyard"
    state.following = "Matt"
    state.followers = []
    state.party_rank = "back"
    state.ally_aim = "ghoul"
    brain = _brain()
    brain.mode = "hunt"
    brain.gear_done = True
    brain._party_rank = "back"
    brain._park_rest = False
    brain.auto_join = True
    snap = resume_mod.capture(
        who="klymacks", state=state, brain=brain, reason="cleanup"
    )
    assert snap.who == "klymacks"
    assert snap.room == "Graveyard"
    assert snap.following == "Matt"
    assert snap.mode == "hunt"
    assert snap.gear_done
    assert snap.hunting
    assert snap.party_rank == "back"
    assert snap.ally_aim == "ghoul"
    assert snap.reason == "cleanup"
    again = resume_mod.ResumeState.from_dict(snap.to_dict())
    assert again is not None
    assert again.following == "Matt"
    assert again.gear_done
    assert again.party_rank == "back"


def test_leader_capture_has_followers() -> None:
    state = WorldState()
    state.in_realm = True
    state.room = "Graveyard"
    state.followers = ["klymacks", "Ryan"]
    brain = _brain(me="matt Matthew", party_leader="Matt", klass="paladin")
    brain.mode = "hunt"
    brain.gear_done = True
    snap = resume_mod.capture(who="matt", state=state, brain=brain, reason="disconnect")
    assert snap.followers == ["klymacks", "Ryan"]
    assert snap.hunting


def test_save_load_clear(tmp_path: Path | None = None) -> None:
    root = Path(tempfile.mkdtemp()) if tmp_path is None else tmp_path
    path = resume_mod.resume_path(root, "klymacks")
    assert path is not None
    snap = resume_mod.ResumeState(
        who="klymacks",
        room="Town Square",
        in_realm=True,
        mode="hunt",
        gear_done=True,
        hunting=True,
        following="Matt",
        party_rank="back",
    )
    resume_mod.save(path, snap)
    assert path.is_file()
    loaded = resume_mod.load(path)
    assert loaded is not None
    assert loaded.following == "Matt"
    assert loaded.room == "Town Square"
    resume_mod.clear(path)
    assert not path.is_file()
    assert resume_mod.load(path) is None


def test_persist_decision_intentional_f12_never() -> None:
    assert not resume_mod.should_persist_disconnect(
        in_realm=True,
        intentional_logoff=True,
        cleanup_seen=True,
        been_on_board=True,
    )


def test_persist_decision_cleanup_or_in_realm() -> None:
    assert resume_mod.should_persist_disconnect(
        in_realm=False,
        intentional_logoff=False,
        cleanup_seen=True,
        been_on_board=True,
    )
    assert resume_mod.should_persist_disconnect(
        in_realm=True,
        intentional_logoff=False,
        cleanup_seen=False,
        been_on_board=True,
    )
    assert not resume_mod.should_persist_disconnect(
        in_realm=False,
        intentional_logoff=False,
        cleanup_seen=False,
        been_on_board=False,
    )


def test_auto_resume_skips_f12_and_idle() -> None:
    hunting = resume_mod.ResumeState(
        who="klymacks", in_realm=True, mode="hunt", hunting=True, gear_done=True
    )
    idle = resume_mod.ResumeState(who="klymacks", in_realm=False, mode="manual")
    assert resume_mod.should_auto_resume(hunting, intentional_logoff=False)
    assert not resume_mod.should_auto_resume(hunting, intentional_logoff=True)
    assert not resume_mod.should_auto_resume(idle, intentional_logoff=False)
    assert not resume_mod.should_auto_resume(None, intentional_logoff=False)


def test_cleanup_text_markers() -> None:
    assert resume_mod.cleanup_text("System CLEANUP in progress")
    assert resume_mod.cleanup_text("The board is off the air")
    assert resume_mod.cleanup_text("Thanks for calling Finn's Realm")
    assert not resume_mod.cleanup_text("[HP=20/28]:")
    assert not resume_mod.cleanup_text("Make your selection:")


def test_apply_restores_hunt_without_rekit() -> None:
    brain = _brain()
    brain.mode = "manual"
    brain.gear_done = False
    snap = resume_mod.ResumeState(
        who="klymacks",
        mode="hunt",
        gear_done=True,
        hunting=True,
        following="Matt",
        party_rank="back",
        auto_join=True,
        leader="Matt",
    )
    resume_mod.apply_to_brain(brain, snap)
    assert brain.gear_done
    assert brain.mode == "hunt"
    assert brain._resume_follow == "Matt"
    assert brain._resume_rank == "back"
    assert not brain._followed


def test_apply_leader_invite_intent() -> None:
    brain = _brain(me="matt Matthew", party_leader="Matt", klass="paladin")
    snap = resume_mod.ResumeState(
        who="matt",
        mode="hunt",
        gear_done=True,
        hunting=True,
        followers=["klymacks", "Ryan"],
        leader="Matt",
    )
    resume_mod.apply_to_brain(brain, snap)
    assert brain._resume_invite == ["klymacks", "Ryan"]
    assert brain._want_join_call


def test_flush_resume_follow(monkeypatch=None) -> None:
    brain = _brain()
    brain.gear_done = True
    brain.mode = "hunt"
    brain._resume_follow = "Matt"
    brain._resume_rank = "back"
    state = WorldState()
    state.in_realm = True
    sent: list[str] = []

    def send(cmd: str) -> None:
        sent.append(cmd)

    assert brain.flush_resume(state, send)
    assert sent == ["follow Matt"]
    state.following = "Matt"
    sent.clear()
    assert brain.flush_resume(state, send)
    assert sent == ["backr"]
    assert brain._followed
    assert not brain._resume_follow


def test_resume_path_klymacks_lower() -> None:
    path = resume_mod.resume_path("/tmp/data", "klymacks ninja")
    assert path is not None
    assert path.name == "resume-klymacks.json"


if __name__ == "__main__":
    test_capture_serialize_roundtrip()
    test_leader_capture_has_followers()
    test_save_load_clear()
    test_persist_decision_intentional_f12_never()
    test_persist_decision_cleanup_or_in_realm()
    test_auto_resume_skips_f12_and_idle()
    test_cleanup_text_markers()
    test_apply_restores_hunt_without_rekit()
    test_apply_leader_invite_intent()
    test_flush_resume_follow()
    test_resume_path_klymacks_lower()
    print("ok")
