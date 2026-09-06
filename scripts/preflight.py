#!/usr/bin/env python3
"""Pre-launch checker for Finn's Realm.

Exit 0: safe to start the board.
Exit 1: hard fail — do not launch.
Exit 2: warnings only (board can start; read them).

Stages stay slow on purpose. Character first. Codes last.

  character  — create a character in DEMO
  demo       — character exists; stay on {DEMO}; no addons
  plus-demo  — Plus real code only (MajorMUD still DEMO)
  mud-code   — MajorMUD real code only if last boot was N>0
  addons     — one id:code per line, reboot each time

Auto-advance stops at demo. Later stages are a file you write:

  echo plus-demo > data/finns-stage

  ./scripts/preflight.py
  FINNS_PREFLIGHT=0 ./scripts/FinnsRealm --sysop
"""
from __future__ import annotations

import json
import re
import socket
import sqlite3
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
if len(sys.argv) > 1 and sys.argv[1] in {"-h", "--help"}:
    print(__doc__)
    raise SystemExit(0)
if len(sys.argv) > 1:
    ROOT = Path(sys.argv[1]).resolve()

MODULE = ROOT / "modules" / "WCCMMUD"
CONFIG = ROOT / "config"
DATA = ROOT / "data"
INSTRUCTIONS = ROOT / "scripts" / "PREFLIGHT.md"
STAGE_FILE = DATA / "finns-stage"
LAST_BOOT = DATA / "last-boot.json"
BOOT_HISTORY = DATA / "boot-history.jsonl"

STAGES = ("character", "demo", "plus-demo", "mud-code", "addons")
BAD_ACTIVATE = {
    "BYPASS",
    "UNLOCKED",
    "UNLOCK",
    "TEST",
    "SPARE",
    "FULL",
    "OK",
    "YES",
    "TRUE",
}
# Tokens that were wrongly re-applied from leftover MCV / "later" files.
# Always FAIL. Do not print them; do not treat them as a real Plus/Mud code.
REJECTED_ACTIVATE = {
    "AWCYYWATAY",
    "UTAUIRUWHT",
}
ACTIVATE_RE = re.compile(r"ACTIVATE\s*\{([^}]*)\}", re.IGNORECASE)
DUMMY_ADDON_RE = re.compile(r"^\s*(\d+)\s*:\s*(\d+)\s*$")
LETTER_CODE_RE = re.compile(r"^[A-Za-z]{6,20}$")
ADDON_LINE_RE = re.compile(r"^\s*(\d+)\s*:\s*([A-Za-z0-9]+)\s*$")
BTURNO_RE = re.compile(r"^\d{8}$")

OK, WARN, FAIL = "ok", "warn", "fail"


def activate_value(path: Path) -> str | None:
    if not path.is_file():
        return None
    match = ACTIVATE_RE.search(path.read_text(errors="replace"))
    return match.group(1).strip() if match else None


def board_listening(port: int = 2323) -> bool:
    try:
        with socket.create_connection(("127.0.0.1", port), timeout=0.4):
            return True
    except OSError:
        return False


def characters() -> list[tuple[str, str]]:
    """BBS user + given name from the Btrieve sqlite cache."""
    for name in ("WCCUSERS.db", "WCCUSERS.DB"):
        db = MODULE / name
        if not db.is_file():
            continue
        con = sqlite3.connect(db)
        try:
            rows = list(con.execute("SELECT key_0, key_1 FROM data_t"))
        except sqlite3.Error:
            return []
        finally:
            con.close()
        out: list[tuple[str, str]] = []
        for user, given in rows:
            u = str(user or "").strip()
            g = str(given or "").strip()
            if u or g:
                out.append((u, g))
        return out
    return []


def last_boot() -> dict[str, object] | None:
    if not LAST_BOOT.is_file():
        return None
    try:
        data = json.loads(LAST_BOOT.read_text())
    except (OSError, json.JSONDecodeError):
        return None
    return data if isinstance(data, dict) else None


def clean_demo_boots() -> int:
    if not BOOT_HISTORY.is_file():
        return 0
    n = 0
    for line in BOOT_HISTORY.read_text().splitlines():
        if not line.strip():
            continue
        try:
            rec = json.loads(line)
        except json.JSONDecodeError:
            continue
        if rec.get("demo") and rec.get("seats_ok") and not rec.get("recovery"):
            n += 1
    return n


def read_stage_file() -> str | None:
    if not STAGE_FILE.is_file():
        return None
    raw = STAGE_FILE.read_text().strip()
    if not raw:
        return None
    token = raw.split()[0].lower()
    return token if token in STAGES else None


