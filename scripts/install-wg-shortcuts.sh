#!/usr/bin/env bash
# NT Worldgroup launchers, plus a DOS MBBSEmu test window for klymacks.
set -euo pipefail
ROOT="$(cd "$(dirname "$(readlink -f "$0")")/.." && pwd)"
PLAY_WG="$ROOT/scripts/play-wg.sh"
PLAY_DOS="$ROOT/scripts/play-dos.sh"
REBOOT="$ROOT/scripts/reboot-board.sh"
DESK="${XDG_DESKTOP_DIR:-$HOME/Desktop}"
APPS="$HOME/.local/share/applications"
mkdir -p "$DESK" "$APPS"

# Old DOS names we used to clobber NT with; keep NT + add a DOS test icon.
rm -f \
  "$DESK/Finn's Realm — Matt.desktop" \
  "$DESK/Check Finn's Realm.desktop" \
  "$DESK/Stop Finn's Realm.desktop" \
  "$DESK/Start BBS.desktop" \
  "$DESK/Start BBS.lnk" \
  "$DESK/Finn's Realm NT — klymacks.desktop" \
  "$DESK/Finn's Realm NT — matt.desktop" \
  "$DESK/Finn's Realm NT — new.desktop" \
  "$APPS/Finn's Realm — Matt.desktop" \
  "$APPS/finns-realm-check.desktop" \
  "$APPS/finns-realm-stop.desktop" \
  "$APPS/finns-realm-nt-klymacks.desktop" \
  "$APPS/finns-realm-nt-matt.desktop" \
  "$APPS/finns-realm-nt-new.desktop"

install_desk() {
  local dest="$1" name="$2" comment="$3" exec_line="$4" wmclass="$5"
  cat > "$dest" <<EOF
[Desktop Entry]
Type=Application
Name=$name
Comment=$comment
Exec=$exec_line
Path=$ROOT
Terminal=false
Categories=Game;
StartupNotify=true
StartupWMClass=$wmclass
EOF
  chmod +x "$dest"
  gio set "$dest" metadata::trusted true 2>/dev/null || true
}

install_desk "$DESK/Finn's Realm — klymacks.desktop" \
  "Finn's Realm — klymacks" \
  "Worldgroup 1.11p NT — sysop / klymacks (VGA 80x30)" \
  "$PLAY_WG klymacks" \
  FinnsRealmWGKlymacks

install_desk "$DESK/Finn's Realm — matt.desktop" \
  "Finn's Realm — matt" \
  "Worldgroup 1.11p NT — matt (VGA 80x30)" \
  "$PLAY_WG matt" \
  FinnsRealmWGMatt

install_desk "$DESK/Finn's Realm — new.desktop" \
  "Finn's Realm — new" \
  "Worldgroup 1.11p NT — type login yourself (VGA 80x30)" \
  "$PLAY_WG new" \
  FinnsRealmWGNew

install_desk "$DESK/Finn's Realm DOS — klymacks.desktop" \
  "Finn's Realm DOS — klymacks" \
  "MBBSEmu localhost:2323 — klymacks (VGA 80x30, testing)" \
  "$PLAY_DOS" \
  FinnsRealmKlymacks

install_desk "$DESK/Reboot Finn's Realm DOS.desktop" \
  "Reboot Finn's Realm DOS" \
  "Stop and start local MBBSEmu on 2323" \
  "$REBOOT" \
  FinnsRealmDosReboot

cp -a "$DESK/Finn's Realm — klymacks.desktop" "$APPS/finns-realm.desktop"
cp -a "$DESK/Finn's Realm — matt.desktop" "$APPS/finns-realm-matt.desktop"
cp -a "$DESK/Finn's Realm — new.desktop" "$APPS/finns-realm-new.desktop"
cp -a "$DESK/Finn's Realm DOS — klymacks.desktop" "$APPS/finns-realm-dos-klymacks.desktop"
cp -a "$DESK/Reboot Finn's Realm DOS.desktop" "$APPS/finns-realm-dos-reboot.desktop"
echo "Desktop: klymacks/matt/new → Worldgroup VM (play-wg.sh)"
echo "Desktop: Finn's Realm DOS — klymacks → MBBSEmu 127.0.0.1:2323 (play-dos.sh)"
