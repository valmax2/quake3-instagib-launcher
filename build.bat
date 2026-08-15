@echo off
REM ============================================================================
REM  Quake III InstaGib Launcher - build.bat
REM  Compila la soluzione in configurazione Release e segnala errori/avvisi.
REM  Richiede .NET 8 SDK (https://dotnet.microsoft.com/download/dotnet/8.0).
REM ============================================================================
setlocal

cd /d "%~dp0"

echo.
echo === Ripristino pacchetti NuGet ===
dotnet restore Quake3InstaGibLauncher.sln
if errorlevel 1 goto :error

echo.
echo === Compilazione in Release ===
dotnet build Quake3InstaGibLauncher.sln -c Release
if errorlevel 1 goto :error

echo.
echo === Build completata con successo ===
echo Eseguibile di debug/test: src\Quake3InstaGibLauncher\bin\Release\net8.0-windows\win-x64\Quake3InstaGibLauncher.exe
echo Per generare il pacchetto finale pronto da distribuire, esegui publish.bat
goto :eof

:error
echo.
echo === ERRORE: la build non e' riuscita. Controlla i messaggi sopra. ===
exit /b 1
