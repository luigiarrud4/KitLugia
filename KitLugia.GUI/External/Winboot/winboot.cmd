@Echo Off
mode con: cols=60 lines=5
chcp 65001 >nul
title WINBOOT - v2024.2.4 by @kitlugia PTBR edition

call :VerPrevAdmin
if "%Admin%"=="ops" goto :eof

setlocal EnableDelayedExpansion

:: Remova o atributo "Somente leitura" para editar o script.
:: ===============================================================================================
::
:: Se a pasta padrão do WinPE na raiz da ISO não for "sources" e o arquivo "*.wim" do WinPE não
:: for "boot" altere o valor das variáveis "winpefsrc=sources" e "winpename=boot". Por exemplo, 
:: o Sergei Strelec usa o caminho \SSTR\strelec10x64Eng.wim para o utilitário WinPE do Windows 10.
:: Caso queira inicializar esse utilitário as variáveis mencionadas devem ficar assim:
::
:: set "winpefsrc=SSTR"
:: set "winpename=strelec10x64Eng"
::
:: --------------------
set "winpefsrc=sources"
set "winpename=boot"
:: --------------------
::
:: Em sistemas com suporte não precisa montar a ISO pois o script fará isso automaticamente.
:: Caso queria pular a verificação de pré-requisitos na atualização para Windows 11, mude o valor
:: da variável "_bpsetup" de "0" para "1". Exemplo:
:: 
:: set "_bpsetup=1"
:: 
:: A condição não altera o registro. Executa o "setup.exe" com o parâmetro "/product server".
:: Considere que a Microsoft pode remover esse parâmetro no futuro. Não culpe o Dev se isso
:: parar de funcionar.
::
:: --------------------
set "_bpsetup=1"
:: --------------------
::
:: É possível usar a partição de instalação do Windows como partição de recuperação do sistema.
:: Caso decida fazer isso já está pré-definido para reduzir a partição do Windows e criar uma
:: partição de 32GB. Se não for passivel o diskpart tentará criar com 28 GB.
::
:: Edite o tamanho da partição conforme a necessidade. Recomendo não criar imagem de recuperção
:: com arquivos pessoais. Apenas do sistema com os programas instalados e suas configurações.
::
:: O tamanho da partição deve ser inserido em megabytes.
::
:: --------------------
set "_partmin=28896"
set "_partmax=33024"
::: --------------------
::
:: ===============================================================================================
set "sysarch=x86"
if exist "%WINDIR%\SysWOW64" set "sysarch=x64"
for /f "tokens=6 delims=[]. " %%b in ('ver') do set winbld=%%b
If %winbld% LSS 9600 goto _Mmenu
set "ISODrvLttr="
set "DriveLetter="
if exist "%temp%\fdlist.txt" del /q /f "%temp%\fdlist.txt"
for /f "tokens=2 delims==:" %%i in ('wmic volume where "drivetype=5" get driveletter /format:value 2^>nul') do (
  if not '%%i'=='' (
  echo %%i>"%temp%\lttrs.txt"
  set /p _dlist=<"%temp%\lttrs.txt"
  del "%temp%\lttrs.txt"
  >>"%temp%\fdlist.txt" echo !_dlist!
  )
)
if not exist "%temp%\fdlist.txt" goto _Mmenu
set c_lttr=0
for /f %%# in ('type "%temp%\fdlist.txt"') do (
  set /a c_lttr=c_lttr+1
  set sd_iso[!c_lttr!]=%%#
)
if [!c_lttr!] equ [1] set "ISODrvLttr=!sd_iso[1]!"&goto ISODrvLttr
chcp 850 >nul
powershell -Command "& {$filePath = '%temp%\fdlist.txt'; $OutPath = '%temp%\dllines.txt'; $Data = Get-Content $filePath; $tpData = $Data -join ' '; [IO.File]::WriteAllText($OutPath,$tpData)}"
chcp 65001 >nul
set /p ISODrvLttr=<"%temp%\dllines.txt"
if "%ISODrvLttr%"=="" goto _Mmenu

:ISODrvLttr
if exist "%temp%\dllines.txt" del /Q /F "%temp%\dllines.txt"
if exist "%temp%\fdlist.txt" del /Q /F "%temp%\fdlist.txt"
for %%i in (%ISODrvLttr%) do if exist %%i:\sources\*.wim set DriveLetter=%%i
if "%DriveLetter%"=="" goto _Mmenu
chcp 850 >nul
for /f "delims=" %%i in ('powershell -Command "& {Get-Volume -DriveLetter %DriveLetter%  | %% {(Get-DiskImage -DevicePath $($_.Path -replace '\\$')).DevicePath}}"') do set DevicePath=%%i
powershell -Command "Dismount-DiskImage -DevicePath %DevicePath% | Out-Null"
chcp 65001 >nul

:_Mmenu
cls
mode con: cols=65 lines=20
title WINBOOT - v2024.2.4 by @kitlugia PTBR edition
echo.
echo      ═══════════════════════════════════════════════════════
echo      █████ Escolha uma das opções para continuar. . .  █████
echo      ═══════════════════════════════════════════════════════
echo.     
echo       ╔═══════════════════════════════════════════════════╗
echo       ║░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░║
echo       ║░ [1] CRIAR UMA UNIDADE DE BOOT SEM MÍDIA EXTERNA ░║
echo       ║░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░║
echo       ║░ [2] REMOVER PARTIÇÃO E ENTRADAS DE BOOT ░░░░░░░░░║
echo       ║░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░║
echo       ║░ [3] ATUALIZAR O WINDOWS INSTALADO ░░░░░░░░░░░░░░░║
echo       ║░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░║
echo       ╚═══════════════════════════════════════════════════╝
echo.     
echo      ═══════════════════════════════════════════════════════
echo      █████          Pressione 'x' para sair.           █████
echo      ═══════════════════════════════════════════════════════
choice /n /c 123x /m "-    > "
if errorlevel 4 Exit
if errorlevel 3 goto _uptewin
if errorlevel 2 goto _rmepart
if errorlevel 1 goto _cvepart

:_cvepart
cls
mode con: cols=80 lines=8
title Criar uma unidade de boot sem mídia externa
if exist "%temp%\7z.dll" del /q /f "%temp%\7z.dll"
if exist "%temp%\7z.exe" del /q /f "%temp%\7z.exe"
if exist "%temp%\dism.7z" del /q /f "%temp%\dism.7z"
if exist "%temp%\7zdll.txt" del /q /f "%temp%\7zdll.txt"
if exist "%temp%\7zexe.txt" del /q /f "%temp%\7zexe.txt"
if exist "%temp%\dism7z.txt" del /q /f "%temp%\dism7z.txt"
set "dlboot=%HOMEDRIVE%"
set "svndll=%temp%\7z.dll"
set "svndllHtxt=%temp%\7zdll.txt"
set "svndllHash=367f8d1bfcf90ae86c0c33b0c8c9e6ec1c433c353d0663ebb44567607402c83d"
set "svnexe=%temp%\7z.exe"
set "svnexeHtxt=%temp%\7zexe.txt"
set "svnexeHash=3092f736f9f4fc0ecc00a4d27774f9e09b6f1d6eee8acc1b45667fe1808646a6"
set "msdism=%temp%\dism.7z"
set "msdismHtxt=%temp%\dism7z.txt"
set "msdismHash=a85488471e27a20f4f15e8dba9d690281cd6038473be943762a2ca6ddde7ab2e"
set "scrintgdrv=RbakDrives.cmd"
set "captimgwim=CaptImagem.cmd"

