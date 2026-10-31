package cz.pavel.eet2.data

import cz.pavel.eet2.eet.TrzbaData
import org.json.JSONArray
import org.json.JSONObject

data class Firma(
    val id: String,
    val nazev: String,
    val dic: String,
    val idProvozovny: String,
    val idPokladny: String,
    val certSoubor: String,
    val heslo: String,
)

data class FtpNastaveni(
    val host: String = "",
    val port: Int = 21,
    val uzivatel: String = "",
    val heslo: String = "",
    val slozka: String = "",
    val ftps: Boolean = false,
    val nazevSouboru: String = "eet",
)

data class Nastaveni(
    val firmy: List<Firma> = emptyList(),
    val vybranaFirmaId: String? = null,
    val ostryProvoz: Boolean = false,
    val mezniDobaS: Int = 5,
    val ftp: FtpNastaveni = FtpNastaveni(),
) {
    val vybranaFirma: Firma? get() = firmy.firstOrNull { it.id == vybranaFirmaId } ?: firmy.firstOrNull()
}

/** Tržba, na kterou nepřišla odpověď – musí se doposlat (prvni_zaslani=false). */
data class NeodeslanaTrzba(
    val firmaId: String,
    val nazevFirmy: String,
    val data: TrzbaData,
    val ostryProvoz: Boolean,
)

/** Řádek čekající na zápis do souboru na FTP. */
data class ZaznamFtp(val soubor: String, val radek: String)

/** Převod do/z JSON; hesla se ukládají zašifrovaná pomocí [sifruj]/[desifruj]. */
class ModelJson(
    private val sifruj: (String) -> String = { it },
    private val desifruj: (String) -> String = { it },
) {
    fun nastaveniToJson(n: Nastaveni): String = JSONObject().apply {
        put("firmy", JSONArray().apply {
            n.firmy.forEach { f ->
                put(JSONObject().apply {
                    put("id", f.id); put("nazev", f.nazev); put("dic", f.dic)
                    put("idProvozovny", f.idProvozovny); put("idPokladny", f.idPokladny)
                    put("certSoubor", f.certSoubor); put("heslo", sifruj(f.heslo))
                })
            }
        })
        put("vybranaFirmaId", n.vybranaFirmaId ?: "")
        put("ostryProvoz", n.ostryProvoz)
        put("mezniDobaS", n.mezniDobaS)
        put("ftp", JSONObject().apply {
            put("host", n.ftp.host); put("port", n.ftp.port); put("uzivatel", n.ftp.uzivatel)
            put("heslo", sifruj(n.ftp.heslo)); put("slozka", n.ftp.slozka); put("ftps", n.ftp.ftps)
            put("nazevSouboru", n.ftp.nazevSouboru)
        })
    }.toString()

    fun nastaveniFromJson(text: String): Nastaveni {
        val o = JSONObject(text)
        val firmy = o.optJSONArray("firmy") ?: JSONArray()
        val ftp = o.optJSONObject("ftp") ?: JSONObject()
        return Nastaveni(
            firmy = (0 until firmy.length()).map { i ->
                val f = firmy.getJSONObject(i)
                Firma(
                    f.getString("id"), f.optString("nazev"), f.optString("dic"),
                    f.optString("idProvozovny"), f.optString("idPokladny"),
                    f.optString("certSoubor"), desifruj(f.optString("heslo")),
                )
            },
            vybranaFirmaId = o.optString("vybranaFirmaId").ifBlank { null },
            ostryProvoz = o.optBoolean("ostryProvoz", false),
            mezniDobaS = o.optInt("mezniDobaS", 5),
            ftp = FtpNastaveni(
                host = ftp.optString("host"), port = ftp.optInt("port", 21),
                uzivatel = ftp.optString("uzivatel"), heslo = desifruj(ftp.optString("heslo")),
                slozka = ftp.optString("slozka"), ftps = ftp.optBoolean("ftps", false),
                nazevSouboru = ftp.optString("nazevSouboru", "eet").ifBlank { "eet" },
            ),
        )
    }

    fun neodeslaneToJson(list: List<NeodeslanaTrzba>): String = JSONArray().apply {
        list.forEach { t ->
            put(JSONObject().apply {
                put("firmaId", t.firmaId); put("nazevFirmy", t.nazevFirmy); put("ostryProvoz", t.ostryProvoz)
                put("dic", t.data.dic); put("idProvozovny", t.data.idProvozovny); put("idPokladny", t.data.idPokladny)
                put("poradCis", t.data.poradCis); put("datTrzby", t.data.datTrzby); put("castka", t.data.castka)
            })
        }
    }.toString()

    fun neodeslaneFromJson(text: String): List<NeodeslanaTrzba> {
        val a = JSONArray(text)
        return (0 until a.length()).map { i ->
            val o = a.getJSONObject(i)
            NeodeslanaTrzba(
                o.getString("firmaId"), o.optString("nazevFirmy"),
                TrzbaData(
                    o.getString("dic"), o.getString("idProvozovny"), o.getString("idPokladny"),
                    o.getString("poradCis"), o.getString("datTrzby"), o.getString("castka"),
                ),
                o.optBoolean("ostryProvoz", false),
            )
        }
    }

    fun ftpToJson(list: List<ZaznamFtp>): String = JSONArray().apply {
        list.forEach { put(JSONObject().put("soubor", it.soubor).put("radek", it.radek)) }
    }.toString()

    fun ftpFromJson(text: String): List<ZaznamFtp> {
        val a = JSONArray(text)
        return (0 until a.length()).map { a.getJSONObject(it).let { o -> ZaznamFtp(o.getString("soubor"), o.getString("radek")) } }
    }
}
