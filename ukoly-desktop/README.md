# Úkoly pro Windows

Desktopová verze aplikace [Úkoly](../ukoly) (Kotlin, Compose Desktop). Má stejné obrazovky a funkce jako Android aplikace a pracuje se stejným JSON souborem na FTP, takže telefon i počítač vidí stejné úkoly.

## Funkce

- **Otevřené úkoly** – seznam `Datum založení : Text` od nejnovějšího, tlačítka *Nový*, *Vyřešeno*, *Nastavení*, přepnutí zobrazení *seznam* (jeden řádek, menší písmo) a ruční aktualizace.
  Kliknutí na úkol nejdřív ověří stav na FTP a nabídne *Editovat*, *Přesunout do vyřešených*, *Smazat*.
- **Vyřešené úkoly** – *Vrátit do otevřených*, *Smazat*.
- **Nastavení** – FTP server, port, uživatel, heslo, cesta k souboru, FTPS, interval automatické aktualizace, *Otestovat připojení*.
- Každá změna se ihned uloží stejným způsobem jako v telefonu: stáhnout aktuální JSON, provést změnu, nahrát zpět přes `*.tmp` + přejmenování.

Formát souboru je popsaný v [`ukoly/README.md`](../ukoly/README.md).

Nastavení a místní kopie úkolů jsou v `%APPDATA%\Ukoly` (`settings.properties`, `ukoly.json`); na Linuxu/macOS v `~/.ukoly`.

## Build

Vyžaduje JDK 17.

```bash
cd ukoly-desktop
./gradlew test              # testy
./gradlew run               # spuštění
./gradlew packageMsi        # instalátor MSI (jen na Windows)
./gradlew packageExe        # instalátor EXE (jen na Windows)
./gradlew createDistributable  # složka s aplikací a vlastním JRE
```

Instalátory pro Windows sestavuje workflow `.github/workflows/ukoly-desktop.yml` na `windows-latest` (při změně v `ukoly-desktop/` nebo ručně), výsledky jsou v artefaktech běhu.