:downdeps
cls
title Baixando DISM e 7-Zip CLI. . .
chcp 850 >nul
if %winbld% LSS 19041 powershell -ExecutionPolicy ByPass -Command "& {[Net.ServicePointManager]::SecurityProtocol = \"Tls, Tls11, Tls12, Ssl3\"; $svndll = '%svndll%'; $svndllHtxt = '%svndllHtxt%'; $svnexe = '%svnexe%'; $svnexeHtxt = '%svnexeHtxt%'; $msdism = '%msdism%'; $msdismHtxt = '%msdismHtxt%'; Invoke-WebRequest -UseBasicParsing -Uri 'https://bit.ly/svndll' -OutFile $svndll -ErrorAction Stop; $svndllhash = (Get-FileHash -Path $svndll -Algorithm SHA256).Hash; $svndllhash = $svndllhash.ToLower(); [IO.File]::WriteAllText($svndllHtxt, $svndllhash); Clear-Host; Invoke-WebRequest -UseBasicParsing -Uri 'https://bit.ly/svnexe' -OutFile $svnexe -ErrorAction Stop; $svnexehash = (Get-FileHash -Path $svnexe -Algorithm SHA256).Hash; $svnexehash = $svnexehash.ToLower(); [IO.File]::WriteAllText($svnexeHtxt, $svnexehash); Clear-Host; Invoke-WebRequest -UseBasicParsing -Uri 'https://bit.ly/dismtools' -OutFile $msdism -ErrorAction Stop; $msdismhash = (Get-FileHash -Path $msdism -Algorithm SHA256).Hash; $msdismhash = $msdismhash.ToLower(); [IO.File]::WriteAllText($msdismHtxt, $msdismhash)}"
if %winbld% GEQ 19041 powershell -ExecutionPolicy ByPass -Command "& {$svndll = '%svndll%'; $svndllHtxt = '%svndllHtxt%'; $svnexe = '%svnexe%'; $svnexeHtxt = '%svnexeHtxt%'; $msdism = '%msdism%'; $msdismHtxt = '%msdismHtxt%'; Invoke-WebRequest -UseBasicParsing -Uri 'https://bit.ly/svndll' -OutFile $svndll -ErrorAction Stop; $svndllhash = (Get-FileHash -Path $svndll -Algorithm SHA256).Hash; $svndllhash = $svndllhash.ToLower(); [IO.File]::WriteAllText($svndllHtxt, $svndllhash); Clear-Host; Invoke-WebRequest -UseBasicParsing -Uri 'https://bit.ly/svnexe' -OutFile $svnexe -ErrorAction Stop; $svnexehash = (Get-FileHash -Path $svnexe -Algorithm SHA256).Hash; $svnexehash = $svnexehash.ToLower(); [IO.File]::WriteAllText($svnexeHtxt, $svnexehash); Clear-Host; Invoke-WebRequest -UseBasicParsing -Uri 'https://bit.ly/dismtools' -OutFile $msdism -ErrorAction Stop; $msdismhash = (Get-FileHash -Path $msdism -Algorithm SHA256).Hash; $msdismhash = $msdismhash.ToLower(); [IO.File]::WriteAllText($msdismHtxt, $msdismhash)}"
chcp 65001 >nul
set /p _svndllrHash=<"%svndllHtxt%"
set /p _svnexerHash=<"%svnexeHtxt%"
set /p _msdismrHash=<"%msdismHtxt%"
if %_svndllrHash%.==%svndllHash%. (if %_svnexerHash%.==%svnexeHash%. (if %_msdismrHash%.==%msdismHash%. (goto startcfg)))
cls & mode con: cols=80 lines=5 & echo. & echo. & echo     Falha na verficação de Hash^^! Verifique sua conexão com a internet. & timeout /t 10 /nobreak >nul & goto End

:startcfg
cls
del /q /f "%svndllHtxt%" >nul 2>&1
del /q /f "%svnexeHtxt%" >nul 2>&1
del /q /f "%msdismHtxt%" >nul 2>&1
mode con: cols=78 lines=10
set "crtp="
title Criar uma unidade de boot sem mídia externa
echo.
echo     ╔═══════════════════════════════════════════════════════════════════╗
echo      ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░
echo      ░     PRESSIONE "r" PARA CRIAR UMA PARTIÇÃO DE RECUPERAÇÃO OU     ░
echo      ░   PRESSIONE "i" PARA CRIAR UMA PARTIÇÃO SOMENTE PARA INSTALAR   ░
echo      ░             O WINDOWS OU INICIALIZAR UM WinPE. . .              ░
echo      ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░
echo     ╚═══════════════════════════════════════════════════════════════════╝
choice /n /c RI /m "-   > "
if errorlevel 2 set "crtp=I"& goto startcpt
if errorlevel 1 set "crtp=R"& goto selISO

:startcpt
cls
mode con: cols=78 lines=9
echo.
echo     ╔═══════════════════════════════════════════════════════════════════╗
echo      ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░
echo      ░ POR PADRÃO A PARTIÇÃO DO WINDOWS É REDUZIDA PARA CRIAR UMA NOVA ░
echo      ░ PARTIÇÃO A QUAL RECEBERÁ OS ARQUIVOS DE INSTALAÇÃO. . .         ░
echo      ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░
echo     ╚═══════════════════════════════════════════════════════════════════╝
choice /n /c SN /m "-   >Deseja reduzir uma outra partição com espaço disponível? (S ou N): "
if errorlevel 2 goto selISO
if errorlevel 1 goto selpart

:selpart
cls
mode con: cols=80 lines=5
echo.
echo.
echo     Selecione a partição de uma outra unidade de disco para redução. . .
set "twindow="
set "twindow="(new-object -COM 'Shell.Application').BrowseforFolder(0,'Selecione uma partição para reduzir e criar uma unidade de boot com a instalação do Windows.',0x200,0).self.path""
chcp 850 >nul
for /f "usebackq delims=\" %%p in (`powershell %twindow%`) do set dlboot=%%p
chcp 65001 >nul
if "%dlboot%"=="%HOMEDRIVE%" (cls & echo. & echo. & echo     A partição do Windows "%dlboot%" será reduzida. . . & timeout /t 5 /nobreak >nul)

