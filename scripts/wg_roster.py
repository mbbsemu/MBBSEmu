#!/usr/bin/python3
"""Worldgroup play logins. Passwords stay in gitignored wg-*.json files.

Configs are identity + login only. Hunt/kit/party/spells/aa/rank live in
client.modules (roles / party / kit / spells). Launch flags come from
play-wg / desktop (--auto-play / --no-auto-play / --no-auto).
"""

from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from client.modules.party import (  # noqa: E402
    AUTO_PLAY_PROFILES,
    CHARS,
    wants_auto_play,
)

CONFIG = ROOT / "config"

# Keys that used to carry per-toon behavior — stripped on rewrite.
_BEHAVIOR_KEYS = (
    "party_leader",
    "auto_join",
    "aa",
    "spells",
    "ambush",
    "rank",
    "stealth",
    "hunt",
    "auto_play",
)


def _password() -> str:
    local = CONFIG / "wg-local.json"
    if local.is_file():
        try:
            data = json.loads(local.read_text())
        except (OSError, json.JSONDecodeError):
            data = {}
        if isinstance(data, dict) and data.get("password"):
            return str(data["password"])
    return "Urm0m!"


def cfg_name(profile: str) -> str:
    if profile in {"klymacks", "sysop"}:
        return "wg-local.json"
    return f"wg-{profile}.json"


def thin_body(
    *,
    user: str,
    given: str,
    klass: str,
    race: str,
    password: str,
    auto_login: bool = True,
) -> dict[str, object]:
    """Identity + login. No party/spells/aa/rank/ambush/auto_play."""
    return {
        "username": user,
        "password": password,
        "given": given,
        "class": klass,
        "race": race,
        "auto_login": bool(auto_login),
        "pvp": False,
    }


def write_configs() -> None:
    CONFIG.mkdir(parents=True, exist_ok=True)
    secret = _password()
    for profile, user, given, klass, race in CHARS:
        path = CONFIG / cfg_name(profile)
        existing: dict[str, object] = {}
        if path.is_file():
            try:
                loaded = json.loads(path.read_text())
            except (OSError, json.JSONDecodeError):
                loaded = {}
            if isinstance(loaded, dict):
                existing = loaded
        password = str(existing.get("password") or secret)
        body = thin_body(
            user=user,
            given=given,
            klass=klass,
            race=race,
            password=password,
            auto_login=bool(existing.get("auto_login", True)),
        )
        path.write_text(json.dumps(body, indent=2) + "\n")
    # Keep wg-new thin (manual type-in login).
    new_path = CONFIG / "wg-new.json"
    existing_new: dict[str, object] = {}
    if new_path.is_file():
        try:
            loaded = json.loads(new_path.read_text())
        except (OSError, json.JSONDecodeError):
            loaded = {}
        if isinstance(loaded, dict):
            existing_new = loaded
    new_body = {
        "username": str(existing_new.get("username") or ""),
        "password": str(existing_new.get("password") or ""),
        "given": str(existing_new.get("given") or ""),
        "auto_login": False,
        "pvp": False,
    }
    new_path.write_text(json.dumps(new_body, indent=2) + "\n")


def desktop_rows() -> list[tuple[str, str, str]]:
    """profile, window title, StartupWMClass."""
    rows = []
    for profile, _user, given, _klass, _race in CHARS:
        label = "klymacks" if given.lower() == "klymacks" else given
        rows.append((profile, f"Finn's Realm — {label}", f"FinnsRealmWG{profile}"))
    return rows


def main() -> int:
    if len(sys.argv) > 1 and sys.argv[1] == "write-configs":
        write_configs()
        return 0
    if len(sys.argv) > 1 and sys.argv[1] == "desktop":
        for profile, title, wmclass in desktop_rows():
            print(f"{profile}\t{title}\t{wmclass}")
        return 0
    if len(sys.argv) > 1 and sys.argv[1] == "auto-play":
        profile = sys.argv[2] if len(sys.argv) > 2 else ""
        return 0 if wants_auto_play(profile) else 1
    if len(sys.argv) > 1 and sys.argv[1] == "auto-play-profiles":
        for name in sorted(AUTO_PLAY_PROFILES):
            print(name)
        return 0
    print(
        "usage: wg_roster.py write-configs | desktop | auto-play PROFILE | auto-play-profiles",
        file=sys.stderr,
    )
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
