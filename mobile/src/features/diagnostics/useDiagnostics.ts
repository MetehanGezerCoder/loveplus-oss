import {useCallback, useEffect, useState} from 'react';
import {PermissionsAndroid, Platform} from 'react-native';
import {loveStatusNative, type NativeDiagnostics} from '../../shared/native/LoveStatusNative';
import {useRealtimeConnection} from '../realtime-status/connectionStatus';
import {diagnosticsApi, probeHealth, type ApiDiagnostics, type HealthProbe} from './diagnosticsApi';

export type PermissionSnapshot = {
  fineLocation: boolean;
  backgroundLocation: boolean;
  activityRecognition: boolean;
  notifications: boolean;
};

export type DiagnosticsReport = {
  loading: boolean;
  health: HealthProbe | null;
  server: ApiDiagnostics | null;
  serverError: string | null;
  native: NativeDiagnostics | null;
  nativeError: string | null;
  permissions: PermissionSnapshot | null;
};

async function readPermissions(): Promise<PermissionSnapshot | null> {
  if (Platform.OS !== 'android') return null;
  const version = Number(Platform.Version);
  const check = async (permission: string | undefined) => {
    if (!permission) return false;
    try {
      return await PermissionsAndroid.check(permission as Parameters<typeof PermissionsAndroid.check>[0]);
    } catch {
      return false;
    }
  };
  return {
    fineLocation: await check(PermissionsAndroid.PERMISSIONS.ACCESS_FINE_LOCATION),
    backgroundLocation:
      version < 29 ? true : await check(PermissionsAndroid.PERMISSIONS.ACCESS_BACKGROUND_LOCATION),
    activityRecognition:
      version < 29 ? true : await check(PermissionsAndroid.PERMISSIONS.ACTIVITY_RECOGNITION),
    notifications: version < 33 ? true : await check(PermissionsAndroid.PERMISSIONS.POST_NOTIFICATIONS),
  };
}

/**
 * Collects every signal needed to answer "why is Love+ not working on this phone" without
 * exposing a token, a coordinate or partner data. Each source fails independently, so an
 * unreachable API still shows the build and permission state that explain the failure.
 */
export function useDiagnostics() {
  const connection = useRealtimeConnection();
  const [report, setReport] = useState<DiagnosticsReport>({
    loading: true,
    health: null,
    server: null,
    serverError: null,
    native: null,
    nativeError: null,
    permissions: null,
  });

  const refresh = useCallback(async () => {
    setReport(current => ({...current, loading: true}));
    const [health, server, native, permissions] = await Promise.all([
      probeHealth(),
      diagnosticsApi.server().then(
        value => ({ok: true as const, value}),
        (error: unknown) => ({
          ok: false as const,
          message: error instanceof Error ? error.message : 'Sunucu tanılaması alınamadı.',
        }),
      ),
      loveStatusNative.diagnostics().then(
        value => ({ok: true as const, value}),
        (error: unknown) => ({
          ok: false as const,
          message: error instanceof Error ? error.message : 'Cihaz servisi okunamadı.',
        }),
      ),
      readPermissions(),
    ]);

    setReport({
      loading: false,
      health,
      server: server.ok ? server.value : null,
      serverError: server.ok ? null : server.message,
      native: native.ok ? native.value : null,
      nativeError: native.ok ? null : native.message,
      permissions,
    });
  }, []);

  useEffect(() => {
    refresh().catch(() => undefined);
  }, [refresh]);

  return {...report, connection, refresh};
}
