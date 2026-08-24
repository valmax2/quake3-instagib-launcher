# Quake III InstaGib Launcher

Launcher grafico non ufficiale per **ioquake3** e la mod **InstaGib129**, per Windows e macOS,
pensato per usare un'installazione di Quake III Arena / ioquake3 **già esistente** sul tuo PC.
Non scarica, non reinstalla e non modifica i file originali del gioco: legge solo quello che
trova.

> Questo programma non include e non richiede alcuna CD key: usa quella già presente
> nella tua installazione di ioquake3 (file `q3key`), esattamente come il gioco originale.
> Nessun asset reale di Quake III (mappe, texture, suoni) è incluso: tutta la grafica
> dell'interfaccia (sfondi, pulsanti, logo) è arte originale.

## Cosa serve prima di usarlo

- **ioquake3** installato (Windows o macOS), con l'eseguibile del motore
- La cartella **baseq3** con almeno `pak0.pk3` (i dati originali di Quake III Arena — vanno
  presi da una copia legittima del gioco, per esempio da Steam o dal CD originale; questo
  launcher non li fornisce)
- La mod **InstaGib129** (file `InstaGib129.pk3`), scaricabile dal sito ufficiale
  <http://www.instagibmod.com>
- Percorso predefinito atteso su Windows: `C:\Giochi\ioquake3` (cambiabile dalle Impostazioni)

### Dove scaricare i componenti

| Componente | Dove trovarlo |
|---|---|
| Motore ioquake3 | <https://ioquake3.org/> (sezione download) |
| Quake III Arena (baseq3, pak0.pk3) | Va posseduto legittimamente — es. Steam, GOG o CD originale. Non è scaricabile gratuitamente: è materiale protetto da copyright. |
| Mod InstaGib129 | <http://www.instagibmod.com> |
| Team Arena / missionpack (facoltativo) | Espansione ufficiale di Quake III Arena, stesso discorso del gioco base |

## Struttura di cartelle attesa (Windows)

```
C:\Giochi\ioquake3\
  ioquake3.x86_64.exe
  baseq3\
    pak0.pk3, pak1.pk3, ...
  InstaGib129\
    InstaGib129.pk3
  missionpack\              (facoltativa)
    pak0.pk3, ...
```

Se la tua installazione ha una struttura diversa, apri **Impostazioni → Sfoglia...** e indica
la cartella corretta: l'app la ricontrolla subito e ti dice esattamente cosa manca, se manca
qualcosa. Su macOS l'app usa due percorsi separati (eseguibile dentro il bundle `.app` +
cartella dati), configurabili allo stesso modo.

## Funzionalità principali

- **Locale InstaGib**: partita contro bot, con mappa/rotazione, numero di giocatori, difficoltà
  bot, fraglimit, timelimit, FOV e modalità (FFA / Team / Torneo / CTF) configurabili.
- **Multiplayer InstaGib — Ospita**: il tuo PC ospita una partita non dedicata (giochi anche tu
  dalla stessa istanza), con nome server, porta UDP, password facoltativa, bot di riempimento e
  scelta LAN/Internet. Puoi **salvare preset** di configurazione server e ricaricarli.
- **Multiplayer InstaGib — Cerca e unisciti**: browser server integrato (protocollo UDP nativo
  di Quake III) per trovare partite già in corso sulla rete locale o su Internet (best-effort
  tramite master server). Filtri per modalità/mod/ping, ordinamento per numero di giocatori,
  doppio click per unirsi direttamente. Il pulsante "Carica giocatori" mostra chi sta giocando
  su ciascun server e distingue giocatori umani da bot (i bot rispondono sempre con ping 0),
  con un filtro dedicato per nascondere i server con soli bot.
- **Giocatori conosciuti**: salva i giocatori incontrati (con valutazione), ritrovali evidenziati
  nel browser server.
- **Tasti/Comandi**: preset pronti di comandi console (voti, cambio arma, FOV...) e messaggi
  chat rapidi (say/say_team), personalizzabili per tasto, con ripristino singolo o completo.
  Una finestra di riferimento elenca altri comandi/frasi copiabili con un click.
- **Personaggio**: nome colorato (codici colore Quake III), stile/dimensione/colore del mirino,
  colori del modello.
- **Chat vocale**: integrazione con Steam (cerca amico, avvia chat) — nessun software di terze
  parti da installare a parte.
- **Invita via WhatsApp**: dalla schermata Multiplayer, un pulsante compone un messaggio di
  invito (nome server, mappa, indirizzo:porta) e apre WhatsApp con il testo già pronto — sei
  sempre tu a scegliere il contatto e a premere invia, l'app non manda nulla da sola.
