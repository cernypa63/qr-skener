package cz.pavel.qrskener.scan

import java.util.Locale

object CsvBuilder {

    const val SEPARATOR = ";"

    fun build(records: List<ScanRecord>): String {
        val builder = StringBuilder()
        builder.append("cislo_dokladu").append(SEPARATOR).append("castka").append("\r\n")
        records.forEach { record ->
            builder.append(escape(record.documentNumber))
                .append(SEPARATOR)
                .append(formatAmount(record))
                .append("\r\n")
        }
        return builder.toString()
    }

    fun formatAmount(record: ScanRecord): String {
        val amount = record.amount ?: return escape(record.amountText)
        return String.format(Locale.US, "%.2f", amount).replace('.', ',')
    }

    private fun escape(value: String): String =
        if (value.contains(SEPARATOR) || value.contains('"')) {
            "\"" + value.replace("\"", "\"\"") + "\""
        } else {
            value
        }
}
