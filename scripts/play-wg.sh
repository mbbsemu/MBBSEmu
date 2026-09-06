#!/usr/bin/env bash
# VGA 80×30 xterm → Win11 Worldgroup (not MBBSEmu).
# Usage: play-wg.sh [klymacks|matt|new] [-- extra bbs_client args]
set -euo pipefail

ROOT="$(cd "$(dirname "$(readlink -f "$0")")/.." && pwd)"
CLIENT="$ROOT/scripts/bbs_client.py"
HOST="${WG_HOST:-192.168.122.33}"
PORT="${WG_PORT:-23}"
PROFILE="${WG_PROFILE:-klymacks}"
EXTRA=()
if [[ $# -gt 0 && "$1" != -* ]]; then
  PROFILE="$1"
  shift
fi
EXTRA=("$@")

case "$PROFILE" in
  klymacks|sysop)
    CFG="$ROOT/config/wg-local.json"
    title="Finn's Realm NT — klymacks"
    wmclass="FinnsRealmWGKlymacks"
    ;;
  matt)
    CFG="$ROOT/config/wg-matt.json"
    title="Finn's Realm NT — matt"
    wmclass="FinnsRealmWGMatt"
    ;;
  new)
    CFG="$ROOT/config/wg-new.json"
    title="Finn's Realm NT — new"
    wmclass="FinnsRealmWGNew"
    EXTRA=(--no-auto "${EXTRA[@]}")
    ;;
  *)
    echo "Usage: $0 [klymacks|matt|new]"
    exit 1
    ;;
esac

if [[ ! -f "$CFG" ]]; then
  echo "Missing $CFG (gitignored Worldgroup login)."
  exit 1
fi
if ! command -v python3 >/dev/null 2>&1; then
  echo "python3 is required."
  exit 1
fi

export PYTHONPATH="$ROOT${PYTHONPATH:+:$PYTHONPATH}"
export DISPLAY="${DISPLAY:-:0}"
client=(python3 "$CLIENT" --config "$CFG" "$HOST" "$PORT" "${EXTRA[@]}")

face='monospace'
size=16
if fc-list 'Px IBM VGA8' family 2>/dev/null | grep -q 'Px IBM VGA8'; then
  face='Px IBM VGA8'
  size=18
fi

echo "Opening $title. Click that window."
if [[ -x /usr/bin/xterm ]]; then
  exec xterm +aw +sb \
    -class "$wmclass" \
    -geometry 80x30 \
    -tn xterm-256color \
    -bg '#0b0e0c' -fg '#c8d2c8' -cr '#3dff9a' \
    -fa "$face" -fs "$size" \
    -b 12 \
    -title "$title" \
    -n "$title" \
    -xrm 'XTerm*scrollBar: false' \
    -xrm 'XTerm*selectToClipboard: true' \
    -xrm 'XTerm*cursorBlink: true' \
    -xrm 'XTerm*cursorOnTime: 480' \
    -xrm 'XTerm*cursorOffTime: 280' \
    -xrm 'XTerm*XftAntialias: false' \
    -xrm 'XTerm.VT100.translations: #override <Key>F11: string(0x1b) string("[23~")\\n<Key>F12: string(0x1b) string("[24~")' \
    -e "${client[@]}"
fi
if command -v konsole >/dev/null 2>&1; then
  exec konsole --hide-menubar --hide-tabbar --geometry 720x640 \
    --title "$title" --name "$wmclass" -e "${client[@]}"
fi
exec "${client[@]}"
