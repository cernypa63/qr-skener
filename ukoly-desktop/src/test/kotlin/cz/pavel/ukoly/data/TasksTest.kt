package cz.pavel.ukoly.data

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import java.time.ZoneOffset

class TasksTest {

    private val zone = ZoneOffset.UTC
    private val t0 = 1_790_000_000_000L

    @Test
    fun addCreatesOpenTaskWithTimestampId() {
        val tasks = emptyList<Task>().applyChange(TaskChange.Add("  Koupit mléko "), t0, zone)!!
        val task = tasks.single()
        assertEquals(t0, task.id)
        assertEquals("Koupit mléko", task.text)
        assertEquals(TaskStatus.OPEN, task.status)
        assertEquals("2026-09-21T14:13:20", task.created)
    }

    @Test
    fun addKeepsIdsUnique() {
        val tasks = emptyList<Task>()
            .applyChange(TaskChange.Add("a"), t0, zone)!!
            .applyChange(TaskChange.Add("b"), t0, zone)!!
        assertEquals(listOf(t0, t0 + 1), tasks.map { it.id })
    }

    @Test
    fun closeReopenEditDelete() {
        var tasks = emptyList<Task>().applyChange(TaskChange.Add("a"), t0, zone)!!
        tasks = tasks.applyChange(TaskChange.Close(t0), t0 + 1000, zone)!!
        assertEquals(TaskStatus.CLOSED, tasks.single().status)
        assertEquals("2026-09-21T14:13:21", tasks.single().closed)
        tasks = tasks.applyChange(TaskChange.Reopen(t0), t0 + 2000, zone)!!
        assertEquals(TaskStatus.OPEN, tasks.single().status)
        assertNull(tasks.single().closed)
        tasks = tasks.applyChange(TaskChange.Edit(t0, "b"), t0 + 3000, zone)!!
        assertEquals("b", tasks.single().text)
        assertEquals("2026-09-21T14:13:23", tasks.single().modified)
        assertTrue(tasks.applyChange(TaskChange.Delete(t0), t0, zone)!!.isEmpty())
    }

    @Test
    fun changeOfMissingTaskReturnsNull() {
        assertNull(emptyList<Task>().applyChange(TaskChange.Close(1L), t0, zone))
        assertNull(emptyList<Task>().applyChange(TaskChange.Delete(1L), t0, zone))
    }

    @Test
    fun jsonRoundTripPreservesUnknownFields() {
        val input = """
            {"version":1,"tasks":[
              {"id":1790000000000,"text":"Úkol č. 1","status":"closed","created":"2026-09-21T14:13:20",
               "modified":"2026-09-21T14:13:21","closed":"2026-09-21T14:13:21","priority":2},
              {"id":"1790000000001","text":"Druhý","status":"open","created":"2026-09-21T14:13:21","modified":"x","closed":null},
              {"text":"bez id"}
            ]}
        """.trimIndent()
        val tasks = TasksJson.parse(input)
        assertEquals(2, tasks.size)
        assertEquals(TaskStatus.CLOSED, tasks[0].status)
        assertEquals(1_790_000_000_001L, tasks[1].id)
        assertNull(tasks[1].closed)

        val reparsed = TasksJson.parse(TasksJson.serialize(tasks, "2026-09-21T14:13:22"))
        assertEquals(tasks, reparsed)
        assertTrue(TasksJson.serialize(tasks, "now").contains("\"priority\": 2"))
    }

    @Test
    fun parseAcceptsEmptyContentAndPlainArray() {
        assertTrue(TasksJson.parse("  ").isEmpty())
        assertEquals("a", TasksJson.parse("""[{"id":5,"text":"a"}]""").single().text)
    }
}
