#!/usr/bin/env bash
# Full MajorMUD reset: virgin databases, DEMO activation, empty addons.
# Default: boots DEMO so SYSOP C/CP can run.
#   ./scripts/reset-game.sh --no-boot
#     stop, wipe, restore DEMO MSG, do not start the emulator.
# Put BTURNO + ACTIVATE in before the first boot when using --no-boot.
set -euo pipefail

BOOT=1
if [[ "${1:-}" == "--no-boot" ]]; then
  BOOT=0
  shift
fi

ROOT="$(cd "$(dirname "$(readlink -f "$0")")/.." && pwd)"
MODULE="$ROOT/modules/WCCMMUD"
DATA="$ROOT/data"
EMU="$ROOT/MBBSEmu/bin/Release/net10.0/MBBSEmu"
SETTINGS="$ROOT/config/appsettings.json"
MODULES_JSON="$DATA/modules.json"
LOG="$DATA/mbbsemu.log"
PID_FILE="$DATA/mbbsemu.pid"
PORT="${TELNET_PORT:-2323}"

port_up() {
  python3 -c "import socket; s=socket.socket(); s.settimeout(0.4); s.connect(('127.0.0.1', $PORT)); s.close()" 2>/dev/null
}

if [[ "$BOOT" == 1 ]]; then
  echo "Finn's Realm — start from scratch (DEMO, then boot)"
else
  echo "Finn's Realm — start from scratch (no boot)"
fi

echo "Stopping the board..."
if [[ -f "$PID_FILE" ]]; then
  pid="$(cat "$PID_FILE")"
  if [[ -n "$pid" && -d "/proc/$pid" ]]; then
    kill -TERM "$pid" 2>/dev/null || true
  fi
fi
mapfile -t pids < <(ss -H -tlnp "sport = :$PORT" 2>/dev/null | grep -oP 'pid=\K[0-9]+' | sort -u || true)
for pid in "${pids[@]:-}"; do
  [[ -n "$pid" && -d "/proc/$pid" ]] && kill -TERM "$pid" 2>/dev/null || true
done
for _ in $(seq 1 20); do
  port_up || break
  sleep 1
done
mapfile -t pids < <(ss -H -tlnp "sport = :$PORT" 2>/dev/null | grep -oP 'pid=\K[0-9]+' | sort -u || true)
for pid in "${pids[@]:-}"; do
  [[ -n "$pid" && -d "/proc/$pid" ]] && kill -KILL "$pid" 2>/dev/null || true
done
rm -f "$PID_FILE"
port_up && { echo "Port $PORT still in use."; exit 1; }

mkdir -p "$DATA"
# shellcheck source=common.sh
source "$ROOT/scripts/common.sh"
wipe_license_leftovers "$MODULE" "$DATA"

echo "Restoring DEMO activation..."
cp -a "$MODULE/WCCMMUD.MSG.original" "$MODULE/WCCMMUD.MSG"
python3 - "$MODULE/WCCMMPLS.MSG" <<'PY'
from pathlib import Path
import re, sys
p = Path(sys.argv[1])
text = p.read_text(errors='replace')
text2, n = re.subn(r'(ACTIVATE \{)[^}]*(\})', r'\1DEMO\2', text, count=1)
if n != 1:
    raise SystemExit(f'could not reset Plus ACTIVATE ({n})')
p.write_text(text2)
print('Plus ACTIVATE -> DEMO')
PY

