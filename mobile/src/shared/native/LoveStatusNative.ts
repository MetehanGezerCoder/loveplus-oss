import {NativeModules, Platform} from 'react-native';
import {API_BASE_URL} from '../config/environment';
import {sessionStorage} from '../storage/sessionStorage';

export type TelemetryState = {
  enabled: boolean;
  locationSharing: boolean;
  activityEnabled: boolean;
  shareLastKnown: boolean;
  mood: string;
};

export type HapticIntensity = 'low' | 'medium' | 'high';
export type PhaseThreePreferences = {
  receiveHeartbeats: boolean;
  sendHeartbeats: boolean;
  hapticIntensity: HapticIntensity;
  receiveAnimation: boolean;
  showProximity: boolean;
  moodSharing: boolean;
};

export type NativeDiagnostics = {
  apiBaseUrl: string;
  buildType: string;
  isDebugBuild: boolean;
  versionName: string;
  versionCode: number;
  mapsEnabled: boolean;
  hasTelemetrySession: boolean;
  telemetryEnabled: boolean;
  locationSharing: boolean;
  activityEnabled: boolean;
  heartbeatIntervalSeconds: number;
  queuedStatusCount: number;
  lastUploadResult?: string | null;
  lastUploadAtMs?: number;
  lastUploadFailureAtMs?: number;
};

type WidgetState = {
  partnerName: string;
  batteryLevel: number;
  isCharging: boolean;
  activity: string;
  distance: string;
  lastUpdated: string;
  presence: string;
  locationSharing: boolean;
  mood: string;
  // Sent so the widget can re-derive presence on redraw instead of repeating a verdict that
  // was only true when the app last pushed state.
  presenceOnlineSeconds: number;
  presenceRecentlyOnlineSeconds: number;
};

type LoveStatusModule = {
  configure(apiBaseUrl: string, accessToken: string, deviceId: string, userId: string): Promise<void>;
  startTelemetry(locationSharing: boolean, activityEnabled: boolean, shareLastKnown: boolean): Promise<void>;
  updateSharing(locationSharing: boolean, activityEnabled: boolean, shareLastKnown: boolean): Promise<void>;
  setMood(mood: string): Promise<void>;
  getTelemetryState(): Promise<TelemetryState>;
  getDiagnostics(): Promise<NativeDiagnostics>;
  publishNow(): Promise<boolean>;
  stopTracking(clearUserData: boolean): Promise<void>;
  updateWidget(state: WidgetState): Promise<void>;
  getPhaseThreePreferences(): Promise<PhaseThreePreferences>;
  updatePhaseThreePreferences(state: PhaseThreePreferences): Promise<void>;
  supportsHaptics(): Promise<boolean>;
  playHeartbeat(eventId: string, pattern: number[], intensity: HapticIntensity): Promise<boolean>;
  previewHeartbeat(): Promise<void>;
  cancelHeartbeat(): Promise<void>;
};

const native = NativeModules.LoveStatus as LoveStatusModule | undefined;

function requireNative() {
  if (!native || Platform.OS !== 'android') throw new Error('Gerçek cihaz telemetrisi yalnızca Android üzerinde kullanılabilir.');
  return native;
}

export const loveStatusNative = {
  async configureSession() {
    const session = sessionStorage.read();
    if (!session) throw new Error('Oturum bulunamadı.');
    await requireNative().configure(API_BASE_URL, session.telemetryToken, sessionStorage.deviceId(), session.user.id);
  },
  async ensureBatteryTelemetry() {
    await this.configureSession();
    const state = await requireNative().getTelemetryState();
    await requireNative().startTelemetry(state.locationSharing, state.activityEnabled, state.shareLastKnown);
  },
  startTelemetry: async (state: Pick<TelemetryState, 'locationSharing' | 'activityEnabled' | 'shareLastKnown'>) => {
    await loveStatusNative.configureSession();
    await requireNative().startTelemetry(state.locationSharing, state.activityEnabled, state.shareLastKnown);
  },
  updateSharing: (state: Pick<TelemetryState, 'locationSharing' | 'activityEnabled' | 'shareLastKnown'>) =>
    requireNative().updateSharing(state.locationSharing, state.activityEnabled, state.shareLastKnown),
  setMood: (mood: string) => requireNative().setMood(mood),
  state: () => requireNative().getTelemetryState(),
  diagnostics: () => requireNative().getDiagnostics(),
  // Returns false when telemetry is off, so the caller can say so instead of implying a send.
  publishNow: () => requireNative().publishNow(),
  stop: (clearUserData = false) => requireNative().stopTracking(clearUserData),
  updateWidget: (state: WidgetState) => requireNative().updateWidget(state),
  phaseThreePreferences: () => requireNative().getPhaseThreePreferences(),
  updatePhaseThreePreferences: (state: PhaseThreePreferences) => requireNative().updatePhaseThreePreferences(state),
  supportsHaptics: () => requireNative().supportsHaptics(),
  playHeartbeat: (eventId: string, pattern: number[], intensity: HapticIntensity) =>
    requireNative().playHeartbeat(eventId, pattern, intensity),
  previewHeartbeat: () => requireNative().previewHeartbeat(),
  cancelHeartbeat: () => requireNative().cancelHeartbeat(),
};
