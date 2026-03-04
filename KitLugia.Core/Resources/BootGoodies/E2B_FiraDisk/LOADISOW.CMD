TITLE LOADISOW.CMD
color 1f
@echo off
cls
REM if agFM has already run then exit
if exist X:\Windows\System32\null.cfg exit
SET BIT=32
if "%PROCESSOR_ARCHITECTURE%"=="AMD64" SET BIT=64

FOR /L %%A IN (0,1,10000) DO ECHO NOTHING > nul
echo LOADISOW.CMD running...
set USBDRIVE=%~d0
%USBDRIVE%

:: check if we have already run LOADISO once (may be multiple drives with E2B files)!
call %USBDRIVE%\_ISO\E2B\firadisk\ISONAME.cmd
if "%MYISO%"=="DONE" exit
if "%MYISO%"=="" exit
if exist %USBDRIVE%\WINHELPER.USB exit

TITLE LOAD WINDOWS XP INSTALL ISO USING IMDISK
color 1f
REM Load ISO using ImDisk
call \_ISO\E2B\firadisk\IMDISK_install.cmd
call \_ISO\E2B\firadisk\IMDISK_instiso.cmd

set SRCDRIVE=
set exit=
SETLOCAL ENABLEDELAYEDEXPANSION
FOR %%D IN (A B C D E F G H I J K L M N O P Q R S T U V W X Y Z) DO (
DIR %%D:\i386\WINNT32.EXE > nul 2>&1 && (call set SRCDRIVE=%%D:) && call set exit=1
REM if defined exit goto :dd2
)
:dd2


IF "%SRCDRIVE%"=="" echo WARNING: \i386\WINNT32.EXE not found!
IF NOT "%SRCDRIVE%"=="" echo FOUND %SRCDRIVE%\i386\WINNT32.EXE
if "%BIT%"=="32" \_ISO\E2B\firadisk\slp.exe 2
if "%BIT%"=="64" FOR /L %%A IN (0,1,20000) DO ECHO NOTHING > nul


TITLE PREPARE HARD DISK
echo.
echo USB DRIVE   =%USBDRIVE%
echo SOURCE DRIVE=%SRCDRIVE%

\_ISO\E2B\firadisk\RUNWINNT.CMD

exit


