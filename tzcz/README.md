# TZCZ.exe – posun českého času pro ISO 8601 (+01:00 / +02:00)

```
TZCZ                         -> +01:00   (aktuální okamžik)
TZCZ 2036-07-15 10:30        -> +02:00
TZCZ 15.7.2036 10:30:00      -> +02:00
TZCZ -iso 15.7.2036 10:30    -> 2036-07-15T10:30:00+02:00
TZCZ -iso                    -> aktuální český čas, např. 2026-11-25T16:30:00+01:00
TZCZ --                      -> nic nevypíše, vrátí jen ERRORLEVEL (jde kombinovat s datem)
```

- Počítá podle pravidla EU, ne podle nastavení Windows: letní čas (+02:00) platí od poslední
  neděle v březnu 2:00 do poslední neděle v říjnu 3:00. Funguje pro roky 1996 až 9999,
  dokud EU pravidlo nezmění.
- Zadaný datum a čas je český místní čas. Datum: `RRRR-MM-DD` nebo `D.M.RRRR`, čas `H:MM` nebo `H:MM:SS`.
  Datum bez času = poledne.
- Hodina 2:00–2:59 v den změny (v březnu neexistuje, v říjnu je dvakrát) dostane `+02:00`.
- Aktuální okamžik se bere z hodin počítače v UTC, takže nezáleží ani na nastaveném časovém pásmu Windows.
- Převzetí do BAT:
  ```bat
  for /f %%i in ('TZCZ') do set POSUN=%%i
  ```
  nebo bez výpisu jen podle ERRORLEVEL:
  ```bat
  TZCZ -- %DATUM% %CAS%
  if errorlevel 3 goto chyba
  if errorlevel 2 (set POSUN=+02:00) else (set POSUN=+01:00)
  ```

## Návratové kódy (ERRORLEVEL)

| Kód | Význam |
|---|---|
| 1 | zimní čas, +01:00 |
| 2 | letní čas, +02:00 |
| 3 | chybné parametry (na výstupu je `Chyba: ...`, s `--` nic) nebo nápověda `TZCZ /?` |

Potřebuje .NET Framework 4.6.2 nebo novější (je ve Windows 10, 11 i Windows Server 2016).
