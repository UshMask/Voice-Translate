@echo off
set "VOICETRANSLATE_TEST_DATA=%~dp0.test-data\VoiceTranslate"
dotnet "%~dp0src\Voica\bin\Release\net8.0-windows10.0.17763.0\Voica.dll" --test-all
