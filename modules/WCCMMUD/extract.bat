@echo off
REM WCCMMUD.Z is an InstallShield library, not an EXE.
REM ICOMP wants: library  dest\*.*  -d  (current or absolute paths only).
REM Extract to C:\WCCMMUD-NT so this does not overlay the live 1.11p module.
if not exist C:\WCCMMUD-NT mkdir C:\WCCMMUD-NT
copy /Y WCCMMUD.Z C:\WCCMMUD-NT\ >nul
copy /Y ICOMP.EXE C:\WCCMMUD-NT\ >nul
C:
cd \WCCMMUD-NT
ICOMP WCCMMUD.Z *.* -d -i -o
echo Extracted to C:\WCCMMUD-NT
