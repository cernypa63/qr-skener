package cz.pavel.qrskener

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.dynamicDarkColorScheme
import androidx.compose.material3.dynamicLightColorScheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.platform.LocalContext
import android.os.Build
import androidx.lifecycle.viewmodel.compose.viewModel
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.rememberNavController
import cz.pavel.qrskener.scan.ScanSessionViewModel
import cz.pavel.qrskener.ui.HomeScreen
import cz.pavel.qrskener.ui.ScannerScreen
import cz.pavel.qrskener.ui.SettingsScreen

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent {
            QrSkenerTheme {
                Surface(color = MaterialTheme.colorScheme.background) {
                    QrSkenerApp()
                }
            }
        }
    }
}

@Composable
private fun QrSkenerTheme(content: @Composable () -> Unit) {
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
private fun QrSkenerApp() {
    val navController = rememberNavController()
    val session: ScanSessionViewModel = viewModel(
        viewModelStoreOwner = LocalContext.current as ComponentActivity
    )
    NavHost(navController = navController, startDestination = Routes.HOME) {
        composable(Routes.HOME) {
            HomeScreen(
                onStartScanning = { navController.navigate(Routes.SCANNER) },
                onOpenSettings = { navController.navigate(Routes.SETTINGS) }
            )
        }
        composable(Routes.SCANNER) {
            ScannerScreen(
                session = session,
                onOpenSettings = { navController.navigate(Routes.SETTINGS) },
                onBack = { navController.popBackStack() }
            )
        }
        composable(Routes.SETTINGS) {
            SettingsScreen(onBack = { navController.popBackStack() })
        }
    }
}

object Routes {
    const val HOME = "home"
    const val SCANNER = "scanner"
    const val SETTINGS = "settings"
}
