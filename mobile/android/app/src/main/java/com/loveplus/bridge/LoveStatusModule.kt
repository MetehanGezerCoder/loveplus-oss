package com.loveplus.bridge

import android.content.Intent
import android.content.Context
import android.os.VibrationEffect
import android.os.Vibrator
import android.os.VibratorManager
import android.os.Build
import androidx.core.content.ContextCompat
import com.facebook.react.bridge.Arguments
import com.facebook.react.bridge.Promise
import com.facebook.react.bridge.ReactApplicationContext
import com.facebook.react.bridge.ReactContextBaseJavaModule
import com.facebook.react.bridge.ReactMethod
import com.facebook.react.bridge.ReadableMap
import com.facebook.react.bridge.ReadableArray
import com.loveplus.BuildConfig
import com.loveplus.foreground.LoveStatusService
import com.loveplus.foreground.StatusQueueStore
import com.loveplus.foreground.StatusSender
import com.loveplus.storage.SecurePrefs
import com.loveplus.widget.WidgetState
import com.loveplus.widget.WidgetStateStore
import com.loveplus.heartbeat.HeartbeatPatternNormalizer

class LoveStatusModule(private val reactContext: ReactApplicationContext) : ReactContextBaseJavaModule(reactContext) {
    override fun getName(): String = "LoveStatus"

    @ReactMethod
    fun configure(apiBaseUrl: String, accessToken: String, deviceId: String, userId: String, promise: Promise) {
        if (!apiBaseUrl.startsWith("https://") && !BuildConfigBridge.isDebug) {
            promise.reject("invalid_api_url", "Production API URL must use HTTPS.")
            return
        }
        val preferences = SecurePrefs.get(reactContext)
        val previousUser = preferences.getString("telemetry_user_id", null)
        if (previousUser != null && previousUser != userId) {
            reactContext.stopService(Intent(reactContext, LoveStatusService::class.java))
            StatusQueueStore(reactContext).clearAll()
            preferences.edit()
                .remove("last_activity")
                .remove("activity_candidate")
                .remove("activity_candidate_count")
                .remove("mood")
                .remove("partner_widget_state")
                .putBoolean("telemetry_enabled", false)
                .putBoolean("location_sharing_enabled", false)
                .commit()
        }
        preferences.edit()
            .putString("api_base_url", apiBaseUrl.trimEnd('/'))
            .putString("access_token", accessToken)
            .putString("device_id", deviceId)
            .putString("telemetry_user_id", userId)
            .apply()
        promise.resolve(null)
    }

    @ReactMethod
    fun startTelemetry(locationSharing: Boolean, activityEnabled: Boolean, shareLastKnown: Boolean, promise: Promise) {
        val preferences = SecurePrefs.get(reactContext)
        if (preferences.getString("access_token", null).isNullOrBlank()) {
            promise.reject("session_missing", "A configured session is required.")
            return
        }
        preferences.edit()
            .putBoolean("telemetry_enabled", true)
            .putBoolean("location_sharing_enabled", locationSharing)
            .putBoolean("activity_enabled", activityEnabled)
            .putBoolean("share_last_location", shareLastKnown)
            .apply()
        ContextCompat.startForegroundService(
            reactContext,
            Intent(reactContext, LoveStatusService::class.java).setAction(LoveStatusService.ACTION_PUBLISH_NOW),
        )
        promise.resolve(null)
    }

    @ReactMethod
    fun updateSharing(locationSharing: Boolean, activityEnabled: Boolean, shareLastKnown: Boolean, promise: Promise) {
        SecurePrefs.get(reactContext).edit()
            .putBoolean("location_sharing_enabled", locationSharing)
            .putBoolean("activity_enabled", activityEnabled)
            .putBoolean("share_last_location", shareLastKnown)
            .apply()
        ContextCompat.startForegroundService(
            reactContext,
            Intent(reactContext, LoveStatusService::class.java).setAction(LoveStatusService.ACTION_PUBLISH_NOW),
        )
        promise.resolve(null)
    }

    @ReactMethod
    fun setMood(mood: String, promise: Promise) {
        val preferences = SecurePrefs.get(reactContext)
        if (preferences.getString("mood", "none") == mood) {
            promise.resolve(null)
            return
        }
        preferences.edit().putString("mood", mood).apply()
        promise.resolve(null)
    }

    @ReactMethod
    fun getPhaseThreePreferences(promise: Promise) {
        val preferences = SecurePrefs.get(reactContext)
        promise.resolve(Arguments.createMap().apply {
            putBoolean("receiveHeartbeats", preferences.getBoolean(userKey("receive_heartbeats"), true))
            putBoolean("sendHeartbeats", preferences.getBoolean(userKey("send_heartbeats"), true))
            putString("hapticIntensity", preferences.getString(userKey("haptic_intensity"), "medium"))
            putBoolean("receiveAnimation", preferences.getBoolean(userKey("receive_animation"), true))
            putBoolean("showProximity", preferences.getBoolean(userKey("show_proximity"), true))
            putBoolean("moodSharing", preferences.getBoolean(userKey("mood_sharing"), true))
        })
    }

