pushd "%~dp0"
call %USBDRIVE%\_ISO\E2B\firadisk\ISONAME.cmd
REM echo Mounting "%MYISO%" using ImDisk
::imdisk -a -o cd -f %USBDRIVE%%MYISO% -m #: 
set ISOLETTER=Y:
:: See if user has defined a Drive letter for the ISO in %MYISO%.cmd, e.g. 'set ISOLETTER=S:'
call :getfname "%MYISO%"


set USBDRIVE=
set exit=
FOR %%D IN (A B C D E F G H I J K L M N O P Q R S T U V W X Y Z) DO (
DIR "%%D:%MYISO%"  > nul 2>&1 && (call set USBDRIVE=%%D:) && call set exit=1
)


echo Looking for "%USBDRIVE%%FNAME%.cmd"
if exist "%USBDRIVE%%FNAME%.cmd" call "%USBDRIVE%%FNAME%.cmd"
echo Using %USBDRIVE%%MYISO%
::check if %ISOLETTER% exists
DIR %ISOLETTER%  > nul 2>&1 && (call set ISY=1) 
::if not then use Y:
if not "%ISY%"=="1" echo Mounting ISO as %ISOLETTER%...
if not "%ISY%"=="1" (
   imdisk -a -o cd -f "%USBDRIVE%%MYISO%" -m %ISOLETTER% 
   :: mark as having loaded ISO
   if not errorlevel 1 call :dodone
   )
::else use any available letter
if "%ISY%"=="1" echo Mounting ISO as next available drive letter...
if "%ISY%"=="1" (
   imdisk -a -o cd -f "%USBDRIVE%%MYISO%" -m #: 
   :: mark as having loaded ISO
   if not errorlevel 1 call :dodone
   )

popd
goto :eof

:getfname
set "FNAME=%~pn1"
goto :EOF

:dodone
REM make a new file of over 1K and set MYISO as DONE so we don't try to load it again
echo set A=                                                                                    A> %USBDRIVE%\_ISO\E2B\firadisk\ISONAME.cmd
echo set A=                                                                                    A>> %USBDRIVE%\_ISO\E2B\firadisk\ISONAME.cmd
echo set A=                                                                                    A>> %USBDRIVE%\_ISO\E2B\firadisk\ISONAME.cmd
echo set A=                                                                                    A>> %USBDRIVE%\_ISO\E2B\firadisk\ISONAME.cmd
echo set A=                                                                                    A>> %USBDRIVE%\_ISO\E2B\firadisk\ISONAME.cmd
echo set A=                                                                                    A>> %USBDRIVE%\_ISO\E2B\firadisk\ISONAME.cmd
echo set A=                                                                                    A>> %USBDRIVE%\_ISO\E2B\firadisk\ISONAME.cmd
echo set A=                                                                                    A>> %USBDRIVE%\_ISO\E2B\firadisk\ISONAME.cmd
echo set A=                                                                                    A>> %USBDRIVE%\_ISO\E2B\firadisk\ISONAME.cmd
echo set A=                                                                                    A>> %USBDRIVE%\_ISO\E2B\firadisk\ISONAME.cmd
echo set A=                                                                                    A>> %USBDRIVE%\_ISO\E2B\firadisk\ISONAME.cmd
echo set A=                                                                                    A>> %USBDRIVE%\_ISO\E2B\firadisk\ISONAME.cmd
echo set A=                                                                                    A>> %USBDRIVE%\_ISO\E2B\firadisk\ISONAME.cmd
echo set A=                                                                                    A>> %USBDRIVE%\_ISO\E2B\firadisk\ISONAME.cmd
echo set A=                                                                                    A>> %USBDRIVE%\_ISO\E2B\firadisk\ISONAME.cmd
echo set A=                                                                                    A>> %USBDRIVE%\_ISO\E2B\firadisk\ISONAME.cmd
echo set A=                                                                                    A>> %USBDRIVE%\_ISO\E2B\firadisk\ISONAME.cmd
echo set A=                                                                                    A>> %USBDRIVE%\_ISO\E2B\firadisk\ISONAME.cmd
echo set A=                                                                                    A>> %USBDRIVE%\_ISO\E2B\firadisk\ISONAME.cmd
echo set A=                                                                                    A>> %USBDRIVE%\_ISO\E2B\firadisk\ISONAME.cmd
echo set A=                                                                                    A>> %USBDRIVE%\_ISO\E2B\firadisk\ISONAME.cmd
echo set A=                                                                                    A>> %USBDRIVE%\_ISO\E2B\firadisk\ISONAME.cmd
echo set A=                                                                                    A>> %USBDRIVE%\_ISO\E2B\firadisk\ISONAME.cmd
echo set A=                                                                                    A>> %USBDRIVE%\_ISO\E2B\firadisk\ISONAME.cmd
echo set A=                                                                                    A>> %USBDRIVE%\_ISO\E2B\firadisk\ISONAME.cmd
echo set A=                                                                                    A>> %USBDRIVE%\_ISO\E2B\firadisk\ISONAME.cmd
echo set A=                                                                                    A>> %USBDRIVE%\_ISO\E2B\firadisk\ISONAME.cmd
echo set A=>> %USBDRIVE%\_ISO\E2B\firadisk\ISONAME.cmd
echo set "MYISO=%MYISO%">> %USBDRIVE%\_ISO\E2B\firadisk\ISONAME.cmd
echo set MYISO=DONE>> %USBDRIVE%\_ISO\E2B\firadisk\ISONAME.cmd
goto :EOF