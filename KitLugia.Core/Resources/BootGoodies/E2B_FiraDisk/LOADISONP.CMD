TITLE LOADISONP.CMD
color 1f
@echo off
cls
REM if agFM has already run then exit
if exist X:\Windows\System32\null.cfg exit
SET BIT=32
if "%PROCESSOR_ARCHITECTURE%"=="AMD64" SET BIT=64

echo LOADISONP.CMD running...
FOR /L %%A IN (0,1,5000) DO ECHO NOTHING > nul
cls
set USBDRIVE=%~d0
set E2BDRIVE=%USBDRIVE%
@echo FOUND USB DRIVE AT DRIVE %USBDRIVE%
cd /d %USBDRIVE%\

:: check if we have already run LOADISO once (may be multiple drives with E2B files)!
call %USBDRIVE%\_ISO\E2B\firadisk\ISONAME.cmd
if "%MYISO%"=="DONE" exit
if "%MYISO%"=="" echo ERROR: No ISO name in ISONAME.cmd & pause & exit
if exist %USBDRIVE%\WINHELPER.USB exit

REM echo About to run ImDisk to load the ISO as a virtual drive
REM Load ISO using ImDisk
TITLE LOAD WINDOWS ISO USING IMDISK
call \_ISO\E2B\firadisk\IMDISK_install.cmd
call \_ISO\E2B\firadisk\IMDISK_instiso.cmd

REM Find Install.wim/Install.esd on mounted drive
set USBDRIVE=
set exit=
FOR %%D IN (A B C D E F G H I J K L M N O P Q R S T U V W X Y Z) DO (
DIR %%D:\SOURCES\install.*  > nul 2>&1 && (call set USBDRIVE=%%D:) && call set exit=1 && echo FOUND %%D:\SOURCES\install.*
)

FOR %%D IN (A B C D E F G H I J K L M N O P Q R S T U V W X Y Z) DO (
DIR %%D:\SOURCES\x86\install.*  > nul 2>&1 && (call set USBDRIVE=%%D:) && call set exit=1 && echo FOUND %%D:\SOURCES\x86\install.*
)
FOR %%D IN (A B C D E F G H I J K L M N O P Q R S T U V W X Y Z) DO (
DIR %%D:\SOURCES\x64\install.*  > nul 2>&1 && (call set USBDRIVE=%%D:) && call set exit=1 && echo FOUND %%D:\SOURCES\x64\install.*
)

FOR %%D IN (A B C D E F G H I J K L M N O P Q R S T U V W X Y Z) DO (
DIR %%D:\x86\SOURCES\install.*  > nul 2>&1 && (call set USBDRIVE=%%D:) && call set exit=1 && echo FOUND %%D:\x86\SOURCES\install.*
)
FOR %%D IN (A B C D E F G H I J K L M N O P Q R S T U V W X Y Z) DO (
DIR %%D:\x64\SOURCES\install.*  > nul 2>&1 && (call set USBDRIVE=%%D:) && call set exit=1 && echo FOUND %%D:\x64\SOURCES\install.*
)

:ENDSEARCH
set DVDDRIVE=%USBDRIVE%
IF "%USBDRIVE%"=="" echo WARNING: install.* not found!
IF "%USBDRIVE%"=="" pause

echo.
IF NOT "%DVDDRIVE%"=="" echo ISO FILE MOUNTED AS %DVDDRIVE%

REM startnet.cmd normally only contains wpeinit.exe, custom ISOs may contain other commands, so try to run them
if exist X:\WINDOWS\SYSTEM32\startnet.cmd call cmd /c  X:\WINDOWS\SYSTEM32\startnet.cmd
if not exist X:\WINDOWS\SYSTEM32\startupe2b.bat exit
goto :EOF
