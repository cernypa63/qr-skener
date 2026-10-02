package cz.pavel.qrskener.scan

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class ScanSessionViewModelTest {

    private fun record(raw: String, amount: Double?, ts: Long) =
        ScanRecord(raw, raw.take(8), amount?.toString() ?: "xyz", amount, ts)

    @Test
    fun `soucet ignoruje necitelne castky a reaguje na odebrani`() {
        val session = ScanSessionViewModel()
        val a = record("a", 1234.5, 1L)
        session.add(a)
        session.add(record("b", null, 2L))
        session.add(record("c", 5.5, 3L))

        assertEquals(1240.0, session.total, 0.001)
        session.remove(a)
        assertEquals(5.5, session.total, 0.001)
        session.clear()
        assertEquals(0, session.records.size)
    }

    @Test
    fun `stejny kod v okne je duplicita, po okne ne`() {
        val session = ScanSessionViewModel()
        session.add(record("a", 1.0, 1000L))

        assertTrue(session.isDuplicate("a", 2999L, 2000L))
        assertFalse(session.isDuplicate("a", 3000L, 2000L))
        assertFalse(session.isDuplicate("b", 1500L, 2000L))
    }
}
