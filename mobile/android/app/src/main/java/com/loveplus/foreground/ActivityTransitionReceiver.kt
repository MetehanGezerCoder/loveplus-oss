package com.loveplus.foreground

import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import com.google.android.gms.location.ActivityRecognitionResult
import com.google.android.gms.location.DetectedActivity
import com.loveplus.storage.SecurePrefs

class ActivityTransitionReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        if (!ActivityRecognitionResult.hasResult(intent)) return
        val result = ActivityRecognitionResult.extractResult(intent) ?: return
        val detected = result.probableActivities.maxByOrNull { it.confidence } ?: return
        if (detected.confidence < CONFIDENCE_THRESHOLD) return
        val next = when (detected.type) {
            DetectedActivity.STILL -> "stationary"
            DetectedActivity.WALKING -> "walking"
            DetectedActivity.RUNNING -> "running"
            DetectedActivity.ON_BICYCLE -> "cycling"
            DetectedActivity.IN_VEHICLE -> "inVehicle"
            else -> "unknown"
        }
        val preferences = SecurePrefs.get(context)
        val candidate = preferences.getString("activity_candidate", null)
        val count = if (candidate == next) preferences.getInt("activity_candidate_count", 0) + 1 else 1
        val editor = preferences.edit()
            .putString("activity_candidate", next)
            .putInt("activity_candidate_count", count)
        if (count >= REQUIRED_MATCHES && preferences.getString("last_activity", "unknown") != next) {
            editor.putString("last_activity", next)
        }
        editor.apply()
    }

    companion object {
        private const val CONFIDENCE_THRESHOLD = 70
        private const val REQUIRED_MATCHES = 2
    }
}
