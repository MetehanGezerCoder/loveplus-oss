package com.loveplus.widget

import android.app.PendingIntent
import android.appwidget.AppWidgetManager
import android.appwidget.AppWidgetProvider
import android.content.ComponentName
import android.content.Context
import android.content.Intent
import android.widget.RemoteViews
import com.loveplus.MainActivity
import com.loveplus.R
import com.loveplus.storage.SecurePrefs
import java.time.Duration
import java.time.Instant

data class WidgetState(
    val partnerName: String,
    val batteryLevel: Int,
    val isCharging: Boolean,
    val activity: String,
    val distance: String,
    val lastUpdated: String,
    val presence: String,
    val locationSharing: Boolean,
    val mood: String,
    // The widget only changes when the app pushes a new value. Without the windows that
    // produced the verdict it would keep reporting a partner as online hours after their
    // phone stopped reporting, so they travel with the state and are re-applied on redraw.
    val presenceOnlineSeconds: Int = DEFAULT_ONLINE_SECONDS,
    val presenceRecentlyOnlineSeconds: Int = DEFAULT_RECENTLY_ONLINE_SECONDS,
) {
    companion object {
        const val DEFAULT_ONLINE_SECONDS = 180
        const val DEFAULT_RECENTLY_ONLINE_SECONDS = 600
    }
}

object WidgetStateStore {
    private const val KEY = "partner_widget_state"

    fun write(context: Context, state: WidgetState) {
        val value = listOf(
            state.partnerName,
            state.batteryLevel.toString(),
            state.isCharging.toString(),
            state.activity,
            state.distance,
            state.lastUpdated,
            state.presence,
            state.locationSharing.toString(),
            state.mood,
            state.presenceOnlineSeconds.toString(),
            state.presenceRecentlyOnlineSeconds.toString(),
        ).joinToString("\u001F")
        SecurePrefs.get(context).edit().putString(KEY, value).apply()
        LoveStatusWidget.refreshAll(context)
    }

    fun read(context: Context): WidgetState? {
        val parts = SecurePrefs.get(context).getString(KEY, null)?.split("\u001F") ?: return null
        if (parts.size !in 8..11) return null
        return WidgetState(
            parts[0],
            parts[1].toIntOrNull() ?: 0,
            parts[2].toBoolean(),
            parts[3],
            parts[4],
            parts[5],
            parts[6],
            parts[7].toBoolean(),
            parts.getOrElse(8) { "none" },
            parts.getOrNull(9)?.toIntOrNull() ?: WidgetState.DEFAULT_ONLINE_SECONDS,
            parts.getOrNull(10)?.toIntOrNull() ?: WidgetState.DEFAULT_RECENTLY_ONLINE_SECONDS,
        )
    }

    fun clear(context: Context) {
        SecurePrefs.get(context).edit().remove(KEY).apply()
        LoveStatusWidget.refreshAll(context)
    }
}

class LoveStatusWidget : AppWidgetProvider() {
    override fun onUpdate(context: Context, manager: AppWidgetManager, ids: IntArray) {
        ids.forEach { manager.updateAppWidget(it, views(context)) }
    }

    companion object {
        fun refreshAll(context: Context) {
            val manager = AppWidgetManager.getInstance(context)
            val component = ComponentName(context, LoveStatusWidget::class.java)
            manager.getAppWidgetIds(component).forEach { manager.updateAppWidget(it, views(context)) }
        }

        private fun views(context: Context): RemoteViews {
            val state = WidgetStateStore.read(context)
            return RemoteViews(context.packageName, R.layout.love_status_widget).apply {
                setTextViewText(R.id.widget_partner_name, state?.partnerName ?: "Partner")
                setTextViewText(R.id.widget_summary, state?.let { "Pil %${it.batteryLevel}${if (it.isCharging) " ⚡" else ""}  •  ${if (it.locationSharing) it.distance else "Konum kapalı"}" } ?: "Pil —  •  Mesafe —")
                setTextViewText(R.id.widget_activity, state?.let { "${activityLabel(it.activity)}  •  ${moodLabel(it.mood)}  •  ${presenceLabel(derivePresence(it))}" } ?: "Aktivite —")
                setTextViewText(R.id.widget_freshness, state?.lastUpdated?.let(::relativeTime) ?: "Henüz güncelleme yok")
                setOnClickPendingIntent(R.id.widget_brand, PendingIntent.getActivity(
                    context, 9, Intent(context, MainActivity::class.java), PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE,
                ))
            }
        }

        private fun relativeTime(value: String): String = runCatching {
            val minutes = Duration.between(Instant.parse(value), Instant.now()).toMinutes().coerceAtLeast(0)
            when { minutes < 1 -> "az önce"; minutes < 60 -> "$minutes dk önce"; else -> "${minutes / 60} sa önce" }
        }.getOrDefault("son güncelleme bilinmiyor")

        private fun activityLabel(activity: String) = when (activity) {
            "stationary" -> "Sabit"; "walking" -> "Yürüyor"; "running" -> "Koşuyor"; "cycling" -> "Bisiklette"; "inVehicle" -> "Araçta"; else -> "Bilinmiyor"
        }

        /// Mirrors mobile/src/features/realtime-status/presence.ts: the stored verdict is
        /// only trusted when the timestamp cannot be read or the clocks disagree.
        private fun derivePresence(state: WidgetState): String = runCatching {
            val ageSeconds = Duration.between(Instant.parse(state.lastUpdated), Instant.now()).seconds
            when {
                ageSeconds < 0 -> state.presence
                ageSeconds <= state.presenceOnlineSeconds -> "online"
                ageSeconds <= state.presenceRecentlyOnlineSeconds -> "recentlyOnline"
                else -> "offline"
            }
        }.getOrDefault(state.presence)

        private fun presenceLabel(presence: String) = when (presence) {
            "online" -> "çevrimiçi"; "recentlyOnline" -> "yakın zamanda"; else -> "çevrimdışı"
        }

        private fun moodLabel(mood: String) = when (mood) {
            "happy" -> "😊 Mutlu"; "calm" -> "😌 Sakin"; "excited" -> "🤩 Heyecanlı"
            "tired" -> "😴 Yorgun"; "sad" -> "😔 Üzgün"; "busy" -> "💼 Meşgul"
            "inLove" -> "😍 Aşık"; "missingYou" -> "🥺 Özledi"; "sulky" -> "😒 Tripli"
            else -> "💭 Seçilmedi"
        }
    }
}
