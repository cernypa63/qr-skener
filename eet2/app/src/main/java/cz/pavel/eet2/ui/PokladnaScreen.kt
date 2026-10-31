package cz.pavel.eet2.ui

import androidx.compose.ui.platform.LocalContext
import androidx.compose.runtime.setValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.getValue
import android.app.Activity
import androidx.compose.foundation.background
import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilterChip
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.remember
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalSoftwareKeyboardController
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun PokladnaScreen(vm: EetViewModel) {
    val n = vm.nastaveni
    val activity = LocalContext.current as? Activity
    var potvrditKonec by remember { mutableStateOf(false) }
    val konec = {
        if (vm.pocetNeodeslanych > 0 || vm.pocetFtpFronta > 0) potvrditKonec = true
        else activity?.finishAndRemoveTask()
    }
    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("EET 2.0") },
                actions = {
                    IconButton(onClick = { vm.otevritNastaveni() }, enabled = !vm.odesilani) {
                        Icon(Icons.Filled.Settings, contentDescription = "Nastavení")
                    }
                    TextButton(onClick = { konec() }, enabled = !vm.odesilani) { Text("Konec", fontSize = 18.sp) }
                },
            )
        },
    ) { padding ->
        Column(
            Modifier.padding(padding).fillMaxSize().imePadding().padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            if (!n.ostryProvoz) {
                Text(
                    "TESTOVACÍ REŽIM – tržby se neevidují naostro",
                    Modifier.fillMaxWidth().background(Color(0xFFFFE0B2)).padding(8.dp),
                    color = Color(0xFFE65100), fontWeight = FontWeight.Bold, textAlign = TextAlign.Center,
                )
            }
            if (n.firmy.isEmpty()) {
                Text("Zatím není nastavena žádná firma. Otevřete Nastavení (ozubené kolečko vpravo nahoře) a přidejte firmu s certifikátem.")
                Button(onClick = { vm.otevritNastaveni() }) { Text("Otevřít nastavení") }
            } else {
                PokladnaObsah(vm)
            }
        }
    }

    if (potvrditKonec) {
        AlertDialog(
            onDismissRequest = { potvrditKonec = false },
            title = { Text("Ukončit aplikaci?") },
            text = {
                Text(
                    "Některé tržby nebo záznamy ještě nejsou odeslané (EET: ${vm.pocetNeodeslanych}, FTP: ${vm.pocetFtpFronta}). " +
                        "Nezapomenou se – doposílají se po dalším spuštění aplikace. Tržby do EET je ale nutné doposlat do 48 hodin."
                )
            },
            confirmButton = { TextButton(onClick = { potvrditKonec = false; activity?.finishAndRemoveTask() }) { Text("Ukončit") } },
            dismissButton = { TextButton(onClick = { potvrditKonec = false }) { Text("Zpět") } },
        )
    }

    vm.vysledek?.let { v ->
        val barva = when (v.druh) {
            Druh.Ok -> Color(0xFF2E7D32)
            Druh.Chyba -> Color(0xFFC62828)
            Druh.Doposlat -> Color(0xFFE65100)
        }
        AlertDialog(
            onDismissRequest = {},
            title = { Text(v.titulek, color = barva, fontWeight = FontWeight.Bold) },
            text = { Text(v.text, fontSize = 18.sp) },
            confirmButton = {
                TextButton(onClick = { vm.potvrditVysledek() }) { Text("OK – další tržba", fontSize = 18.sp) }
            },
            modifier = Modifier.padding(8.dp),
        )
    }
}

@Composable
private fun PokladnaObsah(vm: EetViewModel) {
    val n = vm.nastaveni
    val firma = n.vybranaFirma
    val focus = remember { FocusRequester() }
    val keyboard = LocalSoftwareKeyboardController.current

    LaunchedEffect(vm.vysledek, vm.odesilani) {
        if (vm.vysledek == null && !vm.odesilani) {
            runCatching { focus.requestFocus() }
            keyboard?.show()
        }
    }

    Column(verticalArrangement = Arrangement.spacedBy(12.dp)) {
        if (n.firmy.size > 1) {
            Text("Firma:", style = MaterialTheme.typography.labelLarge)
            Row(Modifier.horizontalScroll(rememberScrollState()), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                n.firmy.forEach { f ->
                    FilterChip(
                        selected = f.id == firma?.id,
                        onClick = { vm.vyberFirmu(f.id) },
                        label = { Text(f.nazev.ifBlank { f.dic }) },
                        enabled = !vm.odesilani,
                    )
                }
            }
        }
        firma?.let {
            Text(
                it.nazev.ifBlank { it.dic },
                Modifier.fillMaxWidth().background(MaterialTheme.colorScheme.primary, RoundedCornerShape(12.dp)).padding(horizontal = 12.dp, vertical = 10.dp),
                color = MaterialTheme.colorScheme.onPrimary,
                style = MaterialTheme.typography.headlineMedium,
                fontWeight = FontWeight.Bold,
                textAlign = TextAlign.Center,
            )
            Text("DIČ ${it.dic}  •  provozovna ${it.idProvozovny}  •  pokladna ${it.idPokladny}", style = MaterialTheme.typography.bodyMedium)
        }
        OutlinedTextField(
            value = vm.castka,
            onValueChange = { t -> vm.castka = t.filter { it.isDigit() || it == ',' || it == '.' || it == '-' }.take(13) },
            label = { Text("Částka (Kč)") },
            singleLine = true,
            enabled = !vm.odesilani,
            textStyle = TextStyle(fontSize = 36.sp, fontWeight = FontWeight.Bold, textAlign = TextAlign.End),
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal, imeAction = ImeAction.Send),
            keyboardActions = KeyboardActions(onSend = { vm.zaevidovat() }),
            modifier = Modifier.fillMaxWidth().focusRequester(focus),
        )
        Button(
            onClick = { vm.zaevidovat() },
            enabled = !vm.odesilani,
            modifier = Modifier.fillMaxWidth().height(64.dp),
        ) {
            if (vm.odesilani) {
                CircularProgressIndicator(Modifier.size(24.dp), color = Color.White, strokeWidth = 3.dp)
                Spacer(Modifier.size(12.dp))
                Text("Odesílám…", fontSize = 20.sp)
            } else {
                Text("Tržbu zaevidovat", fontSize = 20.sp)
            }
        }
        if (vm.pocetNeodeslanych > 0) {
            Text(
                "Nedoručené tržby: ${vm.pocetNeodeslanych} – doposílají se automaticky každou minutu.",
                color = Color(0xFFE65100),
            )
        }
        if (vm.pocetFtpFronta > 0) {
            Text(
                "Záznamy čekající na uložení na FTP: ${vm.pocetFtpFronta}" + (vm.hlaskaFtp?.let { " ($it)" } ?: ""),
                color = Color(0xFFE65100),
            )
        }
    }
}
