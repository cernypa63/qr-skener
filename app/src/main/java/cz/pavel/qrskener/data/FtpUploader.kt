package cz.pavel.qrskener.data

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import org.apache.commons.net.ftp.FTP
import org.apache.commons.net.ftp.FTPClient
import org.apache.commons.net.ftp.FTPSClient
import java.io.ByteArrayInputStream

sealed interface UploadResult {
    data class Success(val remotePath: String) : UploadResult
    data class Failure(val message: String) : UploadResult
}

class FtpUploader {

    suspend fun upload(settings: FtpSettings, fileName: String, content: String): UploadResult =
        withContext(Dispatchers.IO) {
            if (settings.host.isBlank()) {
                return@withContext UploadResult.Failure("FTP server není nastaven")
            }
            val client = if (settings.useFtps) FTPSClient() else FTPClient()
            try {
                client.connect(settings.host, settings.port)
                if (!client.login(settings.user, settings.password)) {
                    return@withContext UploadResult.Failure("Přihlášení na FTP se nezdařilo")
                }
                client.enterLocalPassiveMode()
                client.setFileType(FTP.BINARY_FILE_TYPE)
                if (settings.directory.isNotBlank() && !client.changeWorkingDirectory(settings.directory)) {
                    return@withContext UploadResult.Failure("Složka ${settings.directory} na FTP neexistuje")
                }
                val stored = ByteArrayInputStream(content.toByteArray(Charsets.UTF_8)).use { stream ->
                    client.storeFile(fileName, stream)
                }
                if (!stored) {
                    return@withContext UploadResult.Failure("Soubor se nepodařilo uložit: ${client.replyString.trim()}")
                }
                client.logout()
                UploadResult.Success(listOf(settings.directory.trimEnd('/'), fileName).filter { it.isNotBlank() }.joinToString("/"))
            } catch (e: Exception) {
                UploadResult.Failure(e.message ?: e.javaClass.simpleName)
            } finally {
                runCatching { if (client.isConnected) client.disconnect() }
            }
        }
}
