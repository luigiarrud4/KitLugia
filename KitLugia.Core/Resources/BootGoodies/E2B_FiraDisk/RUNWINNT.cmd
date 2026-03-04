Title Install Windows using WINNT32.EXE
@echo off
cls
echo SOURCE DRIVE=%SRCDRIVE%
echo USB    DRIVE=%USBDRIVE%
echo PTN1   SIZE=%PTN1%
echo UNAT   FILE=%UNAT%
echo.
echo LIST DISK>DP.SCR
echo EXIT >>DP.SCR
diskpart /s DP.SCR
echo.
echo WARNING: This will ERASE ALL DATA on the first hard disk  (DISK 0)!
set SZ=
if not "%PTN1%"=="" set SZ=SIZE=%PTN1%
if not "%PTN1%"=="" set PSZ=%PTN1%MB
if "%PTN1%"=="0" set SZ=
if /i "%PTN1%"=="NONE" set SZ=
if "%PTN1%"=="" set PSZ=MAXIMUM
echo.

set TARGET=
for %%a in (C D E F G H I J K L M N O P Q R S T U V W X Y Z) do CD %%a: 1>> nul 2>&1 & if errorlevel 1 set TARGET=%%a:
::echo ASSIGNING DRIVE LETTER=%TARGET%
::echo.


if "%TARGET%"=="" pause

echo LIST DISK>DP.SCR
echo SELECT DISK 0 >>DP.SCR
echo CLEAN >>DP.SCR
echo CREATE PART PRI %SZ% ALIGN=64260 >>DP.SCR
echo FORMAT QUICK LABEL=SYSTEM >>DP.SCR
echo ASSIGN LETTER=%TARGET% >>DP.SCR
echo ACTIVE >>DP.SCR
echo LIST VOL >>DP.SCR
echo EXIT >>DP.SCR

echo Run DiskPart script
echo ===================
type  DP.SCR
echo.
echo FIRST PARTITION SIZE=%PSZ%   (%TARGET%)
echo Y = EXECUTE THIS DISKPART SCRIPT AND WIPE THE DISK OF ALL DATA
echo N = Format or Install to an existing partition.
echo.
set /p ask=WIPE DISK 0 (Y/N) : 
if /i "%ask%"=="Y" goto :WIPE

cls
echo.
echo LIST VOL >DP.SCR
echo EXIT >>DP.SCR
diskpart /s DP.SCR
echo.
set /p ask=Choose an existing partition (e.g. C:) : 

:: add colon and make 2 chars long
set ask=%ask%:
set ask=%ask:~0,2%
REM CONVERT STRING TO UPPERCASE
FOR /F "tokens=2 delims=-" %%A IN ('FIND "" "%ask%" 2^>^&1') DO SET UpCaseText=%%A
set ask=%UpCaseText:~1,999%
set TARG=%ask%
echo.
if not exist %TARG%\nul goto :END

cls
echo LIST VOL >DP.SCR
echo SELECT VOL %TARG% >>DP.SCR
echo FORMAT QUICK LABEL=SYSTEM >>DP.SCR
echo ASSIGN LETTER=%TARGET% >>DP.SCR
echo ACTIVE >>DP.SCR
echo LIST VOL >>DP.SCR
echo EXIT >>DP.SCR

echo DiskPart Script to run
echo ======================
type  DP.SCR
echo.
echo Y = Format partition using DiskPart script
echo S = Do not format volume %c% (must be a primary partition)
echo N = Abort
echo.
set /p ask=FORMAT VOLUME %TARG% or Skip (N=Abort/S=Skip/Y=Yes) : 
if /i "%ask%"=="Y" goto :WIPE
if /i "%ask%"=="F" goto :WIPE
if /i "%ask%"=="S" set TARGET=%TARG%
if /i "%ask%"=="S" goto :DONEFMT
goto :END


:WIPE
diskpart /s DP.SCR
if errorlevel 1 pause

:DONEFMT
echo INSTALL VOLUME=%TARGET%
if exist DP.SCR del DP.SCR > nul
:: now install XP
if "%UNAT%"=="" set UNAT=NONE
if not "%UNAT%"=="" if not "%UNAT%"=="NONE" echo UNATTEND FILE=%UNAT%
set LUNAT=
if not "%UNAT%"=="NONE" set LUNAT=/unattend10:%TARGET%\i386\unattend.txt
if not "%UNAT%"=="NONE" if not exist %USBDRIVE%%UNAT% echo CANNOT FIND %USBDRIVE%%UNAT%!! & pause
mkdir %TARGET%\i386
if not "%UNAT%"=="NONE" copy %USBDRIVE%%UNAT% %TARGET%\i386\unattend.txt

echo.
echo About to run: winnt32 %LUNAT% /syspart:%TARGET% /tempdrive:%TARGET% /s:%SRCDRIVE%\i386 /makelocalsource
echo.
echo Running WINNT32.exe...
::pause
%SRCDRIVE%\i386\winnt32.exe %LUNAT% /syspart:%TARGET% /tempdrive:%TARGET% /s:%SRCDRIVE%\i386 /makelocalsource 
echo.
echo WINNT32 has finished copying Windows XP to %TARGET%!
echo.
echo You may remove all USB drives now if you wish.
echo.
echo Press a key and reboot from the internal hard disk to continue Setup...
pause > nul
wpeutil reboot

:END
cd \_ISO\E2B\firadisk
echo Aborting!
echo Type RUNWINNT to start again!


