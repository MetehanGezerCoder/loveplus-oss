package com.loveplus.foreground

import android.Manifest
import android.annotation.SuppressLint
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.content.IntentFilter
import android.content.pm.PackageManager
import android.location.Location
import android.net.ConnectivityManager
import android.net.Network
import android.os.BatteryManager
import android.os.Build
import android.os.IBinder
import android.content.pm.ServiceInfo
import androidx.core.app.NotificationCompat
import androidx.core.app.ServiceCompat
import androidx.core.content.ContextCompat
import com.google.android.gms.location.ActivityRecognition
import com.google.android.gms.location.FusedLocationProviderClient
import com.google.android.gms.location.LocationCallback
import com.google.android.gms.location.LocationRequest
import com.google.android.gms.location.LocationResult
import com.google.android.gms.location.LocationServices
import com.google.android.gms.location.Priority
import com.google.android.gms.tasks.CancellationTokenSource
import com.loveplus.MainActivity
import com.loveplus.R
import com.loveplus.storage.SecurePrefs
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import org.json.JSONObject
import java.time.Instant
import kotlin.math.abs

class LoveStatusService : Service() {
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)
    private val sendMutex = Mutex()
    private lateinit var locationClient: FusedLocationProviderClient
    private lateinit var queue: StatusQueueStore
    private lateinit var sender: StatusSender
    private lateinit var connectivity: ConnectivityManager
    private var activityIntent: PendingIntent? = null
    private var heartbeatJob: Job? = null
    private var locationUpdatesStarted = false
    private var lastLocation: Location? = null
    private var lastPublishedLocation: Location? = null
    private var lastBatteryLevel = -1
    private var lastCharging: Boolean? = null
    private var lastPublishedAt = 0L

    private val locationCallback = object : LocationCallback() {
        override fun onLocationResult(result: LocationResult) {
            result.lastLocation?.let {
                lastLocation = it
                if (shouldPublishLocation(it)) publishStatus("location")
            }
        }
    }
    private val networkCallback = object : ConnectivityManager.NetworkCallback() {
        override fun onAvailable(network: Network) { scope.launch { flushQueue() } }
    }
    private val batteryReceiver = object : BroadcastReceiver() {
        override fun onReceive(context: Context?, intent: Intent?) {
            val battery = batteryState(intent)
            if (lastBatteryLevel < 0 || abs(battery.level - lastBatteryLevel) >= BATTERY_DELTA || lastCharging != battery.charging) {
                publishStatus("battery", battery)
            }
        }
    }

    override fun onCreate() {
        super.onCreate()
        createNotificationChannel()
        locationClient = LocationServices.getFusedLocationProviderClient(this)
        queue = StatusQueueStore(this)
        sender = StatusSender(this)
        connectivity = getSystemService(ConnectivityManager::class.java)
        connectivity.registerDefaultNetworkCallback(networkCallback)
        registerReceiver(batteryReceiver, IntentFilter(Intent.ACTION_BATTERY_CHANGED))
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        if (!SecurePrefs.get(this).getBoolean("telemetry_enabled", false)) {
            stopSelf()
            return START_NOT_STICKY
        }
        promoteToForeground()
        if (locationSharingEnabled()) startLocationUpdates() else stopLocationUpdates()
        startActivityRecognition()
        startHeartbeat()
        scope.launch { flushQueue() }
        if (intent?.action == ACTION_PUBLISH_NOW || lastPublishedAt == 0L) publishStatus("start")
        return START_STICKY
    }

    @SuppressLint("MissingPermission")
    private fun startLocationUpdates() {
        if (ContextCompat.checkSelfPermission(this, Manifest.permission.ACCESS_FINE_LOCATION) != PackageManager.PERMISSION_GRANTED) return
        if (locationUpdatesStarted) return
        locationUpdatesStarted = true
        val request = LocationRequest.Builder(Priority.PRIORITY_HIGH_ACCURACY, 60_000L)
            .setMinUpdateIntervalMillis(15_000L)
            .setMinUpdateDistanceMeters(25f)
            .setMaxUpdateDelayMillis(120_000L)
            .setWaitForAccurateLocation(false)
            .build()
        locationClient.lastLocation.addOnSuccessListener { location ->
            location?.let {
                lastLocation = it
                if (shouldPublishLocation(it)) publishStatus("last-location")
            }
        }
        locationClient.getCurrentLocation(Priority.PRIORITY_HIGH_ACCURACY, CancellationTokenSource().token)
            .addOnSuccessListener { location ->
                location?.let {
                    lastLocation = it
                    if (shouldPublishLocation(it)) publishStatus("current-location")
                }
            }
        locationClient.requestLocationUpdates(request, locationCallback, mainLooper)
            .addOnFailureListener { locationUpdatesStarted = false }
    }

    private fun stopLocationUpdates() {
        if (::locationClient.isInitialized) locationClient.removeLocationUpdates(locationCallback)
        locationUpdatesStarted = false
        lastLocation = null
    }

    @SuppressLint("MissingPermission")
    private fun startActivityRecognition() {
        val preferences = SecurePrefs.get(this)
        if (!preferences.getBoolean("activity_enabled", false)) {
            preferences.edit().putString("last_activity", "unknown").apply()
            return
        }
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q &&
            ContextCompat.checkSelfPermission(this, Manifest.permission.ACTIVITY_RECOGNITION) != PackageManager.PERMISSION_GRANTED) {
            preferences.edit().putString("last_activity", "unknown").apply()
            return
        }
        activityIntent = PendingIntent.getBroadcast(
            this,
            42,
            Intent(this, ActivityTransitionReceiver::class.java),
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE,
        )
        ActivityRecognition.getClient(this).requestActivityUpdates(30_000L, activityIntent!!)
    }

    private fun startHeartbeat() {
        if (heartbeatJob?.isActive == true) return
        heartbeatJob = scope.launch {
            while (isActive) {
                delay(HEARTBEAT_INTERVAL_MS)
                publishStatus("heartbeat")
            }
        }
    }

    private fun shouldPublishLocation(location: Location): Boolean {
        if (location.accuracy > MAX_ACCEPTED_ACCURACY_METERS) return false
        val previous = lastPublishedLocation ?: return true
        val elapsed = System.currentTimeMillis() - lastPublishedAt
        return location.distanceTo(previous) >= MIN_DISTANCE_METERS || elapsed >= MAX_LOCATION_SILENCE_MS
    }

    private fun publishStatus(reason: String, batteryOverride: BatterySnapshot? = null) {
        val preferences = SecurePrefs.get(this)
        if (!preferences.getBoolean("telemetry_enabled", false)) return
        val battery = batteryOverride ?: batteryState(null)
        val sharing = locationSharingEnabled()
        val location = lastLocation?.takeIf { sharing && it.accuracy <= MAX_ACCEPTED_ACCURACY_METERS }
        val now = Instant.now().toString()
        val payload = JSONObject()
            .put("isLocationSharingEnabled", sharing)
            .put("shareLastKnownLocation", preferences.getBoolean("share_last_location", true))
            .put("batteryLevel", battery.level)
            .put("isCharging", battery.charging)
            .put("batteryState", battery.state)
            .put("activityType", preferences.getString("last_activity", "unknown"))
            .put("mood", preferences.getString("mood", "none"))
            .put("isMoodSharingEnabled", preferences.getBoolean(userPreferenceKey("mood_sharing"), true))
            .put("recordedAtUtc", now)
            .put("sequenceNumber", nextSequence())
            .put("source", "realDevice")
        if (location != null) {
            payload.put("location", JSONObject()
                .put("latitude", location.latitude)
                .put("longitude", location.longitude)
                .put("accuracy", location.accuracy.toDouble())
                .put("speed", if (location.hasSpeed()) location.speed.toDouble() else JSONObject.NULL)
                .put("heading", if (location.hasBearing()) location.bearing.toDouble() else JSONObject.NULL)
                .put("altitude", if (location.hasAltitude()) location.altitude else JSONObject.NULL)
                .put("recordedAtUtc", Instant.ofEpochMilli(location.time).toString()))
        } else payload.put("location", JSONObject.NULL)
        lastBatteryLevel = battery.level
        lastCharging = battery.charging
        lastPublishedAt = System.currentTimeMillis()
        if (location != null) lastPublishedLocation = Location(location)
        scope.launch {
            sendMutex.withLock {
                flushQueueLocked()
                if (!sender.send(payload.toString())) queue.enqueue(payload.toString())
            }
        }
    }

    private fun batteryState(intent: Intent?): BatterySnapshot {
        val status = intent ?: registerReceiver(null, IntentFilter(Intent.ACTION_BATTERY_CHANGED))
        val level = status?.getIntExtra(BatteryManager.EXTRA_LEVEL, -1) ?: -1
        val scale = status?.getIntExtra(BatteryManager.EXTRA_SCALE, 100) ?: 100
        val percent = if (level >= 0 && scale > 0) ((level * 100f) / scale).toInt().coerceIn(0, 100) else 0
        val rawState = status?.getIntExtra(BatteryManager.EXTRA_STATUS, -1)
        val charging = rawState == BatteryManager.BATTERY_STATUS_CHARGING || rawState == BatteryManager.BATTERY_STATUS_FULL
        val state = when (rawState) {
            BatteryManager.BATTERY_STATUS_CHARGING -> "charging"
            BatteryManager.BATTERY_STATUS_DISCHARGING -> "discharging"
            BatteryManager.BATTERY_STATUS_FULL -> "full"
            BatteryManager.BATTERY_STATUS_NOT_CHARGING -> "notCharging"
            else -> "unknown"
        }
        return BatterySnapshot(percent, charging, state)
    }

    private fun locationSharingEnabled() = SecurePrefs.get(this).getBoolean("location_sharing_enabled", false)

    private fun userPreferenceKey(name: String): String {
        val userId = SecurePrefs.get(this).getString("telemetry_user_id", "anonymous")
        return "phase3:$userId:$name"
    }

    @Synchronized
    private fun nextSequence(): Long {
        val preferences = SecurePrefs.get(this)
        val userId = preferences.getString("telemetry_user_id", "anonymous")
        val key = "status_sequence:$userId"
        val next = preferences.getLong(key, 0L) + 1
        preferences.edit().putLong(key, next).commit()
        return next
    }

    private suspend fun flushQueue() = sendMutex.withLock { flushQueueLocked() }
    private fun flushQueueLocked() {
        val pending = queue.drain()
        pending.forEachIndexed { index, payload ->
            if (!sender.send(payload)) {
                pending.drop(index).forEach(queue::enqueue)
                return
            }
        }
    }

    private fun createNotificationChannel() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            val channel = NotificationChannel(CHANNEL_ID, getString(R.string.tracking_channel_name), NotificationManager.IMPORTANCE_LOW)
            getSystemService(NotificationManager::class.java).createNotificationChannel(channel)
        }
    }

    private fun promoteToForeground() {
        var serviceType = 0
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            val locationGranted = ContextCompat.checkSelfPermission(this, Manifest.permission.ACCESS_FINE_LOCATION) == PackageManager.PERMISSION_GRANTED
            val activityGranted = Build.VERSION.SDK_INT < Build.VERSION_CODES.Q ||
                ContextCompat.checkSelfPermission(this, Manifest.permission.ACTIVITY_RECOGNITION) == PackageManager.PERMISSION_GRANTED
            if (locationSharingEnabled() && locationGranted) serviceType = serviceType or ServiceInfo.FOREGROUND_SERVICE_TYPE_LOCATION
            if (SecurePrefs.get(this).getBoolean("activity_enabled", false) && activityGranted) serviceType = serviceType or ServiceInfo.FOREGROUND_SERVICE_TYPE_HEALTH
            if (serviceType == 0 && Build.VERSION.SDK_INT >= Build.VERSION_CODES.UPSIDE_DOWN_CAKE) {
                serviceType = ServiceInfo.FOREGROUND_SERVICE_TYPE_SPECIAL_USE
            }
        }
        ServiceCompat.startForeground(this, NOTIFICATION_ID, buildNotification(), serviceType)
    }

    private fun buildNotification() = NotificationCompat.Builder(this, CHANNEL_ID)
        .setSmallIcon(android.R.drawable.ic_menu_mylocation)
        .setContentTitle(getString(R.string.tracking_notification_title))
        .setContentText(if (locationSharingEnabled()) "Konum ve cihaz durumu partnerinle paylaşılıyor" else "Pil ve aktivite durumu partnerinle paylaşılıyor")
        .setOngoing(true)
        .setCategory(NotificationCompat.CATEGORY_SERVICE)
        .setContentIntent(PendingIntent.getActivity(this, 7, Intent(this, MainActivity::class.java), PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE))
        .build()

    override fun onDestroy() {
        stopLocationUpdates()
        runCatching { connectivity.unregisterNetworkCallback(networkCallback) }
        runCatching { unregisterReceiver(batteryReceiver) }
        activityIntent?.let { ActivityRecognition.getClient(this).removeActivityUpdates(it) }
        scope.cancel()
        super.onDestroy()
    }

    override fun onBind(intent: Intent?): IBinder? = null

    private data class BatterySnapshot(val level: Int, val charging: Boolean, val state: String)

    companion object {
        const val ACTION_PUBLISH_NOW = "com.loveplus.PUBLISH_NOW"
        private const val CHANNEL_ID = "loveplus-live-status"
        private const val NOTIFICATION_ID = 1201
        private const val BATTERY_DELTA = 2

        // Presence is derived on the server from the age of the newest packet. The backend
        // requires its online window to be at least twice this cadence
        // (Realtime__ClientStatusHeartbeatSeconds), so one missed beat never reports a
        // connected partner as offline. Changing this value means changing that setting too.
        const val HEARTBEAT_INTERVAL_SECONDS = 60
        private const val HEARTBEAT_INTERVAL_MS = HEARTBEAT_INTERVAL_SECONDS * 1_000L
        private const val MAX_LOCATION_SILENCE_MS = 120_000L
        private const val MIN_DISTANCE_METERS = 25f
        private const val MAX_ACCEPTED_ACCURACY_METERS = 250f
    }
}
