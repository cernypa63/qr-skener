package cz.pavel.eet2.eet

import org.w3c.dom.Element
import java.io.ByteArrayInputStream
import java.net.HttpURLConnection
import java.net.URL
import java.util.UUID
import javax.xml.parsers.DocumentBuilderFactory

sealed interface EetResult {
    /** Tržba přijata, [pok] je potvrzovací kód. */
    data class Prijato(val pok: String, val test: Boolean, val varovani: List<String>) : EetResult

    /** Server zprávu odmítl – opakování nepomůže. */
    data class Odmitnuto(val kod: Int, val zprava: String) : EetResult

    /** Odpověď nepřišla (výpadek, překročení mezní doby, dočasná chyba) – tržbu je nutné doposlat. */
    data class Nedoruceno(val zprava: String) : EetResult
}

object EetClient {
    const val URL_PLAYGROUND = "https://pg.trzbyeet.gov.cz/eet/services/EETServiceSOAP/v4"
    const val URL_PRODUKCE = "https://trzbyeet.gov.cz/eet/services/EETServiceSOAP/v4"

    fun send(
        url: String,
        cert: PokladniCertifikat,
        data: TrzbaData,
        prvniZaslani: Boolean,
        timeoutMs: Int,
    ): EetResult {
        val xml = EetXml.signedRequest(
            cert.key, cert.cert, UUID.randomUUID().toString(),
            EetXml.formatDate(EetXml.now()), prvniZaslani, data,
        )
        val bytes = xml.toByteArray(Charsets.UTF_8)
        val conn = URL(url).openConnection() as HttpURLConnection
        return try {
            conn.connectTimeout = timeoutMs
            conn.readTimeout = timeoutMs
            conn.requestMethod = "POST"
            conn.doOutput = true
            conn.setRequestProperty("Content-Type", "text/xml; charset=utf-8")
            conn.setRequestProperty("SOAPAction", "http://fs.gov.cz/eet/OdeslaniTrzby")
            conn.setFixedLengthStreamingMode(bytes.size)
            conn.outputStream.use { it.write(bytes) }
            val code = conn.responseCode
            val stream = if (code in 200..299) conn.inputStream else conn.errorStream
            val text = stream?.use { it.readBytes() }?.toString(Charsets.UTF_8).orEmpty()
            parseResponse(text) ?: EetResult.Nedoruceno("Neplatná odpověď serveru (HTTP $code)")
        } catch (e: java.net.SocketTimeoutException) {
            EetResult.Nedoruceno("Překročena mezní doba odezvy")
        } catch (e: java.io.IOException) {
            EetResult.Nedoruceno("Bez spojení se serverem EET (${e.javaClass.simpleName}: ${e.message})")
        } finally {
            conn.disconnect()
        }
    }

    fun parseResponse(xml: String): EetResult? {
        if (xml.isBlank()) return null
        val doc = runCatching {
            DocumentBuilderFactory.newInstance().apply { isNamespaceAware = true }
                .newDocumentBuilder().parse(ByteArrayInputStream(xml.toByteArray(Charsets.UTF_8)))
        }.getOrNull() ?: return null
        fun first(name: String): Element? =
            doc.getElementsByTagNameNS("*", name).let { if (it.length > 0) it.item(0) as Element else null }

        first("Potvrzeni")?.let { p ->
            val warnings = doc.getElementsByTagNameNS("*", "Varovani").let { list ->
                (0 until list.length).map { i ->
                    val w = list.item(i) as Element
                    "${w.getAttribute("kod_varov")}: ${w.textContent.trim()}"
                }
            }
            return EetResult.Prijato(p.getAttribute("pok"), p.getAttribute("test") == "true", warnings)
        }
        first("Chyba")?.let { c ->
            val kod = c.getAttribute("kod").toIntOrNull() ?: 999
            val text = c.textContent.trim()
            return if (kod < 0) EetResult.Nedoruceno("Dočasná chyba EET ($kod): $text")
            else EetResult.Odmitnuto(kod, text)
        }
        first("Fault")?.let { f ->
            val text = f.getElementsByTagName("faultstring").let { if (it.length > 0) it.item(0).textContent else f.textContent }
            return EetResult.Odmitnuto(-1000, "SOAP chyba: ${text.trim()}")
        }
        return null
    }
}
