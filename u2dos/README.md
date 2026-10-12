# u2dos

Malý program pro Windows: převede textový soubor z Linuxu (UTF-8, konec řádku LF)
na DOSový text v kódování Latin II (cp852) s koncem řádku CRLF.
Jeden soubor `u2dos.exe`, stačí .NET Framework 4.8 (součást Windows 10 a 11).

## Použití

```
u2dos <vstup> <výstup>
u2dos C:\Data\export.txt C:\Data\export852.txt
```

- Konce řádků LF se změní na CRLF (řádky, které už CRLF mají, zůstanou beze změny).
- BOM na začátku UTF-8 souboru se vynechá.
- Znaky, které v cp852 nejsou: typografické uvozovky → `"` / `'`, pomlčky → `-`, `…` → `...`,
  `€` → `EUR`; ostatní (např. emoji) → `?`.
- Výstupní soubor se přepíše; chybějící složka se vytvoří.
- Při úspěchu nic nevypisuje, chyby píše do konzole.

Návratové kódy (`%ERRORLEVEL%`): `0` OK, `1` chybné parametry, `2` vstup neexistuje, `3` chyba čtení/zápisu.

## Instalace

Zkopírujte `u2dos.exe` do `C:\Windows` (nebo jiné složky v `PATH`).

## Sestavení

```
dotnet build src -c Release      # u2dos\src\bin\Release\net48\u2dos.exe
dotnet test tests
```

Hotový `u2dos.exe` sestaví také GitHub Actions (workflow *u2dos pro Windows*, artefakt `u2dos-windows`).