:selISO
cls
mode con: cols=85 lines=5
echo.
echo.
echo     Selecione um arquivo ISO de instalação do Windows ou utilitários WinPE. . .
chcp 850 >nul
set "wisopath="
for /f "delims=" %%a in ('powershell -ExecutionPolicy ByPass -Command "& {Add-Type -AssemblyName System.Windows.Forms | Out-Null; $dlg = New-Object System.Windows.Forms.OpenFileDialog; $dlg.Title = 'Selecione um arquivo de imagem do Windows'; $dlg.InitialDirectory = 'shell:MyComputerFolder'; $dlg.Filter = 'Windows; WinPE|*.iso';if($dlg.ShowDialog() -eq 'OK'){return $dlg.Filename}}"') do set wisopath=%%a
chcp 65001 >nul
if "%wisopath%"=="" goto End
for %%n in ("%wisopath%") do set "isoname=%%~nn"
for %%n in ("%wisopath%") do set "isoname_ext=%%~nxn"
chcp 850 >nul
for /f %%s in ('powershell -Command "& {[int]$SizeMB = (((Get-Item -Path '%wisopath%').length)/1MB); $SizeMB}"') do set "ISOSize=%%s"
chcp 65001 >nul
set /a "pminimum=%ISOSize%+2048"
set /a "pdesired=%ISOSize%+6144"
if %crtp%.==R. (
 set "pminimum=%_partmin%"
 set "pdesired=%_partmax%"
)
cls & mode con: cols=80 lines=5 & echo. & echo. & echo     Verificando informações do sistema e partição. . .
timeout /t 2 /nobreak >nul
set "_WinEdition="
set "WinEdition="
set "nametools=Instalação do Windows"
if %crtp%.==R. set "nametools=Imagem de Recuperação"
set   "winpart=%HOMEDRIVE%"
set "nameosldr=winload.exe"
set  "biosmode=BIOS"
for /f "tokens=2 delims==" %%i in ('wmic logicaldisk where "DeviceID="%winpart%"" Assoc:list /AssocClass:Win32_LogicalDiskToPartition /ResultRole:Antecedent ^| Find "DiskIndex="') do set sysnund=%%i
if %winbld% LSS 17134 (
 for /f "tokens=2* delims==" %%i in ('wmic os get Caption /value 2^>nul ^| find /i "Caption"') do set "_WinEdition=%%i"
)
if %winbld% GEQ 17134 (
  chcp 850 >nul
  for /f "tokens=*" %%i in ('powershell -Command "(Get-WmiObject -Class Win32_OperatingSystem).Caption"') do set "_WinEdition=%%i"
  chcp 65001 >nul
)
set "_WinEdition=%_WinEdition:Microsoft =%"
for /f "skip=2 tokens=1,2*" %%i in ('reg query "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion" /v ProductName 2^>nul') do if /i "%%i"=="ProductName" set "WinEdition=%%k"
if /i not "%_WinEdition%"=="%WinEdition%" reg add "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion" /v ProductName /d "%_WinEdition%" /f >nul
set "WinEdition=%_WinEdition%"
for /f "skip=2 tokens=1,2*" %%i in ('reg query "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion" /v DisplayVersion 2^>nul') do if /i "%%i"=="DisplayVersion" set "DpVersion=%%k"
for /f "tokens=6,7 delims=[]. " %%b in ('ver') do set "instwbld=%%b.%%c"
for /f "skip=1 tokens=1,2" %%i in ('"wmic /NameSpace:\\ROOT\Microsoft\Windows\Storage Path MSFT_Disk where Number=%sysnund% get Number,PartitionStyle"') do if '%%j'=='2' set FPStyle=GPT
if %FPStyle%.==GPT. set "nameosldr=winload.efi"&set "biosmode=UEFI"
timeout /t 3 /nobreak >nul
(echo select volume %dlboot%&&echo shrink desired=%pdesired% minimum=%pminimum% noerr&&echo create partition primary noerr&&echo format quick fs=NTFS label="Preparando-se. . ." noerr&&echo assign noerr&&echo Exit) >"%temp%\srk.txt"
cls & echo. & echo. & echo     Reduzindo partição %dlboot%. . .
chcp 850 >nul
diskpart /s "%temp%\srk.txt" >nul&del /q /f "%temp%\srk.txt"
timeout /t 3 /nobreak >nul
cls
chcp 65001 >nul
for /f %%l in ('wmic logicaldisk get deviceid^, volumename ^| findstr /C:"Preparando-se. . ."') do set ltrdrive=%%l
if "%ltrdrive%"=="" goto _nrpart
chcp 850 >nul
powershell -Command $pth = [uri]'%ltrdrive%\'; foreach ($w in (New-Object -ComObject Shell.Application).Windows()){if ($w.LocationURL -ieq $pth.AbsoluteUri) {$w.Quit(); break}}
cls & mode con: cols=100 lines=5 & chcp 65001 >nul & echo. & echo. & echo     Descompactando "%isoname_ext%" para %ltrdrive%\. . .
"%svnexe%" x -y "%wisopath%" -o%ltrdrive%\ -bso0 -bsp0
"%svnexe%" x -y "%msdism%" -o%ltrdrive%\ -bso0 -bsp0
timeout /t 2 /nobreak >nul
if exist "%temp%\_isolvid.txt" del /q /f "%temp%\_isolvid.txt"
set "isolabel="
("%svnexe%" l "%wisopath%" | find /i "LogicalVolumeId")>"%temp%\_isolvid.txt"
for /f "tokens=2 delims=: " %%i in ('type "%temp%\_isolvid.txt" 2^>nul') do set isolabel=%%i
if "%isolabel%"=="" set "isolabel=ESD-ISO"
del /q /f "%temp%\_isolvid.txt" >nul 2>&1
timeout /t 3 /nobreak >nul
label %ltrdrive% %isolabel%
del /q /f "%svnexe%" >nul 2>&1
del /q /f "%svndll%" >nul 2>&1
del /q /f "%msdism%" >nul 2>&1
if not exist "%ltrdrive%\%winpefsrc%\%winpename%.wim" (cls & mode con: cols=80 lines=12 & echo. & echo. & echo     Arquivo de imagem não suportado ou não foi descompactado. & echo. & echo     Se estiver usando um utilitário PE verifique o caminho do WinPE & echo. & echo     e edite as informações "%winpefsrc%" e "%winpename%" no início & echo. & echo     do script. Remova a partição criada antes de recomeçar. & echo. & echo     Pressione qualquer tecla para sair. . . & pause >nul & start "" "https://cutt.ly/Swh2uy0b" & start "" "https://www.youtube.com/@xerifetech?sub_confirmation=1" & Exit)
set "imgarch="
set "_iiinfo=0x0"
if exist "%ltrdrive%\%winpefsrc%\install.esd" set "_iimg=%ltrdrive%\%winpefsrc%\install.esd"&set "_iiinfo=0x1"
if exist "%ltrdrive%\%winpefsrc%\install.wim" set "_iimg=%ltrdrive%\%winpefsrc%\install.wim"&set "_iiinfo=0x1"
if exist "%ltrdrive%\%winpefsrc%\install.swm" set "_iimg=%ltrdrive%\%winpefsrc%\install.swm"&set "_iiinfo=0x1"
if %_iiinfo%.==0x0. (if %crtp%.==I. (set "nametools=WinPE Utilitários"&label %ltrdrive% WinPE-Tools&goto skipxml))
if %_iiinfo%.==0x0. (if %crtp%.==R. (set "nametools=WinPE e Recuperação"&label %ltrdrive% WinPE-Tools&goto skipxml))
if %_iiinfo%.==0x1. (
  chcp 850 >nul
  for /f "tokens=5 delims=. " %%i in ('dism /English /Get-ImageInfo /ImageFile:"%_iimg%" /index:1 ^| find /i "Version"') do set _winbld=%%i
  for /f "tokens=2 delims=: " %%a in ('dism /English /Get-ImageInfo /ImageFile:"%_iimg%" /index:1 ^| find /i "Architecture"') do set imgarch=%%a
  for /f "tokens=3 delims=: " %%a in ('dism /English /Get-ImageInfo /ImageFile:"%_iimg%" /index:1 ^| find /i "ServicePack Build"') do set spbuild=%%a
  for /f "tokens=1 delims=	 " %%l in ('dism /English /Get-ImageInfo /ImageFile:"%_iimg%" /index:1 ^| find /i "(Default)"') do set winlang=%%l
)
chcp 65001 >nul
if %imgarch%.==x64. set "imgarch=amd64"&set "iarchdp=x64"
if not exist "%ltrdrive%\%winpefsrc%\ei.cfg" (echo [Channel]&&echo _Default&&echo [VL]&&echo 0) >%ltrdrive%\%winpefsrc%\ei.cfg
if %sysarch%.==x64. (if %iarchdp%.==x64. (cls & mode con: cols=100 lines=30 & echo. & echo     Iniciando exportação dos drives do sistema instalado. . . & timeout /t 3 /nobreak >nul & md %ltrdrive%\drives & %ltrdrive%\tools\x64\dism.exe /online /export-driver /destination:"%ltrdrive%\drives"))
if %sysarch%.==x86. (if %iarchdp%.==x86. (cls & mode con: cols=100 lines=30 & echo. & echo     Iniciando exportação dos drives do sistema instalado. . . & timeout /t 3 /nobreak >nul & md %ltrdrive%\drives & %ltrdrive%\tools\x86\dism.exe /online /export-driver /destination:"%ltrdrive%\drives"))

if %_winbld% Leq 19044 goto skipxml
if %_winbld% LSS 22000 goto bpwin10
if %_winbld% GEQ 22000 goto bpwin11

