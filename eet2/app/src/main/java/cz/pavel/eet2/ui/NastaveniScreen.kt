package cz.pavel.eet2.ui

import android.net.Uri
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.Checkbox
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.unit.dp
import cz.pavel.eet2.data.Firma
import cz.pavel.eet2.eet.PokladniCertifikat
import java.text.SimpleDateFormat
import java.util.Locale
import java.util.UUID

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun NastaveniScreen(vm: EetViewModel) {
    val n = vm.nastaveni
    var ostry by remember { mutableStateOf(n.ostryProvoz) }
    var mezniDoba by remember { mutableStateOf(n.mezniDobaS.toString()) }
    var host by remember { mutableStateOf(n.ftp.host) }
    var port by remember { mutableStateOf(n.ftp.port.toString()) }
    var uzivatel by remember { mutableStateOf(n.ftp.uzivatel) }
    var hesloFtp by remember { mutableStateOf(n.ftp.heslo) }
    var slozka by remember { mutableStateOf(n.ftp.slozka) }
    var ftps by remember { mutableStateOf(n.ftp.ftps) }
    var nazevSouboru by remember { mutableStateOf(n.ftp.nazevSouboru) }
    var potvrditOstry by remember { mutableStateOf(false) }
    var hlaska by remember { mutableStateOf<String?>(null) }
    var editovana by remember { mutableStateOf<Firma?>(null) }

    fun ftp() = n.ftp.copy(
        host = host.trim(), port = port.toIntOrNull() ?: 21, uzivatel = uzivatel.trim(), heslo = hesloFtp,
        slozka = slozka.trim(), ftps = ftps, nazevSouboru = nazevSouboru.trim().ifBlank { "eet" },
    )

    fun ulozit() {
        vm.ulozNastaveni(
            vm.nastaveni.copy(ostryProvoz = ostry, mezniDobaS = (mezniDoba.toIntOrNull() ?: 5).coerceIn(2, 60), ftp = ftp()),
        )
        vm.obrazovka = Obrazovka.Pokladna
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Nastavení") },
                navigationIcon = {
                    IconButton(onClick = { vm.obrazovka = Obrazovka.Pokladna }) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Zpět")
                    }
                },
                actions = { TextButton(onClick = { ulozit() }) { Text("Uložit") } },
            )
        },
    ) { padding ->
        Column(
            Modifier.padding(padding).fillMaxSize().imePadding().verticalScroll(rememberScrollState()).padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp),
        ) {
            Text("Firmy", style = MaterialTheme.typography.titleLarge)
            if (n.firmy.isEmpty()) Text("Zatím žádná firma.")
            n.firmy.forEach { f ->
                Card(Modifier.fillMaxWidth().clickable { editovana = f }) {
                    Column(Modifier.padding(12.dp)) {
                        Text(f.nazev.ifBlank { "(bez názvu)" }, fontWeight = FontWeight.Bold)
                        Text("DIČ ${f.dic}, provozovna ${f.idProvozovny}, pokladna ${f.idPokladny}")
                        Text("Klepnutím upravíte", style = MaterialTheme.typography.bodySmall)
                    }
                }
            }
            OutlinedButton(onClick = {
                editovana = Firma(UUID.randomUUID().toString(), "", "", "", "1", "", "")
            }) { Text("Přidat firmu") }

            HorizontalDivider()
            Text("Odesílání do EET", style = MaterialTheme.typography.titleLarge)
            Row(verticalAlignment = Alignment.CenterVertically) {
                Column(Modifier.weight(1f)) {
                    Text(if (ostry) "OSTRÝ PROVOZ" else "Testovací režim (Playground)", fontWeight = FontWeight.Bold,
                        color = if (ostry) Color(0xFF2E7D32) else Color(0xFFE65100))
                    Text("V testovacím režimu se tržby neevidují naostro.", style = MaterialTheme.typography.bodySmall)
                }
                Switch(checked = ostry, onCheckedChange = { if (it) potvrditOstry = true else ostry = false })
            }
            OutlinedTextField(
                mezniDoba, { mezniDoba = it.filter(Char::isDigit).take(2) },
                label = { Text("Mezní doba odezvy (sekundy, nejméně 2)") }, singleLine = true,
                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number), modifier = Modifier.fillMaxWidth(),
            )

            HorizontalDivider()
            Text("FTP pro ukládání výsledků", style = MaterialTheme.typography.titleLarge)
            OutlinedTextField(host, { host = it }, label = { Text("Server (adresa)") }, singleLine = true, modifier = Modifier.fillMaxWidth())
            OutlinedTextField(port, { port = it.filter(Char::isDigit).take(5) }, label = { Text("Port") }, singleLine = true,
                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number), modifier = Modifier.fillMaxWidth())
            OutlinedTextField(uzivatel, { uzivatel = it }, label = { Text("Uživatel") }, singleLine = true, modifier = Modifier.fillMaxWidth())
            OutlinedTextField(hesloFtp, { hesloFtp = it }, label = { Text("Heslo") }, singleLine = true,
                visualTransformation = PasswordVisualTransformation(),
                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Password), modifier = Modifier.fillMaxWidth())
            OutlinedTextField(slozka, { slozka = it }, label = { Text("Složka na serveru (nepovinné)") }, singleLine = true, modifier = Modifier.fillMaxWidth())
            OutlinedTextField(nazevSouboru, { nazevSouboru = it.filter { c -> c.isLetterOrDigit() || c == '_' || c == '-' } },
                label = { Text("Začátek názvu souboru") }, singleLine = true, modifier = Modifier.fillMaxWidth(),
                supportingText = { Text("Soubor: ${nazevSouboru.ifBlank { "eet" }}_RRRR-MM-DD.csv") })
            Row(verticalAlignment = Alignment.CenterVertically) {
                Checkbox(ftps, { ftps = it })
                Text("Šifrované spojení (FTPS)")
            }
            OutlinedButton(onClick = { vm.vyzkousetFtp(ftp()) { hlaska = it } }) { Text("Vyzkoušet připojení k FTP") }
            Button(onClick = { ulozit() }, modifier = Modifier.fillMaxWidth()) { Text("Uložit nastavení") }
        }
    }

    if (potvrditOstry) {
        AlertDialog(
            onDismissRequest = { potvrditOstry = false },
            title = { Text("Přepnout na ostrý provoz?") },
            text = { Text("Tržby se budou evidovat naostro u finanční správy. Použijte jen se skutečnými (ne testovacími) certifikáty.") },
            confirmButton = { TextButton(onClick = { ostry = true; potvrditOstry = false }) { Text("Ano, ostrý provoz") } },
            dismissButton = { TextButton(onClick = { potvrditOstry = false }) { Text("Zrušit") } },
        )
    }
    hlaska?.let {
        AlertDialog(
            onDismissRequest = { hlaska = null },
            text = { Text(it) },
            confirmButton = { TextButton(onClick = { hlaska = null }) { Text("OK") } },
        )
    }
    editovana?.let { f ->
        FirmaDialog(vm, f, onClose = { editovana = null })
    }
}

