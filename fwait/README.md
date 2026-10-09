# fwait

Malý tichý program pro Windows: čeká, až se objeví soubor, a zkopíruje ho jinam.
Nic nevypisuje, takže se hodí do dávek (`.bat`) a pro volání z jiných programů.
Jeden soubor `fwait.exe`, stačí .NET Framework 4.8 (součást Windows 10 a 11).

## Použití

```
fwait <soubor> <sekundy> <cíl>
```

- Každých 0,2 s kontroluje, jestli `<soubor>` existuje a už do něj nezapisuje jiný program.
- Jakmile je soubor hotový, zkopíruje ho na `<cíl>` (existující cíl přepíše; je-li `<cíl>` složka, zkopíruje do ní pod stejným jménem).
- Když se soubor do `<sekundy>` neobjeví, zapíše do `<cíl>` chybové hlášení (text UTF-8).
- Sekundy mohou být i desetinné (`1,5`).

Příklad:

```
fwait C:\Skeny\doklad.pdf 60 \\server\archiv\doklad.pdf
```

Návratové kódy (`%ERRORLEVEL%`): `0` zkopírováno, `1` chybné parametry, `2` soubor se neobjevil, `3` kopírování selhalo.

## Instalace

Zkopírujte `fwait.exe` do `C:\Windows` (nebo jiné složky v `PATH`).

## Sestavení

```
dotnet build src -c Release      # fwait\src\bin\Release\net48\fwait.exe
dotnet test tests
```

Hotový `fwait.exe` sestaví také GitHub Actions (workflow *fwait pro Windows*, artefakt `fwait-windows`).
