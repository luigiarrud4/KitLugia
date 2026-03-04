@echo off
TITLE %~nx0 (strelec.bat)
wpeinit.exe
REM prevent wpeinit from running again
ren X:\windows\system32\wpeinit.exe wpeinit.exe.old

REM Find E2B volume
for %%I in (C D E F G H I J K L M N O P Q R S T U V W X Y Z) do if exist %%I:\_ISO\e2b\firadisk\loadisope.cmd set E2BDRIVE=%%I:
IF "%E2BDRIVE%"=="" (
echo ERROR - COULD NOT FIND E2B DRIVE!
echo Note: Windows 7 does not contain USB 3 drivers or modern USB 2.0 chipset drivers.
echo ERROR - COULD NOT FIND E2B DRIVE!>> X:\strelec.log
echo Note: Windows 7 does not contain USB 3 drivers or modern USB 2.0 chipset drivers.>> X:\strelec.log
echo LIST DISK | diskpart
echo LIST VOL | diskpart
echo LIST DISK | diskpart>> X:\strelec.log
echo LIST VOL | diskpart>> X:\strelec.log
pause
cmd /k
goto :EOF
)

cd %E2BDRIVE%\ 
%E2BDRIVE%
call \_ISO\e2b\firadisk\loadisope.cmd
REM delay a bit
dir Y:>> X:\ss.log
