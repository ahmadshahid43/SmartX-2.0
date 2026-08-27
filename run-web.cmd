@echo off
setlocal

set "ROOT=%~dp0"
if "%ROOT:~-1%"=="\" set "ROOT=%ROOT:~0,-1%"
set "WEBROOT=%ROOT%\web\omnibusiness-web"
set "NG_PERSISTENT_BUILD_CACHE=0"

powershell -NoProfile -Command "if (Get-NetTCPConnection -State Listen -LocalPort 4200 -ErrorAction SilentlyContinue | Select-Object -First 1) { exit 0 } else { exit 1 }" >nul 2>nul
if not errorlevel 1 (
    echo OmniBusiness web app is already running on http://localhost:4200
    goto :eof
)

cd /d "%WEBROOT%"

if not exist "%WEBROOT%\node_modules" (
    echo Installing web dependencies first...
    call npm.cmd ci
    if errorlevel 1 goto :fail
)

echo Starting OmniBusiness web app on http://localhost:4200
call npm.cmd start -- --prebundle=false
if errorlevel 1 goto :fail

goto :eof

:fail
echo.
echo Web app failed to start. If you saw a network error, try: npm.cmd ci
exit /b 1
