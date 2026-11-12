# SENDFTP.exe – odeslání souboru na FTP z BAT souboru

Přístupové údaje k FTP se uloží jednou, zašifrovaně, do `SENDFTP.cfg` vedle exe.
V BAT souboru pak nejsou vidět, volá se jen soubor a cílový adresář.

## 1. Uložení přístupových údajů (jednou)

```
SENDFTP.exe /nastav
```
Program se zeptá na server, port (Enter = 21), login a heslo (při psaní se ukazují hvězdičky),
vyzkouší přihlášení a údaje uloží. Při chybném hesle se nic neuloží.

Jde to i ze souboru (pak ho smažte):
```
SENDFTP.exe /nastav C:\A\ftp.txt
```
```
host=ftp.example.cz
port=21
login=uzivatel
heslo=heslo
```

`SENDFTP.exe /info` vypíše uložený server a login (heslo nikdy).

## 2. Odeslání

```
SENDFTP.exe C:\A\faktura.pdf import-to-eshop/faktury-pdf/2026
SENDFTP.exe "C:\A\Moje faktury\*.pdf" import-to-eshop/faktury-pdf/2026
```
- Cesta se soubory s mezerami musí být v uvozovkách. Jde zadat i masku (`*.pdf`).
- Adresář na FTP se zadává od přihlašovacího adresáře; když neexistuje, vytvoří se.
- Existující soubor stejného jména se přepíše.
- Soubor se nahrává pod jménem `nazev.part` a teprve po úplném přenosu se přejmenuje,
  takže si na serveru nikdo nevezme nedokončený soubor.

## Návratové kódy (ERRORLEVEL)

| Kód | Význam |
|---|---|
| 0 | odesláno |
| 2 | chybné parametry nebo chybí / nejde přečíst `SENDFTP.cfg` |
| 3 | chyba FTP (spojení, heslo, práva, přenos) |
| 4 | soubor k odeslání neexistuje nebo nejde přečíst |

Při chybě je popis v `SENDFTP.err` vedle exe (při každém spuštění se starý smaže).

```bat
SENDFTP.exe C:\A\faktura.pdf faktury
if errorlevel 1 echo Odeslani se nepovedlo & type SENDFTP.err
```

## Bezpečnost

- `SENDFTP.cfg` je zašifrovaný Windows (DPAPI) klíčem tohoto počítače. Zkopírovaný na jiný počítač
  nepůjde přečíst – tam je potřeba spustit `/nastav` znovu.
- Chrání to údaje před tím, aby byly vidět v BAT souborech a v souborech na disku.
  Kdo ale může na tomto počítači SENDFTP.exe spustit, může přes něj odesílat na FTP.
- Pokud FTP server neumí šifrované spojení (TLS), jde heslo i soubor po síti nešifrovaně.

Potřebuje .NET Framework 4.8 (je ve Windows 10 a 11).
