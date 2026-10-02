package cz.pavel.qrskener.data

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

data class FieldPosition(
    val start: Int,
    val length: Int
)

data class FtpSettings(
    val host: String = "",
    val port: Int = 21,
    val user: String = "",
    val password: String = "",
    val directory: String = "",
    val useFtps: Boolean = false
)

data class ScannerSettings(
    val soundEnabled: Boolean = true,
    val vibrationEnabled: Boolean = true,
    val autoCopy: Boolean = false,
    val documentPosition: FieldPosition = FieldPosition(start = 1, length = 10),
    val amountPosition: FieldPosition = FieldPosition(start = 11, length = 10),
    val ftp: FtpSettings = FtpSettings()
)

private val Context.dataStore: DataStore<Preferences> by preferencesDataStore(name = "settings")

class SettingsRepository(private val context: Context) {

    val settings: Flow<ScannerSettings> = context.dataStore.data.map { prefs ->
        ScannerSettings(
            soundEnabled = prefs[SOUND] ?: true,
            vibrationEnabled = prefs[VIBRATION] ?: true,
            autoCopy = prefs[AUTO_COPY] ?: false,
            documentPosition = FieldPosition(
                start = prefs[DOC_START] ?: 1,
                length = prefs[DOC_LENGTH] ?: 10
            ),
            amountPosition = FieldPosition(
                start = prefs[AMOUNT_START] ?: 11,
                length = prefs[AMOUNT_LENGTH] ?: 10
            ),
            ftp = FtpSettings(
                host = prefs[FTP_HOST].orEmpty(),
                port = prefs[FTP_PORT] ?: 21,
                user = prefs[FTP_USER].orEmpty(),
                password = prefs[FTP_PASSWORD].orEmpty(),
                directory = prefs[FTP_DIR].orEmpty(),
                useFtps = prefs[FTP_USE_FTPS] ?: false
            )
        )
    }

    suspend fun setSoundEnabled(value: Boolean) = put(SOUND, value)

    suspend fun setVibrationEnabled(value: Boolean) = put(VIBRATION, value)

    suspend fun setAutoCopy(value: Boolean) = put(AUTO_COPY, value)

    suspend fun setDocumentPosition(start: Int, length: Int) {
        context.dataStore.edit { prefs ->
            prefs[DOC_START] = start
            prefs[DOC_LENGTH] = length
        }
    }

    suspend fun setAmountPosition(start: Int, length: Int) {
        context.dataStore.edit { prefs ->
            prefs[AMOUNT_START] = start
            prefs[AMOUNT_LENGTH] = length
        }
    }

    suspend fun setFtp(ftp: FtpSettings) {
        context.dataStore.edit { prefs ->
            prefs[FTP_HOST] = ftp.host
            prefs[FTP_PORT] = ftp.port
            prefs[FTP_USER] = ftp.user
            prefs[FTP_PASSWORD] = ftp.password
            prefs[FTP_DIR] = ftp.directory
            prefs[FTP_USE_FTPS] = ftp.useFtps
        }
    }

    private suspend fun put(key: Preferences.Key<Boolean>, value: Boolean) {
        context.dataStore.edit { prefs -> prefs[key] = value }
    }

    private companion object {
        val SOUND = booleanPreferencesKey("sound_enabled")
        val VIBRATION = booleanPreferencesKey("vibration_enabled")
        val AUTO_COPY = booleanPreferencesKey("auto_copy")
        val DOC_START = intPreferencesKey("doc_start")
        val DOC_LENGTH = intPreferencesKey("doc_length")
        val AMOUNT_START = intPreferencesKey("amount_start")
        val AMOUNT_LENGTH = intPreferencesKey("amount_length")
        val FTP_HOST = stringPreferencesKey("ftp_host")
        val FTP_PORT = intPreferencesKey("ftp_port")
        val FTP_USER = stringPreferencesKey("ftp_user")
        val FTP_PASSWORD = stringPreferencesKey("ftp_password")
        val FTP_DIR = stringPreferencesKey("ftp_dir")
        val FTP_USE_FTPS = booleanPreferencesKey("ftp_use_ftps")
    }
}
