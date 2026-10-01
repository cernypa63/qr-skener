package cz.pavel.ukoly.data

import org.json.JSONArray
import org.json.JSONObject

/**
 * Shared file format:
 * {
 *   "version": 1,
 *   "modified": "2026-10-01T15:50:12",
 *   "tasks": [
 *     { "id": 1790869812000, "text": "...", "status": "open" | "closed",
 *       "created": "2026-10-01T15:50:12", "modified": "...", "closed": "..." | null }
 *   ]
 * }
 * Unknown fields written by other applications are preserved.
 */
object TasksJson {
    const val VERSION = 1

    private const val ID = "id"
    private const val TEXT = "text"
    private const val STATUS = "status"
    private const val CREATED = "created"
    private const val MODIFIED = "modified"
    private const val CLOSED = "closed"
    private val KNOWN = setOf(ID, TEXT, STATUS, CREATED, MODIFIED, CLOSED)

    fun parse(content: String): List<Task> {
        val trimmed = content.trim()
        if (trimmed.isEmpty()) return emptyList()
        val array = if (trimmed.startsWith("[")) JSONArray(trimmed) else JSONObject(trimmed).optJSONArray("tasks") ?: JSONArray()
        return (0 until array.length()).mapNotNull { index -> array.optJSONObject(index)?.let(::parseTask) }
    }

    private fun parseTask(json: JSONObject): Task? {
        val id = json.optLong(ID, 0L)
        if (id == 0L) return null
        val extra = JSONObject()
        json.keys().forEach { key -> if (key !in KNOWN) extra.put(key, json.get(key)) }
        return Task(
            id = id,
            text = json.optString(TEXT, ""),
            created = json.optString(CREATED, ""),
            modified = json.optString(MODIFIED, ""),
            status = TaskStatus.fromJson(json.optString(STATUS, TaskStatus.OPEN.json)),
            closed = if (json.isNull(CLOSED)) null else json.optString(CLOSED).ifBlank { null },
            extra = extra.toString()
        )
    }

    fun serialize(tasks: List<Task>, modified: String): String {
        val array = JSONArray()
        tasks.forEach { task ->
            val json = runCatching { JSONObject(task.extra) }.getOrDefault(JSONObject())
            json.put(ID, task.id)
            json.put(TEXT, task.text)
            json.put(STATUS, task.status.json)
            json.put(CREATED, task.created)
            json.put(MODIFIED, task.modified)
            json.put(CLOSED, task.closed ?: JSONObject.NULL)
            array.put(json)
        }
        return JSONObject()
            .put("version", VERSION)
            .put(MODIFIED, modified)
            .put("tasks", array)
            .toString(2)
    }
}
