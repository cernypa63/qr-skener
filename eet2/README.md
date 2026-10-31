# EET 2.0 (Android)

Jednoduchá pokladna pro evidenci tržeb do EET 2.0 (Kotlin, Jetpack Compose).

- **Nastavení** – jedna nebo více firem (pokladní certifikát `.p12` + heslo, název, DIČ, číslo provozovny, označení pokladny), společné FTP/FTPS, testovací (Playground) / ostrý režim.
- **Pokladna** – přepínání firem, obsluha zadá částku a stiskne *Tržbu zaevidovat*; výsledek se ohlásí a po potvrzení se nabídne další částka.
- **Výsledky** se ihned připisují na FTP do souboru `<začátek>_RRRR-MM-DD.csv` (UTF-8, středník):
  `DIC;castka;POK;datum_cas;chyba;firma;porad_cis;stav;rezim`
- Nedoručené tržby (bez spojení / dočasná chyba) se doposílají každou minutu s `prvni_zaslani=false`; nedostupné FTP se zapíše později.
- Hesla jsou šifrována klíčem v Android Keystore, certifikáty jsou v soukromém úložišti aplikace.

Rozhraní: `https://pg.trzbyeet.gov.cz/eet/services/EETServiceSOAP/v4` (Playground), `https://trzbyeet.gov.cz/eet/services/EETServiceSOAP/v4` (produkce).

## Build a testy

```bash
./gradlew testDebugUnitTest assembleDebug lintDebug
EET_ONLINE=1 ./gradlew testDebugUnitTest   # navíc skutečné odeslání na Playground
```

APK: `app/build/outputs/apk/debug/app-debug.apk`

Testovací certifikáty v `app/src/test/resources` jsou veřejné certifikáty Playgroundu z eet.gov.cz.
