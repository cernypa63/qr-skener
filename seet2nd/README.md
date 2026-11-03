# SEET2ND – odeslání tržby do EET 2.0 (Windows)

Program pro příkazový řádek Windows (.NET Framework 4.8, je součástí Windows 10/11). Odešle jednu tržbu do EET 2.0 a výsledek zapíše do XML souboru.

```
SEET2ND.exe cesta_k_souboru_vstupnich_hodnot
```

## Vstupní soubor

```xml
<?xml version="1.0" encoding="WINDOWS-1250"?>
<eet2_data>
<cert>C:\A\EET2\eet.p12</cert>
<pass>aaaa1111</pass>
<DIC>CZ00000019</DIC>
<PlayGround>1</PlayGround>
<id_pokl>POK1</id_pokl>
<id_jednotky>11</id_jednotky>
<trzba>123,12</trzba>
<doklad>DD261526</doklad>
<output>C:\A\EET2\res.xml</output>
</eet2_data>
```

| Prvek | Povinný | Význam |
|---|---|---|
| `cert` | ano | cesta k pokladnímu certifikátu (.p12) |
| `pass` | ano | heslo k certifikátu |
| `DIC` | ano | DIČ poplatníka (`CZ` se doplní, pokud chybí) |
| `PlayGround` | ano | `1` = testovací prostředí, `0` = ostré prostředí |
| `id_pokl` | ne | označení pokladny, výchozí `P1` |
| `id_jednotky` | ne | číslo evidenční jednotky (provozovny), výchozí `11` |
| `trzba` | ano | celková tržba včetně DPH, např. `123,12` nebo `-50` |
| `doklad` | ano | číslo dokladu (pořadové číslo účtenky), max. 25 znaků bez diakritiky |
| `output` | ano | soubor, kam se zapíše výsledek |
| `prvni_zaslani` | ne | `1` = první odeslání (výchozí), `0` = opakované odeslání nedoručené tržby |
| `dat_trzby` | ne | datum a čas tržby, např. `31.10.2026 15:01:03`; výchozí je aktuální čas. Při `prvni_zaslani` = 0 povinné – původní čas tržby |
| `timeout` | ne | jak dlouho čekat na odpověď EET v sekundách (2–120), výchozí 10 |

## Výstupní soubor (windows-1250)

```xml
<?xml version="1.0" encoding="windows-1250"?>
<eet2_result>
  <chyba>0</chyba>
  <stav>OK</stav>
  <POK>3b3817ad-23fc-4e8c-b05c-986a62bcf4bb-ff</POK>
  <dat_trzby>2026-10-31T15:01:03+01:00</dat_trzby>
  <dat_odesl>2026-10-31T15:01:03+01:00</dat_odesl>
  <dat_prij>2026-10-31T15:01:04+01:00</dat_prij>
  <kod_chyby />
  <zprava />
  <uuid_zpravy>…</uuid_zpravy>
  <PlayGround>1</PlayGround>
  <prvni_zaslani>1</prvni_zaslani>
  <DIC>CZ00000019</DIC>
  <id_pokl>POK1</id_pokl>
  <id_jednotky>11</id_jednotky>
  <trzba>123,12</trzba>
  <doklad>DD261526</doklad>
</eet2_result>
```

- `chyba`: `0` = tržba zaevidována, `1` = nezaevidována.
- `stav`: `OK`, `CHYBA` (EET zprávu odmítl nebo je chyba certifikátu – opakování nepomůže) nebo `NEDORUCENO` (výpadek spojení nebo dočasná chyba EET – tržbu je nutné doposlat s `prvni_zaslani` = 0 a původním `dat_trzby`, nejpozději do 48 hodin).
- `kod_chyby` a `zprava`: kód a text chyby z EET, `varovani`: případná varování EET.

Pokud vstupní soubor nejde načíst nebo má chybnou strukturu, vznikne vedle `SEET2ND.exe` soubor `SEET2ND.err` s popisem chyby (při každém spuštění se starý `SEET2ND.err` nejdřív smaže).

Návratový kód: `0` = OK, `1` = chyba/nedoručeno (viz výstup), `2` = chybný vstup (viz `SEET2ND.err`), `3` = výstup nelze zapsat (viz `SEET2ND.err`).

## Sestavení

Exe se sestavuje v GitHub Actions (`.github/workflows/seet2nd.yml`) a testuje se skutečným odesláním do Playgroundu EET. Lokálně: `dotnet test tests`, `dotnet build src -c Release -o out`.
