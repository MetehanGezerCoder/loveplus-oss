package com.loveplus.foreground

import android.content.Context
import com.loveplus.storage.SecurePrefs
import org.json.JSONArray
import org.json.JSONObject

class StatusQueueStore(context: Context) {
    private val preferences = SecurePrefs.get(context)
    private fun key(): String = "offline_status_queue:${preferences.getString("telemetry_user_id", "anonymous")}" 

    @Synchronized
    fun enqueue(payload: String) {
        val pending = read().toMutableList()
        val sequence = JSONObject(payload).optLong("sequenceNumber")
        pending.removeAll { JSONObject(it).optLong("sequenceNumber") == sequence }
        pending.add(payload)
        pending.sortBy { JSONObject(it).optLong("sequenceNumber") }
        while (pending.size > MAX_ITEMS) pending.removeAt(0)
        write(pending)
    }

    @Synchronized
    fun drain(): List<String> {
        val values = read().sortedBy { JSONObject(it).optLong("sequenceNumber") }
        preferences.edit().remove(key()).commit()
        return values
    }

    @Synchronized
    fun count(): Int = read().size

    @Synchronized
    fun clearAll() {
        preferences.all.keys
            .filter { it.startsWith("offline_status_queue:") }
            .fold(preferences.edit()) { editor, item -> editor.remove(item) }
            .commit()
    }

    private fun read(): List<String> {
        val array = JSONArray(preferences.getString(key(), "[]"))
        return buildList { for (index in 0 until array.length()) add(array.getString(index)) }
    }

    private fun write(values: List<String>) {
        val array = JSONArray()
        values.forEach(array::put)
        preferences.edit().putString(key(), array.toString()).commit()
    }

    companion object { private const val MAX_ITEMS = 50 }
}
