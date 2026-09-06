# Finn’s Realm on Worldgroup NT (live)

This is the board you play now: **MajorMUD v1.11p-WG3NT** on **Worldgroup 3.30** inside the Win11 KVM guest **FINN**. The Linux Finn’s Realm **xterm** (Px IBM VGA8, 80×30) telnets in. DOS MBBSEmu on `127.0.0.1:2323` opened the door; leave that tree alone.

Do not ICOMP, unzip, or copy NT files onto `majormud/modules/WCCMMUD/` (16-bit NE). Do not hex `GALGSBL` or run a keygen.

## Machines

| Piece | Where |
|---|---|
| Linux host | `192.168.122.1` (libvirt `virbr0`) |
| Win11 guest **FINN** | `192.168.122.33` |
| Worldgroup + Mud | `C:\wgserv` |
| 1.11g snapshot (old) | `C:\backup\wgserv-1.11g` and `Share/backups/wgserv-1.11g` |
| 1.11p snapshot (new, this writeup) | `C:\backup\wgserv-1.11p` and `Share/backups/wgserv-1.11p` |
| Kit on the share | `~/Desktop/Share/wg3nt`, `Share/mudbins` |

Registration showing in WG: **`92784632`**. Sysop BBS login used with the client: **`sysop`** / the password in gitignored `config/wg-local.json`. Mud given name **klymacks** (ninja).

## Network

Telnet is **galtntd** (multiport zip). Defaults: **TCP 23** and **2323**. From Linux: `nc -vz 192.168.122.33 23`.

Win11 **Ethernet** (the only NIC) must be **Private**. Firewall: inbound TCP 23 (and 2323 if you use it), Private, remote **`192.168.122.1`** or `192.168.122.0/24`. Do not open Public to Any.

Ignore **NNTP NOT INITIALIZED**. Mud does not need news.

## How this board was built

1. Worldgroup 3.30 to **`C:\wgserv`**. 16-bit `SETUP.EXE` needed **otvdm**. Packaging **Build** (`wgminst` / INSTMP) is optional; skip if it fails.
2. First Mud extract was **1.11g** (`Share\wg3nt\WCCMMUD.Z` + ICOMP). `.VIR` → `.DAT`. `wccmmud.ini`, houses, **galtntd-mp**.
3. Menu tree: `C:\wgserv\wgsrunmt.exe`. On **TOP**, **M** → destination page **MAJORMUD**. That page must be a **module page** bound to **WCCMMUD**, not a submenu with a blank hotkey. **F10** saves. File: **`WGSMENU2.DAT`**.
4. Security and Accounting → **WCCMMUD**: your **ACTIVATE** (and related) strings. **GAMOPKEY**, **SYSOPKEY**, **PLAYKEY**, **SAVEKEY** = `normal`. `wccmmud.ini`: `SYS_GOTO_KEY` / `SYS_MAP_KEY` = `normal`.
5. Upgrade to **p**: ICOMP **`Share\mudbins\MajorMUD v1.11p\WCCNT8PJ\WCCMMUD.Z`** (different hash than the g Z). Official README wanted **1.11j** between g and p; that pack was not here.
6. After ICOMP, **older `.DAT` + newer `.VIR`**: `Share\wg3nt\cleanup-wcc-vir.bat virgin` kept p templates (wiped g tables). Houses: **7-Zip** the `WCC*.ZIP` files — do not run 16-bit **PKUNZIP** / `WCCMISC.BAT` on Win11 x64.
7. Re-enter Mud activation (p DLL is a new hash). Console: **`MajorMUD v1.11p-WG3NT`**, no DEMO line, Plus found.
8. First p boot: **Found Database Update** → character in-game → **Automatic Database Update Complete**. Then stop WG, `cd /d C:\wgserv` → **`wccmmutl -needed -fast`**. The UI says **database recovery**; that is the offline recovery step. Then start WG.

Undo g-era tree only: `xcopy C:\backup\wgserv-1.11g C:\wgserv /E /I /H /Y` (overwrites live p).

## Play from Linux

Same gorgeous xterm as DOS: **80×30**, font **Px IBM VGA8**, ice colors.

**Worldgroup NT (live board on FINN):**

```bash
cd ~/Code/Repo/Projects/majormud
./scripts/play-wg.sh klymacks   # sysop BBS user
./scripts/play-wg.sh matt
./scripts/play-wg.sh new
```

**DOS MBBSEmu (testing only, `127.0.0.1:2323`, do not overlay NT files):**

```bash
./scripts/play-dos.sh            # starts MBBSEmu if needed; klymacks in player.json
./scripts/reboot-board.sh        # bounce the DOS board
```

Desktop (reinstall: `./scripts/install-wg-shortcuts.sh`):

- Finn's Realm — klymacks / matt / new → NT
- Finn's Realm DOS — klymacks → MBBSEmu
- Reboot Finn's Realm DOS

Logins: NT in gitignored `config/wg-local.json`, `wg-matt.json`. DOS klymacks in `config/player.json`. WG often **one Mud character per BBS user**. ANSI **Y** on new accounts. Client answers telnet **TTYPE** and **CSI 6n** (auto-sense) so color matches system telnet.

Window size: **80×30** for this client (25-line Mud + 5-line footer). Raw `telnet` can be 80×25.

## New BBS user (matt, then two more)

1. Board up. Shortcut **NT — new**.
2. **NEW** (or WG’s new-user prompt). Username lowercase. Pick a password; put the same in `config/wg-*.json` if you add a shortcut later.
3. ANSI **Y**. **M** then **E**. Create the Mud character. Save.
4. Next time use the matching shortcut if auto-login is filled in.

## Backup (this 1.11p board — separate from g)

Do **not** run the old `backup-wgserv.bat` (that is `wgserv-1.11g` only).

Worldgroup **stopped**. If copy hits sharing violation on `WGSMENU2.DAT`, stop **W32MKDE.EXE** (Btrieve) too, or reboot with WG set to **Manual** and copy before it starts.

On the VM:

```bat
Share\wg3nt\backup-wgserv-111p.bat
```

Destinations:

- `C:\backup\wgserv-1.11p`
- `Z:\backups\wgserv-1.11p` if the share is **Z:**

Restore p:

```bat
xcopy C:\backup\wgserv-1.11p C:\wgserv /E /I /H /Y
```

## Everyday start

1. Win11: start Worldgroup Server (Ethernet Private, 23 listening: `netstat -an | find "23"`).
2. Linux: NT shortcut or `play-wg.sh`.
3. After a dirty shutdown: WG off, `wccmmutl -needed -fast`, WG on.

## Do not

- Overlay `C:\wgserv` onto the Linux `modules/WCCMMUD` folder.
- ICOMP the **g** `Share\wg3nt\WCCMMUD.Z` over live p.
- Trust a backup that logged a sharing violation on `WGSMENU2.DAT` without recopying that file.
- Open telnet to the internet; this is LAN-only.
