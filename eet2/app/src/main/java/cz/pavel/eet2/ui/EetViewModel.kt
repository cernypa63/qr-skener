package cz.pavel.eet2.ui

import android.app.Application
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import cz.pavel.eet2.data.CsvLog
import cz.pavel.eet2.data.Firma
import cz.pavel.eet2.data.FtpAppender
import cz.pavel.eet2.data.FtpNastaveni
import cz.pavel.eet2.data.Nastaveni
import cz.pavel.eet2.data.NeodeslanaTrzba
import cz.pavel.eet2.data.Store
import cz.pavel.eet2.data.ZaznamFtp
import cz.pavel.eet2.eet.EetClient
import cz.pavel.eet2.eet.EetResult
import cz.pavel.eet2.eet.EetXml
import cz.pavel.eet2.eet.PokladniCertifikat
import cz.pavel.eet2.eet.TrzbaData
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.withContext
import java.time.format.DateTimeFormatter

enum class Obrazovka { Pokladna, Nastaveni }

enum class Druh { Ok, Chyba, Doposlat }

data class Vysledek(val druh: Druh, val titulek: String, val text: String)

class EetViewModel(app: Application) : AndroidViewModel(app) {
    private val store = Store(app)
    private val mutex = Mutex()
    private val certCache = mutableMapOf<String, PokladniCertifikat>()

    var nastaveni by mutableStateOf(store.nactiNastaveni()); private set
    var obrazovka by mutableStateOf(Obrazovka.Pokladna)
    var castka by mutableStateOf("")
    var odesilani by mutableStateOf(false); private set
    var vysledek by mutableStateOf<Vysledek?>(null); private set
    var pocetNeodeslanych by mutableIntStateOf(store.nactiNeodeslane().size); private set
    var pocetFtpFronta by mutableIntStateOf(store.nactiFtpFrontu().size); private set
    var hlaskaFtp by mutableStateOf<String?>(null); private set

    init {
        viewModelScope.launch {
            while (true) {
                if (!odesilani) mutex.withLock { withContext(Dispatchers.IO) { doposlatInterne() } }
                delay(60_000)
            }
        }
    }

    private val main = android.os.Handler(android.os.Looper.getMainLooper())
    private fun naMain(block: () -> Unit) { main.post(block) }

    private fun url(ostry: Boolean) = if (ostry) EetClient.URL_PRODUKCE else EetClient.URL_PLAYGROUND

    fun vyberFirmu(id: String) {
        ulozNastaveni(nastaveni.copy(vybranaFirmaId = id))
    }

    fun ulozNastaveni(n: Nastaveni) {
        nastaveni = n
        certCache.clear()
        store.ulozNastaveni(n)
    }

    fun certSoubor(firma: Firma) = store.certSoubor(firma.certSoubor)

    /** Uloží firmu i s nově naimportovaným certifikátem. */
    fun ulozFirmu(firma: Firma, novyCert: ByteArray?) {
        var f = firma
        if (novyCert != null) {
            val name = "${firma.id}_${System.currentTimeMillis()}.p12"
            store.certSoubor(name).writeBytes(novyCert)
            if (firma.certSoubor.isNotBlank()) store.certSoubor(firma.certSoubor).delete()
            f = f.copy(certSoubor = name)
        }
        val firmy = nastaveni.firmy.filter { it.id != f.id } + f
        val poradi = nastaveni.firmy.indexOfFirst { it.id == f.id }
        val serazene = if (poradi < 0) firmy else nastaveni.firmy.map { if (it.id == f.id) f else it }
        ulozNastaveni(nastaveni.copy(firmy = serazene, vybranaFirmaId = nastaveni.vybranaFirmaId ?: f.id))
    }

    fun smazFirmu(firma: Firma) {
        if (firma.certSoubor.isNotBlank()) store.certSoubor(firma.certSoubor).delete()
        val firmy = nastaveni.firmy.filter { it.id != firma.id }
        ulozNastaveni(
            nastaveni.copy(
                firmy = firmy,
                vybranaFirmaId = if (nastaveni.vybranaFirmaId == firma.id) firmy.firstOrNull()?.id else nastaveni.vybranaFirmaId,
            ),
        )
    }

    fun vyzkousetFtp(ftp: FtpNastaveni, hotovo: (String) -> Unit) {
        viewModelScope.launch {
            val r = withContext(Dispatchers.IO) { FtpAppender.vyzkouset(ftp) }
            hotovo(r.exceptionOrNull()?.message ?: "Připojení k FTP je v pořádku")
        }
    }

    private fun certifikat(firma: Firma): PokladniCertifikat =
        certCache.getOrPut(firma.id) {
            PokladniCertifikat.load(store.certSoubor(firma.certSoubor).readBytes(), firma.heslo)
        }

    fun zaevidovat() {
        if (odesilani) return
        val n = nastaveni
        val firma = n.vybranaFirma
        if (firma == null) {
            vysledek = Vysledek(Druh.Chyba, "Chybí firma", "V Nastavení nejprve přidejte firmu a její certifikát.")
            return
        }
        val hodnota = EetXml.parseCastka(castka)
        if (hodnota == null || hodnota == "0.00") {
            vysledek = Vysledek(Druh.Chyba, "Neplatná částka", "Zadejte částku, např. 125 nebo 125,50.")
            return
        }
        odesilani = true
        viewModelScope.launch {
            vysledek = mutex.withLock { withContext(Dispatchers.IO) { odeslatNovou(n, firma, hodnota) } }
            odesilani = false
        }
    }

    fun potvrditVysledek() {
        vysledek = null
        castka = ""
    }

