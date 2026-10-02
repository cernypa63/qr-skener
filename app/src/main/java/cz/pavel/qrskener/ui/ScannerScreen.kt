package cz.pavel.qrskener.ui

import android.Manifest
import android.content.ClipData
import android.content.ClipboardManager
import android.content.Context
import android.content.pm.PackageManager
import android.media.AudioManager
import android.media.ToneGenerator
import android.os.Build
import android.os.VibrationEffect
import android.os.Vibrator
import android.os.VibratorManager
import android.widget.Toast
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.camera.core.CameraSelector
import androidx.camera.core.ImageAnalysis
import androidx.camera.core.Preview
import androidx.camera.lifecycle.ProcessCameraProvider
import androidx.camera.view.PreviewView
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Settings
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalLifecycleOwner
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import androidx.compose.ui.viewinterop.AndroidView
import androidx.core.content.ContextCompat
import com.google.mlkit.vision.barcode.BarcodeScanning
import com.google.mlkit.vision.common.InputImage
import cz.pavel.qrskener.R
import cz.pavel.qrskener.data.FtpUploader
import cz.pavel.qrskener.data.ScannerSettings
import cz.pavel.qrskener.data.SettingsRepository
import cz.pavel.qrskener.data.UploadResult
import cz.pavel.qrskener.scan.CodeParser
import cz.pavel.qrskener.scan.CsvBuilder
import cz.pavel.qrskener.scan.ScanRecord
import cz.pavel.qrskener.scan.ScanSessionViewModel
import kotlinx.coroutines.launch
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale
import java.util.concurrent.Executors

private const val DUPLICATE_WINDOW_MILLIS = 2000L

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ScannerScreen(
    session: ScanSessionViewModel,
    onOpenSettings: () -> Unit,
    onBack: () -> Unit
) {
    val context = LocalContext.current
    val repository = remember { SettingsRepository(context) }
    val settings by repository.settings.collectAsState(initial = ScannerSettings())
    val scope = rememberCoroutineScope()

    val records = session.records
    var hasPermission by remember {
        mutableStateOf(
            ContextCompat.checkSelfPermission(context, Manifest.permission.CAMERA) ==
                PackageManager.PERMISSION_GRANTED
        )
    }
    var confirmFinish by remember { mutableStateOf(false) }
    var uploading by remember { mutableStateOf(false) }

    val permissionLauncher = rememberLauncherForActivityResult(
        ActivityResultContracts.RequestPermission()
    ) { granted -> hasPermission = granted }

    LaunchedEffect(Unit) {
        if (!hasPermission) permissionLauncher.launch(Manifest.permission.CAMERA)
    }

    val total = records.sumOf { it.amount ?: 0.0 }

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Column {
                        Text(stringResource(R.string.total_amount, formatAmount(total)))
                        Text(
                            text = stringResource(R.string.scanned_count, records.size),
                            style = MaterialTheme.typography.bodySmall
                        )
                    }
                },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(
                            Icons.AutoMirrored.Filled.ArrowBack,
                            contentDescription = stringResource(R.string.back)
                        )
                    }
                },
                actions = {
                    IconButton(onClick = onOpenSettings) {
                        Icon(
                            Icons.Filled.Settings,
                            contentDescription = stringResource(R.string.settings)
                        )
                    }
                }
            )
        },
        bottomBar = {
            Button(
                onClick = { confirmFinish = true },
                enabled = records.isNotEmpty() && !uploading,
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(16.dp)
                    .height(56.dp)
            ) {
                if (uploading) {
                    CircularProgressIndicator(modifier = Modifier.height(24.dp))
                } else {
                    Text(stringResource(R.string.finish_action))
                }
            }
        }
    ) { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
        ) {
            Box(modifier = Modifier.weight(1f)) {
                if (hasPermission) {
                    CameraPreview { code ->
                        val now = System.currentTimeMillis()
                        if (!session.isDuplicate(code, now, DUPLICATE_WINDOW_MILLIS)) {
                            session.add(CodeParser.parse(code, settings, now))
                            onCodeDetected(context, code, settings)
                        }
                    }
                } else {
                    PermissionRequest(
                        onRequest = { permissionLauncher.launch(Manifest.permission.CAMERA) }
                    )
                }
            }

            HorizontalDivider()

            RecordList(
                records = records,
                onRemove = { record -> session.remove(record) },
                modifier = Modifier.weight(1f)
            )
        }
    }

    if (confirmFinish) {
        AlertDialog(
            onDismissRequest = { confirmFinish = false },
            title = { Text(stringResource(R.string.finish_action)) },
            text = {
                Text(
                    stringResource(
                        R.string.finish_summary,
                        records.size,
                        formatAmount(total)
                    )
                )
            },
            confirmButton = {
                TextButton(onClick = {
                    confirmFinish = false
                    uploading = true
                    scope.launch {
                        val result = uploadCsv(settings, records.toList())
                        uploading = false
                        when (result) {
                            is UploadResult.Success -> {
                                Toast.makeText(
                                    context,
                                    context.getString(R.string.upload_success, result.remotePath),
                                    Toast.LENGTH_LONG
                                ).show()
                                session.clear()
                                onBack()
                            }

                            is UploadResult.Failure -> Toast.makeText(
                                context,
                                context.getString(R.string.upload_failed, result.message),
                                Toast.LENGTH_LONG
                            ).show()
                        }
                    }
                }) {
                    Text(stringResource(R.string.send_csv))
                }
            },
            dismissButton = {
                TextButton(onClick = { confirmFinish = false }) {
                    Text(stringResource(R.string.cancel))
                }
            }
        )
    }
}

