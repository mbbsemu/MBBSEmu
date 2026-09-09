# Finn’s Realm — do this again

Two stacks. Do not mix their files.

| Stack | What | Play |
|---|---|---|
| **Live** | Worldgroup **3.30** + MajorMUD **v1.11p-WG3NT** on Win11 KVM **FINN** | `./scripts/play-wg.sh klymacks` |
| **DOS test** | MBBSEmu + 16-bit NE in this repo (`modules/WCCMMUD/`) | `./scripts/play-dos.sh` |

NT keys **hash on WG3NT**. They do **not** unlock DOS MBBSEmu DEMO. Do not hex `GALGSBL`, do not keygen, do not overlay `C:\wgserv` onto `majormud/modules/WCCMMUD/`.

Code: [klymacks/MBBSEmu](https://github.com/klymacks/MBBSEmu) branch `cursor/setup-dev-environment-5e2e`. Original loopback game (not this board): [klymacks/finns-mud](https://github.com/klymacks/finns-mud).

Same text on the VM share: `Share\wg3nt\FINNS-REALM-NT.txt`. Historical g-extract notes: [wg3nt-sidecar.md](wg3nt-sidecar.md).

---

## Machines

| Piece | Where |
|---|---|
| Linux host | `192.168.122.1` (libvirt `virbr0`) |
| Win11 guest **FINN** | `192.168.122.33` |
| Worldgroup + Mud | `C:\wgserv` |
| 1.11g snapshot | `C:\backup\wgserv-1.11g` and `Share/backups/wgserv-1.11g` |
| 1.11p snapshot | `C:\backup\wgserv-1.11p` and `Share/backups/wgserv-1.11p` |
| Dated share freeze (2026-09-06) | `Share/backups/wgserv-1.11p-20260906` |
| Kit | `~/Desktop/Share/wg3nt`, `Share/mudbins` |

WG registration showing: **`92784632`**. NT BBS **`sysop`** password is gitignored `config/wg-local.json` (not the DOS `player.json` password). Mud given name **klymacks** (ninja). Matt BBS user exists; **USERCOPY** cloned the sysop password into `config/wg-matt.json`.

---

## Network

Telnet is **galtntd** (multiport zip). Defaults: **TCP 23** and **2323**. From Linux: `nc -vz 192.168.122.33 23`.

Win11 **Ethernet** (the only NIC) must be **Private**. Firewall: inbound TCP 23 (and 2323 if you use it), Private, remote **`192.168.122.1`** or `192.168.122.0/24`. Do not open Public to Any.

Ignore **NNTP NOT INITIALIZED**. Mud does not need news. LAN only — do not open telnet to the internet.

---

## How the NT board was built

Redo only if `C:\wgserv` is gone. Prefer restore from backup (below).

1. Worldgroup 3.30 to **`C:\wgserv`**. 16-bit `SETUP.EXE` needed **otvdm**. Packaging **Build** (`wgminst.exe` / INSTMP) **failed** here; skip it. There is no complete `C:\WGMAN` — **Worldgroup Manager** was never installed (`wgminst\SETUP.EXE` has no `_SETUP.LST` / no `wgminst.exe`). Drag-drop onto otvdm is a dead end. Add users from telnet, not WGM.
2. First Mud extract was **1.11g** (`Share\wg3nt\WCCMMUD.Z` + ICOMP). `.VIR` → `.DAT`. `wccmmud.ini`, houses, **galtntd-mp**.
3. Menu tree: `C:\wgserv\wgsrunmt.exe`. On **TOP**, **M** → destination page **MAJORMUD**. That page must be a **module page** bound to **WCCMMUD**, not a submenu with a blank hotkey. **F10** saves. File: **`WGSMENU2.DAT`** (Btrieve **W32MKDE.EXE** locks it while WG is up).
4. Security and Accounting → **WCCMMUD**: your **ACTIVATE** (and related) strings. **GAMOPKEY**, **SYSOPKEY**, **PLAYKEY**, **SAVEKEY** = `normal`. `wccmmud.ini`: `SYS_GOTO_KEY` / `SYS_MAP_KEY` = `normal`.
5. Upgrade to **p**: ICOMP **`Share\mudbins\MajorMUD v1.11p\WCCNT8PJ\WCCMMUD.Z`** — **not** the g Z (different hash). Official README wanted **1.11j** between g and p; that pack was not here.
6. After ICOMP, **older `.DAT` + newer `.VIR`**: `Share\wg3nt\cleanup-wcc-vir.bat virgin` kept p templates (wiped g tables). Houses: **7-Zip** the `WCC*.ZIP` files. Do **not** run 16-bit **PKUNZIP** / `WCCMISC.BAT` on Win11 x64.
7. Re-enter Mud activation (p DLL is a new hash). Console: **`MajorMUD v1.11p-WG3NT`**, no DEMO line, Plus found. NT hashes; DOS MBBSEmu stays DEMO without a DOS license.
8. First p boot: **Found Database Update** → character in-game → **Automatic Database Update Complete**. Then stop WG, `cd /d C:\wgserv` → **`wccmmutl -needed -fast`**. The UI says **database recovery**; that is the **offline recovery** step. Then start WG.

Undo g-era tree only: `xcopy C:\backup\wgserv-1.11g C:\wgserv /E /I /H /Y` (overwrites live p).

---

## Play from Linux

Same xterm: **80×30** (size locked — maximize is dead black and breaks the footer), font **Px IBM VGA8**, ice colors. Icon is `client/icons/finns-realm.png`, not the xterm XT. Raw `telnet` can be 80×25.

Client: TTYPE **ANSI-BBS** first (then ANSI, then xterm) for parchment
TRAIN STATS (`EDITCHA1`). Login Auto-sensing still sends CSI **6n** — answer
with the **real caret** (row and column), not forced `25;1R`. Idle room
listings are **Statline Full** bare Enter / move / combat reprints, not a
WG timer. SGR 5 = iCE. If TRAIN STATS is still a text dump: Account
Display/Edit → ANSI Yes, editor **FSE** (not LINE).

**Worldgroup NT (live):**

```bash
cd ~/Code/Repo/Projects/majormud
./scripts/play-wg.sh klymacks   # BBS sysop → mud klymacks
./scripts/play-wg.sh matt
./scripts/play-wg.sh new        # type login yourself
./scripts/play-wg.sh maint      # sysop BBS menu only (USERCOPY / ANSI / FSE)
```

**DOS MBBSEmu (testing only, `127.0.0.1:2323`):**

```bash
./scripts/play-dos.sh            # starts MBBSEmu if needed; config/player.json
./scripts/reboot-board.sh
```

Desktop folder `Finn's Realm/` (reinstall: `./scripts/install-wg-shortcuts.sh`):

- **NT/** roster + new + **BBS — klymacks** → Worldgroup VM (`192.168.122.33:23`)
- **DOS/** **Finn's Realm DOS — klymacks** + reboot → MBBSEmu
- **Codes/** yours (registration lists) — installer does not write there

Logins: NT `config/wg-local.json`, `wg-matt.json`, `wg-new.json` (gitignored). DOS `config/player.json`. WG is often **one Mud character per BBS user**. ANSI **Y** on new accounts.

### Overnight / 3 AM cleanup

WG nightly **CLEANUP** (~3 AM) drops every client. Leave the play windows up (matt, klymacks, ryan, …).

1. Client detects cleanup / “off the air” / socket death while in the realm.
2. Saves `data/resume-<user>.json` (room, hunt/gear/rest, follow, party, rank).
3. Prefers graceful `x` logoff when still connected; otherwise waits.
4. Polls FINN:23 until the board is back, auto-logs in, **M**/**E**, clears hangup pager with **N**.
5. Resumes: followers `follow` the leader again; leader re-invites; hunt/gear/rest mode restored. Kit is not redone if already geared.

**F12** is intentional logoff — clears the resume file and does **not** auto-reconnect. Cleanup drops do.

Verify: kill the telnet path while hunting (`iptables` / stop WG briefly), or wait for real cleanup. Footer should show wait → sign-in → `hunt · resuming` / follow.

---

## Add a BBS user (matt worked this way)

**Worldgroup Manager is not available.** Use **Finn's Realm BBS — klymacks** (`./scripts/play-wg.sh maint`) so sysop logs in and does **not** press **M**. Close the play window first (one login). Then **S** → accounting → **USERCOPY** (create a new User-ID). **EDIT** is only for keys on an account that already exists.

At the accounting prompt:

```text
USERCOPY
```

- **From / template:** `sysop`
- **New User-ID:** `matt` (lowercase)
- Copy keys: **yes** so they get `normal` / `USER`
- **USERCOPY clones the template password.** matt’s password matched sysop; `config/wg-matt.json` was updated to match. Fine on a private LAN.

Then **X** out, hang up, **Finn's Realm — matt**, **M** **E**, create the paladin. To change the password later: log in as matt → Account Display/Edit, then edit the json.

---

## Hunt (client `>` bar)

Default hunt is **graveyard (`gy`)**, not the pit. **F7** only starts/stops. From **Newhaven, Narrow Road**: pit is **`d`**, healer is **`w`**.

Arena grind:

```text
hunt stop
hunt arena
```

Then **F7**. Footer should say **`hunt arena`**. Solo: **`break`** if following Matt.

- Full Hits: stay in the pit (ninja `sn`), do not rest-camp, do not walk west to the healer.
- Hurt on **arena**: `rest` **in the pit**. Default **gy** still leaves the pit (`u`) to rest.
- `hunt list` — gy / sewer-east / arena.

---

## HP bar (Hits, not Health)

MajorMUD **Health:** on `stat` is the attribute (often ~30–50). **Hits: 22/22** (from `health` or the Hits line on `stat`) is the pool.

The client used to keep a high-water max (creation leftover `[HP=30]` or Health). Then 22/22 painted low and hunt treated you as hurt.

Now: `health` / `stat` **Hits: cur/max replace** max HP. Until that line is seen, the footer bar stays full. After train/level the client asks `stat` / `health` again so a different CP spend gets a new starting pool.

---

## Backup (1.11p — never overwrite g)

Do **not** run `backup-wgserv.bat` (that is **1.11g** only).

Worldgroup **stopped**. If `WGSMENU2.DAT` is locked, stop **W32MKDE.EXE** (Btrieve).

On the VM:

```bat
Share\wg3nt\backup-wgserv-111p.bat
```

No pause, plus a dated folder:

```bat
Share\wg3nt\backup-wgserv-111p-now.bat
```

Writes:

- `C:\backup\wgserv-1.11p` (and `C:\backup\wgserv-1.11p-YYYYMMDD-hhmm` for the now script)
- `Z:\backups\wgserv-1.11p` (and dated) if the share is **Z:**

Linux already has a freeze of the last share copy: `Share/backups/wgserv-1.11p-20260906` (235M; `WGSMENU2.dat`, `wccuser2.dat`, `wccmmud.dll`). That is **not** a live `C:\wgserv` dump unless you ran the bat with WG down.

Spot-check: `wccmmud.dll`, `WGSMENU2.DAT` / `.dat`, `WCCUSER2.DAT` / `wccuser2.dat`. Do not trust a copy that logged a sharing violation on the menu DAT.

Restore **p**:

```bat
xcopy C:\backup\wgserv-1.11p C:\wgserv /E /I /H /Y
```

---

## Everyday start

1. Win11: Worldgroup Server (Ethernet **Private**, `netstat -an | find "23"`).
2. Linux: NT shortcut or `play-wg.sh`.
3. Dirty shutdown: WG off, `wccmmutl -needed -fast`, WG on.

---

## Dead ends (do not retry)

- Overlay NT files onto Linux `modules/WCCMMUD`.
- ICOMP the **g** `Share\wg3nt\WCCMMUD.Z` over live p.
- 16-bit PKUNZIP / `WCCMISC.BAT` for houses on x64.
- Worldgroup Manager / `wgminst.exe` / dragging SETUP onto otvdm.
- DOS keygen, GALGSBL patch, or community unlocker. NT proving your codes does not mint DOS keys.
- Opening telnet past the KVM LAN.

---

## Do not

- Overlay `C:\wgserv` onto `majormud/modules/WCCMMUD`.
- Commit `config/wg-*.json` passwords.
- Force-push `main` on GitHub with live module trees (`data/`, `modules/`, `vendor/` stay untracked).
