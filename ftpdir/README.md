# FTPDIR.exe – výpis adresáře na FTP do CSV

Volání: `FTPDIR.exe cesta_k_souboru_parametru`

## Soubor parametrů

Řádky `klíč=hodnota` (Windows-1250 nebo UTF-8). Prázdné řádky a řádky začínající `;` nebo `#` se přeskočí.

```
host=ftp.server.cz
login=uzivatel
heslo=heslo
adresar=import-to-eshop/faktury-pdf/2026
output=C:\A\seznam.csv
```

| Klíč | Povinný | Význam |
|---|---|---|
| `host` | ano | FTP server, lze i `ftp.server.cz:2121` |
| `login` | ano | přihlašovací jméno |
| `heslo` | ano | heslo (může být prázdné) |
| `adresar` | ano | adresář na serveru |
| `output` | ano | kam uložit CSV |
| `port` | ne | výchozí 21 |
| `maska` | ne | jen vybrané soubory, např. `*.pdf` nebo `*.pdf;*.xml`, výchozí `*` |
| `hlavicka` | ne | `1` = první řádek `soubor;datum;velikost` (výchozí), `0` = bez něj |
| `timeout` | ne | sekundy čekání na server, výchozí 30 |

## Výstup

CSV ve Windows-1250, oddělovač `;`, seřazené podle názvu, jen soubory (bez podadresářů):

```
soubor;datum;velikost
fa_2026010001.pdf;03.01.2026 01:44:04;7045
```

- `datum` = datum a čas změny souboru tak, jak ho uvádí FTP server (přesně na sekundy přes MDTM; když ho server nepodporuje, z výpisu adresáře – u starších souborů pak jen datum).
- `velikost` v bajtech.
- Název se středníkem nebo uvozovkou je v uvozovkách.

## Chyby

Při chybě se výstupní soubor smaže a do `FTPDIR.err` vedle exe se zapíše popis chyby. Starý `FTPDIR.err` se při každém spuštění smaže.

Návratový kód: `0` = OK, `2` = chybný soubor parametrů, `3` = chyba FTP (spojení, přihlášení, adresář), `4` = výstup nelze zapsat.

Používá se nešifrované FTP v pasivním režimu. Heslo je v souboru parametrů – doporučujeme ho po spuštění smazat.
