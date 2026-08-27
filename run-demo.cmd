@echo off
setlocal

set "ROOT=%~dp0"
if "%ROOT:~-1%"=="\" set "ROOT=%ROOT:~0,-1%"

netstat -ano | findstr ":5163" >nul
if errorlevel 1 (
    echo Opening API and web app in separate windows...
    start "OmniBusiness API" cmd /k ""%ROOT%\run-api.cmd""
) else (
    echo API is already running. Reusing http://localhost:5163/swagger
)

netstat -ano | findstr ":4200" >nul
if errorlevel 1 (
    start "OmniBusiness Web" cmd /k ""%ROOT%\run-web.cmd""
) else (
    echo Web app is already running. Reusing http://localhost:4200
)

echo.
echo API URL: http://localhost:5163/swagger
echo Web URL: http://localhost:4200
echo Login: admin@omnibusiness.local / Admin@123

endlocal
