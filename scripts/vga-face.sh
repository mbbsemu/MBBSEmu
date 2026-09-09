#!/usr/bin/env bash
# Shared xterm face for NT and DOS. Exact family only — `fc-match` on a
# missing PxPlus name silently returns Noto/Plex and the sheet looks modern.
vga_pick_face() {
  local family
  local -a faces=(
    'Px IBM VGA8'
    'PxPlus IBM VGA8'
    'Px IBM VGA9'
    'Px VGA SquarePx'
  )
  for family in "${faces[@]}"; do
    if fc-list ":family=${family}" family 2>/dev/null | grep -Fxq "$family"; then
      VGA_FACE="$family"
      VGA_SIZE=18
      return 0
    fi
  done
  VGA_FACE='monospace'
  VGA_SIZE=16
}
