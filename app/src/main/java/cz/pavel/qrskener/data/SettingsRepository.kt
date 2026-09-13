package cz.pavel.qrskener.data

import android.content.Context
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.booleanPreferencesKey
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.preferencesDataStore
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map

data class ScannerSettings(
    val soundEnabled: Boolean = true,
    val vibrationEnabled: Boolean = true,
    val autoCopy: Boolean = false,
    val autoOpenLinks: Boolean = false
)

private val Context.dataStore: DataStore<Preferences> by preferencesDataStore(name = "settings")

class SettingsRepository(private val context: Context) {

    val settings: Flow<ScannerSettings> = context.dataStore.data.map { prefs ->
        ScannerSettings(
            soundEnabled = prefs[SOUND] ?: true,
            vibrationEnabled = prefs[VIBRATION] ?: true,
            autoCopy = prefs[AUTO_COPY] ?: false,
            autoOpenLinks = prefs[AUTO_OPEN_LINKS] ?: false
        )
    }

    suspend fun setSoundEnabled(value: Boolean) = put(SOUND, value)

    suspend fun setVibrationEnabled(value: Boolean) = put(VIBRATION, value)

    suspend fun setAutoCopy(value: Boolean) = put(AUTO_COPY, value)

    suspend fun setAutoOpenLinks(value: Boolean) = put(AUTO_OPEN_LINKS, value)

    private suspend fun put(key: Preferences.Key<Boolean>, value: Boolean) {
        context.dataStore.edit { prefs -> prefs[key] = value }
    }

    private companion object {
        val SOUND = booleanPreferencesKey("sound_enabled")
        val VIBRATION = booleanPreferencesKey("vibration_enabled")
        val AUTO_COPY = booleanPreferencesKey("auto_copy")
        val AUTO_OPEN_LINKS = booleanPreferencesKey("auto_open_links")
    }
}