    private fun odeslatNovou(n: Nastaveni, firma: Firma, hodnota: String): Vysledek {
        val ted = EetXml.now()
        val datum = EetXml.formatDate(ted)
        val castkaText = hodnota.replace('.', ',') + " Kč"
        val data = TrzbaData(
            firma.dic, firma.idProvozovny, firma.idPokladny,
            DateTimeFormatter.ofPattern("yyMMddHHmmss").format(ted), datum, hodnota,
        )
        val ftpNovy = mutableListOf<ZaznamFtp>()
        val soubor = CsvLog.nazevSouboru(n.ftp.nazevSouboru, ted)
        val cert = runCatching { certifikat(firma) }.getOrElse { e ->
            val chyba = "Certifikát nelze načíst: ${e.message}"
            ftpNovy += ZaznamFtp(soubor, CsvLog.radek(firma.dic, hodnota, "", datum, chyba, firma.nazev, data.poradCis, "CHYBA", n.ostryProvoz))
            val ftp = zapsatFtp(n.ftp, ftpNovy)
            return Vysledek(Druh.Chyba, "Tržba NEBYLA zaevidována", "${firma.nazev}\n$castkaText\n\n$chyba$ftp")
        }
        val r = EetClient.send(url(n.ostryProvoz), cert, data, true, n.mezniDobaS.coerceAtLeast(2) * 1000)
        val vysledek = when (r) {
            is EetResult.Prijato -> {
                ftpNovy += ZaznamFtp(soubor, CsvLog.radek(firma.dic, hodnota, r.pok, datum, r.varovani.joinToString(" | "), firma.nazev, data.poradCis, "OK", n.ostryProvoz))
                val varovani = if (r.varovani.isEmpty()) "" else "\n\nVarování: " + r.varovani.joinToString("; ")
                val test = if (r.test || !n.ostryProvoz) "\n(testovací režim – tržba není evidována naostro)" else ""
                Vysledek(Druh.Ok, "Tržba zaevidována", "${firma.nazev}\n$castkaText\n\nPOK: ${r.pok}$test$varovani")
            }
            is EetResult.Odmitnuto -> {
                val chyba = "Chyba ${r.kod}: ${r.zprava}"
                ftpNovy += ZaznamFtp(soubor, CsvLog.radek(firma.dic, hodnota, "", datum, chyba, firma.nazev, data.poradCis, "CHYBA", n.ostryProvoz))
                Vysledek(Druh.Chyba, "Tržba NEBYLA zaevidována", "${firma.nazev}\n$castkaText\n\n$chyba")
            }
            is EetResult.Nedoruceno -> {
                store.ulozNeodeslane(store.nactiNeodeslane() + NeodeslanaTrzba(firma.id, firma.nazev, data, n.ostryProvoz))
                val pocet = store.nactiNeodeslane().size
                naMain { pocetNeodeslanych = pocet }
                ftpNovy += ZaznamFtp(soubor, CsvLog.radek(firma.dic, hodnota, "", datum, r.zprava, firma.nazev, data.poradCis, "NEODESLANO", n.ostryProvoz))
                Vysledek(
                    Druh.Doposlat, "Tržba zatím NEODESLÁNA",
                    "${firma.nazev}\n$castkaText\n\n${r.zprava}\n\nAplikace tržbu doposílá automaticky, jakmile bude spojení (zákonná lhůta 48 hodin). Nechte aplikaci zapnutou.",
                )
            }
        }
        val ftp = zapsatFtp(n.ftp, ftpNovy)
        return vysledek.copy(text = vysledek.text + ftp)
    }

    /** Zapíše čekající + nové řádky na FTP; vrací text pro obsluhu (prázdný při úspěchu). */
    private fun zapsatFtp(ftp: FtpNastaveni, nove: List<ZaznamFtp>): String {
        val vse = store.nactiFtpFrontu() + nove
        store.ulozFtpFrontu(vse)
        val (zbyva, chyba) = FtpAppender.pripsat(ftp, vse)
        store.ulozFtpFrontu(zbyva)
        naMain { pocetFtpFronta = zbyva.size; hlaskaFtp = chyba }
        return if (chyba == null) "" else "\n\nVýsledek se NEPODAŘILO uložit na FTP ($chyba). Uloží se později."
    }

    private fun doposlatInterne() {
        val n = nastaveni
        val fronta = store.nactiNeodeslane().toMutableList()
        val ftpNove = mutableListOf<ZaznamFtp>()
        for (t in fronta.toList()) {
            val firma = n.firmy.firstOrNull { it.id == t.firmaId } ?: continue
            val cert = runCatching { certifikat(firma) }.getOrNull() ?: continue
            val r = EetClient.send(url(t.ostryProvoz), cert, t.data, false, n.mezniDobaS.coerceAtLeast(2) * 1000)
            if (r is EetResult.Nedoruceno) break
            val soubor = CsvLog.nazevSouboru(n.ftp.nazevSouboru, EetXml.now())
            val radek = when (r) {
                is EetResult.Prijato -> CsvLog.radek(t.data.dic, t.data.castka, r.pok, t.data.datTrzby, r.varovani.joinToString(" | "), t.nazevFirmy, t.data.poradCis, "DOPOSLANO", t.ostryProvoz)
                is EetResult.Odmitnuto -> CsvLog.radek(t.data.dic, t.data.castka, "", EetXml.formatDate(EetXml.now()), "Chyba ${r.kod}: ${r.zprava}", t.nazevFirmy, t.data.poradCis, "CHYBA", t.ostryProvoz)
                else -> continue
            }
            ftpNove += ZaznamFtp(soubor, radek)
            fronta.remove(t)
            store.ulozNeodeslane(fronta)
        }
        naMain { pocetNeodeslanych = fronta.size }
        if (ftpNove.isNotEmpty() || store.nactiFtpFrontu().isNotEmpty()) zapsatFtp(n.ftp, ftpNove)
    }
}
