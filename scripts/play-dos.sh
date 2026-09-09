#!/usr/bin/env bash
# VGA 80×30 xterm → local MBBSEmu (127.0.0.1:2323), not Worldgroup NT.
# Default login is BBS sysop / sysop (mud given name klymacks). There is no
# DOS BBS user "klymacks" — player.json klymacks/klymacks1 is NT-only.
# Usage: play-dos.sh [--sysop|--matt|-- extra bbs_client args]
set -euo pipefail
HERE="$(dirname "$(readlink -f "$0")")"
if [[ $# -eq 0 ]]; then
  exec "$HERE/FinnsRealm" --sysop
fi
exec "$HERE/FinnsRealm" "$@"
