package cz.pavel.ukoly.data

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.withContext
import java.io.File

sealed interface ChangeResult {
    data object Saved : ChangeResult
    data object NotFound : ChangeResult
    data class Failed(val message: String) : ChangeResult
}

sealed interface CheckResult {
    data class Current(val task: Task, val changedElsewhere: Boolean) : CheckResult
    data object Deleted : CheckResult
    data class Failed(val message: String, val cached: Task?) : CheckResult
}

class TaskRepository(
    private val settingsRepository: SettingsRepository,
    private val cacheFile: File = File(AppDirs.data, "ukoly.json")
) {

    private val mutex = Mutex()
    private val _tasks = MutableStateFlow(readCache())
    val tasks: StateFlow<List<Task>> = _tasks.asStateFlow()

    private fun storage(): TaskStorage {
        val settings = settingsRepository.settings.value
        return if (settings.ftpConfigured) FtpTaskStorage(settings) else LocalTaskStorage(cacheFile)
    }

    /** Reloads all tasks from the shared file. A missing file is created from the current local list. */
    suspend fun refresh(): Result<Unit> = mutex.withLock {
        runCatching {
            val storage = storage()
            val content = storage.load()
            if (content == null) {
                write(storage, _tasks.value)
            } else {
                publish(TasksJson.parse(content))
            }
        }
    }

    /** Loads the current file, applies the change on top of it and saves it back immediately. */
    suspend fun apply(change: TaskChange): ChangeResult = mutex.withLock {
        try {
            val storage = storage()
            val current = storage.load()?.let(TasksJson::parse) ?: _tasks.value
            val updated = current.applyChange(change, System.currentTimeMillis())
            if (updated == null) {
                publish(current)
                ChangeResult.NotFound
            } else {
                write(storage, updated)
                ChangeResult.Saved
            }
        } catch (e: Exception) {
            ChangeResult.Failed(e.message ?: e.javaClass.simpleName)
        }
    }

    /** Verifies that the task was not changed by another application and pulls the current state. */
    suspend fun check(id: Long): CheckResult = mutex.withLock {
        val cached = _tasks.value.firstOrNull { it.id == id }
        try {
            val content = storage().load() ?: return@withLock CheckResult.Failed("Soubor s úkoly nebyl nalezen", cached)
            val remote = TasksJson.parse(content)
            publish(remote)
            val task = remote.firstOrNull { it.id == id } ?: return@withLock CheckResult.Deleted
            CheckResult.Current(task, changedElsewhere = cached != null && cached != task)
        } catch (e: Exception) {
            CheckResult.Failed(e.message ?: e.javaClass.simpleName, cached)
        }
    }

    private suspend fun write(storage: TaskStorage, tasks: List<Task>) {
        storage.save(TasksJson.serialize(tasks, TaskTime.format(System.currentTimeMillis())))
        publish(tasks)
    }

    private suspend fun publish(tasks: List<Task>) {
        _tasks.value = tasks
        withContext(Dispatchers.IO) {
            cacheFile.writeText(TasksJson.serialize(tasks, TaskTime.format(System.currentTimeMillis())), Charsets.UTF_8)
        }
    }

    private fun readCache(): List<Task> =
        runCatching { if (cacheFile.exists()) TasksJson.parse(cacheFile.readText(Charsets.UTF_8)) else emptyList() }
            .getOrDefault(emptyList())
}
