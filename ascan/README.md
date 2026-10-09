# ascan

Jednoduchá konzolová aplikace pro Windows – skenování z příkazového řádku (cmd) přes WIA
(Získávání obrazu systému Windows). Jeden soubor `ascan.exe` (~30 kB), stačí .NET Framework 4.8,
který je součástí Windows 10 a 11.

## Použití

```
ascan                  výběr skeneru ze seznamu (uloží se jako nastavený)
ascan <soubor>         naskenuje stránku nastaveným skenerem do souboru
ascan -l               vypíše dostupné skenery
ascan -h               nápověda

-d, --dpi <N>          rozlišení v DPI (výchozí 300)
-c, --barva <režim>    barva | seda | cb   (výchozí barva)
```

Bez parametru:

```
C:\> ascan
Dostupné skenery:
  1) Canon LiDE 300   (výchozí ve Windows)
  2) HP OfficeJet 8010
Žádný skener zatím není nastaven.
Vyberte číslo skeneru [Enter = 1, W = výběr ve Windows, Q = konec]:
```

- Pokud není nastaven žádný skener, nabídne se (Enter) výchozí skener Windows – první skener, který hlásí WIA.
- `W` otevře standardní dialog Windows pro výběr skeneru.
- Volba se ukládá do `%APPDATA%\ascan\ascan.cfg`.

Formát výstupu podle přípony: `.jpg .png .bmp .tif .gif` (bez přípony `.jpg`).

Návratové kódy: `0` OK, `1` chybné parametry, `2` skener nenalezen / nedostupný, `3` chyba skenování.

## Instalace

Zkopírujte `ascan.exe` do složky, která je v `PATH` (např. `C:\Windows` nebo vlastní `C:\Nastroje`).

## Sestavení

```
dotnet build src -c Release      # ascan\src\bin\Release\net48\ascan.exe
dotnet test tests
```

Hotový `ascan.exe` sestaví také GitHub Actions (workflow *ascan pro Windows*, artefakt `ascan-windows`).
