# Finn's Realm — check these before every launch

This is the gate. The launcher runs `scripts/preflight.py` before it starts the board. **FAIL** blocks start.

**Rule:** character first. Stay on **DEMO**. Codes last. If anything breaks: `./scripts/stop-board.sh` then `./scripts/reset-game.sh`.

Manual check: `./scripts/preflight.py`

**Check / Reboot / Play** in `Desktop/Finn's Realm/DOS` use this repo board (`data/` + `modules/WCCMMUD/`). The packaged `dist/finns-realm/` copy is a snapshot, not a second game.

---

## Stages (slow on purpose)

Preflight writes [finns-stage](../data/finns-stage). It auto-advances only as far as **demo**. Later stages you type yourself.

| Stage | What is allowed |
| --- | --- |
| `character` | `{DEMO}` only. Create a character. |
| `demo` | Character exists. Still `{DEMO}`. No addon file. **Current.** |
| `plus-demo` | Plus real code. MajorMUD stays `{DEMO}`. |
| `mud-code` | MajorMUD real code. If last boot is **0 users**, revert `{DEMO}` immediately. |
| `addons` | `WCCADDON.SYS` one `id:code` per line, reboot each time. |

```bash
echo plus-demo > data/finns-stage
```

Do not write that until DEMO play feels solid. This tree does not store leftover Plus/Mud/addon tokens. Reset, package, and restore wipe those files so they cannot be pasted back.

Each reboot records [last-boot.json](../data/last-boot.json) (`MajorMUD - N users`, DEMO, recovery). Preflight **FAIL**s a 0-seat last boot.

---

## 1. Activation

| File | Valid now | Crash-prone |
| --- | --- | --- |
| [WCCMMUD.MSG](../modules/WCCMMUD/WCCMMUD.MSG) | `ACTIVATE {DEMO}` | `BYPASS`, `UNLOCKED`, letter codes before stage `mud-code` |
| [WCCMMUD.MSG.original](../modules/WCCMMUD/WCCMMUD.MSG.original) | must stay `{DEMO}` | do not edit |
| [WCCMMPLS.MSG](../modules/WCCMMUD/WCCMMPLS.MSG) | `ACTIVATE {DEMO}` | letter codes before stage `plus-demo` |

---

## 2. Addon slots

| File | Valid now | Crash-prone |
| --- | --- | --- |
| [WCCADDON.SYS](../modules/WCCMMUD/WCCADDON.SYS) | **no file** | 0-byte file (MUD internal error), `1:1`…`9:9`, any codes before stage `addons` |

Official format later: `<id>:<letters>` ([WCCMMUD.RLN §6.7](../modules/WCCMMUD/WCCMMUD.RLN)). Edits while the board is up do nothing until reboot.

---

## 3. BBS login files (USER MISMATCH IN BBSUSR.DAT)

| File | Role |
| --- | --- |
| [mbbsemu.db](../data/mbbsemu.db) | passwords / keys (sqlite `Accounts`) |
| [BBSUSR.DB](../data/BBSUSR.DB) | MajorBBS user file the login screen looks up |

Same usernames in both. Launchers pass `-DBREBUILD BBSUSR`.

---

## 4. Board identity

| File | Valid | Crash-prone |
| --- | --- | --- |
| [appsettings.json](../config/appsettings.json) | `GSBL.BTURNO` is **8 digits** (`84732615`) | missing or placeholder |
| [modules.json](../data/modules.json) | `"Identifier": "WCCMMUD"` | `WCCMMPLS` as its own menu item |

Play **M**, then **E**.

---

## 5. Character and recovery

| File | Meaning |
| --- | --- |
| [WCCUSERS.db](../modules/WCCMMUD/WCCUSERS.db) | saved characters (BBS user / given name) |
| [WCCRECOV.FLG](../modules/WCCMMUD/WCCRECOV.FLG) | lockfile while the board is **up**. Stop/reboot delete it after the process dies. |

`./scripts/reset-game.sh` wipes characters, restores DEMO, and removes `WCCADDON.SYS`.

If live module files vanished, `./scripts/restore-from-package.sh` copies `dist/finns-realm` **except** player DBs, then wipes users/gangs/banks. Packaged `WCCUSERS` is a ghost (given names reserved, no login). Do not copy it.

---

## 6. What to do

**Green / WARN on DEMO:** click **Finn's Realm** and play.

**FAIL:** do not launch.

```bash
./scripts/stop-board.sh
./scripts/reset-game.sh
./scripts/preflight.py
```

Skip the gate only if you know why: `FINNS_PREFLIGHT=0 ./scripts/FinnsRealm --sysop`
