@echo off
if exist X:\%~nx0.log goto :eof
echo %date% %time% START %0 >> X:\%~nx0.log
TITLE LOADISOPE.CMD
color 1f
cls
REM if agFM has already run then exit
if exist X:\Windows\System32\null.cfg exit
echo LOADISOPE.CMD running...
set USBDRIVE=%~d0
@echo USB DRIVE AT DRIVE %USBDRIVE%
cd /d %USBDRIVE%\

:: check if we have already run LOADISO once (may be multiple drives with E2B files)!
call %USBDRIVE%\_ISO\E2B\firadisk\ISONAME.cmd
if "%MYISO%"=="DONE" exit
if "%MYISO%"=="" exit
if exist %USBDRIVE%\WINHELPER.USB exit

TITLE LOADISOPE - LOADING ISO USING IMDISK
call \_ISO\E2B\firadisk\IMDISK_installpe.cmd
call \_ISO\E2B\firadisk\IMDISK_instiso.cmd
FOR /L %%A IN (0,1,5000) DO ECHO NOTHING > nul
if exist "%POSTCMD%" call "%POSTCMD%"

REM startnet.cmd normally only contains wpeinit.exe, custom ISOs may contain other commands, so try to run them
if exist X:\WINDOWS\SYSTEM32\startnet.cmd call cmd /c  X:\WINDOWS\SYSTEM32\startnet.cmd
if exist X:\WINDOWS\SYSTEM32\startnet.cmd echo %date% %time% startnet.cmd called >> X:\%~nx0.log

REM Don't run pecmd if strelec iso as may hang
set SSTR=
FOR %%D IN (A B C D E F G H I J K L M N O P Q R S T U V W X Y Z) DO (
DIR "%%D:\SSTR\"  > nul 2>&1 && (call set SSTR=%%D:)
)
if exist X:\Windows\System32\pecmd.log echo ------- %time% -------- >> X:\%~nx0.pecmd.log
if exist X:\Windows\System32\pecmd.log type X:\Windows\System32\pecmd.log >> X:\%~nx0.pecmd.log
if "%SSTR%"=="" if exist X:\Windows\System32\pecmd.ini echo %date% %time% pecmd called >> X:\%~nx0.log
if "%SSTR%"=="" if exist X:\Windows\System32\pecmd.ini ren X:\Windows\system32\pecmd.ini pecmd.ini.old && cmd /c X:\Windows\System32\pecmd.exe X:\Windows\System32\pecmd.ini.old
if exist X:\Windows\System32\pecmd.log echo ------- %time% -------- >> X:\%~nx0.pecmd.log
if exist X:\Windows\System32\pecmd.log type X:\Windows\System32\pecmd.log >> X:\%~nx0.pecmd.log

if not exist X:\WINDOWS\SYSTEM32\startupe2b.bat exit
goto :EOF

