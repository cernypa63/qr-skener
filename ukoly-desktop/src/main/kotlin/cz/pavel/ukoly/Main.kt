package cz.pavel.ukoly

import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Surface
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateListOf
import androidx.compose.runtime.remember
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.unit.dp
import androidx.compose.ui.window.Window
import androidx.compose.ui.window.application
import androidx.compose.ui.window.rememberWindowState
import cz.pavel.ukoly.ui.ClosedTasksScreen
import cz.pavel.ukoly.ui.EditTaskScreen
import cz.pavel.ukoly.ui.OpenTasksScreen
import cz.pavel.ukoly.ui.SettingsScreen
import cz.pavel.ukoly.ui.TasksViewModel
import kotlinx.coroutines.delay

fun main() = application {
    val viewModel = remember { TasksViewModel() }
    Window(
        onCloseRequest = {
            viewModel.close()
            exitApplication()
        },
        title = "Úkoly",
        icon = painterResource("ukoly.png"),
        state = rememberWindowState(width = 640.dp, height = 820.dp)
    ) {
        UkolyTheme {
            Surface(color = MaterialTheme.colorScheme.background) {
                UkolyApp(viewModel)
            }
        }
    }
}

@Composable
private fun UkolyTheme(content: @Composable () -> Unit) {
    MaterialTheme(colorScheme = if (isSystemInDarkTheme()) darkColorScheme() else lightColorScheme(), content = content)
}

private sealed interface Screen {
    data object Open : Screen
    data object Closed : Screen
    data object Settings : Screen
    data class Edit(val taskId: Long?) : Screen
}

@Composable
private fun UkolyApp(viewModel: TasksViewModel) {
    val backStack = remember { mutableStateListOf<Screen>(Screen.Open) }
    val settings by viewModel.settings.collectAsState()
    val snackbar = remember { SnackbarHostState() }
    val navigate: (Screen) -> Unit = { backStack.add(it) }
    val back: () -> Unit = { if (backStack.size > 1) backStack.removeAt(backStack.lastIndex) }

    LaunchedEffect(Unit) {
        viewModel.messages.collect { snackbar.showSnackbar(it) }
    }

    val ftpKey = settings?.let { listOf(it.host, it.port, it.user, it.password, it.remotePath, it.useFtps, it.refreshMinutes) }
    LaunchedEffect(ftpKey) {
        val current = settings ?: return@LaunchedEffect
        viewModel.refresh(silent = true)
        if (current.refreshMinutes > 0) {
            while (true) {
                delay(current.refreshMinutes * 60_000L)
                viewModel.refresh(silent = true)
            }
        }
    }

    when (val screen = backStack.last()) {
        Screen.Open -> OpenTasksScreen(
            viewModel = viewModel,
            snackbar = snackbar,
            onNew = { navigate(Screen.Edit(null)) },
            onResolved = { navigate(Screen.Closed) },
            onSettings = { navigate(Screen.Settings) },
            onEdit = { navigate(Screen.Edit(it)) }
        )
        Screen.Closed -> ClosedTasksScreen(
            viewModel = viewModel,
            snackbar = snackbar,
            onBack = back,
            onEdit = { navigate(Screen.Edit(it)) }
        )
        is Screen.Edit -> EditTaskScreen(viewModel = viewModel, snackbar = snackbar, taskId = screen.taskId, onDone = back)
        Screen.Settings -> SettingsScreen(viewModel = viewModel, onBack = back)
    }
}
