#!/usr/bin/env bash
# Start MBBSEmu if it is not already up. Clears WCCRECOV.FLG so a SIGTERM
# leftover does not boot Recovery Mode (thin monsters, empty crypt).
set -euo pipefail
source "$(dirname "$0")/common.sh"
require_emulator

port_up() {
  python3 -c "import socket; s=socket.socket(); s.settimeout(0.4); s.connect(('127.0.0.1', $TELNET_PORT)); s.close()" 2>/dev/null
}

if emulator_running || port_up; then
  exit 0
fi

SETTINGS="$CONFIG_DIR/appsettings.json"
write_runtime_modules
rm -f "$MODULE_DIR/WCCRECOV.FLG"
mkdir -p "$DATA_DIR"
cd "$DATA_DIR"
DBR=(-DBREBUILD BBSUSR)
if [[ ! -f mbbsemu.db ]]; then
  DBR=(-DBRESET "$SYSOP_PASSWORD")
fi
setsid "$EMULATOR" -CLI -S "$SETTINGS" -C "$RUNTIME_MODULES" "${DBR[@]}" >>"$LOG_FILE" 2>&1 &
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
  echo "Board did not come up on port $TELNET_PORT." >&2
  tail -n 40 "$LOG_FILE" 2>/dev/null || true
  exit 1
fi
if [[ -f "$ROOT/scripts/record-boot.py" ]]; then
  python3 "$ROOT/scripts/record-boot.py" "$ROOT" || true
fi