echo "Restoring virgin game databases..."
shopt -s nullglob
for vir in "$MODULE"/*.VIR; do
  base="$(basename "$vir" .VIR)"
  cp -a "$vir" "$MODULE/${base}.DAT"
  rm -f "$MODULE/${base}.DB"
done
wipe_player_records "$MODULE" "$DATA"
echo "Play accounts: sysop plus matt (empty toons — you create them)."
ensure_play_accounts "$DATA/mbbsemu.db"

if [[ "$BOOT" != 1 ]]; then
  python3 - "$SETTINGS" <<'PY'
import json, sys
from pathlib import Path
p = Path(sys.argv[1])
data = json.loads(p.read_text())
data["GSBL.BTURNO"] = ""
p.write_text(json.dumps(data, indent=2) + "\n")
print("GSBL.BTURNO cleared — set the new 8-digit board number before boot")
PY
  rm -f "$DATA/last-boot.json"
  echo
  echo "Board is down. Databases are virgin. MSG is DEMO."
  echo "Set GSBL.BTURNO and MajorMUD ACTIVATE before the first boot."
  echo "Do not start the emulator until that pair is in."
  if [[ -f "$ROOT/scripts/preflight.py" ]]; then
    echo
    python3 "$ROOT/scripts/preflight.py" "$ROOT" || true
  fi
  exit 0
fi

echo "Starting the board..."
cd "$DATA"
setsid "$EMU" -CLI -S "$SETTINGS" -C "$MODULES_JSON" -DBREBUILD BBSUSR >>"$LOG" 2>&1 &
echo $! >"$PID_FILE"
ok=0
for _ in $(seq 1 45); do
  if port_up; then
    ok=1
    break
  fi
  sleep 1
done
if [[ "$ok" != 1 ]]; then
  echo "Board did not come up. Last log lines:"
  tail -n 40 "$LOG"
  exit 1
fi

echo "Clearing evil points and saved profiles (must happen before any character)..."
python3 - <<'PY'
import re, select, socket, sys, time

HOST, PORT = "127.0.0.1", 2323
IAC, DO, DONT, WILL, WONT, SB, SE = 255, 253, 254, 251, 252, 250, 240
ANSI_RE = re.compile(rb"\x1b\[[0-9;?]*[A-Za-z]")

def filt(data, sock, pending):
    pending.extend(data)
    out = bytearray()
    i = 0
    buf = pending
    while i < len(buf):
        b = buf[i]
        if b != IAC:
            out.append(b)
            i += 1
            continue
        if i + 1 >= len(buf):
            break
        c = buf[i + 1]
        if c in (DO, DONT, WILL, WONT):
            if i + 2 >= len(buf):
                break
            sock.sendall(bytes((IAC, WONT if c in (DO, DONT) else DONT, buf[i + 2])))
            i += 3
            continue
        if c == SB:
            j = i + 2
            while j + 1 < len(buf) and not (buf[j] == IAC and buf[j + 1] == SE):
                j += 1
            if j + 1 >= len(buf):
                break
            i = j + 2
            continue
        i += 2 if c == IAC else 2
    del pending[:i]
    return bytes(out)

def visible(data):
    return ANSI_RE.sub(b"", data).decode("latin1", "replace").lower()

def wait_for(sock, pending, needles, timeout):
    hay = ""
    deadline = time.time() + timeout
    while time.time() < deadline:
        ready, _, _ = select.select([sock], [], [], min(0.3, max(0.05, deadline - time.time())))
        if sock not in ready:
            continue
        data = sock.recv(4096)
        if not data:
            break
        out = filt(data, sock, pending)
        hay += visible(out)
        if any(n in hay for n in needles):
            return hay
    return hay

def send_line(sock, text):
    sock.sendall(text.encode("ascii") + b"\r")
    time.sleep(0.25)

sock = socket.create_connection((HOST, PORT), timeout=8)
sock.settimeout(None)
pending = bytearray()
try:
    wait_for(sock, pending, ["username:"], 10)
    send_line(sock, "sysop")
    hay = wait_for(sock, pending, ["password:", "already logged"], 8)
    if "already logged" in hay:
        raise SystemExit("sysop already logged in — close that window and re-run")
    send_line(sock, "sysop")
    wait_for(sock, pending, ["make your selection"], 12)
    send_line(sock, "M")
    wait_for(sock, pending, ["[majormud]", "enter the realm"], 10)
    send_line(sock, "SYSOP")
    wait_for(sock, pending, ["sysop menu", "clear all saved"], 8)
    send_line(sock, "C")
    time.sleep(0.6)
    send_line(sock, "Y")
    time.sleep(0.6)
    send_line(sock, "CP")
    time.sleep(0.6)
    send_line(sock, "Y")
    time.sleep(0.8)
    send_line(sock, "X")
    time.sleep(0.4)
    print("C / CP sent")
finally:
    sock.close()
PY

echo
grep -aE 'DEMO MODE|users\]|Module Added|CNF options' "$LOG" | tail -8
echo
echo "Board is up in DEMO. Do not enter activation codes yet."
echo "This reset does not keep leftover Plus/Mud/addon tokens."
if [[ -f "$ROOT/scripts/preflight.py" ]]; then
  echo
  python3 "$ROOT/scripts/preflight.py" "$ROOT" || true
fi
