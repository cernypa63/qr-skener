package cz.pavel.ukoly.ui

import cz.pavel.ukoly.data.AppSettings
import cz.pavel.ukoly.data.ChangeResult
import cz.pavel.ukoly.data.CheckResult
import cz.pavel.ukoly.data.FtpTaskStorage
import cz.pavel.ukoly.data.SettingsRepository
import cz.pavel.ukoly.data.Task
import cz.pavel.ukoly.data.TaskChange
import cz.pavel.ukoly.data.TaskRepository
import cz.pavel.ukoly.data.TasksJson
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.receiveAsFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

class TasksViewModel(
    private val settingsRepository: SettingsRepository = SettingsRepository(),
    private val repository: TaskRepository = TaskRepository(settingsRepository)
) {
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Main)

    val tasks: StateFlow<List<Task>> = repository.tasks

    val settings: StateFlow<AppSettings?> = settingsRepository.settings

    val compactList: StateFlow<Boolean> = settingsRepository.compactList

    fun setCompactList(value: Boolean) {
        scope.launch { settingsRepository.setCompactList(value) }
    }

    private val busyCount = MutableStateFlow(0)
    val busy: StateFlow<Int> = busyCount.asStateFlow()

    private val messageChannel = Channel<String>(Channel.BUFFERED)
    val messages: Flow<String> = messageChannel.receiveAsFlow()

    fun refresh(silent: Boolean = false) = work {
        repository.refresh().onFailure { e ->
            if (!silent) messageChannel.send("Aktualizace z FTP selhala: ${e.message ?: e.javaClass.simpleName}")
        }
    }

    fun check(id: Long, onResult: (CheckResult) -> Unit) = work {
        onResult(repository.check(id))
    }

    fun change(change: TaskChange, successMessage: String? = null, onSaved: () -> Unit = {}) = work {
        when (val result = repository.apply(change)) {
            ChangeResult.Saved -> {
                successMessage?.let { messageChannel.send(it) }
                onSaved()
            }
            ChangeResult.NotFound -> messageChannel.send("Úkol mezitím smazala jiná aplikace")
            is ChangeResult.Failed -> messageChannel.send("Uložení selhalo: ${result.message}")
        }
    }

    fun saveSettings(settings: AppSettings, onSaved: () -> Unit) = work {
        settingsRepository.save(settings)
        onSaved()
        repository.refresh().onFailure { e ->
            messageChannel.send("Aktualizace z FTP selhala: ${e.message ?: e.javaClass.simpleName}")
        }
    }

    fun testConnection(settings: AppSettings, onResult: (String) -> Unit) = work {
        val message = runCatching { FtpTaskStorage(settings).load() }.fold(
            onSuccess = { content ->
                if (content == null) "Spojení v pořádku, soubor zatím neexistuje a bude vytvořen."
                else "Spojení v pořádku, počet úkolů v souboru: ${TasksJson.parse(content).size}"
            },
            onFailure = { e -> "Chyba: ${e.message ?: e.javaClass.simpleName}" }
        )
        onResult(message)
    }

    fun showMessage(message: String) {
        scope.launch { messageChannel.send(message) }
    }

    fun close() {
        scope.cancel()
    }

    private fun work(block: suspend () -> Unit) {
        scope.launch {
            busyCount.update { it + 1 }
            try {
                block()
            } finally {
                busyCount.update { it - 1 }
            }
        }
    }
}
