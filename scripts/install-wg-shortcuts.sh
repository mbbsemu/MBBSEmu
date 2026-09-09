#!/usr/bin/env bash
# NT Worldgroup launchers, plus a DOS MBBSEmu test window for klymacks.
set -euo pipefail
ROOT="$(cd "$(dirname "$(readlink -f "$0")")/.." && pwd)"
PLAY_WG="$ROOT/scripts/play-wg.sh"
PLAY_DOS="$ROOT/scripts/play-dos.sh"
REBOOT="$ROOT/scripts/reboot-board.sh"
DESK="${XDG_DESKTOP_DIR:-$HOME/Desktop}"
REALM="$DESK/Finn's Realm"
NT="$REALM/NT"
DOS="$REALM/DOS"
# Codes/ is registration lists — we never write there.
APPS="$HOME/.local/share/applications"
mkdir -p "$NT" "$DOS" "$APPS"

python3 "$ROOT/scripts/wg_roster.py" write-configs

install_icons() {
  local hicolor="$HOME/.local/share/icons/hicolor"
  local pixmaps="$HOME/.local/share/pixmaps"
  local src="$ROOT/client/icons"
  mkdir -p "$pixmaps"
  cp -a "$src/finns-realm.png" "$pixmaps/finns-realm.png"
  local size file
  for size in 16 32 48 256; do
    file="$src/finns-realm-${size}.png"
    [[ -f "$file" ]] || file="$src/finns-realm.png"
    mkdir -p "$hicolor/${size}x${size}/apps"
    cp -a "$file" "$hicolor/${size}x${size}/apps/finns-realm.png"
  done
  # Plasma panel often asks for 22/24.
  mkdir -p "$hicolor/22x22/apps" "$hicolor/24x24/apps"
  cp -a "$hicolor/32x32/apps/finns-realm.png" "$hicolor/22x22/apps/finns-realm.png"
  cp -a "$hicolor/32x32/apps/finns-realm.png" "$hicolor/24x24/apps/finns-realm.png"
  gtk-update-icon-cache -f "$hicolor" 2>/dev/null || true
  kbuildsycoca6 --noincremental 2>/dev/null || kbuildsycoca5 --noincremental 2>/dev/null || true
}

install_icons

# Leftovers from when launchers sat on the Desktop root. Do not touch Codes/
# or extra files already in NT/ (sysop.old, etc.).
find "$DESK" -maxdepth 1 \( \
  -name "Finn's Realm*.desktop" \
  -o -name "Reboot Finn's Realm*.desktop" \
  -o -name "Check Finn's Realm.desktop" \
  -o -name "Stop Finn's Realm.desktop" \
  -o -name "Start BBS.desktop" \
  -o -name "Start BBS.lnk" \
  \) -delete 2>/dev/null || true
rm -f \
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
Icon=finns-realm
Terminal=false
Categories=Game;
StartupNotify=true
StartupWMClass=$wmclass
EOF
  chmod +x "$dest"
  gio set "$dest" metadata::trusted true 2>/dev/null || true
}

while IFS=$'\t' read -r profile title wmclass; do
  [[ -z "$profile" ]] && continue
  if python3 "$ROOT/scripts/wg_roster.py" auto-play "$profile"; then
    play_flag="--auto-play"
  else
    play_flag="--no-auto-play"
  fi
  dest="$NT/${title}.desktop"
  install_desk "$dest" "$title" \
    "Worldgroup 1.11p NT — $profile (VGA 80x30)" \
    "$PLAY_WG $profile $play_flag" \
    "$wmclass"
  cp -a "$dest" "$APPS/finns-realm-${profile}.desktop"
done < <(python3 "$ROOT/scripts/wg_roster.py" desktop)

install_desk "$NT/Finn's Realm — new.desktop" \
  "Finn's Realm — new" \
  "Worldgroup 1.11p NT — type login yourself (VGA 80x30)" \
  "$PLAY_WG new --no-auto-play" \
  FinnsRealmWGNew

install_desk "$NT/Finn's Realm BBS — klymacks.desktop" \
  "Finn's Realm BBS — klymacks" \
  "Worldgroup NT — sysop login, stay on BBS menu (maint)" \
  "$PLAY_WG maint --no-auto-play" \
  FinnsRealmWGBBS

install_desk "$DOS/Finn's Realm DOS — klymacks.desktop" \
  "Finn's Realm DOS — klymacks" \
  "MBBSEmu localhost:2323 — BBS sysop, mud klymacks (VGA 80x30, testing)" \
  "$PLAY_DOS" \
  FinnsRealmKlymacks

install_desk "$DOS/Reboot Finn's Realm DOS.desktop" \
  "Reboot Finn's Realm DOS" \
  "Stop and start local MBBSEmu on 2323" \
  "$REBOOT" \
  FinnsRealmDosReboot

cp -a "$NT/Finn's Realm — new.desktop" "$APPS/finns-realm-new.desktop"
cp -a "$NT/Finn's Realm BBS — klymacks.desktop" "$APPS/finns-realm-bbs.desktop"
cp -a "$DOS/Finn's Realm DOS — klymacks.desktop" "$APPS/finns-realm-dos-klymacks.desktop"
cp -a "$DOS/Reboot Finn's Realm DOS.desktop" "$APPS/finns-realm-dos-reboot.desktop"
echo "NT: $NT  (roster + new + BBS maint)"
echo "DOS: $DOS  (MBBSEmu play + reboot)"
echo "Codes: $REALM/Codes  (yours — installer does not write there)"
