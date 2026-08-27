@echo off
setlocal

set "ROOT=%~dp0"
if "%ROOT:~-1%"=="\" set "ROOT=%ROOT:~0,-1%"
set "API_PROJECT=%ROOT%\src\OmniBusiness.Api"
set "API_BIN=%ROOT%\.artifacts\bin\OmniBusiness.Api\Debug\net10.0"
set "API_EXE=%API_BIN%\OmniBusiness.Api.exe"

powershell -NoProfile -Command "if (Get-NetTCPConnection -State Listen -LocalPort 5163 -ErrorAction SilentlyContinue | Select-Object -First 1) { exit 0 } else { exit 1 }" >nul 2>nul
if not errorlevel 1 (
    echo OmniBusiness API is already running on http://localhost:5163/swagger
    goto :eof
)

set "DOTNET_CLI_HOME=%ROOT%\.dotnet-home"
set "NUGET_PACKAGES=%ROOT%\.nuget\packages"
set "APPDATA=%ROOT%\.appdata"
set "LOCALAPPDATA=%ROOT%\.localappdata"
set "DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1"
set "DOTNET_CLI_TELEMETRY_OPTOUT=1"
set "DOTNET_NOLOGO=1"
set "DOTNET_GENERATE_ASPNET_CERTIFICATE=false"
set "DOTNET_ADD_GLOBAL_TOOLS_TO_PATH=0"
set "DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=1"
set "ASPNETCORE_URLS=http://localhost:5163"
set "ASPNETCORE_CONTENTROOT=%API_PROJECT%"

if not exist "%APPDATA%\NuGet" mkdir "%APPDATA%\NuGet"
if not exist "%DOTNET_CLI_HOME%" mkdir "%DOTNET_CLI_HOME%"
if not exist "%DOTNET_CLI_HOME%\.dotnet" mkdir "%DOTNET_CLI_HOME%\.dotnet"
if exist "%ROOT%\NuGet.Config" if not exist "%APPDATA%\NuGet\NuGet.Config" copy /Y "%ROOT%\NuGet.Config" "%APPDATA%\NuGet\NuGet.Config" >nul 2>nul

echo Starting OmniBusiness API on http://localhost:5163
dotnet run --project "%ROOT%\src\OmniBusiness.Api" --launch-profile http
if not errorlevel 1 goto :eof

if exist "%API_EXE%" (
    echo.
    echo dotnet run failed on this machine. Falling back to compiled API executable...
    pushd "%API_PROJECT%"
    "%API_EXE%"
    popd
)

endlocal
