# Úkoly

Android aplikace pro správu úkolů (Kotlin, Jetpack Compose). Data se ukládají jako JSON soubor na FTP, aby s nimi mohly pracovat i jiné aplikace.

## Obrazovky

- **Otevřené úkoly** (úvodní) – seznam `Datum založení : Text`, nahoře tlačítka *Nový*, *Vyřešeno*, *Nastavení* a ikona pro ruční aktualizaci z FTP.
  Klepnutí na úkol nejdřív ověří stav na FTP (pokud ho změnila jiná aplikace, načte se aktuální verze) a nabídne *Editovat*, *Přesunout do vyřešených*, *Smazat*.
- **Vyřešené úkoly** – klepnutí nabídne *Vrátit do otevřených*, *Smazat*.
- **Nový / Upravit úkol** – text úkolu, *Uložit*.
- **Nastavení** – FTP server, port, uživatel, heslo, cesta k souboru (např. `/ukoly/ukoly.json`), FTPS, interval automatické aktualizace v minutách (0 = vypnuto), tlačítko *Otestovat připojení*.

## Synchronizace

- Každá změna (nový, úprava, přesun, smazání) se ihned uloží: aplikace stáhne aktuální soubor z FTP, provede změnu a nahraje ho zpět (přes dočasný `*.tmp` + přejmenování). Změny jiných aplikací tak nejsou přepsány.
- Při spuštění/návratu do aplikace a pak v nastaveném intervalu se seznam aktualizuje z FTP.
- Pokud soubor na FTP neexistuje, vytvoří se z aktuálních úkolů v telefonu. Neexistující složky se vytvoří.
- Bez nastaveného FTP se úkoly ukládají jen v telefonu.

## Formát souboru

```json
{
  "version": 1,
  "modified": "2026-10-01T16:34:26",
  "tasks": [
    {
      "id": 1790872466158,
      "text": "Koupit tonery",
      "status": "open",
      "created": "2026-10-01T16:34:26",
      "modified": "2026-10-01T16:34:26",
      "closed": null
    }
  ]
}
```

- `id` – timestamp založení v ms (jednoznačný identifikátor)
- `status` – `open` / `closed`
- `created`, `modified`, `closed` – místní čas ve formátu ISO 8601
- Neznámá pole přidaná jinými aplikacemi se zachovávají.

## Build

```bash
cd ukoly
./gradlew assembleDebug      # APK: app/build/outputs/apk/debug/app-debug.apk
./gradlew testDebugUnitTest
```