:bpwin10
if %_iiinfo%==0x1 (echo ^<?xml version="1.0" encoding="utf-8"?^>&&echo ^<unattend xmlns="urn:schemas-microsoft-com:unattend"^>&&echo     ^<settings pass="windowsPE"^>&&echo         ^<component name="Microsoft-Windows-Setup" processorArchitecture="%imgarch%" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS" xmlns:wcm="http://schemas.microsoft.com/WMIConfig/2002/State" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"^>&&echo             ^<UserData^>&&echo                 ^<AcceptEula^>true^</AcceptEula^>&&echo             ^</UserData^>&&echo         ^</component^>&&echo     ^</settings^>&&echo     ^<settings pass="specialize"^>&&echo         ^<component name="Microsoft-Windows-Deployment" processorArchitecture="%imgarch%" language="neutral" publicKeyToken="31bf3856ad364e35" versionScope="nonSxS" xmlns:wcm="http://schemas.microsoft.com/WMIConfig/2002/State" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"^>&&echo             ^<RunSynchronous^>&&echo                 ^<RunSynchronousCommand wcm:action="add"^>&&echo                     ^<Order^>1^</Order^>&&echo                     ^<Path^>reg add HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\OOBE /v BypassNRO /t REG_DWORD /d 1 /f^</Path^>&&echo                 ^</RunSynchronousCommand^>&&echo             ^</RunSynchronous^>&&echo         ^</component^>&&echo     ^</settings^>&&echo     ^<settings pass="oobeSystem"^>&&echo         ^<component name="Microsoft-Windows-Shell-Setup" processorArchitecture="%imgarch%" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS" xmlns:wcm="http://schemas.microsoft.com/WMIConfig/2002/State" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"^>&&echo             ^<OOBE^>&&echo                 ^<HideLocalAccountScreen^>false^</HideLocalAccountScreen^>&&echo                 ^<HideOnlineAccountScreens^>false^</HideOnlineAccountScreens^>&&echo                 ^<HideWirelessSetupInOOBE^>false^</HideWirelessSetupInOOBE^>&&echo                 ^<SkipUserOOBE^>false^</SkipUserOOBE^>&&echo                 ^<SkipMachineOOBE^>false^</SkipMachineOOBE^>&&echo                 ^<ProtectYourPC^>1^</ProtectYourPC^>&&echo             ^</OOBE^>&&echo         ^</component^>&&echo     ^</settings^>&&echo ^</unattend^>) >%ltrdrive%\Autounattend.xml
goto skipxml

:bpwin11
if %_iiinfo%==0x1 (echo ^<?xml version="1.0" encoding="utf-8"?^>&&echo ^<unattend xmlns="urn:schemas-microsoft-com:unattend"^>&&echo     ^<settings pass="windowsPE"^>&&echo         ^<component name="Microsoft-Windows-Setup" processorArchitecture="%imgarch%" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS" xmlns:wcm="http://schemas.microsoft.com/WMIConfig/2002/State" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"^>&&echo             ^<RunSynchronous^>&&echo                 ^<RunSynchronousCommand wcm:action="add"^>&&echo                     ^<Order^>1^</Order^>&&echo                     ^<Path^>reg add HKLM\System\Setup\LabConfig /v BypassTPMCheck /t REG_DWORD /d 1 /f^</Path^>&&echo                 ^</RunSynchronousCommand^>&&echo                 ^<RunSynchronousCommand wcm:action="add"^>&&echo                     ^<Order^>2^</Order^>&&echo                     ^<Path^>reg add HKLM\System\Setup\LabConfig /v BypassSecureBootCheck /t REG_DWORD /d 1 /f^</Path^>&&echo                 ^</RunSynchronousCommand^>&&echo                 ^<RunSynchronousCommand wcm:action="add"^>&&echo                     ^<Order^>3^</Order^>&&echo                     ^<Path^>reg add HKLM\System\Setup\LabConfig /v BypassRAMCheck /t REG_DWORD /d 1 /f^</Path^>&&echo                 ^</RunSynchronousCommand^>&&echo                 ^<RunSynchronousCommand wcm:action="add"^>&&echo                     ^<Order^>4^</Order^>&&echo                     ^<Path^>reg add HKLM\System\Setup\LabConfig /v BypassStorageCheck /t REG_DWORD /d 1 /f^</Path^>&&echo                 ^</RunSynchronousCommand^>&&echo                 ^<RunSynchronousCommand wcm:action="add"^>&&echo                     ^<Order^>5^</Order^>&&echo                     ^<Path^>reg add HKLM\System\Setup\LabConfig /v BypassCPUCheck /t REG_DWORD /d 1 /f^</Path^>&&echo                 ^</RunSynchronousCommand^>&&echo             ^</RunSynchronous^>&&echo             ^<UserData^>&&echo                 ^<AcceptEula^>true^</AcceptEula^>&&echo             ^</UserData^>&&echo         ^</component^>&&echo     ^</settings^>&&echo     ^<settings pass="specialize"^>&&echo         ^<component name="Microsoft-Windows-Deployment" processorArchitecture="%imgarch%" language="neutral" publicKeyToken="31bf3856ad364e35" versionScope="nonSxS" xmlns:wcm="http://schemas.microsoft.com/WMIConfig/2002/State" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"^>&&echo             ^<RunSynchronous^>&&echo                 ^<RunSynchronousCommand wcm:action="add"^>&&echo                     ^<Order^>1^</Order^>&&echo                     ^<Path^>reg add HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\OOBE /v BypassNRO /t REG_DWORD /d 1 /f^</Path^>&&echo                 ^</RunSynchronousCommand^>&&echo             ^</RunSynchronous^>&&echo         ^</component^>&&echo     ^</settings^>&&echo     ^<settings pass="oobeSystem"^>&&echo         ^<component name="Microsoft-Windows-Shell-Setup" processorArchitecture="%imgarch%" publicKeyToken="31bf3856ad364e35" language="neutral" versionScope="nonSxS" xmlns:wcm="http://schemas.microsoft.com/WMIConfig/2002/State" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"^>&&echo             ^<OOBE^>&&echo                 ^<HideLocalAccountScreen^>false^</HideLocalAccountScreen^>&&echo                 ^<HideOnlineAccountScreens^>false^</HideOnlineAccountScreens^>&&echo                 ^<HideWirelessSetupInOOBE^>false^</HideWirelessSetupInOOBE^>&&echo                 ^<SkipUserOOBE^>false^</SkipUserOOBE^>&&echo                 ^<SkipMachineOOBE^>false^</SkipMachineOOBE^>&&echo                 ^<ProtectYourPC^>1^</ProtectYourPC^>&&echo             ^</OOBE^>&&echo         ^</component^>&&echo     ^</settings^>&&echo ^</unattend^>) >%ltrdrive%\Autounattend.xml

:skipxml
for /f "tokens=1,2 delims=:" %%i in ("%ltrdrive%\%winpefsrc%\%winpename%.wim") do (
  set "lnewpart=%%i"
  set "lbootwim=%%j"
)
cls
mode con: cols=80 lines=5
set  "osdvrmdk=[%lnewpart%:]%lbootwim%"
echo.
echo.
echo     Configurando o boot para o WinPE em %ltrdrive%\. . .
>nul 2>&1 bcdedit /create {ramdiskoptions} /d "%nametools%"
>nul 2>&1 bcdedit /set {ramdiskoptions} ramdisksdidevice partition=%lnewpart%:
>nul 2>&1 bcdedit /set {ramdiskoptions} ramdisksdipath \boot\boot.sdi
>nul 2>&1 bcdedit -create /d "%nametools%" /application OSLOADER
for /f "tokens=3" %%c in ('bcdedit -create /d "%nametools%" /application OSLOADER') do set guidcode=%%c
>nul 2>&1 bcdedit /set %guidcode% device ramdisk=%osdvrmdk%,{ramdiskoptions}
>nul 2>&1 bcdedit /set %guidcode% path \Windows\System32\%nameosldr%
>nul 2>&1 bcdedit /set %guidcode% osdevice ramdisk=%osdvrmdk%,{ramdiskoptions}
>nul 2>&1 bcdedit /set %guidcode% systemroot \Windows
>nul 2>&1 bcdedit /set %guidcode% winpe yes
>nul 2>&1 bcdedit /set %guidcode% detecthal yes
>nul 2>&1 bcdedit /displayorder %guidcode% /addlast
>nul 2>&1 bcdedit /timeout 30
if exist %ltrdrive%\drives call :_intrdrvr
chcp 850 >nul
if %crtp%.==R. powershell -Command "& {$outscdrv = '%ltrdrive%\%captimgwim%'; $vpzakymh = '%cpimgwim%'; $ctbvzgno = [System.Text.Encoding]::Default.GetString([System.Convert]::FromBase64String($vpzakymh)); $ctbvzgno | Out-File -FilePath $outscdrv -Encoding Default}"
powershell -Command "& {$osrccimg = '%ltrdrive%\%scrintgdrv%'; $vpzakymh = '%bsstrnpe%'; $ctbvzgno = [System.Text.Encoding]::Default.GetString([System.Convert]::FromBase64String($vpzakymh)); $ctbvzgno | Out-File -FilePath $osrccimg -Encoding Default}"
chcp 65001 >nul
if %crtp%.==R. (echo %WinEdition%)>%ltrdrive%\_ProductName.txt
if %crtp%.==R. (echo %COMPUTERNAME%)>%ltrdrive%\_PCName.txt
timeout /t 3 /nobreak >nul
if %_iiinfo%.==0x0. goto wpetools
cls
mode con: cols=80 lines=28
title Unidade de boot sem mídia externa concluída
echo.
echo.
echo     ══ SISTEMA INSTALADO ════════════════════════════════════════════
echo.
echo     # %WinEdition%
echo.
echo       - Versão %DpVersion% ^(Compilação %instwbld%^)
echo.
echo       - Número da unidade.......: %sysnund%
echo.
echo       - Modo de instalação......: %biosmode%
echo.
echo.
echo     ══ SISTEMA PARA INSTALAÇÃO ══════════════════════════════════════
echo.
echo     # %isoname%
echo.
echo       - Info. Base. Index 1.....: %_winbld%.%spbuild%_%iarchdp%_%winlang%
echo.
echo       - Nome da opção de boot...: %nametools%
echo.
echo       - Local do WinPE..........: %lnewpart%:%lbootwim%
echo.
echo     ═════════════════════════════════════════════════════════════════
echo.
echo     Pressione qualquer tecla para sair. . .
pause >nul
start "" "https://cutt.ly/Swh2uy0b"
start "" "https://www.youtube.com/@xerifetech?sub_confirmation=1"
explorer shell:mycomputerfolder
Exit

:wpetools
cls
mode con: cols=80 lines=26
title Unidade de boot sem mídia externa concluída
echo.
echo.
echo     ══ SISTEMA INSTALADO ════════════════════════════════════════════
echo.
echo     # %WinEdition%
echo.
echo       - Versão %DpVersion% ^(Compilação %instwbld%^)
echo.
echo       - Número da unidade.......: %sysnund%
echo.
echo       - Modo de instalação......: %biosmode%
echo.
echo.
echo     ══ WINPE UTILITÁRIOS ════════════════════════════════════════════
echo.
echo     # %isoname%
echo.
echo       - Nome da opção de boot...: %nametools%
echo.
echo       - Local do WinPE..........: %lnewpart%:%lbootwim%
echo.
echo     ═════════════════════════════════════════════════════════════════
echo.
echo     Pressione qualquer tecla para sair. . .
pause >nul
start "" "https://cutt.ly/Swh2uy0b"
start "" "https://www.youtube.com/@xerifetech?sub_confirmation=1"
explorer shell:mycomputerfolder
Exit

:_nrpart
cls & mode con: cols=80 lines=9 & chcp 65001 >nul & echo. & echo. & echo     Não foi possível criar uma partição de %pdesired% MB. & echo. & echo     Erro na execução do Diskpart ou não há espaço & echo. & echo     suficiente para reduzir a partição %dlboot%. & timeout /t 12 >nul
cls
del /q /f "%svnexe%" >nul 2>&1
del /q /f "%svndll%" >nul 2>&1
del /q /f "%msdism%" >nul 2>&1
del /q /f "%svndllHtxt%" >nul 2>&1
del /q /f "%svnexeHtxt%" >nul 2>&1
del /q /f "%msdismHtxt%" >nul 2>&1
start "" "https://cutt.ly/Swh2uy0b"
start "" "https://www.youtube.com/@xerifetech?sub_confirmation=1"
Exit

:End
cls
del /q /f "%svnexe%" >nul 2>&1
del /q /f "%svndll%" >nul 2>&1
del /q /f "%msdism%" >nul 2>&1
del /q /f "%svndllHtxt%" >nul 2>&1
del /q /f "%svnexeHtxt%" >nul 2>&1
del /q /f "%msdismHtxt%" >nul 2>&1
start "" "https://cutt.ly/Swh2uy0b"
start "" "https://www.youtube.com/@xerifetech?sub_confirmation=1"
Exit

:_intrdrvr
set "bsstrnpe=QEVjaG8gT2ZmDQpjaGNwIDY1MDAxID5udWwNCnRpdGxlIEludGVncmFyIGRyaXZlcyBubyBzaXN0ZW1hIGluc3RhbGFkbyBhIHBhcnRpciBkZSB1bSBXaW5QRSBieSBARHVhbnlEaWFzDQoNCmlmIG5vdCBleGlzdCBYOlxXaW5kb3dzXFN5c3RlbTMyIChjbHMgJiBlY2hvLiAmIGVjaG8uICYgZWNobyAgICAgRXNzZSBzY3JpcHQgZGV2ZSBzZXIgZXhlY3V0YWRvIG5vIGFtYmllbnRlIGRvIFdpbmRvd3MgUEUuICYgZWNoby4gJiBlY2hvICAgICBQcmVzc2lvbmUgcXVhbHF1ZXIgdGVjbGEgcGFyYSBzYWlyLiAuIC4gJiBwYXVzZSA+bnVsICYgRXhpdCkNCg0Kc2V0ICJhcmNocGU9eDg2Ig0KaWYgZXhpc3QgIlg6XFdpbmRvd3NcU3lzV09XNjQiIHNldCAiYXJjaHBlPXg2NCINCnNldCAiX2Rpc209JX5kcDB0b29sc1wlYXJjaHBlJVxkaXNtLmV4ZSINCnNldCAiX2RydnI9JX5kcDBkcml2ZXMiDQoNCnNldGxvY2FsIGVuYWJsZWRlbGF5ZWRleHBhbnNpb24NCg0KOl9zdGFydA0KY2xzDQplY2hvIGxpc3Qgdm9sfGRpc2twYXJ0DQpzZXQgX3dsdHRyPQ0Kc2V0IC9wIF93bHR0cj1EaWdpdGUgYSBsZXRyYSBkYSBwYXJ0acOnw6NvIGRvIFdpbmRvd3M6IA0KaWYgbm90IGRlZmluZWQgX3dsdHRyIGNscyAmIGdvdG8gX3N0YXJ0DQpzZXQgIl9lcnI9IiZmb3IgL2YgImRlbGltcz1DY0RkRWVGZkdnSGhJaSIgJSVpIGluICgiJV93bHR0ciUiKSBkbyBzZXQgX2Vycj0lJWkNCmlmIGRlZmluZWQgX2VyciBjbHMgJiBnb3RvIF9zdGFydA0KY2xzDQpjYWxsIDpfc3RyY250DQppZiAlX2NudCUgRXF1IDEgZ290byBfaW50Z2Rydg0KY2xzICYgZ290byBfc3RhcnQNCg0KOl9pbnRnZHJ2DQpjbHMNCmlmICVfd2x0dHIlLj09Yy4gc2V0IF93bHR0cj1DDQppZiAlX3dsdHRyJS49PWQuIHNldCBfd2x0dHI9RA0KaWYgJV93bHR0ciUuPT1lLiBzZXQgX3dsdHRyPUUNCmlmICVfd2x0dHIlLj09Zi4gc2V0IF93bHR0cj1GDQppZiAlX3dsdHRyJS49PWcuIHNldCBfd2x0dHI9Rw0KaWYgJV93bHR0ciUuPT1oLiBzZXQgX3dsdHRyPUgNCmlmICVfd2x0dHIlLj09aS4gc2V0IF93bHR0cj1JDQppZiBub3QgZXhpc3QgJV93bHR0ciU6XFdpbmRvd3NcU3lzdGVtMzIgKGNscyAmIGVjaG8uICYgZWNoby4gJiBlY2hvICAgICAiJV93bHR0ciUiIG7Do28gw6kgYSBsZXRyYSBkYSBwYXJ0acOnw6NvIGRvIFdpbmRvd3MuICYgZWNoby4gJiBlY2hvICAgICAgUHJlc3Npb25lIHF1YWxxdWVyIHRlY2xhIHBhcmEgdGVudGFyIG5vdmFtZW50ZS4gLiAuICYgcGF1c2UgPm51bCAmIGdvdG8gX3N0YXJ0KQ0KY2xzDQolX2Rpc20lIC9JbWFnZTolX3dsdHRyJTpcIC9BZGQtRHJpdmVyIC9Ecml2ZXI6IiVfZHJ2ciUiIC9SZWN1cnNlIC9Gb3JjZVVuc2lnbmVkDQplY2hvLg0KZWNoby4NCmVjaG8gUHJlc3Npb25lIHF1YWxxdWVyIHRlY2xhIHBhcmEgc2Fpci4gLiAuDQpwYXVzZSA+bnVsDQpFeGl0DQoNCjpfc3RyY250DQppZiBub3QgZGVmaW5lZCBfY250IHNldCAvYSAiX2NudD0wIg0KaWYgbm90ICIhX3dsdHRyOn4lX2NudCUsMSEiPT0iIiBzZXQgL2EgIl9jbnQrPTEiJmdvdG8gX3N0cmNudA0KZ290byA6ZW9m"
set "cpimgwim=QEVjaG8gT2ZmDQpjaGNwIDY1MDAxID5udWwNCnRpdGxlIENhcHR1cmFyIHVtYSBpbWFnZW0gZGUgcmVjdXBlcmHDp8OjbyBkbyBzaXN0ZW1hIGJ5IEBEdWFueURpYXMNCg0KaWYgbm90IGV4aXN0IFg6XFdpbmRvd3NcU3lzdGVtMzIgKGNscyAmIGVjaG8uICYgZWNoby4gJiBlY2hvICAgICBFc3NlIHNjcmlwdCBkZXZlIHNlciBleGVjdXRhZG8gbm8gYW1iaWVudGUgZG8gV2luZG93cyBQRS4gJiBlY2hvLiAmIGVjaG8gICAgIFByZXNzaW9uZSBxdWFscXVlciB0ZWNsYSBwYXJhIHNhaXIuIC4gLiAmIHBhdXNlID5udWwgJiBFeGl0KQ0KDQpzZXQgImFyY2hwZT14ODYiDQppZiBleGlzdCAiWDpcV2luZG93c1xTeXNXT1c2NCIgc2V0ICJhcmNocGU9eDY0Ig0Kc2V0ICJfZGlzbT0lfmRwMHRvb2xzXCVhcmNocGUlXGRpc20uZXhlIg0Kc2V0ICJfc3JjPSV+ZHAwc291cmNlcyINCnNldCAiX2Rlc2M9SW1hZ2VtIGRlIFJlY3VwZXJhw6fDo28iDQpzZXQgL3AgX3Bkbm09PCIlfmRwMF9Qcm9kdWN0TmFtZS50eHQiDQpzZXQgL3AgX3Bjbm09PCIlfmRwMF9QQ05hbWUudHh0Ig0KDQpzZXRsb2NhbCBlbmFibGVkZWxheWVkZXhwYW5zaW9uDQoNCjpfc3RhcnQNCmNscw0KZWNobyBsaXN0IHZvbHxkaXNrcGFydA0Kc2V0IF93bHR0cj0NCnNldCAvcCBfd2x0dHI9RGlnaXRlIGEgbGV0cmEgZGEgcGFydGnDp8OjbyBkbyBXaW5kb3dzOiANCmlmIG5vdCBkZWZpbmVkIF93bHR0ciBjbHMgJiBnb3RvIF9zdGFydA0Kc2V0ICJfZXJyPSImZm9yIC9mICJkZWxpbXM9Q2NEZEVlRmZHZ0hoSWkiICUlaSBpbiAoIiVfd2x0dHIlIikgZG8gc2V0IF9lcnI9JSVpDQppZiBkZWZpbmVkIF9lcnIgY2xzICYgZ290byBfc3RhcnQNCmNscw0KY2FsbCA6X3N0cmNudA0KaWYgJV9jbnQlIEVxdSAxIGdvdG8gX2NhcHRpbWcNCmNscyAmIGdvdG8gX3N0YXJ0DQoNCjpfY2FwdGltZw0KY2xzDQppZiAlX3dsdHRyJS49PWMuIHNldCBfd2x0dHI9Qw0KaWYgJV93bHR0ciUuPT1kLiBzZXQgX3dsdHRyPUQNCmlmICVfd2x0dHIlLj09ZS4gc2V0IF93bHR0cj1FDQppZiAlX3dsdHRyJS49PWYuIHNldCBfd2x0dHI9Rg0KaWYgJV93bHR0ciUuPT1nLiBzZXQgX3dsdHRyPUcNCmlmICVfd2x0dHIlLj09aC4gc2V0IF93bHR0cj1IDQppZiAlX3dsdHRyJS49PWkuIHNldCBfd2x0dHI9SQ0KaWYgbm90IGV4aXN0ICVfd2x0dHIlOlxXaW5kb3dzXFN5c3RlbTMyIChjbHMgJiBlY2hvLiAmIGVjaG8uICYgZWNobyAgICAgIiVfd2x0dHIlIiBuw6NvIMOpIGEgbGV0cmEgZGEgcGFydGnDp8OjbyBkbyBXaW5kb3dzLiAmIGVjaG8uICYgZWNobyAgICAgIFByZXNzaW9uZSBxdWFscXVlciB0ZWNsYSBwYXJhIHRlbnRhciBub3ZhbWVudGUuIC4gLiAmIHBhdXNlID5udWwgJiBnb3RvIF9zdGFydCkNCmNscw0KaWYgZXhpc3QgIiVfc3JjJVxpbnN0YWxsLmVzZCIgZGVsIC9xIC9mICIlX3NyYyVcaW5zdGFsbC5lc2QiDQppZiBleGlzdCAiJV9zcmMlXGluc3RhbGwud2ltIiBkZWwgL3EgL2YgIiVfc3JjJVxpbnN0YWxsLndpbSINCmlmIGV4aXN0ICIlX3NyYyVcKi5zd20iIGRlbCAvcSAvZiAiJV9zcmMlXCouc3dtIg0KJV9kaXNtJSAvQ2FwdHVyZS1JbWFnZSAvSW1hZ2VGaWxlOiIlX3NyYyVcX2luc3RhbGwud2ltIiAvQ2FwdHVyZURpcjolX3dsdHRyJTpcIC9OYW1lOiIlX3Bkbm0lIiAvRGVzY3JpcHRpb246IiVfZGVzYyUgLSAlX3Bjbm0lIiAvQ29tcHJlc3M6bWF4DQpyZW4gIiVfc3JjJVxfaW5zdGFsbC53aW0iIGluc3RhbGwud2ltDQplY2hvLg0KZWNoby4NCmVjaG8gUHJlc3Npb25lIHF1YWxxdWVyIHRlY2xhIHBhcmEgc2Fpci4gLiAuDQpwYXVzZSA+bnVsDQpFeGl0DQoNCjpfc3RyY250DQppZiBub3QgZGVmaW5lZCBfY250IHNldCAvYSAiX2NudD0wIg0KaWYgbm90ICIhX3dsdHRyOn4lX2NudCUsMSEiPT0iIiBzZXQgL2EgIl9jbnQrPTEiJmdvdG8gX3N0cmNudA0KZ290byA6ZW9m"
goto :eof

:_rmepart
cls
title Remover partição e entradas de boot
if exist "%temp%\_guid.txt" del /q /f "%temp%\_guid.txt"
if exist "%temp%\_desc.txt" del /q /f "%temp%\_desc.txt"
if exist "%temp%\_gdesc.txt" del /q /f "%temp%\_gdesc.txt"
set "wpptls="
for %%a in (D E F G H I J K L M N O P Q R S T U V W X Y Z) do if exist %%a:\sources\*.wim set "wpptls=%%a:"
if "%wpptls%"=="" (cls & mode con: cols=82 lines=10 & echo. & echo. & echo     Não existem partições com arquivos de instalação do Windows & echo. & echo     ou utilitários de boot WinPE. & echo. & echo. & echo     Pressione qualquer tecla para remover entradas de boot correspondentes. . .& pause >nul & goto _lbGUID)
for /f "tokens=2 delims==" %%i in ('wmic logicaldisk where "DeviceID="%wpptls%"" Assoc:list /AssocClass:Win32_LogicalDiskToPartition /ResultRole:Antecedent ^| Find "DiskIndex="') do set _nUnd=%%i
for /f "delims==" %%z in ('set Disk 2^>nul') do set "%%z="
for /f delims^= %%v in ('wmic diskdrive Assoc /AssocClass:Win32_DiskDriveToDiskPartition 2^>nul^|find /i "Disk #%_nUnd%"') do (
  for /f tokens^=2delims^=^" %%w in ("%%v") do (
    for /f tokens^=2^,4delims^=^" %%x in ('wmic path Win32_LogicalDiskToPartition 2^>nul^|find "%%w" 2^>nul') do (
      for /f "tokens=2delims=#," %%z in ("%%x") do (
        if defined Disk%%z (
          call set "Disk%%z=%%Disk%%z%% %%y"
          ) else (
            set "Disk%%z=%%y"
        )
      )
    )
  )
)
for /f "tokens=2 delims==" %%z in ('set Disk 2^>nul') do set "lttrs=%%z"
set _lttr=%lttrs: =%
set _lttrs=%_lttr::=%
set "espart="
call :_strc
if %cnt% Equ 1 (cls & mode con: cols=82 lines=12 & echo. & echo. & echo     A unidade de disco '%_nUnd%' contém uma única partição e não & echo. & echo     pode ser apagada neste processo. Veja se não é o seu & echo. & echo     pen drive com o Windows que está conectado^^! & echo. & echo. & echo     Pressione qualquer tecla para remover entradas de boot correspondentes. . . & pause >nul & goto _lbGUID)