    @ReactMethod
    fun updatePhaseThreePreferences(state: ReadableMap, promise: Promise) {
        if (!state.getBoolean("receiveHeartbeats")) vibrator().cancel()
        SecurePrefs.get(reactContext).edit()
            .putBoolean(userKey("receive_heartbeats"), state.getBoolean("receiveHeartbeats"))
            .putBoolean(userKey("send_heartbeats"), state.getBoolean("sendHeartbeats"))
            .putString(userKey("haptic_intensity"), state.getString("hapticIntensity") ?: "medium")
            .putBoolean(userKey("receive_animation"), state.getBoolean("receiveAnimation"))
            .putBoolean(userKey("show_proximity"), state.getBoolean("showProximity"))
            .putBoolean(userKey("mood_sharing"), state.getBoolean("moodSharing"))
            .apply()
        reactContext.startService(Intent(reactContext, LoveStatusService::class.java).setAction(LoveStatusService.ACTION_PUBLISH_NOW))
        promise.resolve(null)
    }

    @ReactMethod
    fun supportsHaptics(promise: Promise) {
        promise.resolve(vibrator().hasVibrator())
    }

    @ReactMethod
    fun playHeartbeat(eventId: String, rawPattern: ReadableArray, intensity: String, promise: Promise) {
        try {
            if (!SecurePrefs.get(reactContext).getBoolean(userKey("receive_heartbeats"), true)) {
                promise.resolve(false)
                return
            }
            if (wasPlayed(eventId)) {
                promise.resolve(false)
                return
            }
            val pattern = HeartbeatPatternNormalizer.normalize(
                (0 until rawPattern.size()).map { rawPattern.getDouble(it).toLong() },
            )
            val vibrator = vibrator()
            if (!vibrator.hasVibrator()) {
                promise.resolve(false)
                return
            }
            val amplitude = when (intensity) { "low" -> 80; "high" -> 255; else -> 160 }
            val amplitudes = IntArray(pattern.size) { index -> if (index % 2 == 0) 0 else amplitude }
            vibrator.vibrate(VibrationEffect.createWaveform(pattern, amplitudes, -1))
            rememberPlayed(eventId)
            promise.resolve(true)
        } catch (error: IllegalArgumentException) {
            promise.reject("invalid_heartbeat", error.message, error)
        }
    }

    @ReactMethod
    fun previewHeartbeat(promise: Promise) {
        val intensity = SecurePrefs.get(reactContext).getString(userKey("haptic_intensity"), "medium") ?: "medium"
        val amplitude = when (intensity) { "low" -> 80; "high" -> 255; else -> 160 }
        if (vibrator().hasVibrator()) vibrator().vibrate(VibrationEffect.createOneShot(55, amplitude))
        promise.resolve(null)
    }

    @ReactMethod
    fun cancelHeartbeat(promise: Promise) {
        vibrator().cancel()
        promise.resolve(null)
    }

    @ReactMethod
    fun getTelemetryState(promise: Promise) {
        val preferences = SecurePrefs.get(reactContext)
        promise.resolve(Arguments.createMap().apply {
            putBoolean("enabled", preferences.getBoolean("telemetry_enabled", false))
            putBoolean("locationSharing", preferences.getBoolean("location_sharing_enabled", false))
            putBoolean("activityEnabled", preferences.getBoolean("activity_enabled", false))
            putBoolean("shareLastKnown", preferences.getBoolean("share_last_location", true))
            putString("mood", preferences.getString("mood", "none"))
        })
    }

    /// Everything the diagnostics screen needs to explain a broken phone: the build it is
    /// running, whether the foreground service has a session, and when telemetry last
    /// reached the API. It deliberately exposes no token, coordinate or partner value.
    @ReactMethod
    fun getDiagnostics(promise: Promise) {
        val preferences = SecurePrefs.get(reactContext)
        val lastSuccess = preferences.getLong(StatusSender.KEY_LAST_SUCCESS_AT, 0L)
        val lastFailure = preferences.getLong(StatusSender.KEY_LAST_FAILURE_AT, 0L)
        promise.resolve(Arguments.createMap().apply {
            putString("apiBaseUrl", BuildConfig.API_BASE_URL)
            putString("buildType", BuildConfig.BUILD_TYPE)
            putBoolean("isDebugBuild", BuildConfig.DEBUG)
            putString("versionName", BuildConfig.VERSION_NAME)
            putInt("versionCode", BuildConfig.VERSION_CODE)
            putBoolean("mapsEnabled", BuildConfig.MAPS_ENABLED)
            putBoolean("hasTelemetrySession", !preferences.getString("access_token", null).isNullOrBlank())
            putBoolean("telemetryEnabled", preferences.getBoolean("telemetry_enabled", false))
            putBoolean("locationSharing", preferences.getBoolean("location_sharing_enabled", false))
            putBoolean("activityEnabled", preferences.getBoolean("activity_enabled", false))
            putInt("heartbeatIntervalSeconds", LoveStatusService.HEARTBEAT_INTERVAL_SECONDS)
            putInt("queuedStatusCount", StatusQueueStore(reactContext).count())
            putString("lastUploadResult", preferences.getString(StatusSender.KEY_LAST_RESULT, null))
            if (lastSuccess > 0L) putDouble("lastUploadAtMs", lastSuccess.toDouble())
            if (lastFailure > 0L) putDouble("lastUploadFailureAtMs", lastFailure.toDouble())
        })
    }

