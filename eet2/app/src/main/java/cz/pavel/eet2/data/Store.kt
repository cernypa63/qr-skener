package cz.pavel.eet2.data

import android.content.Context
import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import android.util.Base64
import java.io.File
import java.security.KeyStore
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec

/** Ukládání do soukromé složky aplikace; hesla šifrovaná klíčem v Android Keystore. */
class Store(context: Context) {
    private val dir = context.filesDir
    val certDir = File(dir, "certifikaty").apply { mkdirs() }
    private val json = ModelJson(::sifruj, ::desifruj)

    private fun soubor(name: String) = File(dir, name)

    private fun zapis(name: String, text: String) {
        val tmp = soubor("$name.tmp")
        tmp.writeText(text)
        tmp.renameTo(soubor(name))
    }

    fun nactiNastaveni(): Nastaveni =
        runCatching { json.nastaveniFromJson(soubor("nastaveni.json").readText()) }.getOrDefault(Nastaveni())

    fun ulozNastaveni(n: Nastaveni) = zapis("nastaveni.json", json.nastaveniToJson(n))

    fun nactiNeodeslane(): List<NeodeslanaTrzba> =
        runCatching { json.neodeslaneFromJson(soubor("neodeslane.json").readText()) }.getOrDefault(emptyList())

    fun ulozNeodeslane(list: List<NeodeslanaTrzba>) = zapis("neodeslane.json", json.neodeslaneToJson(list))

    fun nactiFtpFrontu(): List<ZaznamFtp> =
        runCatching { json.ftpFromJson(soubor("ftp_fronta.json").readText()) }.getOrDefault(emptyList())

    fun ulozFtpFrontu(list: List<ZaznamFtp>) = zapis("ftp_fronta.json", json.ftpToJson(list))

    fun nactiHesloNastaveni(): String? =
        runCatching { soubor("heslo_nastaveni.txt").readText() }.getOrNull()?.takeIf { it.isNotBlank() }

    fun ulozHesloNastaveni(ulozene: String) = zapis("heslo_nastaveni.txt", ulozene)

    fun certSoubor(name: String) = File(certDir, name)

    private fun klic(): SecretKey {
        val ks = KeyStore.getInstance("AndroidKeyStore").apply { load(null) }
        (ks.getKey(ALIAS, null) as? SecretKey)?.let { return it }
        return KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, "AndroidKeyStore").apply {
            init(
                KeyGenParameterSpec.Builder(ALIAS, KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT)
                    .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                    .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                    .build(),
            )
        }.generateKey()
    }

    private fun sifruj(text: String): String {
        if (text.isEmpty()) return ""
        val c = Cipher.getInstance("AES/GCM/NoPadding").apply { init(Cipher.ENCRYPT_MODE, klic()) }
        val out = c.iv + c.doFinal(text.toByteArray(Charsets.UTF_8))
        return Base64.encodeToString(out, Base64.NO_WRAP)
    }

    private fun desifruj(text: String): String {
        if (text.isEmpty()) return ""
        return runCatching {
            val data = Base64.decode(text, Base64.NO_WRAP)
            val c = Cipher.getInstance("AES/GCM/NoPadding")
            c.init(Cipher.DECRYPT_MODE, klic(), GCMParameterSpec(128, data, 0, 12))
            c.doFinal(data, 12, data.size - 12).toString(Charsets.UTF_8)
        }.getOrDefault("")
    }

    companion object {
        private const val ALIAS = "eet2_hesla"
    }
}
