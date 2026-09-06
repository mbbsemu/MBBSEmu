#!/usr/bin/env bash
# Same 80×30 Finn's Realm window, telnet to the Win11 Worldgroup guest.
# Does not start MBBSEmu. DOS board stays on 127.0.0.1:2323.
set -euo pipefail

ROOT="$(cd "$(dirname "$(readlink -f "$0")")/.." && pwd)"
CLIENT="$ROOT/scripts/bbs_client.py"
CFG="$ROOT/config/wg-local.json"
HOST="${WG_HOST:-192.168.122.33}"
PORT="${WG_PORT:-23}"

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
title="Finn's Realm — Worldgroup"
wmclass="FinnsRealmWG"
client=(python3 "$CLIENT" --config "$CFG" "$HOST" "$PORT" "$@")

face='monospace'
size=16
if fc-list 'Px IBM VGA8' family 2>/dev/null | grep -q 'Px IBM VGA8'; then
  face='Px IBM VGA8'
  size=18
fi

open_client() {
  if [[ -x /usr/bin/xterm ]]; then
    exec xterm +aw +sb \
      -class "$wmclass" \
      -geometry 80x30 \
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
}

cols="$(tput cols 2>/dev/null || echo 0)"
rows="$(tput lines 2>/dev/null || echo 0)"
in_cursor=0
if [[ "${TERM_PROGRAM:-}" == "vscode" || -n "${CURSOR_TRACE_ID:-}" || -n "${VSCODE_INJECTION:-}" ]]; then
  in_cursor=1
fi
if [[ ! -t 0 || "$in_cursor" -eq 1 || "$cols" -lt 80 || "$rows" -lt 24 ]]; then
  echo "Opening Worldgroup in its own window. Click that window."
  open_client
fi
exec "${client[@]}"