- **Multilingua**: italiano e inglese, cambio istantaneo dalla barra di navigazione.
- **Personalizza interfaccia**: colori (palette rapide o hex), stile dei pulsanti, immagini
  personalizzate per logo (anche GIF animata), sfondo finestra, sfondo Home e sfondo pulsanti
  (con galleria di predefiniti inclusi), oltre ai controlli per l'emblema animato della Home.
- **Galleria mappe reale**: le mappe vengono lette analizzando i file `.pk3` esistenti
  (`scripts/arenas.txt`, `scripts/*.arena`, `maps/*.bsp`, `levelshots/*`), non da un elenco
  fisso. Include ricerca, filtri per modalità/sorgente, preferiti e ultima mappa usata.
- **Rotazione mappe**: seleziona più mappe, riordinale, e l'app genera un file `.cfg` dedicato
  (protetto contro rotazioni miste baseq3/missionpack incompatibili, causa nota di crash).
- **Diagnostica**: controlli sull'installazione, ultimo comando generato, numero di versione.
- **Cache anteprime**: le immagini di anteprima (comprese quelle in formato TGA) vengono
  convertite una sola volta. Pulsanti per aggiornare la scansione mappe o svuotare la cache.

## Stabilità di rete e dell'engine

Il launcher imposta automaticamente alcuni cvar spesso trascurati che causano problemi reali:

- `rate`/`snaps`/`cl_maxpackets`/`cl_packetdup` a valori moderni (evita scatti/rubber-banding
  online anche con connessioni a banda larga perfette — il motore lasciato al default storico
  da modem 56k chiede troppo pochi aggiornamenti al secondo).
- `com_hunkmegs` alzato a un valore generoso (previene il crash `HUNK_ALLOC FAILED`, comune con
  mappe custom dai contenuti più pesanti di quelle originali del 1999).
- `cl_allowDownload 1` (assicura che il download automatico di mappe custom richieste da un
  server sia sempre tentato — non garantisce il successo se il server stesso ha una
  configurazione di download non funzionante, quello resta un limite del server).
- `r_mode -2` per la risoluzione (usa sempre quella reale del desktop, niente barre nere).
- Quando ti unisci a un server esterno, il client negozia da solo la mod corretta con il
  server invece di forzare sempre InstaGib129 (comportamento standard del client Quake III).

## Multiplayer da Internet

L'app **non modifica automaticamente** firewall o router. Per far entrare amici da Internet:

1. Consenti l'eseguibile nel Firewall di Windows (la schermata Multiplayer ti dice se è già
   consentito, controllo automatico via API del Firewall)
2. Inoltra la porta UDP scelta (default 27960) dal router al tuo PC
3. Comunica il tuo IP pubblico e la porta (il pulsante "Invita via WhatsApp" lo rileva da solo)

## Dati salvati dall'app

- Windows: `%APPDATA%\Quake3InstaGibLauncher\`
- macOS: `~/Library/Application Support/Quake3InstaGibLauncher/`

Contiene: `settings.json` (percorso di gioco, preferenze, ultime impostazioni partita), `cache\`
(indice mappe e anteprime PNG convertite — nessun asset di gioco originale), `logs\` (log di
crash tecnici, mai CD key/password/dati sensibili).

## Compilazione

Richiede **.NET 8 SDK**.

### Modo consigliato per distribuire una build a qualcuno: GitHub Actions

`.github/workflows/release.yml` compila Windows su una macchina Windows reale e **macOS su una
macchina macOS reale** (runner gratuiti forniti da GitHub per repository come questo), poi
pubblica i tre ZIP sulla pagina Release del repository. Compilare la versione Mac su un Mac
vero (invece che cross-compilarla da Windows) significa che l'app riceve la firma "ad-hoc"
richiesta dai chip Apple Silicon **durante la build stessa** — chi scarica lo ZIP dalla Release
fa doppio click e via, al massimo un clic di conferma sicurezza standard di macOS (vedi nota più
sotto), senza toccare il Terminale.

Si avvia da solo pushando un tag `vX.Y.Z`, oppure a mano dalla scheda **Actions** del repository
su GitHub (pulsante "Run workflow", specificando la versione) se vuoi rigenerare gli allegati di
una release già esistente senza creare un nuovo tag.

### Compilazione/test rapido in locale

Utile per provare una modifica al volo, **non per mandare il file a qualcuno** (la build Mac
prodotta così non è firmata — vedi nota sotto):

```bat
publish.bat        REM Windows: dist\Quake3InstaGibLauncher-win-x64.zip
publish-mac.bat     REM macOS (compilabile anche da Windows, cross-target): due ZIP, uno per
                     REM Apple Silicon (osx-arm64) e uno per Intel (osx-x64)
