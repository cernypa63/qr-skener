# QR Skener

Android aplikace pro skenování QR a čárových kódů (Kotlin, Jetpack Compose, CameraX, ML Kit).

## Obrazovky

- **Úvodní obrazovka** – dvě tlačítka: *Začít skenovat* a *Nastavení*
- **Skenování** – živý náhled kamery, po načtení kódu zobrazí hodnotu s možností kopírovat / otevřít odkaz / skenovat znovu
- **Nastavení** – zvuk, vibrace, automatické kopírování do schránky, automatické otevírání odkazů (uloženo v DataStore)

## Build

```bash
./gradlew assembleDebug
```

APK: `app/build/outputs/apk/debug/app-debug.apk`