def resolve_stage(chars: list[tuple[str, str]]) -> str:
    pinned = read_stage_file()
    if pinned in ("plus-demo", "mud-code", "addons"):
        return pinned
    if chars:
        if pinned != "demo":
            STAGE_FILE.write_text("demo\n")
        return "demo"
    if pinned != "character":
        STAGE_FILE.write_text("character\n")
    return "character"


def classify_activate(value: str | None, *, kind: str, stage: str) -> tuple[str, str]:
    if value is None:
        return FAIL, "ACTIVATE line missing"
    token = value.upper()
    if token in REJECTED_ACTIVATE:
        return FAIL, "rejected leftover — restore DEMO (do not re-apply)"
    if token in BAD_ACTIVATE or token.isdigit() or ":" in token:
        return FAIL, f"crash-prone {{{value}}} — restore DEMO"
    if token == "DEMO":
        return OK, "DEMO"
    if not LETTER_CODE_RE.fullmatch(value):
        return FAIL, f"unrecognized {{{value}}}"
    if kind == "mud" and stage in ("mud-code", "addons"):
        return WARN, f"registered {{{value}}} — last boot must be N>0 or revert DEMO"
    if kind == "plus" and stage in ("plus-demo", "mud-code", "addons"):
        return WARN, f"registered {{{value}}} — only after DEMO play works"
    return FAIL, f"registered {{{value}}} — too early (stage {stage}; character/DEMO first)"


def addon_status(path: Path, stage: str) -> tuple[str, str]:
    if not path.is_file():
        return OK, "no file (correct until stage addons)"
    raw = path.read_text(errors="replace")
    lines = [ln.strip() for ln in raw.splitlines() if ln.strip()]
    if not lines:
        return FAIL, "0-byte file — fgets EOF is MUD internal error; delete it"
    dummies = [ln for ln in lines if DUMMY_ADDON_RE.match(ln)]
    if dummies:
        return FAIL, f"dummy slots {', '.join(dummies)} — delete the file"
    bad = [ln for ln in lines if not ADDON_LINE_RE.match(ln)]
    if bad:
        return FAIL, f"unreadable lines {bad!r}"
    if stage != "addons":
        return FAIL, f"{len(lines)} addon code(s) — too early (stage {stage})"
    return WARN, f"{len(lines)} addon code(s) — reboot after each line"


def leftover_stash_files() -> list[tuple[str, Path]]:
    return [
        ("wccaddon.sys.saved", DATA / "wccaddon.sys.saved"),
        ("WCCADDON.SYS.saved", MODULE / "WCCADDON.SYS.saved"),
    ]


def leftover_stash_status(path: Path) -> tuple[str, str]:
    if not path.is_file():
        return OK, "absent"
    return FAIL, "leftover stash — delete; do not paste these back into MSG"


def addon_live_lines() -> list[str]:
    path = MODULE / "WCCADDON.SYS"
    if not path.is_file():
        return []
    return [ln.strip() for ln in path.read_text(errors="replace").splitlines() if ln.strip()]


def write_activation_later(mud: str | None, plus: str | None) -> Path:
    """Copy live BTURNO / ACTIVATE / addons into data/activation-later.txt."""
    DATA.mkdir(parents=True, exist_ok=True)
    settings = CONFIG / "appsettings.json"
    board = ""
    if settings.is_file():
        board = str(json.loads(settings.read_text()).get("GSBL.BTURNO", "")).strip()
    addons = addon_live_lines()
    lines = [
        "# Rewritten each Check / preflight from the live files. Do not hand-edit.",
        f"GSBL.BTURNO: {board}",
        f"MajorMUD ACTIVATE: {mud or ''}",
        f"MajorMUD Plus ACTIVATE: {plus or ''}",
        "",
        "WCCADDON.SYS:",
    ]
    if addons:
        lines.extend(addons)
    else:
        lines.append("(none)")
    lines.append("")
    dest = DATA / "activation-later.txt"
    dest.write_text("\n".join(lines))
    return dest


def compiled_activate_status(path: Path, msg_token: str | None) -> tuple[str, str]:
    if not path.is_file():
        return OK, "absent (recompiles from MSG on boot)"
    data = path.read_bytes()
    if any(token.encode("ascii") in data for token in REJECTED_ACTIVATE):
        return FAIL, "stale compiled leftover — delete this MCV"
    if msg_token and msg_token.upper() == "DEMO" and b"DEMO" not in data:
        return FAIL, "MCV does not match DEMO MSG — delete so it recompiles"
    return OK, "matches MSG"


def bturno() -> tuple[str, str]:
    settings = CONFIG / "appsettings.json"
    if not settings.is_file():
        return FAIL, "config/appsettings.json missing"
    data = json.loads(settings.read_text())
    value = str(data.get("GSBL.BTURNO", "")).strip()
    if not BTURNO_RE.fullmatch(value):
        return FAIL, f"GSBL.BTURNO={value!r} (need 8 digits)"
    return OK, value


