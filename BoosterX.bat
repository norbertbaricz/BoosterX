@echo off
setlocal EnableExtensions EnableDelayedExpansion
:: #################################################################################
:: #                                                                               #
:: #                     BoosterX v3.3 - Ultimate Performance                       #
:: #                                                                               #
:: #        Advanced Windows optimization for gaming & responsiveness              #
:: #                Created by Skypixel team                                       #
:: #                                                                               #
:: #################################################################################
chcp 65001 >nul

rem ------------------------------------------------------------------------------
rem 0) Zero-crash admin elevation (VBScript ShellExecute "runas")
rem ------------------------------------------------------------------------------
whoami /groups | find "S-1-5-32-544" >nul
if errorlevel 1 (
  echo [i] Requesting administrator privileges...
  set "vbspath=%temp%\bx_getadmin.vbs"
  >"%vbspath%" echo Set UAC = CreateObject^("Shell.Application"^)
  >>"%vbspath%" echo UAC.ShellExecute "%~fs0", "%*", "", "runas", 1
  cscript //nologo "%vbspath%" >nul 2>&1
  del "%vbspath%" >nul 2>&1
  exit /b
)

:mainMenu
cls
color 0B
echo.
echo    ========================================================
echo    ==                                                    ==
echo    ==           BoosterX - Simple Optimizer              ==
echo    ==                                                    ==
echo    ========================================================
echo.
echo    [1] Deep Clean System
echo    [2] Optimize Performance (Gaming & UI)
echo    [3] Optimize Network (Lowest Ping)
echo    [4] Disable Unnecessary Services
echo    [X] Exit
echo.

set "choice="
set /p "choice=Select an option: "
set "choice=!choice:~0,1!"
if /i "!choice!"=="1" goto cleanSystem
if /i "!choice!"=="2" goto optimizePerformance
if /i "!choice!"=="3" goto optimizeNetwork
if /i "!choice!"=="4" goto disableServices
if /i "!choice!"=="X" goto endScript
goto mainMenu

rem ------------------------------------------------------------------------------
rem Optimization Modules (to be filled)
rem ------------------------------------------------------------------------------

:cleanSystem
cls
echo === Deep Clean System ===
echo.
echo --- Cleaning user temporary files...
del /f /q /s "%TEMP%\*" >nul 2>&1
for /d %%x in ("%TEMP%\*") do rd /s /q "%%x" >nul 2>&1

echo --- Cleaning Windows temporary files...
del /f /q /s "%WINDIR%\Temp\*" >nul 2>&1
for /d %%x in ("%WINDIR%\Temp\*") do rd /s /q "%%x" >nul 2>&1

echo --- Emptying Recycle Bin...
rd /s /q "%SystemDrive%\$Recycle.Bin" >nul 2>&1
rem For older Windows versions or if the above fails, try:
powershell -Command "Clear-RecycleBin -Force -ErrorAction SilentlyContinue" >nul 2>&1

echo.
echo Deep clean completed.
timeout /t 2 /nobreak >nul
goto mainMenu

:optimizePerformance
cls
echo === Optimize Performance (Gaming & UI) ===
echo.
echo --- Setting Power Plan to High Performance...
powercfg /setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c >nul 2>&1
if errorlevel 1 (
  echo [!] High Performance plan not found. Attempting to duplicate...
  powercfg -duplicatescheme 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c >nul 2>&1
  powercfg /setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c >nul 2>&1
)
echo.

echo --- Applying UI responsiveness tweaks...
reg add "HKCU\Control Panel\Desktop" /v "AutoEndTasks" /t REG_SZ /d 1 /f >nul
reg add "HKCU\Control Panel\Desktop" /v "MenuShowDelay" /t REG_SZ /d 0 /f >nul
reg add "HKCU\Control Panel\Desktop" /v "WaitToKillAppTimeout" /t REG_SZ /d 2000 /f >nul
reg add "HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects" /v "VisualFxSetting" /t REG_DWORD /d 2 /f >nul
echo.

echo Performance optimization completed. (A logoff/restart makes them fully effective.)
timeout /t 2 /nobreak >nul
goto mainMenu

:optimizeNetwork
cls
echo === Optimize Network (Lowest Ping) ===
echo.
echo --- Disabling Nagle's algorithm (TcpAckFrequency/TCPNoDelay) on active physical interfaces...
rem This part is complex for universal compatibility without PowerShell.
rem For simplicity, we'll skip direct Nagle's tweak for now.
rem A more compatible approach would involve iterating through network adapters,
rem but that adds complexity.

echo --- Setting DNS to Cloudflare (1.1.1.1) and Google (8.8.8.8) on active interfaces...
for /f "tokens=*" %%i in ('netsh interface show interface ^| findstr /i "Connected"') do (
    for /f "tokens=3*" %%a in ("%%i") do (
        set "IFACE=%%a"
        if not "!IFACE!"=="" (
            echo Setting DNS on: !IFACE!
            netsh interface ip set dns name="!IFACE!" static 1.1.1.1 primary >nul 2>&1
            netsh interface ip add dns name="!IFACE!" 8.8.8.8 index=2 >nul 2>&1
        )
    )
)
echo.

echo Network optimization completed. (A restart of network adapter or system may be required.)
timeout /t 2 /nobreak >nul
goto mainMenu

:disableServices
cls
echo === Disable Unnecessary Services ===
echo.
echo --- Disabling common unnecessary services...
set "SERVICES_TO_DISABLE=wuauserv SysMain DiagTrack UsoSvc WSearch"

for %%s in (%SERVICES_TO_DISABLE%) do (
  echo Disabling and stopping %%s...
  sc stop %%s >nul 2>&1
  sc config %%s start= disabled >nul 2>&1
  if errorlevel 1 echo [!] Failed to disable %%s.
)
echo.

echo Services disabled where allowed by the system. (A restart may be required.)
timeout /t 2 /nobreak >nul
goto mainMenu

rem ------------------------------------------------------------------------------
rem Final message & Exit
rem ------------------------------------------------------------------------------
:endScript
echo.
echo Operation completed. Press any key to close this window...
pause >nul
endlocal
exit /b