package cz.pavel.qrskener.scan

import cz.pavel.qrskener.data.FieldPosition
import cz.pavel.qrskener.data.ScannerSettings

object CodeParser {

    fun parse(rawCode: String, settings: ScannerSettings, now: Long = System.currentTimeMillis()): ScanRecord {
        val documentNumber = substring(rawCode, settings.documentPosition)
        val amountText = substring(rawCode, settings.amountPosition)
        return ScanRecord(
            rawCode = rawCode,
            documentNumber = documentNumber,
            amountText = amountText,
            amount = parseAmount(amountText),
            timestampMillis = now
        )
    }

    fun parseAmount(text: String): Double? {
        val normalized = text.trim()
            .replace(" ", "")
            .replace("\u00a0", "")
            .replace(',', '.')
        return normalized.toDoubleOrNull()
    }

    private fun substring(value: String, position: FieldPosition): String {
        val from = (position.start - 1).coerceAtLeast(0)
        if (from >= value.length || position.length <= 0) return ""
        val to = (from + position.length).coerceAtMost(value.length)
        return value.substring(from, to).trim()
    }
}