def sqlite_usernames() -> set[str]:
    db = DATA / "mbbsemu.db"
    if not db.is_file():
        return set()
    con = sqlite3.connect(db)
    try:
        return {str(r[0]).upper() for r in con.execute("SELECT userName FROM Accounts")}
    finally:
        con.close()


def bbsusr_usernames() -> set[str] | None:
    db = DATA / "BBSUSR.DB"
    if not db.is_file():
        return None
    con = sqlite3.connect(db)
    try:
        return {str(r[0]).upper() for r in con.execute("SELECT key_0 FROM data_t")}
    except sqlite3.Error:
        return set()
    finally:
        con.close()


def bbsusr_status() -> tuple[str, str]:
    accounts = sqlite_usernames()
    bbsusr = bbsusr_usernames()
    if bbsusr is None:
        return FAIL, "BBSUSR.DB missing — login will say USER MISMATCH"
    if not accounts:
        return WARN, "no Accounts in mbbsemu.db"
    missing = sorted(accounts - bbsusr)
    if missing:
        # Launchers pass -DBREBUILD BBSUSR. FAIL here blocks the rebuild.
        return WARN, f"sqlite users missing from BBSUSR (rebuild on start): {', '.join(missing)}"
    extra = sorted(bbsusr - accounts)
    if extra:
        return WARN, f"BBSUSR extras not in sqlite: {', '.join(extra)}"
    if not bbsusr:
        return FAIL, "BBSUSR.DB is empty (0 users) — USER MISMATCH on login"
    return OK, f"{len(bbsusr)} users: {', '.join(sorted(bbsusr))}"


def modules_id() -> tuple[str, str]:
    path = DATA / "modules.json"
    if not path.is_file():
        return FAIL, "data/modules.json missing"
    data = json.loads(path.read_text())
    ident = ""
    modules = data.get("Modules") or []
    if modules:
        ident = str(modules[0].get("Identifier", ""))
    if ident != "WCCMMUD":
        return FAIL, f"Identifier={ident!r} (must be WCCMMUD, not Plus)"
    return OK, ident


def last_boot_status() -> tuple[str, str]:
    rec = last_boot()
    if rec is None:
        return WARN, "no recorded boot yet — reboot records users/DEMO"
    users = rec.get("users")
    demo = bool(rec.get("demo"))
    recovery = bool(rec.get("recovery"))
    parts = [f"{users} users" if users is not None else "users unknown"]
    parts.append("DEMO" if demo else "not DEMO")
    if recovery:
        parts.append("recovered")
    note = ", ".join(parts)
    if users == 0 and not demo:
        return FAIL, f"{note} — revert MajorMUD to {{DEMO}} and reboot"
    if recovery:
        return WARN, note
    if demo and rec.get("seats_ok"):
        return OK, note
    return WARN, note


def character_status(chars: list[tuple[str, str]], stage: str) -> tuple[str, str]:
    if not chars:
        if stage in ("mud-code", "plus-demo", "addons"):
            return WARN, "none yet — create after this licensed boot if N>0"
        if stage == "character":
            return WARN, "none yet — enter the realm and create one before any codes"
        return FAIL, "no character — stay in DEMO and create one"
    shown = ", ".join(f"{u}/{g}" if g else u for u, g in chars)
    return OK, shown


def required_files() -> list[tuple[str, Path]]:
    return [
        ("MajorMUD DLL", MODULE / "WCCMMUD.DLL"),
        ("MajorMUD MDF", MODULE / "WCCMMUD.MDF"),
        ("MajorMUD MSG", MODULE / "WCCMMUD.MSG"),
        ("MajorMUD MSG.original (DEMO restore)", MODULE / "WCCMMUD.MSG.original"),
        ("Plus DLL", MODULE / "WCCMMPLS.DLL"),
        ("Plus MDF", MODULE / "WCCMMPLS.MDF"),
        ("Plus MSG", MODULE / "WCCMMPLS.MSG"),
        ("appsettings", CONFIG / "appsettings.json"),
        ("modules.json", DATA / "modules.json"),
    ]


def stage_next(stage: str) -> str:
    return {
        "character": "create a character, then stay on DEMO",
        "demo": "play. codes stay blocked until you write data/finns-stage",
        "plus-demo": "Plus code only; MajorMUD stays DEMO; reboot and check N users",
        "mud-code": "MajorMUD code; if last boot is 0 users, revert DEMO immediately",
        "addons": "one id:code line, then reboot",
    }[stage]


