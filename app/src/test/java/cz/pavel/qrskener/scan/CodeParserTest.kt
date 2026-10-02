package cz.pavel.qrskener.scan

import cz.pavel.qrskener.data.FieldPosition
import cz.pavel.qrskener.data.ScannerSettings
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class CodeParserTest {

    private val settings = ScannerSettings(
        documentPosition = FieldPosition(start = 1, length = 8),
        amountPosition = FieldPosition(start = 9, length = 8)
    )

    @Test
    fun `vyctu cislo dokladu a castku z pozic`() {
        val record = CodeParser.parse("240001231234,50", settings, now = 1L)

        assertEquals("24000123", record.documentNumber)
        assertEquals("1234,50", record.amountText)
        assertEquals(1234.50, record.amount!!, 0.001)
    }

    @Test
    fun `castka s teckou i mezerou`() {
        assertEquals(1234.5, CodeParser.parseAmount("1 234.50")!!, 0.001)
    }

    @Test
    fun `necitelna castka vrati null`() {
        assertNull(CodeParser.parseAmount("ABC"))
    }

    @Test
    fun `kratky kod nespadne`() {
        val record = CodeParser.parse("2400", settings, now = 1L)

        assertEquals("2400", record.documentNumber)
        assertEquals("", record.amountText)
        assertNull(record.amount)
    }
}
