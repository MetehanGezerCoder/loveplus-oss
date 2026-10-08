package com.loveplus.heartbeat

import org.junit.Assert.assertArrayEquals
import org.junit.Assert.assertThrows
import org.junit.Test

class HeartbeatPatternNormalizerTest {
    @Test
    fun normalizes_entry_duration_without_repeating() {
        assertArrayEquals(longArrayOf(0, 1500), HeartbeatPatternNormalizer.normalize(listOf(0, 1900)))
    }

    @Test
    fun rejects_pattern_that_does_not_start_with_delay() {
        assertThrows(IllegalArgumentException::class.java) {
            HeartbeatPatternNormalizer.normalize(listOf(90, 120))
        }
    }

    @Test
    fun rejects_total_duration_over_safety_limit() {
        assertThrows(IllegalArgumentException::class.java) {
            HeartbeatPatternNormalizer.normalize(listOf(0, 1500, 1500, 1500, 1500, 1500, 1500))
        }
    }
}
