import {AppState} from 'react-native';
import {loveStatusNative} from '../../shared/native/LoveStatusNative';
import {heartbeatApi} from './heartbeatApi';
import type {HeartbeatAcknowledgement, HeartbeatAcknowledgementState, HeartbeatPattern} from './types';

type AckListener = (event: HeartbeatAcknowledgement) => void;
type ReceiveListener = (event: HeartbeatPattern) => void;
const ackListeners = new Set<AckListener>();
const receiveListeners = new Set<ReceiveListener>();

async function receive(event: HeartbeatPattern) {
  if (Date.parse(event.expiresAtUtc) <= Date.now() || event.schemaVersion !== 1) return;
  let state: HeartbeatAcknowledgementState = 'unsupported';
  try {
    const preferences = await loveStatusNative.phaseThreePreferences();
    if (!preferences.receiveHeartbeats) {
      state = 'hapticDisabled';
    } else if (await loveStatusNative.supportsHaptics()) {
      const played = await loveStatusNative.playHeartbeat(event.eventId, event.pattern, preferences.hapticIntensity);
      state = played ? 'played' : 'hapticDisabled';
      if (played && preferences.receiveAnimation && AppState.currentState === 'active') {
        receiveListeners.forEach(listener => listener(event));
      }
    } else if (preferences.receiveAnimation && AppState.currentState === 'active') {
      // Devices without a vibrator still receive a private, in-app visual pulse.
      receiveListeners.forEach(listener => listener(event));
    }
  } finally {
    await heartbeatApi.acknowledge(event.eventId, state).catch(() => undefined);
  }
}

export const heartbeatRuntime = {
  receive,
  acknowledged(event: HeartbeatAcknowledgement) { ackListeners.forEach(listener => listener(event)); },
  onAcknowledged(listener: AckListener) { ackListeners.add(listener); return () => ackListeners.delete(listener); },
  onReceived(listener: ReceiveListener) { receiveListeners.add(listener); return () => receiveListeners.delete(listener); },
};