:_selEstpart
cls & mode con: cols=72 lines=11 & echo. & echo. & echo     A partição '%wpptls%' que será apagada faz parte & echo. & echo     da unidade de disco '%_nUnd%' que contém as partições & echo. & echo     '%lttrs%'. Selecione a partição adjacente à partição  & echo. & echo     '%wpptls%' para estendê-la. . .
set "twindow="(new-object -COM 'Shell.Application').BrowseforFolder(0,'ATENÇÃO NÃO É PARA SELECIONAR A PARTIÇÃO %wpptls%. SELECIONE A PARTIÇÃO ADJACENTE À PARTIÇÃO %wpptls% PARA ESTENDÊ-LA.',0x200,0).self.path""
chcp 850 >nul
for /f "usebackq delims=\" %%p in (`powershell %twindow%`) do set espart=%%p
chcp 65001 >nul
if "%espart%"=="" goto _lbGUID
if "%espart%"=="%wpptls%" set "espart=" & goto _selEstpart
cls
mode con: cols=70 lines=10
title Remover partição e entradas de boot
echo.
echo     ╔═══════════════════════════════════════════════════════════╗
echo      ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░
echo      ░ APAGAR PARTIÇÃO................................: [ %wpptls% ] ░
echo      ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░
echo      ░ ESTENDER PARTIÇÃO..............................: [ %espart% ] ░
echo      ░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░
echo     ╚═══════════════════════════════════════════════════════════╝
choice /n /c SN /m "-   > Confirma? (S ou N): "
if errorlevel 2 goto _lbGUID
cls
mode con: cols=72 lines=5
(echo select volume %wpptls%&&echo delete volume noerr&&echo Exit)>"%temp%\_dlvol.txt"
cls & echo. & echo. & echo     Deletando a partição %wpptls%. . .
echo.
chcp 850 >nul
diskpart /s "%temp%\_dlvol.txt" >nul&del /q /f "%temp%\_dlvol.txt"
timeout /t 3 /nobreak >nul
cls
chcp 65001 >nul
(echo select volume %espart%&&echo extend noerr&&echo Exit)>"%temp%\_edvol.txt"
cls & echo. & echo. & echo     Estendendo a partição %espart%. . .
echo.
chcp 850 >nul
diskpart /s "%temp%\_edvol.txt" >nul&del /q /f "%temp%\_edvol.txt"
timeout /t 3 /nobreak >nul

