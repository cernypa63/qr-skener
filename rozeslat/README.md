# ROZESLAT – hromadné rozeslání e-mailu adresám z CSV

Program načte adresy ze souboru CSV a každému adresátovi pošle samostatný e-mail
přes výchozí poštovní program ve Windows (rozhraní MAPI). E-maily odešle poštovní
program sám, takže se uloží do jeho Odeslané pošty.

## Instalace
ZIP rozbalte do libovolné složky, nic se neinstaluje. Ve složce musí zůstat oba
programy (`ROZESLAT.exe` a `ROZESLAT32.exe`) i jejich soubory `.config`.
Spouštějte vždy `ROZESLAT.exe`. Pokud máte 32bitový Outlook, program sám spustí
`ROZESLAT32.exe`.

Potřebuje .NET Framework 4.6.2, který je ve Windows 10, 11 a Serveru 2016 a novějších.

## Podporované poštovní programy
- Outlook (klasický, z Microsoft 365 nebo Office 2010–2024)
- Thunderbird
- eM Client

Nový Outlook (s přepínačem „Nový Outlook“) ani aplikace Pošta ve Windows rozhraní
MAPI nemají, s nimi program nefunguje.

Program použije ten poštovní program, který je ve Windows nastavený jako výchozí:
- Outlook: Soubor → Možnosti → Obecné → „Nastavit Outlook jako výchozí program…“
- Thunderbird: Nastavení → Obecné → „Nastavit jako výchozí…“ → E-mail
- eM Client: Nabídka → Nastavení → Obecné → „Nastavit jako výchozí e-mailový klient“

## Použití
1. **Vybrat CSV…** – zvolte soubor s adresami. Oddělovač (středník, čárka,
   tabulátor) a kódování (UTF-8 nebo Windows-1250) program pozná sám.
2. **Sloupec s e-maily** – program předvybere sloupec, ve kterém je nejvíc adres.
   Zkontrolujte ho, u každého sloupce je ukázka obsahu. Pod ním je počet nalezených
   adres. Duplicitní adresy se pošlou jen jednou, neplatné se neodešlou
   (seznam ukáže odkaz **Zobrazit adresy**).
3. Vyplňte **Předmět** a **Text e-mailu**, případně přidejte **Přílohy**.
4. **Zkušební e-mail…** – pošle jeden e-mail na adresu, kterou zadáte (např. svoji).
5. **Odeslat všem** – po potvrzení se odešle e-mail každé adrese zvlášť.
   Adresáti nevidí ostatní adresy. Odesílání jde zastavit tlačítkem **Zastavit**.

Výsledek každého odeslání se zapíše do souboru `<název CSV>_odeslano.csv` vedle
souboru CSV. Předmět, text a vybraný soubor si program pamatuje do příště.

## Upozornění
- **Pauza mezi e-maily** (výchozí 2 vteřiny): poskytovatelé pošty omezují počet
  odeslaných e-mailů (např. několik set denně). Při velkém množství raději zvyšte pauzu
  a posílejte po částech.
- **Outlook** může při každém e-mailu zobrazit upozornění „Program se pokouší odeslat
  e-mail vaším jménem“. Objevuje se, když ve Windows není aktuální antivirus. Pak ho
  je nutné potvrdit, nebo to v Outlooku změnit (Soubor → Možnosti → Centrum zabezpečení
  → Nastavení Centra zabezpečení → Programový přístup), na což jsou potřeba práva správce.
- **Thunderbird** se může před odesláním zeptat na potvrzení.
- Poštovní program musí být při odesílání spuštěný nebo se spustí sám. Zda e-maily
  opravdu odešly, ověříte v jeho Odeslané poště.
