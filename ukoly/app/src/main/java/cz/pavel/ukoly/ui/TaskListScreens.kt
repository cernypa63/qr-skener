package cz.pavel.ukoly.ui

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.DoneAll
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material3.Button
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilledTonalButton
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import cz.pavel.ukoly.data.Task
import cz.pavel.ukoly.data.TaskChange
import cz.pavel.ukoly.data.TaskStatus

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun OpenTasksScreen(
    viewModel: TasksViewModel,
    snackbar: SnackbarHostState,
    onNew: () -> Unit,
    onResolved: () -> Unit,
    onSettings: () -> Unit,
    onEdit: (Long) -> Unit
) {
    val settings by viewModel.settings.collectAsStateWithLifecycle()
    TaskListScaffold(
        viewModel = viewModel,
        snackbar = snackbar,
        title = { Text("Úkoly") },
        navigationIcon = {},
        status = TaskStatus.OPEN,
        emptyText = "Žádné otevřené úkoly",
        onEdit = onEdit,
        header = {
            Row(
                modifier = Modifier.fillMaxWidth().padding(horizontal = 12.dp, vertical = 8.dp),
                horizontalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                TopButton("Nový", Icons.Filled.Add, onNew, primary = true, modifier = Modifier.weight(1f))
                TopButton("Vyřešeno", Icons.Filled.DoneAll, onResolved, modifier = Modifier.weight(1f))
                TopButton("Nastavení", Icons.Filled.Settings, onSettings, modifier = Modifier.weight(1f))
            }
            if (settings?.ftpConfigured == false) {
                Text(
                    "FTP není nastaveno – úkoly se ukládají jen v telefonu.",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.error,
                    modifier = Modifier.padding(horizontal = 16.dp, vertical = 4.dp)
                )
            }
        }
    )
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ClosedTasksScreen(
    viewModel: TasksViewModel,
    snackbar: SnackbarHostState,
    onBack: () -> Unit,
    onEdit: (Long) -> Unit
) {
    TaskListScaffold(
        viewModel = viewModel,
        snackbar = snackbar,
        title = { Text("Vyřešené úkoly") },
        navigationIcon = {
            IconButton(onClick = onBack) { Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Zpět") }
        },
        status = TaskStatus.CLOSED,
        emptyText = "Žádné vyřešené úkoly",
        onEdit = onEdit,
        header = {}
    )
}

@Composable
private fun TopButton(
    label: String,
    icon: ImageVector,
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
    primary: Boolean = false
) {
    val content: @Composable () -> Unit = {
        Icon(icon, contentDescription = null)
        Spacer(Modifier.width(4.dp))
        Text(label, maxLines = 1)
    }
    if (primary) {
        Button(onClick = onClick, modifier = modifier, contentPadding = ButtonDefaultsCompact) { content() }
    } else {
        FilledTonalButton(onClick = onClick, modifier = modifier, contentPadding = ButtonDefaultsCompact) { content() }
    }
}

private val ButtonDefaultsCompact = PaddingValues(horizontal = 8.dp, vertical = 8.dp)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun TaskListScaffold(
    viewModel: TasksViewModel,
    snackbar: SnackbarHostState,
    title: @Composable () -> Unit,
    navigationIcon: @Composable () -> Unit,
    status: TaskStatus,
    emptyText: String,
    onEdit: (Long) -> Unit,
    header: @Composable () -> Unit
) {
    val allTasks by viewModel.tasks.collectAsStateWithLifecycle()
    val busy by viewModel.busy.collectAsStateWithLifecycle()
    val tasks = allTasks.filter { it.status == status }.sortedBy { it.id }
    var selected by remember { mutableStateOf<SelectedTask?>(null) }
    var toDelete by remember { mutableStateOf<Task?>(null) }

    Scaffold(
        topBar = {
            TopAppBar(
                title = title,
                navigationIcon = navigationIcon,
                actions = {
                    IconButton(onClick = { viewModel.refresh() }) {
                        Icon(Icons.Filled.Refresh, contentDescription = "Aktualizovat z FTP")
                    }
                }
            )
        },
        snackbarHost = { SnackbarHost(snackbar) }
    ) { padding ->
        Column(modifier = Modifier.fillMaxSize().padding(padding)) {
            header()
            BusyIndicator(busy > 0)
            TaskList(
                tasks = tasks,
                emptyText = emptyText,
                onClick = { task ->
                    viewModel.check(task.id) { result ->
                        selected = result.toSelection {
                            viewModel.showMessage("Úkol byl mezitím smazán jinou aplikací")
                        }
                    }
                }
            )
        }
    }

    selected?.let { selection ->
        val task = selection.task
        val dismiss = { selected = null }
        val actions = when (task.status) {
            TaskStatus.OPEN -> listOf(
                TaskAction("Editovat") { dismiss(); onEdit(task.id) },
                TaskAction("Přesunout do vyřešených") {
                    dismiss()
                    viewModel.change(TaskChange.Close(task.id), "Úkol přesunut do vyřešených")
                },
                TaskAction("Smazat") { dismiss(); toDelete = task }
            )
            TaskStatus.CLOSED -> listOf(
                TaskAction("Vrátit do otevřených") {
                    dismiss()
                    viewModel.change(TaskChange.Reopen(task.id), "Úkol vrácen do otevřených")
                },
                TaskAction("Smazat") { dismiss(); toDelete = task }
            )
        }
        val notice = selection.notice ?: if (task.status != status) {
            if (task.status == TaskStatus.CLOSED) "Úkol byl jinou aplikací přesunut do vyřešených."
            else "Úkol byl jinou aplikací vrácen do otevřených."
        } else null
        TaskActionsDialog(task = task, notice = notice, actions = actions, onDismiss = dismiss)
    }

    toDelete?.let { task ->
        ConfirmDeleteDialog(
            task = task,
            onConfirm = {
                toDelete = null
                viewModel.change(TaskChange.Delete(task.id), "Úkol smazán")
            },
            onDismiss = { toDelete = null }
        )
    }
}
