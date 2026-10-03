package cz.pavel.ukoly.data

import android.content.Context
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.booleanPreferencesKey
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.intPreferencesKey
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map

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

private val Context.dataStore: DataStore<Preferences> by preferencesDataStore(name = "settings")

class SettingsRepository(private val context: Context) {

    val settings: Flow<AppSettings> = context.dataStore.data.map { prefs ->
        AppSettings(
            host = prefs[HOST] ?: "",
            port = prefs[PORT] ?: 21,
            user = prefs[USER] ?: "",
            password = prefs[PASSWORD] ?: "",
            remotePath = prefs[PATH] ?: AppSettings.DEFAULT_PATH,
            useFtps = prefs[FTPS] ?: false,
            refreshMinutes = prefs[REFRESH] ?: 5
        )
    }

    val compactList: Flow<Boolean> = context.dataStore.data.map { prefs -> prefs[COMPACT] ?: false }

    suspend fun setCompactList(value: Boolean) {
        context.dataStore.edit { prefs -> prefs[COMPACT] = value }
    }

    suspend fun save(settings: AppSettings) {
        context.dataStore.edit { prefs ->
            prefs[HOST] = settings.host.trim()
            prefs[PORT] = settings.port
            prefs[USER] = settings.user.trim()
            prefs[PASSWORD] = settings.password
            prefs[PATH] = settings.remotePath.trim().ifBlank { AppSettings.DEFAULT_PATH }
            prefs[FTPS] = settings.useFtps
            prefs[REFRESH] = settings.refreshMinutes
        }
    }

    private companion object {
        val HOST = stringPreferencesKey("ftp_host")
        val PORT = intPreferencesKey("ftp_port")
        val USER = stringPreferencesKey("ftp_user")
        val PASSWORD = stringPreferencesKey("ftp_password")
        val PATH = stringPreferencesKey("ftp_path")
        val FTPS = booleanPreferencesKey("ftp_ftps")
        val REFRESH = intPreferencesKey("refresh_minutes")
        val COMPACT = booleanPreferencesKey("compact_list")
    }
}
