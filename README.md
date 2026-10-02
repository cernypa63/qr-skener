# QR Skener

Android aplikace pro sběr dokladů z QR kódů (Kotlin, Jetpack Compose, CameraX, ML Kit).

## Obrazovky

- **Úvodní obrazovka** – tlačítka *Začít skenovat* a *Nastavení*
- **Skenování** – opakované snímání kódů; z textu kódu se podle nastavených pozic vyčte číslo dokladu a částka, v titulku běží průběžný součet a počet dokladů, načtené doklady jsou vypsané pod náhledem (lze je odebrat). Tlačítko *Ukončit akci* vytvoří CSV a odešle ho na FTP.
- **Nastavení** – zvuk, vibrace, kopírování do schránky, pozice údajů v kódu (od znaku + délka, počítáno od 1) a FTP server (adresa, port, uživatel, heslo, složka, FTPS)

## Formát CSV

```
cislo_dokladu;castka
24000123;1234,50
```

Soubor se ukládá v UTF-8 pod jménem `skenovani_<yyyyMMdd_HHmmss>.csv`. Formát je předběžný a upraví se podle cílového systému.

## Build

```bash
./gradlew assembleDebug   # APK: app/build/outputs/apk/debug/app-debug.apk
./gradlew testDebugUnitTest lintDebug
```