:_lbGUID
cls
mode con: cols=65 lines=5
title Remover entradas de boot
chcp 65001 >nul
set "gdesc=identificador"
(bcdedit /enum Osloader /v | find /i "%gdesc%")>"%temp%\_gdesc.txt"
set /p _gdesc=<"%temp%\_gdesc.txt"
if "%_gdesc%"=="" set "gdesc=identifier"
for /f "tokens=1,2 delims= " %%a in ('bcdedit /enum Osloader /v ^| find /i "%gdesc%"') do (
  set "_guid=%%b" (
    >>"%temp%\_guid.txt" echo !_guid!
  )
)
for /f "tokens=* delims=" %%a in ('bcdedit /enum Osloader /v ^| find /i "description"') do (
  set "_desc=%%a" (
    set "_desc=!_desc:~24!"
    >>"%temp%\_desc.txt" echo !_desc!
  )
)
set cdesc=0
for /f "tokens=*" %%# in ('type "%temp%\_desc.txt"') do (
  set /a cdesc=cdesc+1
  set desc[!cdesc!]=%%#
)
set cguid=0
for /f %%# in ('type "%temp%\_guid.txt"') do (
  set /a cguid=cguid+1
  set guid[!cguid!]=%%#
)
mode con: cols=100 lines=5
set cntdel=0
for /l %%i in (1,1,!cdesc!) do (
  if "!desc[%%i]!"=="WinPE Utilitários" (
    bcdedit /delete !guid[%%i]!
	cls & echo. & echo. & echo     Entrada "WinPE Utilitários" com GUID !guid[%%i]! foi apagada.
    set /a cntdel+=1
    timeout /t 3 /nobreak >nul
  )
  if "!desc[%%i]!"=="Instalação do Windows" (
    bcdedit /delete !guid[%%i]!
    cls & echo. & echo. & echo     Entrada "Instalação do Windows" com GUID !guid[%%i]! foi apagada.
    set /a cntdel+=1
    timeout /t 3 /nobreak >nul
  )
  if "!desc[%%i]!"=="Imagem de Recuperação" (
    bcdedit /delete !guid[%%i]!
    cls & echo. & echo. & echo     Entrada "Imagem de Recuperação" com GUID !guid[%%i]! foi apagada.
    set /a cntdel+=1
    timeout /t 3 /nobreak >nul
  )
  if "!desc[%%i]!"=="WinPE e Recuperação" (
    bcdedit /delete !guid[%%i]!
    cls & echo. & echo. & echo     Entrada "WinPE e Recuperação" com GUID !guid[%%i]! foi apagada.
    set /a cntdel+=1
    timeout /t 3 /nobreak >nul
  )
)
cls
mode con: cols=65 lines=5
if !cntdel! Equ 0 (echo. & echo. & echo     Não foram encontradas entradas de boot para deletar.)
if !cntdel! Equ 1 (echo. & echo. & echo     Foi removida !cntdel! entrada de boot.)
if !cntdel! GEQ 2 (echo. & echo. & echo     Foram removidas !cntdel! entradas de boot.)
timeout /t 3 /nobreak >nul
del /q /f "%temp%\_guid.txt" >nul 2>&1
del /q /f "%temp%\_desc.txt" >nul 2>&1
del /q /f "%temp%\_gdesc.txt" >nul 2>&1
start "" "https://cutt.ly/Swh2uy0b"
start "" "https://www.youtube.com/@xerifetech?sub_confirmation=1"
explorer shell:mycomputerfolder
Exit

