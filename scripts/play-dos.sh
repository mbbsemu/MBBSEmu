#!/usr/bin/env bash
# VGA 80×30 xterm → local MBBSEmu (127.0.0.1:2323), not Worldgroup NT.
# Starts the DOS board if needed. Login: config/player.json (klymacks).
# Usage: play-dos.sh [--sysop|--matt|-- extra bbs_client args]
set -euo pipefail
exec "$(dirname "$(readlink -f "$0")")/FinnsRealm" "$@"
