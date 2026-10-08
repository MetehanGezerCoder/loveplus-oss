package com.loveplus.bridge

import com.facebook.react.bridge.ReactApplicationContext
import com.facebook.react.bridge.ReactContextBaseJavaModule
import com.loveplus.BuildConfig

class LovePlusConfigModule(context: ReactApplicationContext) : ReactContextBaseJavaModule(context) {
    override fun getName(): String = "LovePlusConfig"
    override fun getConstants(): Map<String, Any> = mapOf(
        "apiBaseUrl" to BuildConfig.API_BASE_URL,
        "mapsEnabled" to BuildConfig.MAPS_ENABLED,
    )
}
