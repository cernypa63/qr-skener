package cz.pavel.eet2.eet

import java.math.BigDecimal
import java.math.RoundingMode
import java.security.MessageDigest
import java.security.PrivateKey
import java.security.Signature
import java.security.cert.X509Certificate
import java.time.OffsetDateTime
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import java.time.temporal.ChronoUnit
import java.util.Base64

/** Údaje oblasti Data – při opakovaném zaslání se nemění. */
data class TrzbaData(
    val dic: String,
    val idProvozovny: String,
    val idPokladny: String,
    val poradCis: String,
    val datTrzby: String,
    val castka: String,
)

/**
 * Sestavení podepsané SOAP zprávy EET 2.0 (schéma v4).
 * Podepisované části (Body a SignedInfo) se rovnou skládají v kanonickém tvaru
 * (exclusive c14n s PrefixList "v4", resp. "soapenv v4"), takže není potřeba XML knihovna.
 */
object EetXml {
    const val NS_SOAP = "http://schemas.xmlsoap.org/soap/envelope/"
    const val NS_V4 = "http://fs.gov.cz/eet/schema/v4"
    const val NS_WSU = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd"
    const val NS_WSSE = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd"
    const val NS_DS = "http://www.w3.org/2000/09/xmldsig#"
    const val NS_EC = "http://www.w3.org/2001/10/xml-exc-c14n#"
    private const val X509_TYPE = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-x509-token-profile-1.0#X509v3"
    private const val B64_TYPE = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-soap-message-security-1.0#Base64Binary"

    val ZONE: ZoneId = ZoneId.of("Europe/Prague")
    private val DATE_FORMAT = DateTimeFormatter.ofPattern("yyyy-MM-dd'T'HH:mm:ssXXX")

    fun now(): OffsetDateTime = OffsetDateTime.now(ZONE).truncatedTo(ChronoUnit.SECONDS)

    fun formatDate(t: OffsetDateTime): String = DATE_FORMAT.format(t)

    /** "123,5" -> "123.50"; null když text není platná částka. */
    fun parseCastka(text: String): String? {
        val clean = text.trim().replace(" ", "").replace('\u00a0', ' ').replace(" ", "").replace(',', '.')
        if (!Regex("-?\\d{1,8}(\\.\\d{0,2})?").matches(clean)) return null
        val value = BigDecimal(clean).setScale(2, RoundingMode.UNNECESSARY)
        return value.toPlainString()
    }

    private fun esc(s: String) = s.replace("&", "&amp;").replace("<", "&lt;").replace("\"", "&quot;")
        .replace("\t", "&#x9;").replace("\n", "&#xA;").replace("\r", "&#xD;")

    fun canonicalBody(uuid: String, datOdesl: String, prvniZaslani: Boolean, d: TrzbaData): String =
        "<soapenv:Body xmlns:soapenv=\"$NS_SOAP\" xmlns:v4=\"$NS_V4\" xmlns:wsu=\"$NS_WSU\" wsu:Id=\"id-body\">" +
            "<v4:Trzba>" +
            "<v4:Hlavicka dat_odesl=\"${esc(datOdesl)}\" prvni_zaslani=\"$prvniZaslani\" uuid_zpravy=\"${esc(uuid)}\"></v4:Hlavicka>" +
            "<v4:Data celk_trzba=\"${esc(d.castka)}\" dat_trzby=\"${esc(d.datTrzby)}\" eic_popl=\"${esc(d.dic)}\"" +
            " id_jednotky=\"${esc(d.idProvozovny)}\" id_pokl=\"${esc(d.idPokladny)}\" porad_cis=\"${esc(d.poradCis)}\"></v4:Data>" +
            "</v4:Trzba></soapenv:Body>"

    fun canonicalSignedInfo(digestB64: String): String =
        "<ds:SignedInfo xmlns:ds=\"$NS_DS\" xmlns:soapenv=\"$NS_SOAP\" xmlns:v4=\"$NS_V4\">" +
            "<ds:CanonicalizationMethod Algorithm=\"$NS_EC\"><ec:InclusiveNamespaces xmlns:ec=\"$NS_EC\" PrefixList=\"soapenv v4\"></ec:InclusiveNamespaces></ds:CanonicalizationMethod>" +
            "<ds:SignatureMethod Algorithm=\"http://www.w3.org/2001/04/xmldsig-more#rsa-sha256\"></ds:SignatureMethod>" +
            "<ds:Reference URI=\"#id-body\"><ds:Transforms><ds:Transform Algorithm=\"$NS_EC\"><ec:InclusiveNamespaces xmlns:ec=\"$NS_EC\" PrefixList=\"v4\"></ec:InclusiveNamespaces></ds:Transform></ds:Transforms>" +
            "<ds:DigestMethod Algorithm=\"http://www.w3.org/2001/04/xmlenc#sha256\"></ds:DigestMethod>" +
            "<ds:DigestValue>$digestB64</ds:DigestValue></ds:Reference></ds:SignedInfo>"

    fun signedRequest(
        key: PrivateKey,
        cert: X509Certificate,
        uuid: String,
        datOdesl: String,
        prvniZaslani: Boolean,
        data: TrzbaData,
    ): String {
        val b64 = Base64.getEncoder()
        val body = canonicalBody(uuid, datOdesl, prvniZaslani, data)
        val digest = b64.encodeToString(MessageDigest.getInstance("SHA-256").digest(body.toByteArray(Charsets.UTF_8)))
        val signedInfo = canonicalSignedInfo(digest)
        val signature = Signature.getInstance("SHA256withRSA").run {
            initSign(key)
            update(signedInfo.toByteArray(Charsets.UTF_8))
            b64.encodeToString(sign())
        }
        return "<soapenv:Envelope xmlns:soapenv=\"$NS_SOAP\" xmlns:v4=\"$NS_V4\"><soapenv:Header>" +
            "<wsse:Security xmlns:wsse=\"$NS_WSSE\" xmlns:wsu=\"$NS_WSU\">" +
            "<wsse:BinarySecurityToken EncodingType=\"$B64_TYPE\" ValueType=\"$X509_TYPE\" wsu:Id=\"X509-1\">" +
            b64.encodeToString(cert.encoded) + "</wsse:BinarySecurityToken>" +
            "<ds:Signature xmlns:ds=\"$NS_DS\">" + signedInfo +
            "<ds:SignatureValue>$signature</ds:SignatureValue>" +
            "<ds:KeyInfo><wsse:SecurityTokenReference><wsse:Reference URI=\"#X509-1\" ValueType=\"$X509_TYPE\"/></wsse:SecurityTokenReference></ds:KeyInfo>" +
            "</ds:Signature></wsse:Security></soapenv:Header>" + body + "</soapenv:Envelope>"
    }
}
