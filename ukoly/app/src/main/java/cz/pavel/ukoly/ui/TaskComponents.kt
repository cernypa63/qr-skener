package cz.pavel.ukoly.ui

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import cz.pavel.ukoly.data.CheckResult
import cz.pavel.ukoly.data.Task
import cz.pavel.ukoly.data.TaskTime

@Composable
fun BusyIndicator(busy: Boolean) {
    if (busy) {
        LinearProgressIndicator(modifier = Modifier.fillMaxWidth())
    } else {
        Spacer(modifier = Modifier.height(4.dp))
    }
}

@Composable
fun TaskList(
    tasks: List<Task>,
    emptyText: String,
    onClick: (Task) -> Unit,
    modifier: Modifier = Modifier
) {
    if (tasks.isEmpty()) {
        Box(modifier = modifier.fillMaxSize().padding(24.dp), contentAlignment = Alignment.Center) {
            Text(emptyText, style = MaterialTheme.typography.bodyLarge, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
        return
    }
    LazyColumn(modifier = modifier.fillMaxSize(), contentPadding = PaddingValues(bottom = 24.dp)) {
        items(tasks, key = { it.id }) { task ->
            Text(
                text = "${TaskTime.displayDate(task)} : ${task.text}",
                style = MaterialTheme.typography.bodyLarge,
                modifier = Modifier
                    .fillMaxWidth()
                    .clickable { onClick(task) }
                    .padding(horizontal = 16.dp, vertical = 14.dp)
            )
            HorizontalDivider()
        }
    }
}

data class TaskAction(val label: String, val onClick: () -> Unit)

/** Dialog shown after tapping a task; contains the freshly checked state and available operations. */
@Composable
fun TaskActionsDialog(
    task: Task,
    notice: String?,
    actions: List<TaskAction>,
    onDismiss: () -> Unit
) {
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Úkol z ${TaskTime.displayDate(task)}") },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
                if (notice != null) {
                    Text(notice, color = MaterialTheme.colorScheme.error, style = MaterialTheme.typography.bodyMedium)
                }
                Text(task.text, style = MaterialTheme.typography.bodyLarge)
                actions.forEach { action ->
                    TextButton(onClick = action.onClick, modifier = Modifier.fillMaxWidth()) {
                        Text(action.label, fontWeight = FontWeight.SemiBold)
                    }
                }
            }
        },
        confirmButton = {},
        dismissButton = { TextButton(onClick = onDismiss) { Text("Zavřít") } }
    )
}

@Composable
fun ConfirmDeleteDialog(task: Task, onConfirm: () -> Unit, onDismiss: () -> Unit) {
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Smazat úkol?") },
        text = { Text(task.text) },
        confirmButton = { TextButton(onClick = onConfirm) { Text("Smazat") } },
        dismissButton = { TextButton(onClick = onDismiss) { Text("Zrušit") } }
    )
}

/** State of the tap-to-check flow shared by both task lists. */
data class SelectedTask(val task: Task, val notice: String?)

fun CheckResult.toSelection(onDeleted: () -> Unit): SelectedTask? = when (this) {
    is CheckResult.Current -> SelectedTask(task, if (changedElsewhere) "Úkol byl změněn jinou aplikací – načten aktuální stav." else null)
    is CheckResult.Failed -> cached?.let { SelectedTask(it, "Aktuální stav nelze ověřit: $message") }
    CheckResult.Deleted -> {
        onDeleted()
        null
    }
}
