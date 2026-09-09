"""Overnight board cleanup: remember activity, wait, reconnect, resume.

WG nightly CLEANUP (~3 AM / MCUHR) drops every client. Leave play windows
up — the client persists what you were doing, waits for FINN:23, logs back
in, re-enters the realm, and restores hunt / follow / party role.

Intentional F12 logoff clears the resume file and does not reconnect.
"""

from __future__ import annotations

import json
import socket
import time
from dataclasses import asdict, dataclass, field
from pathlib import Path
from typing import Any

from . import spells

RESUME_VERSION = 1

# Cleanup / downtime text (WG + board goodbye). Not every random hangup —
# but in_realm + socket death still persists when unsure.
_CLEANUP_MARKERS = (
    "cleanup",
    "off the air",
    "off-the-air",
    "board is shutting",
    "shutting down",
    "nightly maintenance",
    "system maintenance",
    "please hang up",
    "thanks for calling",
    "thank you for calling",
    "now logged off",
    "disconnected while playing",
)

_ACTIVE_MODES = frozenset({"hunt", "gear", "rest", "goto"})


@dataclass
class ResumeState:
    """Enough to restore 'what they were doing' after cleanup."""

    version: int = RESUME_VERSION
    who: str = ""
    room: str = ""
    area: str = ""
    in_realm: bool = False
    mode: str = "manual"
    gear_done: bool = False
    hunting: bool = False
    following: str = ""
    followers: list[str] = field(default_factory=list)
    party_rank: str = ""
    park_rest: bool = False
    party_rest: bool = False
    ally_aim: str = ""
    auto_join: bool = True
    leader: str = ""
    reason: str = ""
    saved_at: float = 0.0

    def to_dict(self) -> dict[str, Any]:
        data = asdict(self)
        data["followers"] = list(self.followers)
        return data

    @classmethod
    def from_dict(cls, raw: dict[str, Any] | None) -> ResumeState | None:
        if not isinstance(raw, dict):
            return None
        who = str(raw.get("who") or "").strip().lower()
        if not who:
            return None
        followers_raw = raw.get("followers") or []
        followers: list[str] = []
        if isinstance(followers_raw, list):
            for name in followers_raw:
                text = str(name).strip()
                if text and text not in followers:
                    followers.append(text)
        mode = str(raw.get("mode") or "manual").strip().lower() or "manual"
        return cls(
            version=int(raw.get("version") or RESUME_VERSION),
            who=who,
            room=str(raw.get("room") or "").strip(),
            area=str(raw.get("area") or "").strip(),
            in_realm=bool(raw.get("in_realm")),
            mode=mode,
            gear_done=bool(raw.get("gear_done")),
            hunting=bool(raw.get("hunting")) or mode in _ACTIVE_MODES,
            following=str(raw.get("following") or "").strip(),
            followers=followers,
            party_rank=str(raw.get("party_rank") or "").strip().lower(),
            park_rest=bool(raw.get("park_rest")),
            party_rest=bool(raw.get("party_rest")),
            ally_aim=str(raw.get("ally_aim") or "").strip(),
            auto_join=bool(raw.get("auto_join", True)),
            leader=str(raw.get("leader") or "").strip(),
            reason=str(raw.get("reason") or "").strip(),
            saved_at=float(raw.get("saved_at") or 0.0),
        )


def resume_who(me: str) -> str:
    """Stable filename key — first token, klymacks lowercase."""
    return spells.learned_who(me)


def resume_path(data_dir: str | Path | None, who: str) -> Path | None:
    key = resume_who(who)
    if not data_dir or not key:
        return None
    return Path(data_dir) / f"resume-{key}.json"


def cleanup_text(text: str) -> bool:
    """True when the screen/payload looks like WG cleanup / board drop."""
    low = " ".join((text or "").lower().split())
    if not low:
        return False
    return any(mark in low for mark in _CLEANUP_MARKERS)


def should_persist_disconnect(
    *,
    in_realm: bool,
    intentional_logoff: bool,
    cleanup_seen: bool = False,
    been_on_board: bool = False,
) -> bool:
    """Save resume on cleanup or unexpected drop while playing.

    Intentional F12 never persists. If unsure, any disconnect while in_realm
    still saves so we can try. been_on_board alone (login hangup) does not.
    """
    if intentional_logoff:
        return False
    if cleanup_seen:
        return True
    if in_realm:
        return True
    # Mid-cleanup splash after leaving the realm — still come back.
    return bool(been_on_board and cleanup_seen)


def should_auto_resume(
    snap: ResumeState | None,
    *,
    intentional_logoff: bool,
) -> bool:
    """Reconnect loop only for cleanup / unexpected drop — never F12."""
    if intentional_logoff or snap is None:
        return False
    if not snap.in_realm and not snap.hunting and snap.mode == "manual":
        return False
    return True


def prefer_snap(
    existing: ResumeState | None,
    fresh: ResumeState | None,
) -> ResumeState | None:
    """Keep a pre-logoff snapshot; walk.start() clears hunt before re-capture."""
    if should_auto_resume(existing, intentional_logoff=False):
        return existing
    if should_auto_resume(fresh, intentional_logoff=False):
        return fresh
    return None


