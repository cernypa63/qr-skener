# Telefonní seznam

Jednoduchá webová aplikace pro zobrazení firemního telefonního seznamu z CSV souboru.
Celá aplikace je jediný soubor `index.html` – nepotřebuje žádný server ani instalaci,
stačí ji nakopírovat na síťový disk a otevřít v prohlížeči (dvojklikem).

## Vstupní soubor

CSV oddělené středníkem, s hlavičkou nebo bez ní:

```
Firma;OsCislo;Jméno;TelČíslo;Utvar
A;0012;Novák Jan;+420 251 111 012;1200
```

| Pole     | Délka          | Poznámka                                        |
|----------|----------------|-------------------------------------------------|
| Firma    | 1 znak         |                                                 |
| OsCislo  | 4 číslice      | kratší se doplní zleva nulami                   |
| Jméno    | 20 znaků       |                                                 |
| TelČíslo | libovolná      | zobrazuje se jako odkaz `tel:`                  |
| Utvar    | 4 znaky        |                                                 |

Kódování UTF-8 nebo Windows-1250 (rozpozná se automaticky, lze vynutit v Nastavení).
Řádky s jiným počtem polí se přeskočí a jejich počet se vypíše v hlášení o importu.

## Použití

1. Otevřete `index.html` v Google Chrome nebo Microsoft Edge (verze 2021 a novější).
2. **Nastavení → Vybrat soubor…** a najděte CSV na síťovém disku. Data se hned naimportují.
3. Při dotazu prohlížeče na přístup k souboru zvolte **„Povolit při každé návštěvě“** –
   jinak je nutné po každém spuštění kliknout na **Aktualizovat** a přístup potvrdit.

Při každém startu aplikace porovná datum změny CSV s datem posledního importu a je-li
soubor novější, automaticky ho převezme. Totéž dělá tlačítko **Aktualizovat**; pokud
soubor novější není, vypíše `Data jsou aktuální ze dne …`.

## Hledání a zobrazení

- Hledací okénko prohledává současně **jméno i osobní číslo**, bez ohledu na diakritiku
  a velikost písmen; nalezený text se zvýrazní. `Esc` hledání vymaže.
- Kliknutím na záhlaví sloupce se seznam řadí (opakovaným kliknutím obráceně).
- Data jsou uložena lokálně v prohlížeči (IndexedDB), takže seznam je k dispozici
  i když je síťový disk dočasně nedostupný.

## Omezení

Zapamatování cesty k souboru využívá File System Access API – funguje v Chrome a Edge,
ne ve Firefoxu ani Safari.

`vzor/telefonni_seznam.csv` je ukázkový vstupní soubor.
