# WG3NT MajorMUD — sidecar, not Finn’s board

Finn’s Realm on this Linux box is **DOS 1.11p-WG** in MBBSEmu (`modules/WCCMMUD/`, 16-bit NE, `BBSFSD`). Do not overlay NT files on that folder.

NT MajorMUD is a **second stack**: Worldgroup 3 on Windows (the [jabber422/mudbins](https://github.com/jabber422/mudbins) path). MBBSEmu cannot load the PE32 `WG3NT` DLL.

## What lives where

| Stack | Binary | Host |
|---|---|---|
| Default (this repo) | `modules/WCCMMUD/WCCMMUD.DLL` NE `1.11p-WG` | MBBSEmu, `127.0.0.1:2323` |
| NT option | PE `1.11p-WG3NT` from `WCCMMUD.Z` | Worldgroup 3 `c:\wgserv` on Windows |

You already have NT payloads in Downloads (`wccmmud.dll` 1.11p-WG3NT, `WCCMMUD-NT/` 1.11g-WG3NT, `WCCNT7PW.ZIP`). The FAQ DOS zip is the same NE as live.

## Extract NT (Windows VM)

`setup.exe` on modern Windows usually dies (`softwarekeyProd`). Same as mudbins: ICOMP the library into **wgserv**, never into this repo’s live module.

```bat
REM from the NT pack that contains ICOMP.EXE and WCCMMUD.Z
REM mudbins used: MajorMUD v1.11p\WCCNT8PJ
icomp WCCMMUD.Z c:\wgserv\*.* -d -i -o
```

Then on that Windows tree only: rename `*.vir` → `*.dat` where the pack still uses VIR, copy `wccmmud.ini`, unzip house/banner packs (`WCCHSE.ZIP` / mudbins `MajorMUD_HSE_FILES_ODSBBS.zip`) and `galtntd` if you want a telnet port in WG config.

This repo’s `modules/WCCMMUD/extract.bat` already aims at `C:\WCCMMUD-NT` so it cannot paint over the live 1.11p module.

## What this repo will not do

- Hex-edit `GALGSBL.DLL` or run a keygen. Use the registration number and activation you actually own (WG3NT codes do not hash on the DOS DLL).
- Clone mudbins’ key bins into Finn’s tree.

Point the Finn’s Realm client at the Windows telnet port when that VM is up; keep `2323` as DOS MBBSEmu.

`./scripts/play-wg.sh` opens the same 80×30 window at `192.168.122.33:23` (libvirt NAT). Login file is gitignored `config/wg-local.json`.

## Backup then 1.11p (Win11 VM)

Working board is **1.11g-WG3NT** in `C:\wgserv`. WCC’s own notes say **1.11g or lower should go through 1.11j conversion** before newest; we do not have a 1.11j pack here. Trial is **DLL only** after a full tree copy.

On the VM, BBS **stopped**:

1. `Share\wg3nt\backup-wgserv.bat` → `C:\backup\wgserv-1.11g` (and `Z:\backups\...` if the share is Z:).
2. `Share\wg3nt\upgrade-dll-to-111p.bat` copies `wccmmud-1.11p-WG3NT.dll` over `C:\wgserv\wccmmud.dll`.
3. Start WG. Console must say **`1.11p-WG3NT`**. Re-enter NT activation if it falls back to DEMO.
4. If it dies or corrupts data: stop WG, `xcopy C:\backup\wgserv-1.11g C:\wgserv /E /I /H /Y`.

Do **not** ICOMP `mudbins\MajorMUD v1.11p\WCCNT8PJ\WCCMMUD.Z` over the live `.DAT` files unless you are willing to replace the world. That Z is **not** the same library as the 1.11g `Share\wg3nt\WCCMMUD.Z` already extracted.