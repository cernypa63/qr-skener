package cz.pavel.ukoly.ui

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.Button
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.unit.dp
import androidx.compose.runtime.collectAsState
import cz.pavel.ukoly.data.AppSettings

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun SettingsScreen(viewModel: TasksViewModel, onBack: () -> Unit) {
    val loaded by viewModel.settings.collectAsState()
    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Nastavení") },
                navigationIcon = {
                    IconButton(onClick = onBack) { Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Zpět") }
                }
            )
        }
    ) { padding ->
        loaded?.let { initial ->
            SettingsForm(viewModel, initial, onBack, Modifier.padding(padding))
        }
    }
}

@Composable
private fun SettingsForm(viewModel: TasksViewModel, initial: AppSettings, onBack: () -> Unit, modifier: Modifier) {
    val busy by viewModel.busy.collectAsState()
    var host by rememberSaveable { mutableStateOf(initial.host) }
    var port by rememberSaveable { mutableStateOf(initial.port.toString()) }
    var user by rememberSaveable { mutableStateOf(initial.user) }
    var password by rememberSaveable { mutableStateOf(initial.password) }
    var path by rememberSaveable { mutableStateOf(initial.remotePath) }
    var ftps by rememberSaveable { mutableStateOf(initial.useFtps) }
    var refresh by rememberSaveable { mutableStateOf(initial.refreshMinutes.toString()) }
    var testResult by rememberSaveable { mutableStateOf<String?>(null) }

    fun current() = AppSettings(
        host = host.trim(),
        port = port.toIntOrNull() ?: 21,
        user = user.trim(),
        password = password,
        remotePath = path.trim().ifBlank { AppSettings.DEFAULT_PATH },
        useFtps = ftps,
        refreshMinutes = refresh.toIntOrNull()?.coerceAtLeast(0) ?: 0
    )

    Column(
        modifier = modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp)
    ) {
        BusyIndicator(busy > 0)
        Text("FTP úložiště", style = MaterialTheme.typography.titleMedium)
        OutlinedTextField(
            value = host, onValueChange = { host = it }, label = { Text("Server (např. ftp.example.cz)") },
            singleLine = true, modifier = Modifier.fillMaxWidth(),
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Uri)
        )
        OutlinedTextField(
            value = port, onValueChange = { port = it.filter(Char::isDigit) }, label = { Text("Port") },
            singleLine = true, modifier = Modifier.fillMaxWidth(),
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number)
        )
        OutlinedTextField(
            value = user, onValueChange = { user = it }, label = { Text("Uživatel") },
            singleLine = true, modifier = Modifier.fillMaxWidth()
        )
        OutlinedTextField(
            value = password, onValueChange = { password = it }, label = { Text("Heslo") },
            singleLine = true, modifier = Modifier.fillMaxWidth(),
            visualTransformation = PasswordVisualTransformation(),
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Password)
        )
        OutlinedTextField(
            value = path, onValueChange = { path = it }, label = { Text("Cesta k souboru (např. /ukoly/ukoly.json)") },
            singleLine = true, modifier = Modifier.fillMaxWidth()
        )
        Row(verticalAlignment = Alignment.CenterVertically, modifier = Modifier.fillMaxWidth()) {
            Text("Šifrované spojení (FTPS)", modifier = Modifier.weight(1f))
            Switch(checked = ftps, onCheckedChange = { ftps = it })
        }
        Text("Synchronizace", style = MaterialTheme.typography.titleMedium)
        OutlinedTextField(
            value = refresh, onValueChange = { refresh = it.filter(Char::isDigit) },
            label = { Text("Automatická aktualizace z FTP (minuty, 0 = vypnuto)") },
            singleLine = true, modifier = Modifier.fillMaxWidth(),
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number)
        )
        OutlinedButton(
            onClick = {
                testResult = null
                viewModel.testConnection(current()) { testResult = it }
            },
            enabled = host.isNotBlank() && busy == 0,
            modifier = Modifier.fillMaxWidth()
        ) { Text("Otestovat připojení") }
        testResult?.let { Text(it, style = MaterialTheme.typography.bodyMedium) }
        Button(
            onClick = { viewModel.saveSettings(current(), onBack) },
            enabled = busy == 0,
            modifier = Modifier.fillMaxWidth()
        ) { Text("Uložit") }
    }
}