def main() -> int:
    DATA.mkdir(parents=True, exist_ok=True)
    chars = characters()
    stage = resolve_stage(chars)
    rows: list[tuple[str, str, str, Path]] = []

    rows.append((OK, "stage", f"{stage} — {stage_next(stage)}", STAGE_FILE))

    for label, path in required_files():
        if path.is_file() and path.stat().st_size > 0:
            rows.append((OK, label, "present", path))
        else:
            rows.append((FAIL, label, "missing", path))

    mud = activate_value(MODULE / "WCCMMUD.MSG")
    tone, note = classify_activate(mud, kind="mud", stage=stage)
    rows.append((tone, "WCCMMUD.MSG ACTIVATE", note, MODULE / "WCCMMUD.MSG"))

    orig = activate_value(MODULE / "WCCMMUD.MSG.original")
    if orig and orig.upper() == "DEMO":
        rows.append((OK, "MSG.original ACTIVATE", "DEMO restore point", MODULE / "WCCMMUD.MSG.original"))
    else:
        rows.append((FAIL, "MSG.original ACTIVATE", f"{{{orig}}} — restore point is not DEMO", MODULE / "WCCMMUD.MSG.original"))

    plus = activate_value(MODULE / "WCCMMPLS.MSG")
    tone, note = classify_activate(plus, kind="plus", stage=stage)
    rows.append((tone, "WCCMMPLS.MSG ACTIVATE", note, MODULE / "WCCMMPLS.MSG"))

    tone, note = addon_status(MODULE / "WCCADDON.SYS", stage)
    rows.append((tone, "WCCADDON.SYS", note, MODULE / "WCCADDON.SYS"))

    later = write_activation_later(mud, plus)
    rows.append((OK, "activation-later.txt", "snapshot of live BTURNO / ACTIVATE / addons", later))

    for label, path in leftover_stash_files():
        tone, note = leftover_stash_status(path)
        rows.append((tone, label, note, path))

    for label, path, token in (
        ("WCCMMUD.MCV", MODULE / "WCCMMUD.MCV", mud),
        ("WCCMMPLS.MCV", MODULE / "WCCMMPLS.MCV", plus),
    ):
        tone, note = compiled_activate_status(path, token)
        rows.append((tone, label, note, path))

    tone, note = bturno()
    rows.append((tone, "GSBL.BTURNO", note, CONFIG / "appsettings.json"))

    tone, note = modules_id()
    rows.append((tone, "modules.json Identifier", note, DATA / "modules.json"))

    recov = MODULE / "WCCRECOV.FLG"
    if recov.is_file():
        if board_listening():
            rows.append((OK, "WCCRECOV.FLG", "present (normal while the board is running)", recov))
        else:
            rows.append((WARN, "WCCRECOV.FLG", "leftover lock — stop/reboot delete this", recov))
    else:
        rows.append((OK, "WCCRECOV.FLG", "absent (clean shutdown)", recov))

    tone, note = bbsusr_status()
    rows.append((tone, "BBSUSR.DB vs Accounts", note, DATA / "BBSUSR.DB"))

    tone, note = character_status(chars, stage)
    rows.append((tone, "character", note, MODULE / "WCCUSERS.db"))

    boots = clean_demo_boots()
    boot_note = f"{boots} clean DEMO boot(s) recorded"
    if stage == "character":
        rows.append((WARN, "clean DEMO boots", f"{boot_note} — need a character first", BOOT_HISTORY))
    elif boots < 2:
        rows.append((WARN, "clean DEMO boots", f"{boot_note} — a couple more before any codes", BOOT_HISTORY))
    else:
        rows.append((OK, "clean DEMO boots", boot_note, BOOT_HISTORY))

    tone, note = last_boot_status()
    rows.append((tone, "last boot", note, LAST_BOOT))

    fails = [r for r in rows if r[0] == FAIL]
    warns = [r for r in rows if r[0] == WARN]

    print("Finn's Realm preflight")
    print(f"Instructions: {INSTRUCTIONS}")
    print()
    width = max(len(r[1]) for r in rows)
    for tone, label, note, path in rows:
        mark = {"ok": "OK  ", "warn": "WARN", "fail": "FAIL"}[tone]
        print(f"  {mark}  {label:<{width}}  {note}")
        print(f"        {path}")
    print()

    report = DATA / "preflight-last.txt"
    report.write_text(
        "\n".join(f"{tone}\t{label}\t{note}\t{path}" for tone, label, note, path in rows)
        + "\n"
    )

    if fails:
        print("Do not launch. Fix FAILs first.")
        print("Start clean:  ./scripts/reset-game.sh --no-boot")
        return 1
    if warns:
        if stage in ("mud-code", "plus-demo", "addons"):
            print("Launch allowed. First boot must show N>0 users and 1.11p-WG.")
            print("If N=0 or version vd: stop, restore DEMO, do not keep that pair.")
        else:
            print("Launch allowed. Stay on DEMO. Do not paste codes.")
        return 2
    print("Safe to launch. Play DEMO. Codes stay blocked until you change data/finns-stage.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
