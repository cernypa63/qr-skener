package cz.pavel.eet2.data

import org.apache.commons.net.ftp.FTP
import org.apache.commons.net.ftp.FTPClient
import org.apache.commons.net.ftp.FTPSClient
import java.io.ByteArrayInputStream

object FtpAppender {

    /** Připojí se a přihlásí; vrací chybovou hlášku nebo null. */
    private fun pripoj(client: FTPClient, s: FtpNastaveni): String? {
        client.connectTimeout = 10_000
        client.defaultTimeout = 10_000
        client.connect(s.host.trim(), s.port)
        client.soTimeout = 10_000
        if (!client.login(s.uzivatel, s.heslo)) return "Přihlášení na FTP se nezdařilo"
        if (client is FTPSClient) {
            client.execPBSZ(0)
            client.execPROT("P")
        }
        client.enterLocalPassiveMode()
        client.setFileType(FTP.BINARY_FILE_TYPE)
        client.controlEncoding = "UTF-8"
        if (s.slozka.isNotBlank() && !client.changeWorkingDirectory(s.slozka.trim())) {
            return "Složka ${s.slozka} na FTP neexistuje"
        }
        return null
    }

    private fun <T> sKlientem(s: FtpNastaveni, akce: (FTPClient) -> T): Result<T> {
        if (s.host.isBlank()) return Result.failure(IllegalStateException("FTP server není nastaven"))
        val client = if (s.ftps) FTPSClient() else FTPClient()
        return try {
            pripoj(client, s)?.let { return Result.failure(IllegalStateException(it)) }
            val r = akce(client)
            runCatching { client.logout() }
            Result.success(r)
        } catch (e: Exception) {
            Result.failure(IllegalStateException("Chyba FTP: ${e.message ?: e.javaClass.simpleName}"))
        } finally {
            runCatching { if (client.isConnected) client.disconnect() }
        }
    }

    fun vyzkouset(s: FtpNastaveni): Result<Unit> = sKlientem(s) { }

    /**
     * Připíše řádky na konec denních souborů (nový soubor dostane hlavičku).
     * Vrací řádky, které se zapsat nepodařilo, a případnou chybu.
     */
    fun pripsat(s: FtpNastaveni, zaznamy: List<ZaznamFtp>): Pair<List<ZaznamFtp>, String?> {
        if (zaznamy.isEmpty()) return emptyList<ZaznamFtp>() to null
        val zbyva = zaznamy.toMutableList()
        val r = sKlientem(s) { client ->
            for ((soubor, skupina) in zaznamy.groupBy { it.soubor }) {
                val existuje = client.listNames(soubor)?.isNotEmpty() == true
                val text = buildString {
                    if (!existuje) append(CsvLog.HLAVICKA).append("\r\n")
                    skupina.forEach { append(it.radek).append("\r\n") }
                }
                val ok = ByteArrayInputStream(text.toByteArray(Charsets.UTF_8)).use { client.appendFile(soubor, it) }
                if (!ok) throw IllegalStateException("soubor $soubor nelze zapsat: ${client.replyString.trim()}")
                zbyva.removeAll(skupina)
            }
        }
        return zbyva to r.exceptionOrNull()?.message
    }
}
