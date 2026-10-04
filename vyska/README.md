# Výška

Jednoduchá Android aplikace, která na obrazovce ukazuje nadmořskou výšku z GPS.

- Výška nad mořem se bere z NMEA věty GGA (přijímač ji udává už přepočtenou na geoid).
- Když telefon NMEA neposkytne, použije se `Location.getMslAltitudeMeters()` (Android 14+),
  jinak výška nad elipsoidem WGS84 s upozorněním (v ČR je o ~45 m vyšší než nadmořská).
- Zobrazuje i vertikální přesnost a počet satelitů, pokud jsou k dispozici.
- Displej zůstává rozsvícený, dokud je aplikace na popředí.
- Ikona: průhledné pozadí s modrým písmenem A. Většina launcherů (např. Pixel) ale pod ikonu aplikace
  vždy dokreslí pozadí, proto je k dispozici i widget 1×1 „Výška“ – opravdu průhledné písmeno A,
  které po klepnutí otevře aplikaci (dlouhý stisk na ploše → Widgety → Výška).

## Build

```bash
cd vyska
./gradlew testDebugUnitTest assembleDebug lintDebug
# APK: app/build/outputs/apk/debug/app-debug.apk
```
