@echo off
REM ============================================================================
REM  Quake III InstaGib Launcher - publish.bat
REM  Pubblica una versione Windows x64 self-contained (non serve .NET installato
REM  sul PC di destinazione) e crea uno ZIP pronto da distribuire in dist\.
REM ============================================================================
setlocal

cd /d "%~dp0"

set "PROJECT=src\Quake3InstaGibLauncher\Quake3InstaGibLauncher.csproj"
set "OUTDIR=dist\Quake3InstaGibLauncher-win-x64"
set "ZIPNAME=dist\Quake3InstaGibLauncher-win-x64.zip"

echo.
echo === Pulizia output precedente ===
if exist "dist" rmdir /s /q "dist"
mkdir "dist"

echo.
echo === Pubblicazione self-contained win-x64 ===
dotnet publish "%PROJECT%" ^
    -c Release ^
    -r win-x64 ^
    --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -o "%OUTDIR%"
if errorlevel 1 goto :error

echo.
echo === Copia README ===
copy /y "README.md" "%OUTDIR%\README.md" >nul

echo.
echo === Creazione archivio ZIP ===
powershell -NoProfile -Command "Compress-Archive -Path '%OUTDIR%\*' -DestinationPath '%ZIPNAME%' -Force"
if errorlevel 1 goto :error

echo.
echo === Pubblicazione completata ===
echo Cartella pubblicata: %OUTDIR%
echo Archivio ZIP pronto: %ZIPNAME%
goto :eof

:error
echo.
echo === ERRORE: la pubblicazione non e' riuscita. Controlla i messaggi sopra. ===
exit /b 1
