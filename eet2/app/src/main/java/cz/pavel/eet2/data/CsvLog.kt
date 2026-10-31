package cz.pavel.eet2.data

import java.time.OffsetDateTime
import java.time.format.DateTimeFormatter

/** Formát souboru s výsledky: jeden den = jeden soubor, oddělovač středník, kódování UTF-8. */
object CsvLog {
    const val HLAVICKA = "DIC;castka;POK;datum_cas;chyba;firma;porad_cis;stav;rezim"

    fun nazevSouboru(prefix: String, den: OffsetDateTime): String =
        "${prefix.ifBlank { "eet" }}_${DateTimeFormatter.ISO_LOCAL_DATE.format(den)}.csv"

    fun radek(
        dic: String,
        castka: String,
        pok: String,
        datumCas: String,
        chyba: String,
        firma: String,
        poradCis: String,
        stav: String,
        ostryProvoz: Boolean,
    ): String = listOf(dic, castka, pok, datumCas, chyba, firma, poradCis, stav, if (ostryProvoz) "OSTRY" else "TEST")
        .joinToString(";") { pole(it) }

    private fun pole(s: String): String {
        val one = s.replace("\r", " ").replace("\n", " ")
        return if (one.contains(';') || one.contains('"')) "\"" + one.replace("\"", "\"\"") + "\"" else one
    }
}
