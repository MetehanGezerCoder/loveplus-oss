import type {PartnerStatus, PresenceState} from './types';

/**
 * The server computes presence when it builds the DTO, but the client keeps that DTO in cache
 * between pushes. Reading `status.presence` directly would keep claiming "Çevrimiçi" for a
 * phone that stopped reporting minutes ago, so presence is re-derived from the packet age
 * using the windows the server sent alongside it.
 */
export function derivePresence(
  status: Pick<PartnerStatus, 'recordedAtUtc' | 'presence' | 'presenceOnlineSeconds' | 'presenceRecentlyOnlineSeconds'>,
  now: number = Date.now(),
): PresenceState {
  const recordedAt = Date.parse(status.recordedAtUtc);
  if (Number.isNaN(recordedAt)) return status.presence;

  // A packet from the future means the two clocks disagree; trust the server's verdict
  // rather than inventing a negative age.
  const ageSeconds = (now - recordedAt) / 1000;
  if (ageSeconds < 0) return status.presence;

  const onlineWindow = status.presenceOnlineSeconds ?? DEFAULT_ONLINE_SECONDS;
  const recentWindow = status.presenceRecentlyOnlineSeconds ?? DEFAULT_RECENTLY_ONLINE_SECONDS;
  if (ageSeconds <= onlineWindow) return 'online';
  return ageSeconds <= recentWindow ? 'recentlyOnline' : 'offline';
}

/**
 * Distance and proximity are only honest while both samples are fresh. Once the partner
 * packet ages out of the "recently online" window the dashboard must stop presenting a
 * distance as if it were current.
 */
export function isStatusStale(
  status: Pick<PartnerStatus, 'recordedAtUtc' | 'presence' | 'presenceOnlineSeconds' | 'presenceRecentlyOnlineSeconds'>,
  now: number = Date.now(),
): boolean {
  return derivePresence(status, now) === 'offline';
}

export const DEFAULT_ONLINE_SECONDS = 180;
export const DEFAULT_RECENTLY_ONLINE_SECONDS = 600;
