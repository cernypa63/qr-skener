package cz.pavel.eet2

import cz.pavel.eet2.eet.EetClient
import cz.pavel.eet2.eet.EetResult
import cz.pavel.eet2.eet.EetXml
import cz.pavel.eet2.eet.PokladniCertifikat
import cz.pavel.eet2.eet.TrzbaData
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Assume.assumeTrue
import org.junit.Test

class EetXmlTest {
    private fun res(name: String) = javaClass.classLoader!!.getResourceAsStream(name)!!.readBytes()
    private val heslo get() = res("password_pokladni_cert_playground.txt").toString(Charsets.UTF_8).trim()
    private fun cert() = PokladniCertifikat.load(res("CA_EET-Playground-CZ683555118.p12"), heslo)

    @Test
    fun castka() {
        assertEquals("123.50", EetXml.parseCastka("123,5"))
        assertEquals("1000.00", EetXml.parseCastka(" 1 000 "))
        assertEquals("-20.00", EetXml.parseCastka("-20"))
        assertNull(EetXml.parseCastka("12,345"))
        assertNull(EetXml.parseCastka("abc"))
        assertNull(EetXml.parseCastka(""))
    }

    @Test
    fun certifikat() {
        val c = cert()
        assertEquals("CZ683555118", c.dic)
    }

    @Test(expected = Exception::class)
    fun spatneHeslo() {
        PokladniCertifikat.load(res("CA_EET-Playground-CZ683555118.p12"), "spatne")
    }

    /** Výstup lxml exclusive c14n (PrefixList "v4") pro stejnou zprávu. */
    @Test
    fun kanonickeTeloOdpovidaLxml() {
        val body = EetXml.canonicalBody(
            "bd6533c6-5f9b-4f82-8424-6d131745c6b9", "2026-10-31T12:20:16+01:00", true,
            TrzbaData("CZ683555118", "101", "Android01", "TEST/0001", "2026-10-31T12:20:16+01:00", "123.00"),
        )
        assertEquals("<soapenv:Body xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\" xmlns:v4=\"http://fs.gov.cz/eet/schema/v4\" xmlns:wsu=\"http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd\" wsu:Id=\"id-body\"><v4:Trzba><v4:Hlavicka dat_odesl=\"2026-10-31T12:20:16+01:00\" prvni_zaslani=\"true\" uuid_zpravy=\"bd6533c6-5f9b-4f82-8424-6d131745c6b9\"></v4:Hlavicka><v4:Data celk_trzba=\"123.00\" dat_trzby=\"2026-10-31T12:20:16+01:00\" eic_popl=\"CZ683555118\" id_jednotky=\"101\" id_pokl=\"Android01\" porad_cis=\"TEST/0001\"></v4:Data></v4:Trzba></soapenv:Body>", body)
    }

    @Test
    fun parsovaniOdpovedi() {
        val ok = """<soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/" xmlns:eet="http://fs.gov.cz/eet/schema/v4"><soapenv:Body><eet:Odpoved><eet:Hlavicka uuid_zpravy="x" dat_prij="2026-10-31T12:20:17+01:00"/><eet:Potvrzeni pok="abc-ff" test="true"/><eet:Varovani kod_varov="1">Pozor</eet:Varovani></eet:Odpoved></soapenv:Body></soapenv:Envelope>"""
        assertEquals(EetResult.Prijato("abc-ff", true, listOf("1: Pozor")), EetClient.parseResponse(ok))
        val err = ok.replace("""<eet:Potvrzeni pok="abc-ff" test="true"/><eet:Varovani kod_varov="1">Pozor</eet:Varovani>""", """<eet:Chyba kod="4" test="true">Neplatny podpis SOAP zpravy</eet:Chyba>""")
        assertEquals(EetResult.Odmitnuto(4, "Neplatny podpis SOAP zpravy"), EetClient.parseResponse(err))
        val tmp = err.replace("kod=\"4\"", "kod=\"-1\"")
        assertTrue(EetClient.parseResponse(tmp) is EetResult.Nedoruceno)
        assertNull(EetClient.parseResponse("<html>chyba</html>"))
    }

    @Test
    fun odeslaniDoPlayground() {
        assumeTrue(System.getenv("EET_ONLINE") == "1")
        val now = EetXml.formatDate(EetXml.now())
        val data = TrzbaData("CZ683555118", "101", "1", "UT" + System.currentTimeMillis(), now, "99.90")
        val r = EetClient.send(EetClient.URL_PLAYGROUND, cert(), data, true, 10000)
        println(r)
        assertTrue(r.toString(), r is EetResult.Prijato)
        val r2 = EetClient.send(EetClient.URL_PLAYGROUND, cert(), data, false, 10000)
        println(r2)
        assertTrue(r2.toString(), r2 is EetResult.Prijato)
    }
}
