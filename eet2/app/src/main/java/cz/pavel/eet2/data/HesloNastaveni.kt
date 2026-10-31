package cz.pavel.eet2.data

import java.security.MessageDigest
import java.security.SecureRandom
import java.util.Base64
import javax.crypto.SecretKeyFactory
import javax.crypto.spec.PBEKeySpec

/** Heslo k Nastavení – ukládá se jen sůl a otisk (PBKDF2), ne heslo samotné. */
object HesloNastaveni {
    private const val ITERACE = 20_000

    private fun otisk(heslo: String, sul: ByteArray): ByteArray =
        SecretKeyFactory.getInstance("PBKDF2WithHmacSHA256")
            .generateSecret(PBEKeySpec(heslo.toCharArray(), sul, ITERACE, 256)).encoded

    fun vytvor(heslo: String): String {
        val sul = ByteArray(16).also { SecureRandom().nextBytes(it) }
        val b64 = Base64.getEncoder()
        return b64.encodeToString(sul) + ":" + b64.encodeToString(otisk(heslo, sul))
    }

    fun over(ulozene: String, heslo: String): Boolean {
        val casti = ulozene.split(":")
        if (casti.size != 2) return false
        val b64 = Base64.getDecoder()
        return runCatching {
            MessageDigest.isEqual(b64.decode(casti[1]), otisk(heslo, b64.decode(casti[0])))
        }.getOrDefault(false)
    }
}
