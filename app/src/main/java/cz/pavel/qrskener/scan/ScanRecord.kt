package cz.pavel.qrskener.scan

data class ScanRecord(
    val rawCode: String,
    val documentNumber: String,
    val amountText: String,
    val amount: Double?,
    val timestampMillis: Long
)