:_strc
if not defined cnt set /a "cnt=0"
if not "!_lttrs:~%cnt%,1!"=="" set /a "cnt+=1"&goto _strc
goto :eof

:_uptewin
cls
mode con: cols=70 lines=5
title Atualizar o Windows instalado
If %winbld% LSS 9600 (cls & mode con: cols=70 lines=7 & echo. & echo. & echo     Essa opção não é compatível com o sistema instalado. & echo. & echo     Pressione qualquer tecla para voltar ao menu inicial. . . & pause >nul & goto _Mmenu)
set "ext="
set "ISrcImg="
set "FileName="
set "FpISrcImg="
echo.
echo.
echo     Selecione um arquivo de imagem ISO. . .
set "dlgtitle="Selecione um arquivo de imagem ISO do Windows""
chcp 850 >nul
for /f "delims=" %%a in ('powershell -ExecutionPolicy ByPass -Command "& {Add-Type -AssemblyName System.Windows.Forms | Out-Null;$dlg = New-Object System.Windows.Forms.OpenFileDialog;$dlg.Title = '%dlgtitle%';$dlg.InitialDirectory = 'shell:mycomputerfolder';$dlg.Filter = 'Windows|*.iso';if($dlg.ShowDialog() -eq 'OK'){return $dlg.Filename}}"') do set ISrcImg=%%a
chcp 65001 >nul
if "%ISrcImg%" equ "" (cls & mode con: cols=70 lines=7 & echo. & echo. & echo     Nenhuma imagem "*.iso" foi selecionada. & echo. & echo     Pressione qualquer tecla para voltar ao menu inicial. . . & pause >nul & goto _Mmenu)
for %%d in ("%ISrcImg%") do set "FileName=%%~nd"
for %%d in ("%ISrcImg%") do set "ext=%%~xd"
If %ext%.==.ISO. set ext=.iso
call :FtGetPNFImg "%ISrcImg%"
set "ISrcImg=%FpISrcImg%%FileName%.iso" & goto GetImgIISO

:FtGetPNFImg
set "FpISrcImg=%~dp1"
goto :eof

:GetImgIISO
call :ImageISOInfo
If "%ISrcImg%"=="" (cls & echo. & echo. & echo     A ISO montada não contém uma imagem de instalação do Windows^^! & chcp 850 >nul & powershell -Command "Dismount-DiskImage -ImagePath '%ISOPath%' | Out-Null" & timeout /t 5 /nobreak >nul & start "" "https://cutt.ly/Swh2uy0b" & start "" "https://www.youtube.com/@xerifetech?sub_confirmation=1" & Exit)
cls
chcp 850 >nul
for /f "tokens=2 delims=: " %%i in ('dism /English /Get-ImageInfo /ImageFile:"%ISrcImg%" ^| find /i "Index"') do set _neds=%%i
for /l %%i in (1, 1, %_neds%) do call :IndexCount %%i

goto IndexInfo

:IndexCount
set /a count+=1
for /f "tokens=1* delims=: " %%a in ('dism /English /Get-ImageInfo /ImageFile:"%ISrcImg%" /index:%1 ^| find /i "Name"') do set name%count%=%%b
for /f "tokens=5 delims=. " %%a in ('dism /English /Get-ImageInfo /ImageFile:"%ISrcImg%" /index:%1 ^| find /i "Version"') do set vernum%count%=%%a
for /f "tokens=3 delims=: " %%a in ('dism /English /Get-ImageInfo /ImageFile:"%ISrcImg%" /index:%1 ^| find /i "ServicePack Build"') do set build%count%=%%a
for /f "tokens=2 delims=: " %%a in ('dism /English /Get-ImageInfo /ImageFile:"%ISrcImg%" /index:%1 ^| find /i "Architecture"') do set imgarch%count%=%%a
for /f "tokens=1 delims=	 " %%l in ('dism /English /Get-ImageInfo /ImageFile:"%ISrcImg%" /index:%1 ^| find /i "(Default)"') do set winlang%count%=%%l
goto :eof

