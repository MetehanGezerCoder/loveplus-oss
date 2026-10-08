import {useCallback, useEffect, useRef, useState} from 'react';
import {ApiError} from '../../shared/api/apiClient';
import {loveStatusNative, type PhaseThreePreferences} from '../../shared/native/LoveStatusNative';
import {heartbeatApi} from './heartbeatApi';
import {heartbeatRuntime} from './heartbeatRuntime';
import {buildHeartbeatPattern, createEventId, MAX_TAPS} from './heartbeatPattern';

export type DeliveryState = 'idle' | 'recording' | 'sending' | 'pending' | 'delivered' | 'disabled' | 'timeout' | 'error';
const defaults: PhaseThreePreferences = {
  receiveHeartbeats: true, sendHeartbeats: true, hapticIntensity: 'medium',
  receiveAnimation: true, showProximity: true, moodSharing: true,
};

export function useHeartbeat() {
  const [timestamps, setTimestamps] = useState<number[]>([]);
  const [delivery, setDelivery] = useState<DeliveryState>('idle');
  const [message, setMessage] = useState('Kalbe kendi ritminde dokun.');
  const [preferences, setPreferences] = useState<PhaseThreePreferences>(defaults);
  const [receivedPulse, setReceivedPulse] = useState(0);
  const [hapticsSupported, setHapticsSupported] = useState<boolean | null>(null);
  const pendingEvent = useRef<string | null>(null);
  const timeout = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    loveStatusNative.phaseThreePreferences().then(setPreferences).catch(() => undefined);
    loveStatusNative.supportsHaptics().then(setHapticsSupported).catch(() => setHapticsSupported(false));
    const offAck = heartbeatRuntime.onAcknowledged(event => {
      if (event.eventId !== pendingEvent.current) return;
      if (timeout.current) clearTimeout(timeout.current);
      pendingEvent.current = null;
      if (event.state === 'played') {
        setDelivery('delivered'); setMessage('Kalp atışın ulaştı ♥');
      } else {
        setDelivery('disabled'); setMessage('Ulaştı; partnerinde titreşim kapalı.');
      }
    });
    const offReceive = heartbeatRuntime.onReceived(event => {
      setReceivedPulse(value => value + 1);
      setMessage(`${event.senderDisplayName} sana kalp gönderdi ♥`);
    });
    return () => { offAck(); offReceive(); if (timeout.current) clearTimeout(timeout.current); loveStatusNative.cancelHeartbeat().catch(() => undefined); };
  }, []);

  const tap = useCallback(() => {
    if (!preferences.sendHeartbeats) { setDelivery('disabled'); setMessage('Kalp atışı gönderme ayarın kapalı.'); return; }
    setTimestamps(current => current.length >= MAX_TAPS ? current : [...current, Date.now()]);
    setDelivery('recording');
    setMessage('Ritmin kaydediliyor…');
    loveStatusNative.previewHeartbeat().catch(() => undefined);
  }, [preferences.sendHeartbeats]);

  const reset = useCallback(() => {
    setTimestamps([]); setDelivery('idle'); setMessage('Kalbe kendi ritminde dokun.');
  }, []);

  const send = useCallback(async () => {
    const pattern = buildHeartbeatPattern(timestamps);
    if (!pattern.length) { setMessage('Önce kalbe en az bir kez dokun.'); return; }
    const eventId = createEventId();
    try {
      setDelivery('sending'); setMessage('Kalp atışın gönderiliyor…');
      // Register before HTTP completes: a fast partner ACK may race the response.
      pendingEvent.current = eventId;
      await heartbeatApi.send(eventId, pattern);
      if (pendingEvent.current !== eventId) return;
      setDelivery('pending'); setMessage('Partnerinin cihaz yanıtı bekleniyor…');
      setTimestamps([]);
      timeout.current = setTimeout(() => {
        if (pendingEvent.current !== eventId) return;
        pendingEvent.current = null; setDelivery('timeout'); setMessage('Henüz teslim onayı gelmedi.');
      }, 7000);
    } catch (error) {
      if (pendingEvent.current === eventId) pendingEvent.current = null;
      setDelivery('error');
      setMessage(error instanceof ApiError && error.status === 429
        ? 'Biraz yavaş ♥ Yeniden göndermek için bekle.'
        : error instanceof ApiError && error.status === 409
          ? 'Partnerin şu an çevrimdışı.'
        : error instanceof Error ? error.message : 'Kalp atışı gönderilemedi.');
    }
  }, [timestamps]);

  return {timestamps, delivery, message, preferences, receivedPulse, hapticsSupported, tap, reset, send};
}
