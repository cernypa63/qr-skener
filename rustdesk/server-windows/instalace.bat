@echo off
rem Instalace RustDesk serveru (hbbs + hbbr) jako sluzby Windows. Spustit jako spravce.
rem Ve stejne slozce musi byt hbbs.exe, hbbr.exe (z rustdesk-server-windows-x86_64-unsigned.zip)
rem a nssm.exe (https://nssm.cc/download, win64\nssm.exe).
setlocal
set ADR=C:\RustDeskServer
cd /d "%~dp0"
for %%f in (hbbs.exe hbbr.exe nssm.exe) do if not exist "%%f" (echo Chybi %%f & pause & exit /b 2)
if not exist "%ADR%" mkdir "%ADR%"
for %%s in (hbbs hbbr) do (
  if exist "%ADR%\nssm.exe" "%ADR%\nssm.exe" stop RustDesk_%%s >nul 2>&1
  if exist "%ADR%\nssm.exe" "%ADR%\nssm.exe" remove RustDesk_%%s confirm >nul 2>&1
)
rem Kdyz se skript spousti primo z %ADR%, soubory uz jsou na miste a nekopiruji se.
if /i not "%~dp0"=="%ADR%\" (
  for %%f in (hbbs.exe hbbr.exe nssm.exe) do (
    copy /y %%f "%ADR%\" >nul || (echo Nelze prepsat %ADR%\%%f & pause & exit /b 3)
  )
)
rem Sluzby bezi pod omezenym uctem LOCAL SERVICE, ktery smi zapisovat jen do slozky serveru.
icacls "%ADR%" /grant "*S-1-5-19:(OI)(CI)M" >nul
cd /d "%ADR%"
for %%s in (hbbs hbbr) do (
  nssm install RustDesk_%%s "%ADR%\%%s.exe"
  nssm set RustDesk_%%s AppDirectory "%ADR%"
  nssm set RustDesk_%%s ObjectName "NT AUTHORITY\LocalService" ""
  nssm set RustDesk_%%s Start SERVICE_AUTO_START
  nssm start RustDesk_%%s
)
netsh advfirewall firewall delete rule name="RustDesk Server TCP" >nul 2>&1
netsh advfirewall firewall delete rule name="RustDesk Server UDP" >nul 2>&1
netsh advfirewall firewall add rule name="RustDesk Server TCP" dir=in action=allow protocol=TCP localport=21115-21117
netsh advfirewall firewall add rule name="RustDesk Server UDP" dir=in action=allow protocol=UDP localport=21116
timeout /t 5 >nul
echo.
echo Klic serveru (key) pro klienty:
type "%ADR%\id_ed25519.pub"
echo.
echo Na routeru presmerujte na tento server porty TCP 21115-21117 a UDP 21116.
pause
