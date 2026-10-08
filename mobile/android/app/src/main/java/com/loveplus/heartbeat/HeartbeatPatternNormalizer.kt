package com.loveplus.heartbeat

object HeartbeatPatternNormalizer {
    const val MAX_ENTRIES = 32
    const val MAX_ENTRY_MS = 1_500L
    const val MAX_TOTAL_MS = 8_000L

    fun normalize(raw: List<Long>): LongArray {
        require(raw.size in 2..MAX_ENTRIES) { "Heartbeat pattern size is invalid." }
        require(raw.first() == 0L) { "Heartbeat pattern must start with zero delay." }
        val normalized = raw.mapIndexed { index, value ->
            if (index == 0) 0L else value.coerceIn(30L, MAX_ENTRY_MS)
        }
        require(normalized.sum() in 1..MAX_TOTAL_MS) { "Heartbeat pattern duration is invalid." }
        return normalized.toLongArray()
    }
}
