package cz.pavel.ukoly.data

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import org.apache.commons.net.ftp.FTP
import org.apache.commons.net.ftp.FTPClient
import org.apache.commons.net.ftp.FTPReply
import org.apache.commons.net.ftp.FTPSClient
import java.io.ByteArrayInputStream
import java.io.ByteArrayOutputStream
import java.io.File
import java.io.IOException
import java.time.Duration

interface TaskStorage {
    /** Returns the file content, or null when the file does not exist yet. */
    suspend fun load(): String?

    suspend fun save(content: String)
}

class LocalTaskStorage(private val file: File) : TaskStorage {
    override suspend fun load(): String? = withContext(Dispatchers.IO) {
        if (file.exists()) file.readText(Charsets.UTF_8) else null
    }

    override suspend fun save(content: String) = withContext(Dispatchers.IO) {
        file.writeText(content, Charsets.UTF_8)
    }
}

class FtpTaskStorage(private val settings: AppSettings) : TaskStorage {

    private val directory = settings.remotePath.trim().substringBeforeLast('/', "")
        .let { if (it.isEmpty() && settings.remotePath.trim().startsWith("/")) "/" else it }
    private val fileName = settings.remotePath.trim().substringAfterLast('/').ifBlank { AppSettings.DEFAULT_PATH }

    override suspend fun load(): String? = withContext(Dispatchers.IO) {
        session { client ->
            if (directory.isNotEmpty() && !client.changeWorkingDirectory(directory)) return@session null
            val out = ByteArrayOutputStream()
            when {
                client.retrieveFile(fileName, out) -> out.toString(Charsets.UTF_8.name())
                client.replyCode == FTPReply.FILE_UNAVAILABLE -> null
                else -> throw IOException("Soubor nelze načíst: ${client.replyString.trim()}")
            }
        }
    }

    override suspend fun save(content: String) = withContext(Dispatchers.IO) {
        session { client ->
            if (directory.isNotEmpty()) ensureDirectory(client, directory)
            val bytes = content.toByteArray(Charsets.UTF_8)
            val tmpName = "$fileName.tmp"
            if (!client.storeFile(tmpName, ByteArrayInputStream(bytes))) {
                throw IOException("Soubor nelze uložit: ${client.replyString.trim()}")
            }
            if (client.rename(tmpName, fileName)) return@session
            client.deleteFile(fileName)
            if (client.rename(tmpName, fileName)) return@session
            if (!client.storeFile(fileName, ByteArrayInputStream(bytes))) {
                throw IOException("Soubor nelze uložit: ${client.replyString.trim()}")
            }
            client.deleteFile(tmpName)
        }
    }

    private fun ensureDirectory(client: FTPClient, path: String) {
        if (client.changeWorkingDirectory(path)) return
        if (path.startsWith("/")) client.changeWorkingDirectory("/")
        path.split('/').filter { it.isNotBlank() }.forEach { segment ->
            if (!client.changeWorkingDirectory(segment)) {
                client.makeDirectory(segment)
                if (!client.changeWorkingDirectory(segment)) {
                    throw IOException("Složku $path nelze na FTP vytvořit: ${client.replyString.trim()}")
                }
            }
        }
    }

    private fun <T> session(block: (FTPClient) -> T): T {
        val client = if (settings.useFtps) FTPSClient() else FTPClient()
        client.controlEncoding = Charsets.UTF_8.name()
        client.connectTimeout = TIMEOUT_MS
        client.defaultTimeout = TIMEOUT_MS
        client.setDataTimeout(Duration.ofMillis(TIMEOUT_MS.toLong()))
        try {
            client.connect(settings.host.trim(), settings.port)
            if (!FTPReply.isPositiveCompletion(client.replyCode)) {
                throw IOException("FTP server odmítl spojení: ${client.replyString.trim()}")
            }
            client.soTimeout = TIMEOUT_MS
            val user = settings.user.ifBlank { "anonymous" }
            if (!client.login(user, settings.password)) {
                throw IOException("Přihlášení na FTP se nezdařilo")
            }
            if (client is FTPSClient) {
                client.execPBSZ(0)
                client.execPROT("P")
            }
            client.enterLocalPassiveMode()
            client.setFileType(FTP.BINARY_FILE_TYPE)
            val result = block(client)
            runCatching { client.logout() }
            return result
        } finally {
            runCatching { if (client.isConnected) client.disconnect() }
        }
    }

    private companion object {
        const val TIMEOUT_MS = 15_000
    }
}
