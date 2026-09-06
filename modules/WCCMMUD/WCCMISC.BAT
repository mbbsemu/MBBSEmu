@echo off
cls
echo ---------------------------------------------------------------
echo Copying files...
echo----------------------------------------------------------------
if not exist wccuser2.dat goto dostext
del wcctext2.dat
copy wcctext2.nu1  wcctext2.dat
goto cont

:dostext
del wcctext.dat
echo Removing old file...
copy wcctext.nu1 wcctext.dat
echo File copied.

:cont

echo Cleanup needed on board startup > wccrecov.flg
echo =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
echo *******
echo WARNING: Ganghouse/Statue/Stone Custom Description Overwrite!
echo *******
echo  The following will extract original ganghouse customizable
echo  descriptions for ganghouse rooms, emblems and banners. It
echo  will also overwrite customizable stones and statues.
echo  This will overwrite any custom descriptions that exist on
echo  your system currently. Pressing CTRL/BRK will stop this.
echo  If you wish to access these files manually, please see the
echo  WCCHSE.ZIP, WCCEMB.ZIP, WCCBAN.ZIP, WCCSTA.ZIP and WCCSTO.ZIP
echo  files for access to the original files directly. 
echo =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
echo Press CTRL/BRK now to stop this process.
echo =-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
pause
pkunzip -o wcchse.zip
pkunzip -o wccemb.zip
pkunzip -o wccban.zip
pkunzip -o wccsta.zip
pkunzip -o wccsto.zip
