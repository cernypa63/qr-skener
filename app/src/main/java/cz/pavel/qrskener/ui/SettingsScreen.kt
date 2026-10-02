package cz.pavel.qrskener.ui

import androidx.compose.foundation.clickable
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
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.ListItem
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.unit.dp
import cz.pavel.qrskener.R
import cz.pavel.qrskener.data.ScannerSettings
import cz.pavel.qrskener.data.SettingsRepository
import kotlinx.coroutines.launch

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun SettingsScreen(onBack: () -> Unit) {
    val context = LocalContext.current
    val repository = remember { SettingsRepository(context) }
    val settings by repository.settings.collectAsState(initial = ScannerSettings())
    val scope = rememberCoroutineScope()

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text(stringResource(R.string.settings)) },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(
                            Icons.AutoMirrored.Filled.ArrowBack,
                            contentDescription = stringResource(R.string.back)
                        )
                    }
                }
            )
        }
    ) { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .verticalScroll(rememberScrollState())
        ) {
            SettingSwitch(
                label = stringResource(R.string.setting_sound),
                checked = settings.soundEnabled
            ) { value -> scope.launch { repository.setSoundEnabled(value) } }

            SettingSwitch(
                label = stringResource(R.string.setting_vibration),
                checked = settings.vibrationEnabled
            ) { value -> scope.launch { repository.setVibrationEnabled(value) } }

            SettingSwitch(
                label = stringResource(R.string.setting_copy),
                checked = settings.autoCopy
            ) { value -> scope.launch { repository.setAutoCopy(value) } }

            SectionHeader(stringResource(R.string.section_parsing))
            Text(
                text = stringResource(R.string.parsing_hint),
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.padding(horizontal = 16.dp, vertical = 4.dp)
            )

            PositionRow(
                label = stringResource(R.string.setting_document_position),
                start = settings.documentPosition.start,
                length = settings.documentPosition.length,
                onChange = { start, length ->
                    scope.launch { repository.setDocumentPosition(start, length) }
                }
            )

            PositionRow(
                label = stringResource(R.string.setting_amount_position),
                start = settings.amountPosition.start,
                length = settings.amountPosition.length,
                onChange = { start, length ->
                    scope.launch { repository.setAmountPosition(start, length) }
                }
            )

            SectionHeader(stringResource(R.string.section_ftp))

            TextSetting(
                label = stringResource(R.string.ftp_host),
                value = settings.ftp.host
            ) { value -> scope.launch { repository.setFtp(settings.ftp.copy(host = value)) } }

            TextSetting(
                label = stringResource(R.string.ftp_port),
                value = settings.ftp.port.toString(),
                keyboardType = KeyboardType.Number
            ) { value ->
                val port = value.toIntOrNull() ?: return@TextSetting
                scope.launch { repository.setFtp(settings.ftp.copy(port = port)) }
            }

            TextSetting(
                label = stringResource(R.string.ftp_user),
                value = settings.ftp.user
            ) { value -> scope.launch { repository.setFtp(settings.ftp.copy(user = value)) } }

            TextSetting(
                label = stringResource(R.string.ftp_password),
                value = settings.ftp.password,
                isPassword = true
            ) { value -> scope.launch { repository.setFtp(settings.ftp.copy(password = value)) } }

            TextSetting(
                label = stringResource(R.string.ftp_directory),
                value = settings.ftp.directory
            ) { value -> scope.launch { repository.setFtp(settings.ftp.copy(directory = value)) } }

            SettingSwitch(
                label = stringResource(R.string.ftp_use_ftps),
                checked = settings.ftp.useFtps
            ) { value -> scope.launch { repository.setFtp(settings.ftp.copy(useFtps = value)) } }
        }
    }
}

@Composable
private fun SectionHeader(title: String) {
    HorizontalDivider(modifier = Modifier.padding(top = 8.dp))
    Text(
        text = title,
        style = MaterialTheme.typography.titleSmall,
        color = MaterialTheme.colorScheme.primary,
        modifier = Modifier.padding(start = 16.dp, top = 16.dp, bottom = 4.dp)
    )
}

@Composable
private fun SettingSwitch(label: String, checked: Boolean, onCheckedChange: (Boolean) -> Unit) {
    ListItem(
        headlineContent = { Text(label) },
        trailingContent = { Switch(checked = checked, onCheckedChange = onCheckedChange) },
        modifier = Modifier
            .fillMaxWidth()
            .clickable { onCheckedChange(!checked) }
    )
}

@Composable
private fun TextSetting(
    label: String,
    value: String,
    keyboardType: KeyboardType = KeyboardType.Text,
    isPassword: Boolean = false,
    onValueChange: (String) -> Unit
) {
    OutlinedTextField(
        value = value,
        onValueChange = onValueChange,
        label = { Text(label) },
        singleLine = true,
        keyboardOptions = KeyboardOptions(keyboardType = keyboardType),
        visualTransformation = if (isPassword) PasswordVisualTransformation() else androidx.compose.ui.text.input.VisualTransformation.None,
        modifier = Modifier
            .fillMaxWidth()
            .padding(horizontal = 16.dp, vertical = 6.dp)
    )
}

@Composable
private fun PositionRow(
    label: String,
    start: Int,
    length: Int,
    onChange: (start: Int, length: Int) -> Unit
) {
    Column(modifier = Modifier.padding(horizontal = 16.dp, vertical = 6.dp)) {
        Text(text = label, style = MaterialTheme.typography.bodyLarge)
        Row(modifier = Modifier.fillMaxWidth()) {
            OutlinedTextField(
                value = start.toString(),
                onValueChange = { value ->
                    val parsed = value.toIntOrNull() ?: return@OutlinedTextField
                    if (parsed >= 1) onChange(parsed, length)
                },
                label = { Text(stringResource(R.string.position_start)) },
                singleLine = true,
                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                modifier = Modifier
                    .weight(1f)
                    .padding(end = 8.dp, top = 4.dp)
            )
            OutlinedTextField(
                value = length.toString(),
                onValueChange = { value ->
                    val parsed = value.toIntOrNull() ?: return@OutlinedTextField
                    if (parsed >= 1) onChange(start, parsed)
                },
                label = { Text(stringResource(R.string.position_length)) },
                singleLine = true,
                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                modifier = Modifier
                    .weight(1f)
                    .padding(top = 4.dp)
            )
        }
    }
}
