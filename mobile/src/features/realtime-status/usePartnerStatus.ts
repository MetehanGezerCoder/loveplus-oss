import {useCallback, useEffect, useRef, useState} from 'react';
import {AppState, type AppStateStatus} from 'react-native';
import notifee, {AndroidImportance} from '@notifee/react-native';
import {useQuery, useQueryClient} from '@tanstack/react-query';
import {HubConnectionState, type HubConnection} from '@microsoft/signalr';
import {apiRequest} from '../../shared/api/apiClient';
import {loveStatusNative} from '../../shared/native/LoveStatusNative';
import {useAuthStore} from '../auth';
import {createStatusConnection} from './statusConnection';
import {derivePresence, DEFAULT_ONLINE_SECONDS, DEFAULT_RECENTLY_ONLINE_SECONDS} from './presence';
import {useRealtimeConnection, type ConnectionState} from './connectionStatus';
import type {CriticalBattery, PartnerStatus, PresenceState} from './types';
import {heartbeatRuntime} from '../heartbeat';

export const partnerStatusQueryKey = ['realtime-status', 'partner'] as const;

export type {ConnectionState};

/** Presence is re-derived on this cadence so an idle screen ages out of "online" on its own. */
const PRESENCE_TICK_MS = 15_000;

/** A closed hub is retried forever; a couple's phone must recover from a night in Doze. */
const RECONNECT_DELAYS_MS = [0, 2_000, 5_000, 10_000, 20_000, 30_000];

export function usePartnerStatus() {
  const session = useAuthStore(state => state.session);
  const queryClient = useQueryClient();
  const connectionState = useRealtimeConnection(state => state.state);
  const lastConnectedAt = useRealtimeConnection(state => state.lastConnectedAt);
  const report = useRealtimeConnection(state => state.report);
  const [, setPresenceTick] = useState(0);
  const connectionRef = useRef<HubConnection | null>(null);
  const restartTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const disposed = useRef(false);

  const query = useQuery({
    queryKey: partnerStatusQueryKey,
    queryFn: () => apiRequest<PartnerStatus | null>('/api/status/partner'),
    refetchInterval: 60_000,
  });

  useEffect(() => {
    if (!session) return;
    disposed.current = false;

    const onStatus = (status: PartnerStatus) => {
      if (status.userId === session.user.id) return;
      queryClient.setQueryData(partnerStatusQueryKey, status);
      loveStatusNative
        .updateWidget({
          partnerName: status.partnerDisplayName,
          batteryLevel: status.batteryLevel,
          isCharging: status.isCharging,
          activity: status.activityType,
          distance: formatDistance(status.distanceMeters),
          lastUpdated: status.recordedAtUtc,
          presence: derivePresence(status),
          locationSharing: status.isLocationSharingEnabled,
          mood: status.mood,
          presenceOnlineSeconds: status.presenceOnlineSeconds ?? DEFAULT_ONLINE_SECONDS,
          presenceRecentlyOnlineSeconds:
            status.presenceRecentlyOnlineSeconds ?? DEFAULT_RECENTLY_ONLINE_SECONDS,
        })
        .catch(() => undefined);
    };

    const onCritical = async (event: CriticalBattery) => {
      if (event.userId === session.user.id) return;
      const channelId = await notifee.createChannel({
        id: 'critical-battery',
        name: 'Kritik pil',
        importance: AndroidImportance.HIGH,
      });
      await notifee.displayNotification({
        title: 'Partnerinin pili kritik',
        body: `Pil seviyesi %${event.batteryLevel}. Son bilinen konum işaretlendi.`,
        android: {channelId, pressAction: {id: 'default'}},
      });
    };

    const connection = createStatusConnection(
      onStatus,
      event => {
        onCritical(event).catch(() => undefined);
      },
      event => {
        heartbeatRuntime.receive(event).catch(() => undefined);
      },
      heartbeatRuntime.acknowledged,
    );
    connectionRef.current = connection;

    const start = () => {
      if (disposed.current) return;
      if (connection.state !== HubConnectionState.Disconnected) return;
      report('connecting');
      connection
        .start()
        .then(() => {
          attempt = 0;
          report('connected');
          // The cached DTO is as old as the outage; refresh it before it is trusted again.
          queryClient.invalidateQueries({queryKey: partnerStatusQueryKey}).catch(() => undefined);
        })
        .catch(() => {
          report('offline', 'hub_start_failed');
          scheduleRestart();
        });
    };

    let attempt = 0;
    const scheduleRestart = () => {
      if (disposed.current) return;
      if (restartTimer.current) clearTimeout(restartTimer.current);
      const delay = RECONNECT_DELAYS_MS[Math.min(attempt, RECONNECT_DELAYS_MS.length - 1)] ?? 30_000;
      attempt += 1;
      restartTimer.current = setTimeout(start, delay);
    };

    connection.onreconnecting(() => report('connecting'));
    connection.onreconnected(() => {
      attempt = 0;
      report('connected');
      queryClient.invalidateQueries({queryKey: partnerStatusQueryKey}).catch(() => undefined);
    });
    connection.onclose(() => {
      // withAutomaticReconnect can still surrender on a hard failure. Without this the hub
      // stays dead until the screen remounts, which is how a backgrounded phone used to show
      // a permanently offline partner.
      report('offline', 'hub_closed');
      scheduleRestart();
    });

    start();

    const subscription = AppState.addEventListener('change', (next: AppStateStatus) => {
      if (next !== 'active') return;
      attempt = 0;
      start();
      queryClient.invalidateQueries({queryKey: partnerStatusQueryKey}).catch(() => undefined);
      // Publish our own state immediately so the partner does not wait out the heartbeat.
      loveStatusNative.publishNow().catch(() => undefined);
    });

    return () => {
      disposed.current = true;
      subscription.remove();
      if (restartTimer.current) clearTimeout(restartTimer.current);
      connectionRef.current = null;
      if (connection.state !== HubConnectionState.Disconnected) {
        connection.stop().catch(() => undefined);
      }
    };
  }, [queryClient, report, session]);

  useEffect(() => {
    const timer = setInterval(() => setPresenceTick(value => value + 1), PRESENCE_TICK_MS);
    return () => clearInterval(timer);
  }, []);

  const reconnect = useCallback(() => {
    const connection = connectionRef.current;
    queryClient.invalidateQueries({queryKey: partnerStatusQueryKey}).catch(() => undefined);
    loveStatusNative.publishNow().catch(() => undefined);
    if (!connection || connection.state !== HubConnectionState.Disconnected) return;
    report('connecting');
    connection
      .start()
      .then(() => report('connected'))
      .catch(() => report('offline', 'manual_retry_failed'));
  }, [queryClient, report]);

  const presence: PresenceState | undefined = query.data ? derivePresence(query.data) : undefined;

  return {...query, connectionState, presence, lastConnectedAt, reconnect};
}

export function formatDistance(distance?: number) {
  if (distance === undefined || distance === null) return '—';
  if (distance < 1_000) return `${Math.round(distance)} m`;
  return `${(distance / 1_000).toFixed(1)} km`;
}
