package cz.pavel.qrskener.scan

import org.junit.Assert.assertEquals
import org.junit.Test

class CsvBuilderTest {

    @Test
    fun `csv obsahuje hlavicku a radky`() {
        val csv = CsvBuilder.build(
            listOf(
                ScanRecord("a", "24000123", "1234,50", 1234.5, 1L),
                ScanRecord("b", "24000124", "10", 10.0, 2L)
            )
        )

        assertEquals(
            "cislo_dokladu;castka\r\n24000123;1234,50\r\n24000124;10,00\r\n",
            csv
        )
    }

    @Test
    fun `necitelna castka se ulozi jako text`() {
        val csv = CsvBuilder.build(listOf(ScanRecord("a", "1", "XY", null, 1L)))

        assertEquals("cislo_dokladu;castka\r\n1;XY\r\n", csv)
    }
}
