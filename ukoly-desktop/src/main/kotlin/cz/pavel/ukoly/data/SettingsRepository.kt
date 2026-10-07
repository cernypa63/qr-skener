package cz.pavel.ukoly.data

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.withContext
import java.io.File
import java.util.Properties

data class AppSettings(
    val host: String = "",
    val port: Int = 21,
    val user: String = "",
    val password: String = "",
    val remotePath: String = DEFAULT_PATH,
    val useFtps: Boolean = false,
    val refreshMinutes: Int = 5
) {
    val ftpConfigured: Boolean get() = host.isNotBlank()

    companion object {
        const val DEFAULT_PATH = "ukoly.json"
    }
}

/** Per-user data folder: %APPDATA%\Ukoly on Windows, ~/.ukoly elsewhere. */
object AppDirs {
    val data: File by lazy {
        val appData = System.getenv("APPDATA")
        val dir = if (!appData.isNullOrBlank()) File(appData, "Ukoly") else File(System.getProperty("user.home"), ".ukoly")
        dir.apply { mkdirs() }
    }
}

class SettingsRepository(private val file: File = File(AppDirs.data, "settings.properties")) {

    private val props = Properties().apply {
        if (file.exists()) file.reader(Charsets.UTF_8).use { load(it) }
    }

    private val _settings = MutableStateFlow(readSettings())
    val settings: StateFlow<AppSettings> = _settings.asStateFlow()

    private val _compactList = MutableStateFlow(props.getProperty(COMPACT)?.toBoolean() ?: false)
    val compactList: StateFlow<Boolean> = _compactList.asStateFlow()

    suspend fun setCompactList(value: Boolean) {
        props.setProperty(COMPACT, value.toString())
        persist()
        _compactList.value = value
    }

    suspend fun save(settings: AppSettings) {
        props.setProperty(HOST, settings.host.trim())
        props.setProperty(PORT, settings.port.toString())
        props.setProperty(USER, settings.user.trim())
        props.setProperty(PASSWORD, settings.password)
        props.setProperty(PATH, settings.remotePath.trim().ifBlank { AppSettings.DEFAULT_PATH })
        props.setProperty(FTPS, settings.useFtps.toString())
        props.setProperty(REFRESH, settings.refreshMinutes.toString())
        persist()
        _settings.value = readSettings()
    }

    private fun readSettings() = AppSettings(
        host = props.getProperty(HOST, ""),
        port = props.getProperty(PORT)?.toIntOrNull() ?: 21,
        user = props.getProperty(USER, ""),
        password = props.getProperty(PASSWORD, ""),
        remotePath = props.getProperty(PATH, AppSettings.DEFAULT_PATH),
        useFtps = props.getProperty(FTPS)?.toBoolean() ?: false,
        refreshMinutes = props.getProperty(REFRESH)?.toIntOrNull() ?: 5
    )

    private suspend fun persist() = withContext(Dispatchers.IO) {
        file.parentFile?.mkdirs()
        file.writer(Charsets.UTF_8).use { props.store(it, "Úkoly – nastavení") }
    }

    private companion object {
        const val HOST = "ftp_host"
        const val PORT = "ftp_port"
        const val USER = "ftp_user"
        const val PASSWORD = "ftp_password"
        const val PATH = "ftp_path"
        const val FTPS = "ftp_ftps"
        const val REFRESH = "refresh_minutes"
        const val COMPACT = "compact_list"
    }
}
