# MBBSEmu Module Compatibility Table

Source of truth: https://www.mbbsemu.com/modules (support level, per module) and https://wiki.mbbsemu.com (linked known issues, per module). Last synced 2026-08-17.

Re-sync by re-reading the module list embedded in the site's `Modules-*.js` asset bundle (the page itself is client-rendered, so the data isn't in the static HTML) and cross-checking each module's `known_issues` section on the wiki.

## Support levels

| Level | Meaning |
|---|---|
| 1 ? | Unknown |
| 2 ✖ | Crashes on Initialization |
| 3 ⚠ | Completes Initialization, Crashes on Entry |
| 4 ◐ | Somewhat Playable |
| 5 ◕ | Mostly Playable |
| 6 ✅ | Fully Working & Supported |

## Modules

| Identifier | Name | Level | Status | Known Issues | Wiki |
|---|---|---|---|---|---|
| `SFABM` | BladeMaster | 6 ✅ | Fully Working & Supported | - | [wiki](https://wiki.mbbsemu.com/doku.php?id=modules:sfabm) |
| `ELWCAMEL` | Cross Country Camel | 6 ✅ | Fully Working & Supported | - | [wiki](https://wiki.mbbsemu.com/doku.php?id=modules:elwcamel) |
| `HVSXROAD` | Crossroads of the Elements | 6 ✅ | Fully Working & Supported | - | [wiki](https://wiki.mbbsemu.com/doku.php?id=modules:hvsxroad) |
| `INFCT` | Cybertank | 6 ✅ | Fully Working & Supported | - | [wiki](https://wiki.mbbsemu.com/doku.php?id=modules:infct) |
| `WLDERT` | Erotica | 6 ✅ | Fully Working & Supported | - | [wiki](https://wiki.mbbsemu.com/doku.php?id=modules:wldert) |
| `FW_OTHEL` | Farwest Othello | 6 ✅ | Fully Working & Supported | - | [wiki](https://wiki.mbbsemu.com/doku.php?id=modules:fw_othel) |
| `FW_FTRIV` | Farwest Trivia | 6 ✅ | Fully Working & Supported | - | [wiki](https://wiki.mbbsemu.com/doku.php?id=modules:fw_ftriv) |
| `GWWARROW` | GWW Archery | 6 ✅ | Fully Working & Supported | - | [wiki](https://wiki.mbbsemu.com/doku.php?id=modules:gwwarrow) |
| `LUNATIX` | Lunatix | 6 ✅ | Fully Working & Supported | - | [wiki](https://wiki.mbbsemu.com/doku.php?id=modules:lunatix) |
| `EWEPNT` | Phantasia | 6 ✅ | Fully Working & Supported | - | [wiki](https://wiki.mbbsemu.com/doku.php?id=modules:ewepnt) |
| `TSGARN` | Tele-Arena | 6 ✅ | Fully Working & Supported | - | [wiki](https://wiki.mbbsemu.com/doku.php?id=modules:tsgarn) |
| `CPTLANDS` | The Forbidden Lands Part 1: The City of Falchon | 6 ✅ | Fully Working & Supported | - | [wiki](https://wiki.mbbsemu.com/doku.php?id=modules:cptlands) |
| `CPTGRIM` | The Forbidden Lands Part 2: The Vale of Grimyre | 6 ✅ | Fully Working & Supported | - | [wiki](https://wiki.mbbsemu.com/doku.php?id=modules:cptgrim) |
| `CPTDAWN` | The Forbidden Lands Part 3: The Islands of Dawn | 6 ✅ | Fully Working & Supported | - | [wiki](https://wiki.mbbsemu.com/doku.php?id=modules:cptdawn) |
| `RTSLORD` | Tournament LORD | 6 ✅ | Fully Working & Supported | - | [wiki](https://wiki.mbbsemu.com/doku.php?id=modules:rtslord) |
| `MJWWHL` | Wheel of Fame | 6 ✅ | Fully Working & Supported | - | [wiki](https://wiki.mbbsemu.com/doku.php?id=modules:mjwwhl) |
| `SFAYTZ` | Yahtzee! | 6 ✅ | Fully Working & Supported | - | [wiki](https://wiki.mbbsemu.com/doku.php?id=modules:sfaytz) |
| `SFABLX` | Blox! | 5 ◕ | Mostly Playable | - | [wiki](https://wiki.mbbsemu.com/doku.php?id=modules:sfablx) |
| `DIALCHAT` | DialChat! | 5 ◕ | Mostly Playable | [#550](https://github.com/mbbsemu/MBBSEmu/issues/550) | [wiki](https://wiki.mbbsemu.com/doku.php?id=modules:dialchat) |
| `XCLIBUR` | Excalibur! | 5 ◕ | Mostly Playable | [#278](https://github.com/mbbsemu/MBBSEmu/issues/278) | [wiki](https://wiki.mbbsemu.com/doku.php?id=modules:xclibur) |
| `MBMGEMP` | Galactic Empire | 5 ◕ | Mostly Playable | [#604](https://github.com/mbbsemu/MBBSEmu/issues/604) | [wiki](https://wiki.mbbsemu.com/doku.php?id=modules:mbmgemp) |
| `ELWIC` | iNfInItY CoMpLeX | 5 ◕ | Mostly Playable | [#591](https://github.com/mbbsemu/MBBSEmu/issues/591) | [wiki](https://wiki.mbbsemu.com/doku.php?id=modules:elwic) |
| `MUICYBER` | Lords of Cyberspace | 5 ◕ | Mostly Playable | - | [wiki](https://wiki.mbbsemu.com/doku.php?id=modules:muicyber) |
| `WCCMMUD` | MajorMUD | 5 ◕ | Mostly Playable | [#570](https://github.com/mbbsemu/MBBSEmu/issues/570), [#569](https://github.com/mbbsemu/MBBSEmu/issues/569), [#555](https://github.com/mbbsemu/MBBSEmu/issues/555), [#554](https://github.com/mbbsemu/MBBSEmu/issues/554), [#553](https://github.com/mbbsemu/MBBSEmu/issues/553), [#545](https://github.com/mbbsemu/MBBSEmu/issues/545), [#544](https://github.com/mbbsemu/MBBSEmu/issues/544) | [wiki](https://wiki.mbbsemu.com/doku.php?id=modules:wccmmud) |
| `MJWMUT` | Mutants! | 5 ◕ | Mostly Playable | - | [wiki](https://wiki.mbbsemu.com/doku.php?id=modules:mjwmut) |
| `ELWSW` | Space Wumpus | 5 ◕ | Mostly Playable | - | [wiki](https://wiki.mbbsemu.com/doku.php?id=modules:elwsw) |
| `MUICHAOS` | Swords of Chaos | 5 ◕ | Mostly Playable | - | [wiki](https://wiki.mbbsemu.com/doku.php?id=modules:muichaos) |
| `LOGCAS` | The Casino | 5 ◕ | Mostly Playable | - | [wiki](https://wiki.mbbsemu.com/doku.php?id=modules:logcas) |
| `TTIOLT` | Oltima 2000 | 4 ◐ | Somewhat Playable | - | [wiki](https://wiki.mbbsemu.com/doku.php?id=modules:ttiolt) |
| `HVSTW` | TradeWars 2002 | 2 ✖ | Crashes on Initialization | - | [wiki](https://wiki.mbbsemu.com/doku.php?id=modules:hvstw) |

## Notes

- `identifier` is the module directory name MBBSEmu expects (e.g. under `modules/<identifier>/` when running via docker compose, or with `-EXE` in standalone mode) and matches the identifier used in module download filenames on `download.mbbsemu.com`.
- All claims here are upstream-reported (mbbsemu.com / wiki.mbbsemu.com). If your own testing disagrees with a listed status, the fix is to update the wiki page (the source of truth), then re-sync this table.
