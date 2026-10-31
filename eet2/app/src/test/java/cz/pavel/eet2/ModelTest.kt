package cz.pavel.eet2

import cz.pavel.eet2.data.CsvLog
import cz.pavel.eet2.data.Firma
import cz.pavel.eet2.data.ModelJson
import cz.pavel.eet2.data.Nastaveni
import cz.pavel.eet2.data.NeodeslanaTrzba
import cz.pavel.eet2.data.ZaznamFtp
import cz.pavel.eet2.eet.TrzbaData
import org.junit.Assert.assertEquals
import org.junit.Test
import java.time.OffsetDateTime

class ModelTest {
    private val json = ModelJson({ "enc:$it" }, { it.removePrefix("enc:") })

    @Test
    fun nastaveniRoundTrip() {
        val n = Nastaveni(
            firmy = listOf(Firma("a", "Firma A", "CZ00000019", "101", "1", "a.p12", "tajne")),
            vybranaFirmaId = "a", ostryProvoz = true, mezniDobaS = 3,
        )
        val text = json.nastaveniToJson(n)
        assert(!text.contains("\"tajne\""))
        assertEquals(n, json.nastaveniFromJson(text))
    }

    @Test
    fun frontyRoundTrip() {
        val t = listOf(NeodeslanaTrzba("a", "Firma A", TrzbaData("CZ1", "1", "2", "3", "4", "5.00"), false))
        assertEquals(t, json.neodeslaneFromJson(json.neodeslaneToJson(t)))
        val f = listOf(ZaznamFtp("eet_2027-01-01.csv", "a;b"))
        assertEquals(f, json.ftpFromJson(json.ftpToJson(f)))
    }

    @Test
    fun csv() {
        assertEquals("eet_2027-01-02.csv", CsvLog.nazevSouboru("eet", OffsetDateTime.parse("2027-01-02T10:00:00+01:00")))
        assertEquals(
            "CZ00000019;125.50;abc;2027-01-02T10:00:00+01:00;\"chyba; \"\"x\"\"\";Firma;1;CHYBA;TEST",
            CsvLog.radek("CZ00000019", "125.50", "abc", "2027-01-02T10:00:00+01:00", "chyba; \"x\"", "Firma", "1", "CHYBA", false),
        )
    }
}
