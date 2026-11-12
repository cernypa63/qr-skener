# RENDT.exe – přejmenování souboru na jmeno_YYMMDD_hhmmss

```
RENDT.exe C:\A\faktura.pdf
```
přejmenuje soubor na `C:\A\faktura_261112_094300.pdf` (12. 11. 2026 9:43:00).

- Použije se datum a čas přejmenování, vždy ve tvaru `YYMMDD_hhmmss` (čas 24hodinový),
  bez ohledu na to, jak je ve Windows nastavený formát data a času.
- Datum se vkládá před příponu. Soubor bez přípony: `README` → `README_261112_094300`.
- Cesta s mezerami musí být v uvozovkách.
- Nové jméno (celou cestu) program vypíše, v BAT se dá převzít:
  ```bat
  for /f "delims=" %%i in ('RENDT.exe "C:\A\faktura.pdf"') do set NOVY=%%i
  ```

## Návratové kódy (ERRORLEVEL)

| Kód | Význam |
|---|---|
| 0 | přejmenováno |
| 2 | chybné parametry |
| 3 | soubor s novým jménem už existuje (nic se nepřejmenovalo) |
| 4 | soubor neexistuje nebo nejde přejmenovat (např. je otevřený) |

Při chybě je popis v `RENDT.err` vedle exe (při každém spuštění se starý smaže).

Potřebuje .NET Framework 4.8 (je ve Windows 10 a 11).
