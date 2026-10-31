package cz.pavel.eet2.ui

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.unit.dp

@Composable
fun HesloDialog(vm: EetViewModel, rezim: HesloRezim) {
    var heslo by remember(rezim) { mutableStateOf("") }
    var znovu by remember(rezim) { mutableStateOf("") }
    var chyba by remember(rezim) { mutableStateOf<String?>(null) }
    val focus = remember { FocusRequester() }
    val nove = rezim != HesloRezim.Zadat
    LaunchedEffect(rezim) { runCatching { focus.requestFocus() } }

    AlertDialog(
        onDismissRequest = { vm.hesloDialog = null },
        title = {
            Text(
                when (rezim) {
                    HesloRezim.Nastavit -> "Nastavte heslo k nastavení"
                    HesloRezim.Zadat -> "Heslo k nastavení"
                    HesloRezim.Zmenit -> "Nové heslo k nastavení"
                }
            )
        },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                if (rezim == HesloRezim.Nastavit) {
                    Text("Nastavení zatím není chráněno. Zvolte heslo, které se bude chtít při každém otevření nastavení.")
                }
                OutlinedTextField(
                    heslo, { heslo = it; chyba = null },
                    label = { Text(if (nove) "Nové heslo" else "Heslo") },
                    singleLine = true,
                    visualTransformation = PasswordVisualTransformation(),
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Password),
                    modifier = Modifier.fillMaxWidth().focusRequester(focus),
                )
                if (nove) {
                    OutlinedTextField(
                        znovu, { znovu = it; chyba = null },
                        label = { Text("Heslo znovu") },
                        singleLine = true,
                        visualTransformation = PasswordVisualTransformation(),
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Password),
                        modifier = Modifier.fillMaxWidth(),
                    )
                }
                chyba?.let { Text(it, color = MaterialTheme.colorScheme.error) }
            }
        },
        confirmButton = {
            TextButton(onClick = { chyba = vm.potvrditHeslo(heslo, znovu) }) { Text(if (nove) "Uložit heslo" else "Otevřít") }
        },
        dismissButton = { TextButton(onClick = { vm.hesloDialog = null }) { Text("Zrušit") } },
    )
}
