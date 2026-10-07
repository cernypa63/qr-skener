package cz.pavel.ukoly.data

import java.time.Instant
import java.time.LocalDateTime
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import java.time.temporal.ChronoUnit

enum class TaskStatus(val json: String) {
    OPEN("open"),
    CLOSED("closed");

    companion object {
        fun fromJson(value: String): TaskStatus = entries.firstOrNull { it.json == value } ?: OPEN
    }
}

data class Task(
    val id: Long,
    val text: String,
    val created: String,
    val modified: String,
    val status: TaskStatus,
    val closed: String? = null,
    val extra: String = "{}"
)

object TaskTime {
    private val displayDate = DateTimeFormatter.ofPattern("d.M.yyyy")

    fun format(millis: Long, zone: ZoneId = ZoneId.systemDefault()): String =
        LocalDateTime.ofInstant(Instant.ofEpochMilli(millis), zone)
            .truncatedTo(ChronoUnit.SECONDS)
            .format(DateTimeFormatter.ISO_LOCAL_DATE_TIME)

    fun displayDate(task: Task): String =
        runCatching { LocalDateTime.parse(task.created).format(displayDate) }
            .getOrElse {
                if (task.created.isNotBlank()) task.created
                else LocalDateTime.ofInstant(Instant.ofEpochMilli(task.id), ZoneId.systemDefault()).format(displayDate)
            }
}

sealed interface TaskChange {
    data class Add(val text: String) : TaskChange
    data class Edit(val id: Long, val text: String) : TaskChange
    data class Close(val id: Long) : TaskChange
    data class Reopen(val id: Long) : TaskChange
    data class Delete(val id: Long) : TaskChange
}

/** Returns the updated list, or null when the change targets a task that no longer exists. */
fun List<Task>.applyChange(change: TaskChange, nowMillis: Long, zone: ZoneId = ZoneId.systemDefault()): List<Task>? {
    val now = TaskTime.format(nowMillis, zone)
    return when (change) {
        is TaskChange.Add -> {
            val ids = map { it.id }.toSet()
            var id = nowMillis
            while (id in ids) id++
            this + Task(id = id, text = change.text.trim(), created = now, modified = now, status = TaskStatus.OPEN)
        }
        is TaskChange.Edit -> update(change.id) { it.copy(text = change.text.trim(), modified = now) }
        is TaskChange.Close -> update(change.id) { it.copy(status = TaskStatus.CLOSED, closed = now, modified = now) }
        is TaskChange.Reopen -> update(change.id) { it.copy(status = TaskStatus.OPEN, closed = null, modified = now) }
        is TaskChange.Delete -> if (any { it.id == change.id }) filterNot { it.id == change.id } else null
    }
}

private inline fun List<Task>.update(id: Long, transform: (Task) -> Task): List<Task>? {
    if (none { it.id == id }) return null
    return map { if (it.id == id) transform(it) else it }
}
