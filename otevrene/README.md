# OTEVRENE.exe

Vypíše do okna příkazového řádku, které soubory na tomto Windows serveru mají přes síť (sdílenou složku) otevřené jiné počítače: **IP adresu**, uživatele, ID a soubor.

Funguje na Windows Serveru 2008 R2 až 2025 (i na Windows 7–11). Nic se neinstaluje, stačí .NET Framework 3.5 nebo 4.x, který Windows už mají.

## Použití

Příkazový řádek je nutné spustit **jako správce** (pravým tlačítkem na cmd → Spustit jako správce).

```
OTEVRENE.exe                    všechny otevřené soubory
OTEVRENE.exe D:\DATA            jen soubory, v jejichž cestě je D:\DATA
OTEVRENE.exe D:\DATA\*.DBF      maska s * a ?
OTEVRENE.exe 192.168.1.15       jen soubory otevřené z tohoto počítače
OTEVRENE.exe novak              jen soubory uživatele novak
```

Výsledek:

```
IP adresa     Uzivatel      ID  Soubor
192.168.1.9   FIRMA\novak   77  D:\DATA\FAKTURY.DBF
192.168.1.15  FIRMA\dvorak  81  D:\DATA\SKLAD.DBF

Otevrenych souboru: 2  (SMB)
```

Odpojení souboru, který někdo drží: `net file 77 /close` (77 je ID ze seznamu). Uživateli se může ztratit neuložená práce.

## Návratové kódy (ERRORLEVEL)

| Kód | Význam |
|---|---|
| 0 | nějaký soubor je otevřený (vypsán) |
| 1 | žádný soubor není otevřený (nebo žádný neodpovídá zadanému textu) |
| 2 | chybné parametry |
| 3 | chybí práva správce |
| 5 | jiná chyba |

## Jak to funguje

- **Windows Server 2012 a novější:** Windows u každého otevřeného souboru uvádí IP adresu klienta (jako `Get-SmbOpenFile`).
- **Windows Server 2008 R2 a starší:** Windows u souboru uvádí jen uživatele. IP adresa se doplní podle síťových připojení tohoto uživatele. Je-li uživatel připojen z více počítačů, vypíše se např. `192.168.1.9 nebo 192.168.1.12`; když ji nejde zjistit, `?`. Tento způsob jde vynutit parametrem `/net`.

Vypisují se jen soubory otevřené **přes síť**. Soubory otevřené programem přímo na serveru nebo přes vzdálenou plochu (RDP) Windows mezi síťově otevřené nepočítá.
