package cz.pavel.vyska

import android.Manifest
import android.annotation.SuppressLint
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.location.Location
import android.location.LocationListener
import android.location.LocationManager
import android.location.OnNmeaMessageListener
import android.os.Build
import android.os.Bundle
import android.os.Handler
import android.os.Looper
import android.os.SystemClock
import android.provider.Settings
import android.view.WindowManager
import androidx.activity.ComponentActivity
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.compose.setContent
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalLifecycleOwner
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.core.content.ContextCompat
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import kotlin.math.roundToInt

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)
        setContent {
            MaterialTheme(colorScheme = if (isSystemInDarkTheme()) darkColorScheme() else lightColorScheme()) {
                Surface(modifier = Modifier.fillMaxSize()) {
                    AltitudeScreen()
                }
            }
        }
    }
}

private enum class AltitudeSource { NMEA, MSL, ELLIPSOID }

private data class AltitudeReading(
    val meters: Double,
    val source: AltitudeSource,
    val verticalAccuracy: Float?,
    val satellites: Int?
)

private const val NMEA_MAX_AGE_MS = 5_000L

private fun hasLocationPermission(context: Context) =
    ContextCompat.checkSelfPermission(context, Manifest.permission.ACCESS_FINE_LOCATION) ==
        PackageManager.PERMISSION_GRANTED

@Composable
private fun AltitudeScreen() {
    val context = LocalContext.current
    var permitted by remember { mutableStateOf(hasLocationPermission(context)) }
    val launcher = rememberLauncherForActivityResult(ActivityResultContracts.RequestMultiplePermissions()) {
        permitted = hasLocationPermission(context)
    }

    Column(
        modifier = Modifier.fillMaxSize().padding(24.dp),
        verticalArrangement = Arrangement.Center,
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        if (!permitted) {
            Text(
                "Pro zjištění nadmořské výšky potřebuje aplikace přístup k přesné poloze (GPS).",
                textAlign = TextAlign.Center
            )
            Spacer(Modifier.height(16.dp))
            Button(onClick = {
                launcher.launch(
                    arrayOf(Manifest.permission.ACCESS_FINE_LOCATION, Manifest.permission.ACCESS_COARSE_LOCATION)
                )
            }) { Text("Povolit polohu") }
        } else {
            AltitudeContent()
        }
    }
}

@SuppressLint("MissingPermission")
@Composable
private fun AltitudeContent() {
    val context = LocalContext.current
    val lifecycleOwner = LocalLifecycleOwner.current
    val locationManager = remember { context.getSystemService(Context.LOCATION_SERVICE) as LocationManager }
    var reading by remember { mutableStateOf<AltitudeReading?>(null) }
    var gpsEnabled by remember { mutableStateOf(locationManager.isProviderEnabled(LocationManager.GPS_PROVIDER)) }

    DisposableEffect(lifecycleOwner) {
        var lastGga: GgaFix? = null
        var lastGgaTime = 0L

        val nmeaListener = OnNmeaMessageListener { message, _ ->
            Nmea.parseGga(message)?.let {
                lastGga = it
                lastGgaTime = SystemClock.elapsedRealtime()
                val previous = reading
                reading = AltitudeReading(it.altitudeMsl, AltitudeSource.NMEA, previous?.verticalAccuracy, it.satellites)
            }
        }

        val locationListener = object : LocationListener {
            override fun onLocationChanged(location: Location) {
                gpsEnabled = true
                val accuracy = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O && location.hasVerticalAccuracy()) {
                    location.verticalAccuracyMeters
                } else {
                    null
                }
                val gga = lastGga
                reading = when {
                    gga != null && SystemClock.elapsedRealtime() - lastGgaTime < NMEA_MAX_AGE_MS ->
                        AltitudeReading(gga.altitudeMsl, AltitudeSource.NMEA, accuracy, gga.satellites)
                    Build.VERSION.SDK_INT >= Build.VERSION_CODES.UPSIDE_DOWN_CAKE && location.hasMslAltitude() ->
                        AltitudeReading(location.mslAltitudeMeters, AltitudeSource.MSL, location.mslAltitudeAccuracyMeters, null)
                    location.hasAltitude() ->
                        AltitudeReading(location.altitude, AltitudeSource.ELLIPSOID, accuracy, null)
                    else -> reading
                }
            }

            override fun onProviderEnabled(provider: String) {
                gpsEnabled = true
            }

            override fun onProviderDisabled(provider: String) {
                gpsEnabled = false
            }

            @Deprecated("Deprecated in Java")
            override fun onStatusChanged(provider: String?, status: Int, extras: Bundle?) = Unit
        }

        fun start() {
            gpsEnabled = locationManager.isProviderEnabled(LocationManager.GPS_PROVIDER)
            locationManager.requestLocationUpdates(
                LocationManager.GPS_PROVIDER, 1_000L, 0f, locationListener, Looper.getMainLooper()
            )
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.R) {
                locationManager.addNmeaListener(ContextCompat.getMainExecutor(context), nmeaListener)
            } else {
                @Suppress("DEPRECATION")
                locationManager.addNmeaListener(nmeaListener, Handler(Looper.getMainLooper()))
            }
        }

        fun stop() {
            locationManager.removeUpdates(locationListener)
            locationManager.removeNmeaListener(nmeaListener)
        }

        val observer = LifecycleEventObserver { _, event ->
            when (event) {
                Lifecycle.Event.ON_START -> start()
                Lifecycle.Event.ON_STOP -> stop()
                else -> Unit
            }
        }
        lifecycleOwner.lifecycle.addObserver(observer)
        onDispose {
            lifecycleOwner.lifecycle.removeObserver(observer)
            stop()
        }
    }

    Text("Nadmořská výška", style = MaterialTheme.typography.titleLarge)
    Spacer(Modifier.height(16.dp))

    val current = reading
    if (current == null) {
        Text("— m", fontSize = 72.sp, fontWeight = FontWeight.Bold)
        Spacer(Modifier.height(16.dp))
        if (gpsEnabled) {
            Text("Čekám na signál GPS…", textAlign = TextAlign.Center)
            Text(
                "Venku pod širým nebem to bývá nejrychlejší.",
                style = MaterialTheme.typography.bodySmall,
                textAlign = TextAlign.Center
            )
        } else {
            Text("GPS je vypnutá.", textAlign = TextAlign.Center)
            Spacer(Modifier.height(8.dp))
            Button(onClick = {
                context.startActivity(Intent(Settings.ACTION_LOCATION_SOURCE_SETTINGS))
            }) { Text("Zapnout polohu") }
        }
    } else {
        Text("${current.meters.roundToInt()} m", fontSize = 72.sp, fontWeight = FontWeight.Bold)
        Spacer(Modifier.height(8.dp))
        current.verticalAccuracy?.let {
            Text("přesnost ± ${it.roundToInt()} m", style = MaterialTheme.typography.bodyLarge)
        }
        current.satellites?.let {
            Text("satelity: $it", style = MaterialTheme.typography.bodyMedium)
        }
        if (current.source == AltitudeSource.ELLIPSOID) {
            Spacer(Modifier.height(16.dp))
            Text(
                "Telefon neposkytl výšku nad mořem, zobrazena je výška nad elipsoidem WGS84 (v ČR asi o 45 m vyšší).",
                style = MaterialTheme.typography.bodySmall,
                textAlign = TextAlign.Center
            )
        }
        if (!gpsEnabled) {
            Spacer(Modifier.height(8.dp))
            Text("GPS je vypnutá, hodnota nemusí být aktuální.", style = MaterialTheme.typography.bodySmall)
        }
    }
}
