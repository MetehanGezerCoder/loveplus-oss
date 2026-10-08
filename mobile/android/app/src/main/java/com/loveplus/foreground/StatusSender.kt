package com.loveplus.foreground

import android.content.Context
import com.loveplus.storage.SecurePrefs
import java.net.HttpURLConnection
import java.net.URL

class StatusSender(context: Context) {
    private val preferences = SecurePrefs.get(context)

    fun send(payload: String): Boolean {
        val baseUrl = preferences.getString("api_base_url", null) ?: return record(false, "not_configured")
        val token = preferences.getString("access_token", null) ?: return record(false, "not_configured")
        val connection = (URL("$baseUrl/api/status/").openConnection() as HttpURLConnection).apply {
            requestMethod = "POST"
            connectTimeout = 8_000
            readTimeout = 8_000
            doOutput = true
            setRequestProperty("Authorization", "Bearer $token")
            setRequestProperty("Content-Type", "application/json")
            setRequestProperty("Accept", "application/json")
        }
        return try {
            connection.outputStream.use { it.write(payload.toByteArray(Charsets.UTF_8)) }
            val status = connection.responseCode
            // 409 means the server already holds a newer packet for this user. The upload
            // itself reached the API, so it counts as a healthy round trip rather than a
            // reason to re-queue a stale packet.
            when {
                status in 200..299 -> record(true, "ok")
                status == 409 -> record(true, "superseded")
                status == 401 || status == 403 -> record(false, "unauthorized")
                status == 429 -> record(false, "rate_limited")
                else -> record(false, "http_$status")
            }
        } catch (_: Exception) {
            // Only the failure category is stored. Payloads carry coordinates and must never
            // reach diagnostics, preferences or logs.
            record(false, "network_unreachable")
        } finally {
            connection.disconnect()
        }
    }

    private fun record(success: Boolean, reason: String): Boolean {
        val now = System.currentTimeMillis()
        val editor = preferences.edit().putString(KEY_LAST_RESULT, reason)
        if (success) editor.putLong(KEY_LAST_SUCCESS_AT, now) else editor.putLong(KEY_LAST_FAILURE_AT, now)
        editor.apply()
        return success
    }

    companion object {
        const val KEY_LAST_SUCCESS_AT = "telemetry_last_success_at"
        const val KEY_LAST_FAILURE_AT = "telemetry_last_failure_at"
        const val KEY_LAST_RESULT = "telemetry_last_result"
    }
}