```

In alternativa, comando diretto per singola piattaforma:

```bash
# Windows (self-contained, file singolo)
dotnet publish src/Quake3InstaGibLauncher/Quake3InstaGibLauncher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

# macOS Apple Silicon (self-contained, file singolo)
dotnet publish src/Quake3InstaGibLauncher.Mac/Quake3InstaGibLauncher.Mac.csproj -c Release -r osx-arm64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

# macOS Intel (self-contained, file singolo)
dotnet publish src/Quake3InstaGibLauncher.Mac/Quake3InstaGibLauncher.Mac.csproj -c Release -r osx-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Le build pubblicate sono **self-contained** e **a file singolo**: non richiedono .NET
installato sul PC di destinazione. Il risultato del comando `dotnet publish` diretto è un solo
eseguibile più la cartella `Presets\` (sfondi/pulsanti/loghi predefiniti — resta fuori dal file
singolo per un limite noto di .NET sui contenuti oltre ~2 MB) e, solo su macOS, un pugno di
librerie native (`.dylib` di Avalonia/SkiaSharp — non impacchettabili nel file singolo, limite
noto di .NET con questo genere di dipendenze).

`publish-mac.bat` va oltre il semplice `dotnet publish`: assembla il tutto in un vero bundle
**`Quake III InstaGib Launcher.app`** (struttura `Contents/MacOS/` + `Contents/Info.plist`, con
`README.md` e le istruzioni di primo avvio accanto ma fuori dal bundle) — così chi lo riceve
vede una singola icona da trascinare in Applicazioni, come una normale app Mac, invece della
cartella con l'eseguibile e le librerie sciolti.

> **Primo avvio su Mac**: se compili la versione macOS da Windows (come questo script fa —
> funziona, NuGet scarica i runtime pack necessari), lo ZIP risultante non porta con sé il "bit
> eseguibile" Unix e l'app non è firmata/notarizzata Apple (nessun account sviluppatore Apple a
> pagamento dietro questo progetto amatoriale gratuito). Su Mac con chip Apple (M1/M2/.../M5...)
> serve anche una firma "ad-hoc" locale, che si può generare solo su un Mac vero (il tool
> `codesign` non esiste su Windows) — senza, macOS rifiuta di avviare l'app con l'errore
> "l'applicazione non è supportata sul Mac" (non è Gatekeeper: è un controllo più a basso livello,
> specifico dei chip Apple Silicon, e persiste anche dopo aver tolto la quarantena). Il
> destinatario deve fare **una tantum**, dopo aver scompattato, tre comandi da Terminale
> (`chmod +x` sull'eseguibile dentro il bundle, `xattr -cr` sul bundle, `codesign --force --deep
> --sign -` sul bundle) prima che il doppio click funzioni, poi eventualmente confermare in
> Impostazioni di Sistema → Privacy e sicurezza → "Apri comunque". Istruzioni
> passo-passo pronte per l'utente finale in `packaging/macos/Avvia su Mac - LEGGIMI.txt`
> (`publish-mac.bat` la copia già dentro ogni ZIP).

## Struttura del codice

- `src\Quake3InstaGibLauncher.Core\` — logica condivisa e senza dipendenze da un framework UI
  specifico (scansione `.pk3`, costruzione comando di avvio, avvio processo, generazione
  rotazione, browser server, chat Steam, modelli dati). Usata sia dalla versione Windows sia
  da quella macOS.
- `src\Quake3InstaGibLauncher\` — app Windows (WPF/.NET 8, MVVM con CommunityToolkit.Mvvm).
- `src\Quake3InstaGibLauncher.Mac\` — app macOS (Avalonia/.NET 8, stessa architettura MVVM).

## Sicurezza

- Nessun file `.pk3` o file di gioco originale viene mai modificato o cancellato.
- Nessuna CD key viene mai richiesta, salvata o mostrata.
- Tutti gli argomenti passati al processo di gioco sono validati (nomi mappa, porte, testo
  libero) e passati tramite `ProcessStartInfo.ArgumentList`, mai per concatenazione di stringhe.
- L'app non richiede privilegi di amministratore.

## Autore

Sviluppato da **VStudio Apps**. Segnalazioni, richieste o correzioni: vstudioapps@gmail.com

## Licenza

Distribuito con licenza [MIT](LICENSE).
