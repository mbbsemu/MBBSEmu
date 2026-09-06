#!/usr/bin/env bash
# Stop Finn's Realm on port 2323 and start it again.
# Double-click the desktop shortcut, or: ./scripts/reboot-board.sh
set -euo pipefail

HERE="$(cd "$(dirname "$(readlink -f "$0")")" && pwd)"

# A packaged copy sitting inside this repo is not a second board.
if [[ "$HERE" == */dist/finns-realm ]]; then
  _live="$(cd "$HERE/../.." && pwd)"
  if [[ -f "$_live/modules/WCCMMUD/WCCMMUD.DLL" && -d "$_live/scripts" ]]; then
    HERE="$_live/scripts"
  fi
fi

if [[ -x "$HERE/bin/MBBSEmu" ]]; then
  ROOT="$HERE"
  EMU="$HERE/bin/MBBSEmu"
elif [[ -x "$HERE/../MBBSEmu/bin/Release/net10.0/MBBSEmu" ]]; then
  ROOT="$(cd "$HERE/.." && pwd)"
  EMU="$ROOT/MBBSEmu/bin/Release/net10.0/MBBSEmu"
elif [[ -x "$HERE/../vendor/mbbsemu/MBBSEmu" ]]; then
  ROOT="$(cd "$HERE/.." && pwd)"
  EMU="$ROOT/vendor/mbbsemu/MBBSEmu"
else
  echo "Finn's Realm: MBBSEmu is not in this folder."
  read -rp "Enter to close" || true
  exit 1
fi

CONFIG="$ROOT/config"
DATA="$ROOT/data"
SETTINGS="$CONFIG/appsettings.json"
MODULES="$DATA/modules.json"
LOG="$DATA/mbbsemu.log"
PID_FILE="$DATA/mbbsemu.pid"
PORT="${TELNET_PORT:-2323}"
SYSOP_PASSWORD="${SYSOP_PASSWORD:-sysop}"

mkdir -p "$DATA" "$CONFIG"

write_modules_json() {
  cat > "$MODULES" <<EOF
{
  "Modules": [
    {
      "Identifier": "WCCMMUD",
      "Path": "$ROOT/modules/WCCMMUD/",
      "MenuOptionKey": "M",
      "Enabled": 1
    }
  ]
}
EOF
}

if [[ ! -f "$MODULES" ]]; then
  write_modules_json
fi

port_up() {
  python3 -c "import socket; s=socket.socket(); s.settimeout(0.4); s.connect(('127.0.0.1', $PORT)); s.close()" 2>/dev/null
}

listener_pids() {
  ss -H -tlnp "sport = :$PORT" 2>/dev/null | grep -oP 'pid=\K[0-9]+' | sort -u
}

emu_pids() {
  {
    listener_pids
    pgrep -x MBBSEmu || true
    if [[ -f "$PID_FILE" ]]; then
      cat "$PID_FILE"
    fi
    if [[ -f "$ROOT/dist/finns-realm/data/mbbsemu.pid" ]]; then
      cat "$ROOT/dist/finns-realm/data/mbbsemu.pid"
    fi
  } | tr -d ' ' | grep -E '^[0-9]+$' | sort -u
}

echo "Finn's Realm — reboot"
echo "Stopping the board..."
mapfile -t pids < <(emu_pids)
if [[ ${#pids[@]} -eq 0 ]]; then
  echo "Nothing was running."
else
  for pid in "${pids[@]}"; do
    if [[ -d "/proc/$pid" ]]; then
      echo "  SIGTERM $pid"
      kill -TERM "$pid" 2>/dev/null || true
    fi
  done
  for _ in $(seq 1 20); do
    still=0
    for pid in "${pids[@]}"; do
      if [[ -d "/proc/$pid" ]]; then
        still=1
      fi
    done
    if [[ "$still" == 0 ]]; then
      break
    fi
    sleep 1
  done
  for pid in "${pids[@]}"; do
    if [[ -d "/proc/$pid" ]]; then
      echo "  SIGKILL $pid"
      kill -KILL "$pid" 2>/dev/null || true
    fi
  done
fi
rm -f "$PID_FILE"

for _ in $(seq 1 10); do
  if ! port_up; then
    break
  fi
  sleep 1
done
if port_up; then
  echo "Port $PORT is still in use. Close whatever is bound to it and try again."
  read -rp "Enter to close" || true
  exit 1
fi

# MajorMUD writes WCCRECOV.FLG while it is up and only deletes it in finrou.
# SIGTERM skips that, so the leftover lock made every reboot recover.
rm -f "$ROOT/modules/WCCMMUD/WCCRECOV.FLG"
# FINNS_RECOVER=1 ./scripts/reboot-board.sh  — plant the flag after that
# delete so the next boot rebuilds room/monster pointers (monbadroom).
if [[ "${FINNS_RECOVER:-0}" != 0 ]]; then
  : > "$ROOT/modules/WCCMMUD/WCCRECOV.FLG"
  echo "Recovery boot: WCCRECOV.FLG planted. Monsters stay thin until recovery finishes."
fi

if [[ ! -x "$EMU" ]]; then
  echo "MBBSEmu missing: $EMU"
  read -rp "Enter to close" || true
  exit 1
fi
if [[ ! -f "$SETTINGS" ]]; then
  echo "Missing $SETTINGS"
  read -rp "Enter to close" || true
  exit 1
fi
if [[ ! -f "$ROOT/modules/WCCMMUD/WCCMMUD.DLL" ]]; then
  echo "MajorMUD files missing from $ROOT/modules/WCCMMUD/"
  echo "From the repo: ./scripts/restore-from-package.sh"
  read -rp "Enter to close" || true
  exit 1
fi
if [[ ! -f "$MODULES" ]]; then
  write_modules_json
fi

PREFLIGHT="$ROOT/scripts/preflight.py"
if [[ "${FINNS_PREFLIGHT:-1}" != 0 && -f "$PREFLIGHT" ]]; then
  echo "Preflight..."
  set +e
  python3 "$PREFLIGHT" "$ROOT"
  pf=$?
  set -e
  if [[ "$pf" -eq 1 ]]; then
    echo
    echo "Board not started. Read $ROOT/scripts/PREFLIGHT.md"
    echo "Clean slate: ./scripts/reset-game.sh"
    read -rp "Enter to close" || true
    exit 1
  fi
fi

echo "Starting the board..."
cd "$DATA"
DBR=(-DBREBUILD BBSUSR)
if [[ ! -f mbbsemu.db ]]; then
  DBR=(-DBRESET "$SYSOP_PASSWORD")
fi
setsid "$EMU" -CLI -S "$SETTINGS" -C "$MODULES" "${DBR[@]}" >>"$LOG" 2>&1 &
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
  echo "Board did not come up on port $PORT. Last log lines:"
  tail -n 40 "$LOG" 2>/dev/null || true
  read -rp "Enter to close" || true
  exit 1
fi

echo
echo "Board is up on port $PORT."
if [[ -f "$ROOT/scripts/record-boot.py" ]]; then
  python3 "$ROOT/scripts/record-boot.py" "$ROOT" || true
fi
echo "Click Finn's Realm to play."
read -rp "Enter to close" || true