@Composable
private fun FirmaDialog(vm: EetViewModel, puvodni: Firma, onClose: () -> Unit) {
    val context = LocalContext.current
    val nova = vm.nastaveni.firmy.none { it.id == puvodni.id }
    var nazev by remember { mutableStateOf(puvodni.nazev) }
    var dic by remember { mutableStateOf(puvodni.dic) }
    var provozovna by remember { mutableStateOf(puvodni.idProvozovny) }
    var pokladna by remember { mutableStateOf(puvodni.idPokladny) }
    var heslo by remember { mutableStateOf(puvodni.heslo) }
    var certBytes by remember { mutableStateOf<ByteArray?>(null) }
    var certNazev by remember { mutableStateOf(if (puvodni.certSoubor.isBlank()) "" else "uložený certifikát") }
    var info by remember { mutableStateOf<String?>(null) }
    var chyba by remember { mutableStateOf<String?>(null) }
    var smazat by remember { mutableStateOf(false) }
    val datum = SimpleDateFormat("d. M. yyyy", Locale("cs"))

    fun overit(): PokladniCertifikat? {
        val bytes = certBytes ?: runCatching { vm.certSoubor(puvodni).readBytes() }.getOrNull()
        if (bytes == null) { chyba = "Nejprve vyberte soubor certifikátu (.p12)"; return null }
        return runCatching { PokladniCertifikat.load(bytes, heslo) }
            .onFailure { chyba = "Certifikát nelze otevřít – zkontrolujte heslo a soubor." }
            .onSuccess {
                chyba = null
                info = "Certifikát pro ${it.dic}, platný do ${datum.format(it.platnostDo)}"
                if (dic.isBlank()) dic = it.dic
            }
            .getOrNull()
    }

    val vyber = rememberLauncherForActivityResult(ActivityResultContracts.OpenDocument()) { uri: Uri? ->
        if (uri != null) {
            certBytes = context.contentResolver.openInputStream(uri)?.use { it.readBytes() }
            certNazev = uri.lastPathSegment?.substringAfterLast('/') ?: "vybraný soubor"
            info = null
            if (heslo.isNotEmpty()) overit()
        }
    }

    AlertDialog(
        onDismissRequest = onClose,
        title = { Text(if (nova) "Nová firma" else "Upravit firmu") },
        text = {
            Column(Modifier.verticalScroll(rememberScrollState()), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                OutlinedButton(onClick = { vyber.launch(arrayOf("*/*")) }, modifier = Modifier.fillMaxWidth()) {
                    Text(if (certNazev.isBlank()) "Vybrat certifikát (.p12)" else "Certifikát: $certNazev")
                }
                OutlinedTextField(heslo, { heslo = it; info = null }, label = { Text("Heslo k certifikátu") }, singleLine = true,
                    visualTransformation = PasswordVisualTransformation(),
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Password), modifier = Modifier.fillMaxWidth())
                TextButton(onClick = { overit() }) { Text("Ověřit certifikát a heslo") }
                info?.let { Text(it, color = Color(0xFF2E7D32)) }
                chyba?.let { Text(it, color = Color(0xFFC62828)) }
                OutlinedTextField(nazev, { nazev = it }, label = { Text("Název firmy") }, singleLine = true, modifier = Modifier.fillMaxWidth())
                OutlinedTextField(dic, { dic = it.uppercase().trim() }, label = { Text("DIČ (např. CZ12345678)") }, singleLine = true, modifier = Modifier.fillMaxWidth())
                OutlinedTextField(provozovna, { provozovna = it.filter(Char::isDigit).take(9) }, label = { Text("Číslo provozovny") }, singleLine = true,
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number), modifier = Modifier.fillMaxWidth(),
                    supportingText = { Text("Najdete na portálu MOJE daně") })
                OutlinedTextField(pokladna, { pokladna = it.filter { c -> c.isLetterOrDigit() || c in "-_/" }.take(20) },
                    label = { Text("Označení pokladny") }, singleLine = true, modifier = Modifier.fillMaxWidth())
                if (!nova) TextButton(onClick = { smazat = true }) { Text("Smazat firmu", color = Color(0xFFC62828)) }
            }
        },
        confirmButton = {
            TextButton(onClick = {
                val cert = overit() ?: return@TextButton
                when {
                    !Regex("CZ\\d{8,10}").matches(dic) -> chyba = "DIČ musí mít tvar CZ a 8 až 10 číslic"
                    dic != cert.dic -> chyba = "DIČ se neshoduje s certifikátem (${cert.dic})"
                    provozovna.isBlank() || provozovna.toLong() == 0L -> chyba = "Zadejte číslo provozovny"
                    pokladna.isBlank() -> chyba = "Zadejte označení pokladny"
                    else -> {
                        vm.ulozFirmu(
                            puvodni.copy(nazev = nazev.trim(), dic = dic, idProvozovny = provozovna.trimStart('0'),
                                idPokladny = pokladna, heslo = heslo),
                            certBytes,
                        )
                        onClose()
                    }
                }
            }) { Text("Uložit") }
        },
        dismissButton = { TextButton(onClick = onClose) { Text("Zrušit") } },
    )

    if (smazat) {
        AlertDialog(
            onDismissRequest = { smazat = false },
            title = { Text("Smazat firmu ${puvodni.nazev}?") },
            text = { Text("Smaže se i uložený certifikát.") },
            confirmButton = { TextButton(onClick = { vm.smazFirmu(puvodni); smazat = false; onClose() }) { Text("Smazat") } },
            dismissButton = { TextButton(onClick = { smazat = false }) { Text("Zrušit") } },
        )
    }
}
