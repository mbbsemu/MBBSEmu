#!/usr/bin/env python3
"""Headless Curtis (or any) login → realm idle → report short-look / 6n evidence.

Uses the same Telnet / AnsiScreen / paint_mud / ensure_realm_dsr path as
play-wg. Writes data/dsr-trace.jsonl and prints a summary.
"""

from __future__ import annotations

import json
import select
import socket
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(ROOT))

import importlib.util

_spec = importlib.util.spec_from_file_location("bbs_client", ROOT / "scripts" / "bbs_client.py")
bbs = importlib.util.module_from_spec(_spec)
assert _spec.loader is not None
_spec.loader.exec_module(bbs)

from client.brain import Brain
from client.parse import events_from_payload, harvest_screen, parse_events
from client.state import WorldState
from client.transcript import Transcript


def load_cfg(name: str) -> dict:
    path = ROOT / "config" / f"wg-{name}.json"
    return json.loads(path.read_text(encoding="utf-8"))


def main() -> int:
    profile = sys.argv[1] if len(sys.argv) > 1 else "curtis"
    host = sys.argv[2] if len(sys.argv) > 2 else "192.168.122.33"
    port = int(sys.argv[3]) if len(sys.argv) > 3 else 23
    idle_s = float(sys.argv[4]) if len(sys.argv) > 4 else 45.0

    player = load_cfg(profile)
    user = str(player.get("username") or profile)
    password = str(player.get("password") or "")
    print(f"tick-probe {user}@{host}:{port} idle={idle_s}s ttype0={bbs.TTYPE_NAMES[0]!r}")

    trace = ROOT / "data" / "dsr-trace.jsonl"
    trace.write_text("", encoding="utf-8")

    screen = bbs.AnsiScreen()
    state = WorldState()
    brain = Brain(allowed=True, me=str(player.get("given") or user), klass=str(player.get("class") or ""))
    filt = bbs.FormHoldFilter()
    pacer = bbs.KeyPacer()
    pilot = bbs.Autopilot(player, play=False, enter_mud=True)
    transcript = Transcript()
    seen_rows: set[str] = set()

    sock = socket.create_connection((host, port), timeout=8)
    sock.setsockopt(socket.IPPROTO_TCP, socket.TCP_NODELAY, 1)
    sock.setblocking(False)
    telnet = bbs.Telnet(sock)

    sent_look = False
    in_realm_at = 0.0
    also_after_realm = 0
    sixn_after_realm = 0
    deadline = time.monotonic() + 120.0  # login budget
    idle_deadline = 0.0

    try:
        while time.monotonic() < deadline:
            now = time.monotonic()
            if state.in_realm and idle_deadline == 0.0:
                in_realm_at = now
                idle_deadline = now + idle_s
                print(f"in_realm at t={now:.0f}; idling {idle_s}s (no look)")
            if idle_deadline and now >= idle_deadline:
                break

            timeout = 0.05 if pacer.pending() else 0.25
            readable, _, _ = select.select([sock], [], [], timeout)
            if sock in readable:
                try:
                    chunk = sock.recv(4096)
                except BlockingIOError:
                    chunk = b""
                if not chunk:
                    print("disconnect")
                    break
                payload = telnet.feed(chunk)
                hold = bbs.form_hold_now(
                    screen,
                    payload,
                    sheet_lock=False,
                    in_realm=state.in_realm,
                    train_hold=False,
                )
                bbs.paint_mud(
                    screen, payload, hold=hold, filt=filt, in_realm=state.in_realm
                )
                bbs.ensure_realm_dsr(screen, payload, in_realm=state.in_realm)
                for reply in screen.replies:
                    telnet.send(reply)
                cprs = [r for r in screen.replies if r.endswith(b"R")]
                screen.replies.clear()

                was_realm = state.in_realm
                for line in transcript.feed(payload):
                    for ev in parse_events(line):
                        state.apply(ev)
                for ev in events_from_payload(payload):
                    state.apply(ev)
                for ev in harvest_screen(screen.text(), seen_rows):
                    state.apply(ev)
                blob = screen.text()
                if (
                    "[HP=" in blob
                    and not screen.looks_like_creation()
                    and not bbs.board_menu_screen(blob)
                ):
                    idx = blob.rfind("[HP=")
                    for ev in parse_events(blob[idx : idx + 40].split("\n", 1)[0]):
                        state.apply(ev)
                bbs.leave_realm_on_board(state, blob)

                if state.in_realm:
                    if b"[6n" in payload or b"[6N" in payload or b"\x9b6n" in payload:
                        sixn_after_realm += 1
                        print(
                            f"  6n in-realm cpr={cprs!r} cup={bbs._cup_rows(payload)[:6]}"
                        )
                    if b"Also here:" in payload:
                        also_after_realm += 1
                        print(
                            f"  Also here in-realm (look_sent={sent_look}) "
                            f"preview={payload[-60]!r}..."
                        )

                if not was_realm and state.in_realm:
                    bbs._trace_dsr(screen, payload, in_realm=True)

            pilot.tick(screen.text(), pacer)
            out = pacer.take(now)
            if out:
                if out.endswith(b"\r") and out[:-1].lower() in {b"l", b"look"}:
                    sent_look = True
                telnet.send(out)
    finally:
        try:
            sock.shutdown(socket.SHUT_RDWR)
        except OSError:
            pass
        sock.close()

    lines = [
        json.loads(line)
        for line in trace.read_text(encoding="utf-8").splitlines()
        if line.strip()
    ]
    realm_6n = [r for r in lines if r.get("in_realm") and r.get("has_6n")]
    realm_also = [r for r in lines if r.get("in_realm") and r.get("also_here")]
    print("--- summary ---")
    print(f"trace lines={len(lines)} realm_6n={len(realm_6n)} realm_also_here={len(realm_also)}")
    print(f"probe also_after_realm={also_after_realm} sixn_after_realm={sixn_after_realm} sent_look={sent_look}")
    print(f"in_realm={state.in_realm} room={state.room!r} hp={state.hp}")
    if realm_6n:
        print("sample 6n", realm_6n[0])
    if realm_also:
        print("sample also", realm_also[0])
    # Success: Also here while in realm without us sending look.
    ok = also_after_realm > 0 and not sent_look and sixn_after_realm > 0
    # Soft ok: Also here tick without look even if 6n missed in count
    soft = also_after_realm > 1 and not sent_look  # enter listing + tick
    print("PASS" if ok or soft else "FAIL")
    return 0 if (ok or soft) else 1


if __name__ == "__main__":
    raise SystemExit(main())
