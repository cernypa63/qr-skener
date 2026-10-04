package cz.pavel.vyska

data class GgaFix(val altitudeMsl: Double, val satellites: Int?)

object Nmea {
    /** Parses altitude above mean sea level from a GGA sentence ($GPGGA, $GNGGA, ...); null when not a valid fix. */
    fun parseGga(sentence: String): GgaFix? {
        val body = sentence.trim().substringBefore('*')
        val fields = body.split(',')
        if (fields.size < 11 || !fields[0].startsWith("$") || !fields[0].endsWith("GGA")) return null
        val quality = fields[6].toIntOrNull() ?: return null
        if (quality == 0) return null
        if (fields[10].isNotEmpty() && fields[10] != "M") return null
        val altitude = fields[9].toDoubleOrNull() ?: return null
        return GgaFix(altitude, fields[7].toIntOrNull())
    }
}
