package cz.pavel.eet2.eet

import java.io.ByteArrayInputStream
import java.security.KeyStore
import java.security.PrivateKey
import java.security.cert.X509Certificate
import java.util.Date

class PokladniCertifikat(val key: PrivateKey, val cert: X509Certificate) {
    /** DIČ z položky CN certifikátu (např. CZ683555118). */
    val dic: String
        get() = Regex("CN=([^,]+)").find(cert.subjectX500Principal.name)?.groupValues?.get(1).orEmpty()

    val platnostDo: Date get() = cert.notAfter

    companion object {
        /** Načte PKCS#12 (.p12). Vyhodí výjimku při chybném heslu nebo souboru. */
        fun load(p12: ByteArray, heslo: String): PokladniCertifikat {
            val ks = KeyStore.getInstance("PKCS12")
            ks.load(ByteArrayInputStream(p12), heslo.toCharArray())
            for (alias in ks.aliases()) {
                if (!ks.isKeyEntry(alias)) continue
                val key = ks.getKey(alias, heslo.toCharArray()) as? PrivateKey ?: continue
                val cert = ks.getCertificate(alias) as? X509Certificate ?: continue
                return PokladniCertifikat(key, cert)
            }
            throw IllegalArgumentException("Soubor neobsahuje certifikát se soukromým klíčem")
        }
    }
}