private suspend fun uploadCsv(settings: ScannerSettings, records: List<ScanRecord>): UploadResult {
    val timestamp = SimpleDateFormat("yyyyMMdd_HHmmss", Locale.US).format(Date())
    return FtpUploader().upload(
        settings = settings.ftp,
        fileName = "skenovani_$timestamp.csv",
        content = CsvBuilder.build(records)
    )
}

@Composable
private fun RecordList(
    records: List<ScanRecord>,
    onRemove: (ScanRecord) -> Unit,
    modifier: Modifier = Modifier
) {
    LazyColumn(modifier = modifier.fillMaxWidth()) {
        items(records) { record ->
            Card(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(horizontal = 12.dp, vertical = 4.dp)
            ) {
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(12.dp),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Column {
                        Text(
                            text = record.documentNumber.ifBlank {
                                stringResource(R.string.unknown_document)
                            },
                            style = MaterialTheme.typography.titleSmall
                        )
                        Text(
                            text = record.amount?.let { formatAmount(it) }
                                ?: stringResource(R.string.invalid_amount, record.amountText),
                            style = MaterialTheme.typography.bodyMedium
                        )
                    }
                    TextButton(onClick = { onRemove(record) }) {
                        Text(stringResource(R.string.remove))
                    }
                }
            }
        }
    }
}

@Composable
private fun PermissionRequest(onRequest: () -> Unit) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(32.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center
    ) {
        Text(stringResource(R.string.camera_permission_rationale))
        Button(onClick = onRequest, modifier = Modifier.padding(top = 16.dp)) {
            Text(stringResource(R.string.grant_permission))
        }
    }
}

@androidx.annotation.OptIn(androidx.camera.core.ExperimentalGetImage::class)
@Composable
private fun CameraPreview(onCode: (String) -> Unit) {
    val context = LocalContext.current
    val lifecycleOwner = LocalLifecycleOwner.current
    val executor = remember { Executors.newSingleThreadExecutor() }
    val scanner = remember { BarcodeScanning.getClient() }

    AndroidView(
        modifier = Modifier.fillMaxSize(),
        factory = { ctx ->
            val previewView = PreviewView(ctx)
            val providerFuture = ProcessCameraProvider.getInstance(ctx)
            providerFuture.addListener({
                val provider = providerFuture.get()
                val preview = Preview.Builder().build().also {
                    it.setSurfaceProvider(previewView.surfaceProvider)
                }
                val analysis = ImageAnalysis.Builder()
                    .setBackpressureStrategy(ImageAnalysis.STRATEGY_KEEP_ONLY_LATEST)
                    .build()

                analysis.setAnalyzer(executor) { imageProxy ->
                    val mediaImage = imageProxy.image
                    if (mediaImage == null) {
                        imageProxy.close()
                        return@setAnalyzer
                    }
                    val image = InputImage.fromMediaImage(
                        mediaImage,
                        imageProxy.imageInfo.rotationDegrees
                    )
                    scanner.process(image)
                        .addOnSuccessListener { barcodes ->
                            val value = barcodes.firstNotNullOfOrNull { it.rawValue }
                            if (value != null) {
                                previewView.post { onCode(value) }
                            }
                        }
                        .addOnCompleteListener { imageProxy.close() }
                }

                provider.unbindAll()
                provider.bindToLifecycle(
                    lifecycleOwner,
                    CameraSelector.DEFAULT_BACK_CAMERA,
                    preview,
                    analysis
                )
            }, ContextCompat.getMainExecutor(ctx))
            previewView
        },
        onRelease = {
            ProcessCameraProvider.getInstance(context).get().unbindAll()
            scanner.close()
            executor.shutdown()
        }
    )
}

private fun formatAmount(amount: Double): String =
    String.format(Locale.US, "%.2f", amount).replace('.', ',')

private fun onCodeDetected(context: Context, code: String, settings: ScannerSettings) {
    if (settings.soundEnabled) {
        ToneGenerator(AudioManager.STREAM_NOTIFICATION, 80)
            .startTone(ToneGenerator.TONE_PROP_BEEP, 150)
    }
    if (settings.vibrationEnabled) vibrate(context)
    if (settings.autoCopy) copyToClipboard(context, code)
}

private fun vibrate(context: Context) {
    val vibrator = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
        val manager = context.getSystemService(Context.VIBRATOR_MANAGER_SERVICE) as VibratorManager
        manager.defaultVibrator
    } else {
        @Suppress("DEPRECATION")
        context.getSystemService(Context.VIBRATOR_SERVICE) as Vibrator
    }
    if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
        vibrator.vibrate(VibrationEffect.createOneShot(100, VibrationEffect.DEFAULT_AMPLITUDE))
    } else {
        @Suppress("DEPRECATION")
        vibrator.vibrate(100)
    }
}

private fun copyToClipboard(context: Context, code: String) {
    val clipboard = context.getSystemService(Context.CLIPBOARD_SERVICE) as ClipboardManager
    clipboard.setPrimaryClip(ClipData.newPlainText("kod", code))
}
