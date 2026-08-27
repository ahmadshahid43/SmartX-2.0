@echo off
setlocal

set "ROOT=%~dp0"
if "%ROOT:~-1%"=="\" set "ROOT=%ROOT:~0,-1%"

set "DOTNET_CLI_HOME=%ROOT%\.dotnet"
set "NUGET_PACKAGES=%ROOT%\.nuget\packages"
set "APPDATA=%ROOT%\.appdata"
set "LOCALAPPDATA=%ROOT%\.localappdata"

if not exist "%APPDATA%\NuGet" mkdir "%APPDATA%\NuGet"
copy /Y "%ROOT%\NuGet.Config" "%APPDATA%\NuGet\NuGet.Config" >nul

echo Starting OmniBusiness desktop shell...
dotnet run --project "%ROOT%\desktop\OmniBusiness.Desktop"

endlocal