    /// Asks the foreground service to publish immediately. The dashboard calls this when the
    /// app returns to the foreground so a partner does not wait out the remaining heartbeat.
    @ReactMethod
    fun publishNow(promise: Promise) {
        if (!SecurePrefs.get(reactContext).getBoolean("telemetry_enabled", false)) {
            promise.resolve(false)
            return
        }
        ContextCompat.startForegroundService(
            reactContext,
            Intent(reactContext, LoveStatusService::class.java).setAction(LoveStatusService.ACTION_PUBLISH_NOW),
        )
        promise.resolve(true)
    }

    @ReactMethod
    fun stopTracking(clearUserData: Boolean, promise: Promise) {
        val preferences = SecurePrefs.get(reactContext)
        preferences.edit()
            .putBoolean("telemetry_enabled", false)
            .putBoolean("location_sharing_enabled", false)
            .apply()
        reactContext.stopService(Intent(reactContext, LoveStatusService::class.java))
        if (clearUserData) {
            StatusQueueStore(reactContext).clearAll()
            preferences.edit()
                .remove("access_token")
                .remove("telemetry_user_id")
                .remove("last_activity")
                .remove("mood")
                .remove("partner_widget_state")
                .commit()
            WidgetStateStore.clear(reactContext)
        }
        promise.resolve(null)
    }

    @ReactMethod
    fun updateWidget(state: ReadableMap, promise: Promise) {
        WidgetStateStore.write(
            reactContext,
            WidgetState(
                partnerName = state.getString("partnerName") ?: "Partner",
                batteryLevel = state.getInt("batteryLevel"),
                isCharging = state.getBoolean("isCharging"),
                activity = state.getString("activity") ?: "unknown",
                distance = state.getString("distance") ?: "—",
                lastUpdated = state.getString("lastUpdated") ?: "",
                presence = state.getString("presence") ?: "offline",
                locationSharing = state.getBoolean("locationSharing"),
                mood = state.getString("mood") ?: "none",
                presenceOnlineSeconds = if (state.hasKey("presenceOnlineSeconds")) {
                    state.getInt("presenceOnlineSeconds")
                } else {
                    WidgetState.DEFAULT_ONLINE_SECONDS
                },
                presenceRecentlyOnlineSeconds = if (state.hasKey("presenceRecentlyOnlineSeconds")) {
                    state.getInt("presenceRecentlyOnlineSeconds")
                } else {
                    WidgetState.DEFAULT_RECENTLY_ONLINE_SECONDS
                },
            ),
        )
        promise.resolve(null)
    }

    override fun invalidate() {
        runCatching { vibrator().cancel() }
        super.invalidate()
    }

    private fun vibrator(): Vibrator = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
        reactContext.getSystemService(VibratorManager::class.java).defaultVibrator
    } else {
        @Suppress("DEPRECATION")
        reactContext.getSystemService(Context.VIBRATOR_SERVICE) as Vibrator
    }

    private fun userKey(name: String): String {
        val userId = SecurePrefs.get(reactContext).getString("telemetry_user_id", "anonymous")
        return "phase3:$userId:$name"
    }

    private fun wasPlayed(eventId: String): Boolean {
        val seen = SecurePrefs.get(reactContext).getString(userKey("played_heartbeats"), "") ?: ""
        return seen.split(',').contains(eventId)
    }

    private fun rememberPlayed(eventId: String) {
        val preferences = SecurePrefs.get(reactContext)
        val recent = (preferences.getString(userKey("played_heartbeats"), "") ?: "")
            .split(',').filter { it.isNotBlank() && it != eventId }.takeLast(49) + eventId
        preferences.edit().putString(userKey("played_heartbeats"), recent.joinToString(",")).apply()
    }
}

private object BuildConfigBridge {
    val isDebug: Boolean = com.loveplus.BuildConfig.DEBUG
}
