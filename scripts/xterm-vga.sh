#!/usr/bin/env bash
# Fixed 80×30 VGA xterm for the Finn's Realm client.
# Maximize / drag-resize only adds dead black; the BBS screen stays 80×30.
set -euo pipefail
ROOT="$(cd "$(dirname "$(readlink -f "$0")")/.." && pwd)"
LOCK="$ROOT/scripts/lock-x11-frame.py"
ICON_HINT="$ROOT/client/icons/finns-realm"
ICON_PNG="$ROOT/client/icons/finns-realm.png"

usage() {
  echo "Usage: xterm-vga.sh TITLE WM_CLASS -- command..." >&2
  exit 1
}

[[ $# -ge 4 && "$3" == "--" ]] || usage
title="$1"
wmclass="$2"
shift 3

# shellcheck source=vga-face.sh
source "$ROOT/scripts/vga-face.sh"
vga_pick_face
face="$VGA_FACE"
size="$VGA_SIZE"

if [[ ! -x /usr/bin/xterm ]]; then
  exec "$@"
fi

icon_xrm=()
xpm_list=()
for sz in 16x16 32x32 48x48; do
  [[ -f "${ICON_HINT}_${sz}.xpm" ]] && xpm_list+=("${ICON_HINT}_${sz}.xpm")
done
if [[ ${#xpm_list[@]} -gt 0 ]]; then
  IFS=,
  joined="${xpm_list[*]}"
  unset IFS
  # -class is not XTerm, so XTerm*iconHint never applies. Bind the
  # instance/class we actually set, plus a wildcard for Xt lookup.
  icon_xrm=(
    -xrm "*iconHint: ${joined}"
    -xrm "${wmclass}*iconHint: ${joined}"
    -xrm "${wmclass}.iconHint: ${joined}"
  )
fi

# -name sets WM_CLASS instance. Plasma's taskbar matches that to a .desktop
# file; leaving it as "xterm" shows the XT icon even when the titlebar is ours.
xterm -name "$wmclass" -class "$wmclass" \
  +aw +sb +fullscreen +maximized \
  -geometry 80x30 \
  -tn xterm-256color \
  -bg '#0b0e0c' -fg '#c8d2c8' -cr '#3dff9a' \
  -fa "$face" -fs "$size" \
  -b 12 \
  -xrm "*faceName: $face" \
  -xrm "*faceSize: $size" \
  -title "$title" \
  -n "$title" \
  "${icon_xrm[@]}" \
  -xrm '*scrollBar: false' \
  -xrm '*selectToClipboard: true' \
  -xrm '*cursorBlink: true' \
  -xrm '*cursorOnTime: 480' \
  -xrm '*cursorOffTime: 280' \
  -xrm '*XftAntialias: false' \
  -xrm '*fullscreen: never' \
  -xrm '*allowWindowOps: false' \
  -xrm '*allowFontOps: false' \
  -xrm '*VT100.translations: #override <Key>F11: string(0x1b) string("[23~")\\n<Key>F12: string(0x1b) string("[24~")' \
  -e "$@" &
pid=$!
if [[ -f "$LOCK" ]]; then
  desk=()
  if [[ -n "${FINNS_DESKTOP:-}" ]]; then
    desk=(--desktop "$FINNS_DESKTOP")
  fi
  python3 "$LOCK" --pid "$pid" --class "$wmclass" --icon "$ICON_PNG" "${desk[@]}" || true
fi
wait "$pid"
