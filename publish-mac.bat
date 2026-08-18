@echo off
REM ============================================================================
REM  Quake III InstaGib Launcher - publish-mac.bat
REM  Pubblica le versioni macOS (Apple Silicon e Intel) self-contained a file
REM  singolo (non serve .NET installato sul Mac di destinazione) e crea due
REM  ZIP pronti da distribuire in dist\.
REM
REM  NOTA IMPORTANTE: questo script compila DA WINDOWS per macOS (funziona,
REM  NuGet scarica i runtime pack necessari), ma Windows non ha il concetto di
REM  "bit eseguibile" Unix: quando il tuo amico scompatta lo ZIP sul Mac, il
REM  file potrebbe non risultare eseguibile subito. Nello ZIP trovi anche
REM  "Avvia su Mac - LEGGIMI.txt" con le 2 righe da incollare in Terminale la
REM  primissima volta (chmod +x e rimozione blocco Gatekeeper) - dopo quel
REM  passaggio una tantum, il doppio click funziona normalmente.
REM ============================================================================
setlocal enabledelayedexpansion

cd /d "%~dp0"

set "PROJECT=src\Quake3InstaGibLauncher.Mac\Quake3InstaGibLauncher.Mac.csproj"

echo.
echo === Pulizia output precedente ===
if exist "dist" rmdir /s /q "dist"
mkdir "dist"

for %%R in (osx-arm64 osx-x64) do (
    set "RID=%%R"
    set "OUTDIR=dist\Quake3InstaGibLauncher-!RID!"
    set "ZIPNAME=dist\Quake3InstaGibLauncher-macOS-!RID!.zip"

    echo.
    echo === Pubblicazione self-contained !RID! ===
    dotnet publish "%PROJECT%" ^
        -c Release ^
        -r !RID! ^
        --self-contained true ^
        -p:PublishSingleFile=true ^
        -p:IncludeNativeLibrariesForSelfExtract=true ^
        -o "!OUTDIR!"
    if errorlevel 1 goto :error

    echo === Copia README e istruzioni primo avvio ===
    copy /y "README.md" "!OUTDIR!\README.md" >nul
    copy /y "packaging\macos\Avvia su Mac - LEGGIMI.txt" "!OUTDIR!\Avvia su Mac - LEGGIMI.txt" >nul

    echo === Creazione archivio ZIP ===
    powershell -NoProfile -Command "Compress-Archive -Path '!OUTDIR!\*' -DestinationPath '!ZIPNAME!' -Force"
    if errorlevel 1 goto :error
)

echo.
echo === Pubblicazione completata ===
echo Archivi ZIP pronti in dist\:
echo   - Quake3InstaGibLauncher-macOS-osx-arm64.zip  (Mac con chip Apple: M1/M2/M3/M4...)
echo   - Quake3InstaGibLauncher-macOS-osx-x64.zip    (Mac Intel, piu' vecchi)
echo Manda al tuo amico SOLO lo ZIP giusto per il suo Mac (chip Apple = arm64, la stragrande
echo maggioranza dei Mac venduti dal 2020 in poi; Intel solo se piu' vecchio).
goto :eof

:error
echo.
echo === ERRORE: la pubblicazione non e' riuscita. Controlla i messaggi sopra. ===
exit /b 1
