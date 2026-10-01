package cz.pavel.ukoly

import android.os.Build
import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Surface
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.dynamicDarkColorScheme
import androidx.compose.material3.dynamicLightColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalLifecycleOwner
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.lifecycle.repeatOnLifecycle
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.navigation.NavType
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.rememberNavController
import androidx.navigation.navArgument
import cz.pavel.ukoly.ui.ClosedTasksScreen
import cz.pavel.ukoly.ui.EditTaskScreen
import cz.pavel.ukoly.ui.OpenTasksScreen
import cz.pavel.ukoly.ui.SettingsScreen
import cz.pavel.ukoly.ui.TasksViewModel
import kotlinx.coroutines.delay

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent {
            UkolyTheme {
                Surface(color = MaterialTheme.colorScheme.background) {
                    UkolyApp()
                }
            }
        }
    }
}

@Composable
private fun UkolyTheme(content: @Composable () -> Unit) {
    val dark = isSystemInDarkTheme()
    val context = LocalContext.current
    val colors = when {
        Build.VERSION.SDK_INT >= Build.VERSION_CODES.S ->
            if (dark) dynamicDarkColorScheme(context) else dynamicLightColorScheme(context)
        dark -> darkColorScheme()
        else -> lightColorScheme()
    }
    MaterialTheme(colorScheme = colors, content = content)
}

@Composable
private fun UkolyApp(viewModel: TasksViewModel = viewModel()) {
    val navController = rememberNavController()
    val settings by viewModel.settings.collectAsStateWithLifecycle()
    val lifecycleOwner = LocalLifecycleOwner.current
    val snackbar = remember { SnackbarHostState() }

    LaunchedEffect(Unit) {
        viewModel.messages.collect { snackbar.showSnackbar(it) }
    }

    val ftpKey = settings?.let { listOf(it.host, it.port, it.user, it.password, it.remotePath, it.useFtps, it.refreshMinutes) }
    LaunchedEffect(ftpKey) {
        val current = settings ?: return@LaunchedEffect
        lifecycleOwner.repeatOnLifecycle(Lifecycle.State.STARTED) {
            viewModel.refresh(silent = true)
            if (current.refreshMinutes > 0) {
                while (true) {
                    delay(current.refreshMinutes * 60_000L)
                    viewModel.refresh(silent = true)
                }
            }
        }
    }

    NavHost(navController = navController, startDestination = Routes.OPEN) {
        composable(Routes.OPEN) {
            OpenTasksScreen(
                viewModel = viewModel,
                snackbar = snackbar,
                onNew = { navController.navigate(Routes.edit(null)) },
                onResolved = { navController.navigate(Routes.CLOSED) },
                onSettings = { navController.navigate(Routes.SETTINGS) },
                onEdit = { navController.navigate(Routes.edit(it)) }
            )
        }
        composable(Routes.CLOSED) {
            ClosedTasksScreen(
                viewModel = viewModel,
                snackbar = snackbar,
                onBack = { navController.popBackStack() },
                onEdit = { navController.navigate(Routes.edit(it)) }
            )
        }
        composable(
            Routes.EDIT,
            arguments = listOf(navArgument("id") { type = NavType.LongType; defaultValue = 0L })
        ) { entry ->
            val id = entry.arguments?.getLong("id")?.takeIf { it != 0L }
            EditTaskScreen(viewModel = viewModel, snackbar = snackbar, taskId = id, onDone = { navController.popBackStack() })
        }
        composable(Routes.SETTINGS) {
            SettingsScreen(viewModel = viewModel, onBack = { navController.popBackStack() })
        }
    }
}

object Routes {
    const val OPEN = "open"
    const val CLOSED = "closed"
    const val SETTINGS = "settings"
    const val EDIT = "edit?id={id}"

    fun edit(id: Long?) = "edit?id=${id ?: 0L}"
}
