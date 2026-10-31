package cz.pavel.eet2

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.BackHandler
import androidx.activity.compose.setContent
import androidx.activity.viewModels
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.ui.graphics.Color
import cz.pavel.eet2.ui.EetViewModel
import cz.pavel.eet2.ui.HesloDialog
import cz.pavel.eet2.ui.NastaveniScreen
import cz.pavel.eet2.ui.Obrazovka
import cz.pavel.eet2.ui.PokladnaScreen

class MainActivity : ComponentActivity() {
    private val vm: EetViewModel by viewModels()

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        vm.obrazovka
        setContent {
            MaterialTheme(colorScheme = lightColorScheme(primary = Color(0xFF2E7D32), secondary = Color(0xFF1565C0))) {
                vm.hesloDialog?.let { HesloDialog(vm, it) }
                when (vm.obrazovka) {
                    Obrazovka.Pokladna -> PokladnaScreen(vm)
                    Obrazovka.Nastaveni -> {
                        BackHandler { vm.obrazovka = Obrazovka.Pokladna }
                        NastaveniScreen(vm)
                    }
                }
            }
        }
    }
}
