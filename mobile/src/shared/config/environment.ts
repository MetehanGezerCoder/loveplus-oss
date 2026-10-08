import {NativeModules} from 'react-native';

type LovePlusConfigModule = {apiBaseUrl?: string; mapsEnabled?: boolean};
const nativeConfig = NativeModules.LovePlusConfig as LovePlusConfigModule | undefined;

// The native bridge (BuildConfig.API_BASE_URL) always supplies a value on Android, in both
// debug and release — Gradle defaults it to the emulator loopback when unset. A JS-side
// fallback string would be dead code that a minifier cannot always strip, leaving a stray
// local-development address inside an otherwise clean release bundle.
export const API_BASE_URL = nativeConfig?.apiBaseUrl ?? '';
export const MAPS_ENABLED = nativeConfig?.mapsEnabled ?? false;

if (!API_BASE_URL) {
  throw new Error('A native LovePlusConfig.apiBaseUrl value is required.');
}
