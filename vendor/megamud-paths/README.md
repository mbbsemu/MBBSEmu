# MegaMud path pack

Recorded MajorMUD walks from MegaMud (`.mp` step tapes, `.inf` notes).

The client indexes every `.mp` and, when the live room matches a tape's start,
takes the first step of the shortest tape whose end is a nav destination
(Town Square, Graveyard Bridge, …). Finn titles `Bridge` and MegaMud
`Graveyard Bridge` are treated as the same tile.

## Layout

| Path | Role |
|---|---|
| `allpaths/` | Full 1998–2000 community pack (~412 `.mp`, ~133 `.inf`) as shipped in `allpaths.zip`. Original names (`GAV1LOOP.MP`). |
| `*.mp` in this directory | Winterhawk / Finn-checked seeds. Same stem as a pack file **wins** (e.g. `GAV1LOOP.mp` is the complete Silvermere sewers, not Connor's town-sewer loop in `allpaths/`). |
| `data/klymacks-maps/` | Finn's Realm edits. Same stem wins over everything here. |

Do not flatten `allpaths/` onto this directory — several stems collide and the Winterhawk tapes are the ones we verified live.

## What the bot will not do

- Town Square loops (`STSQLOOP`, quest tapes, …) never fire as “go to the graveyard.” First step is `go manhole`.
- Farm loops without a destination only apply when we are already on the graveyard / bridge and hunting GY.
- `.inf` files are comments (mobs, level, author). They are not walked.

Verify a tape on Finn's Realm before treating it as gospel. Room hashes are MegaMud's; we only use the compass/special steps and the `[CODE:Area:Title]` start/end lines.
