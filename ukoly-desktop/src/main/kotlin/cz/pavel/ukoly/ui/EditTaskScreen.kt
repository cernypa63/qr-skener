package cz.pavel.ukoly.ui

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.Button
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.unit.dp
import androidx.compose.runtime.collectAsState
import cz.pavel.ukoly.data.TaskChange

/** Creates a new task when [taskId] is null, otherwise edits the existing one. */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun EditTaskScreen(
    viewModel: TasksViewModel,
    snackbar: SnackbarHostState,
    taskId: Long?,
    onDone: () -> Unit
) {
    val tasks by viewModel.tasks.collectAsState()
    val busy by viewModel.busy.collectAsState()
    val original = remember(taskId) { taskId?.let { id -> tasks.firstOrNull { it.id == id } } }
    var text by rememberSaveable { mutableStateOf(original?.text ?: "") }
    val focus = remember { FocusRequester() }

    LaunchedEffect(Unit) {
        focus.requestFocus()
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text(if (taskId == null) "Nový úkol" else "Upravit úkol") },
                navigationIcon = {
                    IconButton(onClick = onDone) { Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Zpět") }
                }
            )
        },
        snackbarHost = { SnackbarHost(snackbar) }
    ) { padding ->
        Column(
            modifier = Modifier.fillMaxSize().padding(padding),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            BusyIndicator(busy > 0)
            OutlinedTextField(
                value = text,
                onValueChange = { text = it },
                label = { Text("Text úkolu") },
                modifier = Modifier
                    .fillMaxWidth()
                    .heightIn(min = 140.dp)
                    .padding(horizontal = 16.dp)
                    .focusRequester(focus)
            )
            Button(
                onClick = {
                    val change = if (taskId == null) TaskChange.Add(text) else TaskChange.Edit(taskId, text)
                    viewModel.change(change, if (taskId == null) "Úkol uložen" else "Změny uloženy", onDone)
                },
                enabled = text.isNotBlank() && busy == 0,
                modifier = Modifier.fillMaxWidth().padding(horizontal = 16.dp)
            ) {
                Text("Uložit")
            }
        }
    }
}