def capture(
    *,
    who: str,
    state: Any,
    brain: Any,
    reason: str = "",
) -> ResumeState:
    """Snapshot activity from live WorldState + Brain."""
    following = str(getattr(state, "following", "") or "").strip()
    if not following:
        following = str(getattr(brain, "_follow_sent_to", "") or "").strip()
    if not following and getattr(brain, "_followed", False):
        following = str(getattr(brain, "leader", "") or "").strip()
    followers = [
        str(name).strip()
        for name in (getattr(state, "followers", None) or [])
        if str(name).strip()
    ]
    rank = str(
        getattr(brain, "_party_rank", "")
        or getattr(state, "party_rank", "")
        or getattr(brain, "rank", "")
        or ""
    ).strip().lower()
    mode = str(getattr(brain, "mode", "manual") or "manual").strip().lower()
    room = str(getattr(state, "room", "") or "").strip()
    area = ""
    crew = getattr(brain, "_crew", None)
    if crew is not None:
        area = str(getattr(crew, "area", "") or "").strip()
    return ResumeState(
        who=resume_who(who) or resume_who(str(getattr(brain, "me", "") or "")),
        room=room,
        area=area or room,
        in_realm=bool(getattr(state, "in_realm", False)),
        mode=mode,
        gear_done=bool(getattr(brain, "gear_done", False)),
        hunting=bool(getattr(brain, "hunting", lambda: False)()),
        following=following,
        followers=followers,
        party_rank=rank,
        park_rest=bool(getattr(brain, "_park_rest", False)),
        party_rest=bool(getattr(brain, "_party_rest", False)),
        ally_aim=str(getattr(state, "ally_aim", "") or "").strip(),
        auto_join=bool(getattr(brain, "auto_join", True)),
        leader=str(getattr(brain, "leader", "") or "").strip(),
        reason=(reason or "").strip(),
        saved_at=time.time(),
    )


def save(path: str | Path | None, snap: ResumeState | None) -> None:
    if not path or snap is None or not snap.who:
        return
    file = Path(path)
    file.parent.mkdir(parents=True, exist_ok=True)
    file.write_text(json.dumps(snap.to_dict(), indent=2) + "\n", encoding="utf-8")


def load(path: str | Path | None) -> ResumeState | None:
    if not path:
        return None
    file = Path(path)
    if not file.is_file():
        return None
    try:
        raw = json.loads(file.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return None
    return ResumeState.from_dict(raw if isinstance(raw, dict) else None)


def clear(path: str | Path | None) -> None:
    if not path:
        return
    file = Path(path)
    try:
        if file.is_file():
            file.unlink()
    except OSError:
        pass


def apply_to_brain(brain: Any, snap: ResumeState) -> None:
    """Restore mode / kit flags / party intent. Does not invent a kit redo."""
    if snap.gear_done:
        brain.gear_done = True
    brain.auto_join = bool(snap.auto_join)
    if snap.leader:
        brain.leader = snap.leader
    mode = snap.mode if snap.mode in _ACTIVE_MODES else (
        "hunt" if snap.hunting else "manual"
    )
    if mode == "rest" or snap.park_rest or snap.party_rest:
        brain.mode = "rest"
        brain._park_rest = bool(snap.park_rest or snap.party_rest)
        brain._party_rest = bool(snap.party_rest or snap.park_rest)
        brain.next_action = "rest"
    elif mode == "gear" and not snap.gear_done:
        brain.mode = "gear"
        brain.next_action = "gear"
        brain._need_kit_check = True
    elif mode in {"hunt", "gear", "goto"} or snap.hunting:
        # Already geared: hunt, do not re-shop.
        if snap.gear_done or mode == "hunt":
            brain.gear_done = True
            brain.mode = "hunt"
            brain.next_action = "hunt"
        else:
            brain.mode = "gear"
            brain.next_action = "gear"
    else:
        brain.mode = "manual"
        brain.next_action = "manual"
    # Party is gone after cleanup — re-follow / re-invite, do not pretend joined.
    brain._joined = False
    brain._followed = False
    brain._ranked = False
    brain._party_rank = ""
    brain._rank_sent = ""
    brain._follow_sent_to = ""
    brain._resume_follow = (snap.following or "").strip()
    brain._resume_invite = list(snap.followers)
    brain._resume_rank = (snap.party_rank or "").strip().lower()
    if brain._resume_rank:
        brain.rank = brain._resume_rank
    if snap.ally_aim:
        # Hint only; live ally_aim comes back from combat text.
        brain._last_aim = snap.ally_aim
    if brain._named_leader() and brain._resume_invite:
        brain._want_join_call = True


def board_up(host: str, port: int, *, timeout: float = 2.0) -> bool:
    try:
        sock = socket.create_connection((host, port), timeout=timeout)
    except OSError:
        return False
    try:
        sock.close()
    except OSError:
        pass
    return True


def wait_for_board(
    host: str,
    port: int,
    *,
    stop: Any | None = None,
    on_wait: Any | None = None,
    initial: float = 2.0,
    max_backoff: float = 30.0,
    settle: float = 2.0,
) -> bool:
    """Poll until telnet accepts. Backoff. stop() True cancels."""
    delay = max(0.5, float(initial))
    cap = max(delay, float(max_backoff))
    while True:
        if stop is not None and stop():
            return False
        if board_up(host, port):
            time.sleep(max(0.0, float(settle)))
            if board_up(host, port):
                return True
        if on_wait is not None:
            on_wait(delay)
        slept = 0.0
        while slept < delay:
            if stop is not None and stop():
                return False
            step = min(0.25, delay - slept)
            time.sleep(step)
            slept += step
        delay = min(delay * 1.5, cap)