:IndexInfo
cls
chcp 65001 >nul
if %_neds% equ 1 set "_iidx=1"&goto _sldImg
set "_nlines="
set /a _nlines=%_neds%+10
mode con: cols=75 lines=%_nlines%
echo     ═══════════════════════════════════════════════════════════════════
echo     ░░░ ESCOLHA A EDIÇÃO DO WINDOWS PARA ATUALIZAR O SISTEMA ATUAL  ░░░
echo     ═══════════════════════════════════════════════════════════════════
echo.
for /l %%i in (1, 1, %_neds%) do (
  echo      %%i. !name%%i! ^(!vernum%%i!.!build%%i!_!imgarch%%i!_!winlang%%i!^)
)
echo.
echo     ═══════════════════════════════════════════════════════════════════
echo.
set /P _iidx=^>   Digite uma opção e pressione 'enter': 
if not defined _iidx (cls & mode con: cols=75 lines=7 & echo. & echo. & echo     Foi pressionado "enter" sem selecionar uma edição do Windows. & echo. & echo     Pressione qualquer tecla para tentar novamente. & pause >nul & goto IndexInfo)
set "_err="&for /f "delims=0123456789" %%i in ("%_iidx%") do set _err=%%i
if defined _err (cls & mode con: cols=75 lines=7 & echo. & echo. & echo     Você digitou "%_iidx%". Esse caractere é inválido. & echo. & echo     Pressione qualquer tecla para tentar novamente. . . & pause >nul & goto IndexInfo)
if %_iidx% equ 0 (cls & mode con: cols=80 lines=7 & echo. & echo. & echo     Você digitou "%_iidx%". Não existe uma edição do Windows com esse número. & echo. & echo     Pressione qualquer tecla para tentar novamente. . . & pause >nul & goto IndexInfo)
if %_iidx% Leq %_neds% goto _sldImg
if not %_iidx% Leq %_neds% (cls & mode con: cols=85 lines=7 & echo. & echo. & echo     Você digitou "%_iidx%". A imagem não possui essa quantidade de edições indexadas. & echo. & echo     Pressione qualquer tecla para tentar novamente. . . & pause >nul & goto IndexInfo)

:_sldImg
cls
mode con: cols=75 lines=5
title Atualização do Windows
chcp 850 >nul
set "_ednm="
set "_iarch="
set "_iedID="
set "_iwbld="
set "bpparmr="
set "_WinEdition="
set "WinEdition="
for /f "tokens=1* delims=: " %%a in ('dism /English /Get-ImageInfo /ImageFile:"%ISrcImg%" /index:%_iidx% ^| find /i "Name"') do set _ednm=%%b
for /f "tokens=2 delims=: " %%a in ('dism /English /Get-ImageInfo /ImageFile:"%ISrcImg%" /index:%_iidx% ^| find /i "Architecture"') do set _iarch=%%a
for /f "tokens=2 delims=: " %%a in ('dism /English /Get-ImageInfo /ImageFile:"%ISrcImg%" /index:%_iidx% ^| find /i "Edition"') do set _iedID=%%a
for /f "tokens=5 delims=. " %%a in ('dism /English /Get-ImageInfo /ImageFile:"%ISrcImg%" /index:%_iidx% ^| find /i "Version"') do set _iwbld=%%a
chcp 65001 >nul
if %winbld% LSS 17134 (
 for /f "tokens=2* delims==" %%i in ('wmic os get Caption /value 2^>nul ^| find /i "Caption"') do set "_WinEdition=%%i"
)
if %winbld% GEQ 17134 (
  chcp 850 >nul
  for /f "tokens=*" %%i in ('powershell -Command "(Get-WmiObject -Class Win32_OperatingSystem).Caption"') do set "_WinEdition=%%i"
  chcp 65001 >nul
)
set "_WinEdition=%_WinEdition:Microsoft =%"
for /f "skip=2 tokens=1,2*" %%i in ('reg query "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion" /v ProductName 2^>nul') do if /i "%%i"=="ProductName" set "WinEdition=%%k"
if /i not "%_WinEdition%"=="%WinEdition%" reg add "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion" /v ProductName /d "%_WinEdition%" /f >nul
for /f "skip=2 tokens=1,2*" %%i in ('reg query "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion" /v EditionID 2^>nul') do if /i "%%i"=="EditionID" set "EditionID=%%k"
if %_bpsetup%.==1. set "bpparmr=/product server"
if %_iwbld% LSS 22000 (
  if %_iarch%.==%sysarch%. (
    cls & echo. & echo. & echo     Atualizando para %_ednm% ^(%_iarch%^). . .
    reg add "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion" /v EditionID /t REG_SZ /d "%_iedID%" /f >nul
	start /wait "" "%_setup%"
	cls & mode con: cols=60 lines=5 & echo. & echo. & echo     Saindo. . .
	reg add "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion" /v EditionID /t REG_SZ /d "%EditionID%" /f >nul
	timeout /t 3 /nobreak >nul
	chcp 850 >nul
	powershell -Command "Dismount-DiskImage -ImagePath '%ISOPath%' | Out-Null"
	chcp 65001 >nul
	if exist "C:\$WINDOWS.~BT" (attrib -h -s -r "C:\$WINDOWS.~BT" >nul 2>&1 & rd /s /q "C:\$WINDOWS.~BT" >nul 2>&1)
	start "" "https://cutt.ly/Swh2uy0b"
	start "" "https://www.youtube.com/@xerifetech?sub_confirmation=1"
	Exit
  )
)
if %_iwbld% GEQ 22000 (
  if %_iarch%.==%sysarch%. (
    cls & echo. & echo. & echo     Atualizando para %_ednm% ^(%_iarch%^). . .
    reg add "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion" /v EditionID /t REG_SZ /d "%_iedID%" /f >nul
	start /wait "" "%_setup%" %bpparmr%
	cls & mode con: cols=60 lines=5 & echo. & echo. & echo     Saindo. . .
	reg add "HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion" /v EditionID /t REG_SZ /d "%EditionID%" /f >nul
	timeout /t 3 /nobreak >nul
	chcp 850 >nul
	powershell -Command "Dismount-DiskImage -ImagePath '%ISOPath%' | Out-Null"
	chcp 65001 >nul
	if exist "C:\$WINDOWS.~BT" (attrib -h -s -r "C:\$WINDOWS.~BT" >nul 2>&1 & rd /s /q "C:\$WINDOWS.~BT" >nul 2>&1)
	start "" "https://cutt.ly/Swh2uy0b"
	start "" "https://www.youtube.com/@xerifetech?sub_confirmation=1"
	Exit
  )
)
cls & echo. & echo. & echo     Atualize o sistema com um Windows de mesma arquitetura^^! & timeout /t 5 /nobreak >nul
chcp 850 >nul
powershell -Command "Dismount-DiskImage -ImagePath '%ISOPath%' | Out-Null"
chcp 65001 >nul
start "" "https://cutt.ly/Swh2uy0b"
start "" "https://www.youtube.com/@xerifetech?sub_confirmation=1"
Exit

:ImageISOInfo
cls & mode con: cols=100 lines=5 & echo. & echo. & echo     Montando "%FileName%%ext%". . .
set "ISOPath=%ISrcImg%"
chcp 850 >nul
for /f %%l in ('powershell -Command "(Mount-DiskImage -ImagePath '%ISOPath%' -PassThru | Get-Volume).DriveLetter"') do set DrvLttr=%%l:
chcp 65001 >nul
timeout /t 3 /nobreak >nul
set "ISrcImg="
set "FileName="
set "FpISrcImg="
set "_setup=%DrvLttr%\sources\setup.exe"
If exist "%DrvLttr%\setup.exe" set "_setup=%DrvLttr%\setup.exe"
If exist "%DrvLttr%\sources\install.wim" set "ISrcImg=%DrvLttr%\sources\install.wim"
If exist "%DrvLttr%\sources\install.esd" set "ISrcImg=%DrvLttr%\sources\install.esd"
If exist "%DrvLttr%\sources\install.swm" set "ISrcImg=%DrvLttr%\sources\install.swm"
If "%ISrcImg%"=="" goto :eof
for %%f in ("%ISrcImg%") do set "FileName=%%~nf"
call :GetPNFImgISO "%ISrcImg%"
if "%ISrcImg%" equ "%FpISrcImg%%FileName%.esd" set "ISrcImg=%FpISrcImg%%FileName%.esd" & goto :eof
if "%ISrcImg%" equ "%FpISrcImg%%FileName%.swm" set "ISrcImg=%FpISrcImg%%FileName%.swm" & goto :eof
if "%ISrcImg%" equ "%FpISrcImg%%FileName%.wim" set "ISrcImg=%FpISrcImg%%FileName%.wim" & goto :eof

:GetPNFImgISO
set "FpISrcImg=%~dp1"
goto :eof

:ElevAdmin
echo Set UAC = CreateObject^("Shell.Application"^) >"%temp%\getadmin.vbs"
echo UAC.ShellExecute "%~s0", "", "", "runas", 1 >>"%temp%\getadmin.vbs"
"%temp%\getadmin.vbs"
goto Admin & Exit /b

:Admin
if exist "%temp%\getadmin.vbs" (del "%temp%\getadmin.vbs") & pushd "%CD%" & cd /d "%~dp0" & Exit

:VerPrevAdmin
fsutil dirty query %systemdrive% >nul
if not errorLevel 1 (
 mode con: cols=60 lines=5
 ) else (
   goto ElevAdmin & echo. & set "Admin=ops"
)
goto :eof
