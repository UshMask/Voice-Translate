@echo off
if not defined APPDATA set "APPDATA=%USERPROFILE%\AppData\Roaming"
if not defined PROGRAMFILES set "PROGRAMFILES=C:\Program Files"
if not defined PROGRAMFILES(X86) set "PROGRAMFILES(X86)=C:\Program Files (x86)"
dotnet build "%~dp0src\Voica\Voica.csproj" -c Release
