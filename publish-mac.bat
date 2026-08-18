@echo off
REM ============================================================================
REM  Quake III InstaGib Launcher - publish-mac.bat
REM
REM  *** PER DISTRIBUIRE L'APP A QUALCUNO, USA INVECE LA RELEASE UFFICIALE SU
REM  *** GITHUB (.github/workflows/release.yml, scheda Actions del repo): quella
REM  *** build viene compilata E FIRMATA su un Mac vero, chi la scarica fa solo
REM  *** doppio click (con al massimo un clic di conferma sicurezza standard),
REM  *** senza toccare il Terminale. Usa questo script SOLO per test rapidi in
REM  *** locale.
REM
REM  Pubblica le versioni macOS (Apple Silicon e Intel) self-contained a file
REM  singolo, le impacchetta in un vero bundle "Quake III InstaGib Launcher.app"
REM  (Contents/MacOS + Info.plist, come una normale app Mac) e crea due ZIP in
REM  dist\.
REM
REM  NOTA IMPORTANTE: questo script compila DA WINDOWS per macOS (funziona,
REM  NuGet scarica i runtime pack necessari), ma Windows non ha il concetto di
REM  "bit eseguibile" Unix, l'app non e' firmata/notarizzata Apple (nessun
REM  account sviluppatore a pagamento dietro questo progetto amatoriale
REM  gratuito) e su Apple Silicon manca anche la firma "ad-hoc" (il tool
REM  "codesign" esiste solo su macOS): il destinatario deve fare 3 comandi da
REM  Terminale una tantum al primo avvio. Nello ZIP trovi
REM  "Avvia su Mac (build locale da Windows) - LEGGIMI.txt" con le istruzioni
REM  pronte da fargli leggere.
REM ============================================================================
setlocal enabledelayedexpansion

cd /d "%~dp0"

set "PROJECT=src\Quake3InstaGibLauncher.Mac\Quake3InstaGibLauncher.Mac.csproj"
set "APP_NAME=Quake III InstaGib Launcher.app"
set "EXE_NAME=Quake3InstaGibLauncher.Mac"

echo.
echo === Lettura versione da csproj ===
set "VERSION=1.0.0"
for /f "delims=" %%V in ('powershell -NoProfile -Command "(([xml](Get-Content '%PROJECT%')).Project.PropertyGroup.Version | Select-Object -First 1)"') do set "VERSION=%%V"
echo Versione: %VERSION%

echo.
echo === Pulizia output precedente ===
if exist "dist" rmdir /s /q "dist"
mkdir "dist"

for %%R in (osx-arm64 osx-x64) do (
    set "RID=%%R"
    set "PUBLISHDIR=dist\publish-!RID!"
    set "STAGEDIR=dist\Quake3InstaGibLauncher-!RID!"
    set "APPDIR=!STAGEDIR!\%APP_NAME%"
    set "MACOSDIR=!APPDIR!\Contents\MacOS"
    set "ZIPNAME=dist\Quake3InstaGibLauncher-macOS-!RID!.zip"

    echo.
    echo === Pubblicazione self-contained !RID! ===
    dotnet publish "%PROJECT%" ^
        -c Release ^
        -r !RID! ^
        --self-contained true ^
        -p:PublishSingleFile=true ^
        -p:IncludeNativeLibrariesForSelfExtract=true ^
        -o "!PUBLISHDIR!"
    if errorlevel 1 goto :error

    echo === Assemblaggio bundle .app ===
    mkdir "!MACOSDIR!"
    xcopy /E /I /Y "!PUBLISHDIR!\*" "!MACOSDIR!\" >nul
    if errorlevel 1 goto :error

    powershell -NoProfile -Command "(Get-Content 'packaging\macos\Info.plist.template') -replace '__VERSION__', '%VERSION%' | Set-Content -Encoding UTF8 '!APPDIR!\Contents\Info.plist'"
    if errorlevel 1 goto :error

    echo === Copia README e istruzioni primo avvio - accanto all'app, non dentro ===
    copy /y "README.md" "!STAGEDIR!\README.md" >nul
    copy /y "packaging\macos\Avvia su Mac (build locale da Windows) - LEGGIMI.txt" "!STAGEDIR!\Avvia su Mac - LEGGIMI.txt" >nul

    echo === Creazione archivio ZIP ===
    powershell -NoProfile -Command "Compress-Archive -Path '!STAGEDIR!\*' -DestinationPath '!ZIPNAME!' -Force"
    if errorlevel 1 goto :error

    rmdir /s /q "!PUBLISHDIR!"
)

echo.
echo === Pubblicazione completata ===
echo Archivi ZIP pronti in dist\:
echo   - Quake3InstaGibLauncher-macOS-osx-arm64.zip  (Mac con chip Apple: M1/M2/M3/M4/M5...)
echo   - Quake3InstaGibLauncher-macOS-osx-x64.zip    (Mac Intel, piu' vecchi)
echo Ogni ZIP contiene "%APP_NAME%" (bundle .app, un'unica icona) + README + istruzioni.
echo.
echo PROMEMORIA: questa build NON e' firmata (compilata da Windows) - chi la riceve deve
echo seguire i 3 comandi da Terminale nel LEGGIMI. Per mandare qualcosa a qualcuno senza
echo fargli aprire il Terminale, usa invece la Release ufficiale su GitHub (build compilata
echo e firmata su un Mac vero da .github\workflows\release.yml, scheda Actions del repo).
goto :eof

:error
echo.
echo === ERRORE: la pubblicazione non e' riuscita. Controlla i messaggi sopra. ===
exit /b 1
