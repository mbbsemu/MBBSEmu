#!/usr/bin/env bash
# VGA 80x30 xterm → Win11 Worldgroup (not MBBSEmu).
# Usage: play-wg.sh [profile|new|maint] [-- extra bbs_client args]
# maint = sysop/klymacks BBS login only (USERCOPY, ANSI, FSE). Does not press M.
# Hunt/kit: AUTO_PLAY_PROFILES in modules.party get --auto-play; others --no-auto-play (F7).
# --no-auto is BBS login, not hunt. Extra args after the profile override.
set -euo pipefail

ROOT="$(cd "$(dirname "$(readlink -f "$0")")/.." && pwd)"
CLIENT="$ROOT/scripts/bbs_client.py"
ROSTER="$ROOT/scripts/wg_roster.py"
HOST="${WG_HOST:-192.168.122.33}"
PORT="${WG_PORT:-23}"
PROFILE="${WG_PROFILE:-klymacks}"
EXTRA=()
if [[ $# -gt 0 && "$1" != -* ]]; then
  PROFILE="$1"
  shift
fi
EXTRA=("$@")

wmclass="FinnsRealmWG${PROFILE}"
CFG=""
FINNS_DESKTOP="finns-realm-${PROFILE}"
case "$PROFILE" in
  klymacks|sysop)
    CFG="$ROOT/config/wg-local.json"
    wmclass="FinnsRealmWGklymacks"
    FINNS_DESKTOP="finns-realm-klymacks"
    ;;
  new)
    CFG="$ROOT/config/wg-new.json"
    wmclass="FinnsRealmWGNew"
    FINNS_DESKTOP="finns-realm-new"
    ;;
  maint|bbs)
    CFG="$ROOT/config/wg-local.json"
    wmclass="FinnsRealmWGBBS"
    FINNS_DESKTOP="finns-realm-bbs"
    ;;
  *)
    CFG="$ROOT/config/wg-${PROFILE}.json"
    ;;
esac

case "$PROFILE" in
  new)
    EXTRA=(--no-auto --no-auto-play "${EXTRA[@]}")
    ;;
  maint|bbs)
    EXTRA=(--bbs --no-auto-play "${EXTRA[@]}")
    ;;
  *)
    if python3 "$ROSTER" auto-play "$PROFILE"; then
      EXTRA=(--auto-play "${EXTRA[@]}")
    else
      EXTRA=(--no-auto-play "${EXTRA[@]}")
    fi
    ;;
esac
export FINNS_DESKTOP

_title_from_cfg() {
  python3 -c '
import json, sys
p = json.load(open(sys.argv[1]))
u = str(p.get("username") or "").strip()
g = str(p.get("given") or "").strip()
if u.lower() in {"klymacks", "sysop"} or g.lower() == "klymacks":
    print("klymacks")
elif g:
    print(g[:1].upper() + g[1:] if len(g) > 1 else g.upper())
elif u:
    print(u[:1].upper() + u[1:])
else:
    print("new")
' "$1"
}

case "$PROFILE" in
  new)
    title="Finn's Realm — new"
    ;;
  maint|bbs)
    title="Finn's Realm BBS — klymacks"
    ;;
  *)
    if [[ -f "$CFG" ]]; then
      title="Finn's Realm — $(_title_from_cfg "$CFG")"
    else
      title="Finn's Realm — ${PROFILE}"
    fi
    ;;
esac

if [[ ! -f "$CFG" ]]; then
  echo "Missing $CFG (gitignored Worldgroup login). Run ./scripts/install-wg-shortcuts.sh"
  exit 1
fi
if ! command -v python3 >/dev/null 2>&1; then
  echo "python3 is required."
  exit 1
fi

export PYTHONPATH="$ROOT${PYTHONPATH:+:$PYTHONPATH}"
export DISPLAY="${DISPLAY:-:0}"
client=(python3 "$CLIENT" --config "$CFG" "$HOST" "$PORT" "${EXTRA[@]}")

echo "Opening $title. Click that window (80x30, size locked)."
if [[ -x /usr/bin/xterm ]]; then
  exec "$ROOT/scripts/xterm-vga.sh" "$title" "$wmclass" -- "${client[@]}"
fi
if command -v konsole >/dev/null 2>&1; then
  exec konsole --hide-menubar --hide-tabbar --geometry 720x640 \
    --title "$title" --name "$wmclass" -e "${client[@]}"
fi
exec "${client[@]}"
