import {HubConnectionBuilder, HttpTransportType, LogLevel} from '@microsoft/signalr';
import {API_BASE_URL} from '../../shared/config/environment';
import {sessionStorage} from '../../shared/storage/sessionStorage';
import type {CriticalBattery, PartnerStatus} from './types';
import type {HeartbeatAcknowledgement, HeartbeatPattern} from '../heartbeat/types';

export const createStatusConnection = (
  onStatus: (status: PartnerStatus) => void,
  onCritical: (event: CriticalBattery) => void,
  onHeartbeat: (event: HeartbeatPattern) => void,
  onHeartbeatAcknowledged: (event: HeartbeatAcknowledgement) => void,
) => {
  const connection = new HubConnectionBuilder()
    .withUrl(`${API_BASE_URL}/hubs/status`, {
      accessTokenFactory: () => sessionStorage.read()?.accessToken ?? '',
      transport: HttpTransportType.WebSockets,
      skipNegotiation: true,
    })
    // A fixed retry array gives up after its last entry, which left a phone returning from
    // Doze with a permanently dead hub. Retry forever with a capped backoff instead.
    .withAutomaticReconnect({
      nextRetryDelayInMilliseconds: ({previousRetryCount}) =>
        Math.min(30_000, previousRetryCount === 0 ? 0 : 2_000 * 2 ** (previousRetryCount - 1)),
    })
    // Expected reconnect/offline transitions are surfaced through product UI.
    // Keep them out of React Native's development error overlay.
    .configureLogging(LogLevel.None)
    .build();
  connection.on('PartnerStatusUpdated', onStatus);
  connection.on('CriticalBattery', onCritical);
  connection.on('HeartbeatPatternReceived', onHeartbeat);
  connection.on('HeartbeatAcknowledged', onHeartbeatAcknowledged);
  return connection;
};
