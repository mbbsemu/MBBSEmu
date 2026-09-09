"""Worldgroup realm session cues. Brain / bbs_client ask; they do not invent FSD rules.

Room listings (Also here / You notice / Obvious exits) arrive from:
  - move / combat / arrive reprints
  - bare Enter when Statline is Full (brief room reprint — quiet)
  - typed ``look`` (loud: “X is looking around the room”)

Live Curtis probes: idle standing does **not** emit a WG timer or in-realm
CSI 6n. Login Auto-sensing still sends 6n; answer the **real** caret
(row+col). Live TRAIN STATS keeps the field caret. Never “kick” with look
just because ``scanned`` is false — soft-refresh with Enter first.
"""

from __future__ import annotations

# Unscanned room: wait this long, then soft-refresh (Enter) before look.
LOOK_GAP = 12.0
# How many bare-Enter soft refreshes before an active look.
SOFT_REFRESH_TRIES = 1
# Prompt band row on 80×25 (documentation / tests). Live CPR uses real cx too.
REALM_CPR_ROW = 25


def realm_cpr_reply(row: int | None = None, col: int = 1) -> bytes:
    """Telnet CPR helper. Prefer AnsiScreen real caret in bbs_client."""
    r = REALM_CPR_ROW if row is None else row
    return f"\x1b[{r};{col}R".encode("ascii")


def room_refresh_cmd(*, soft_tries_used: int) -> str:
    """How to refresh Also here when unscanned past LOOK_GAP.

    ``\"\"`` → bare Enter (Statline Full brief reprint, no looking-around).
    ``\"look\"`` → loud look after soft tries are exhausted.
    """
    if soft_tries_used < SOFT_REFRESH_TRIES:
        return ""
    return "look"
