import {derivePresence, isStatusStale} from './presence';

const base = {
  presence: 'online' as const,
  presenceOnlineSeconds: 180,
  presenceRecentlyOnlineSeconds: 600,
};

const now = Date.parse('2026-08-15T09:00:00.000Z');

function at(ageSeconds: number) {
  return {...base, recordedAtUtc: new Date(now - ageSeconds * 1000).toISOString()};
}

describe('derivePresence', () => {
  it('keeps a fresh packet online', () => {
    expect(derivePresence(at(0), now)).toBe('online');
    expect(derivePresence(at(179), now)).toBe('online');
  });

  it('downgrades a cached online verdict once the packet ages out', () => {
    // The server said "online" when it sent this DTO; the client must not repeat that
    // forever while the partner's phone has stopped reporting.
    expect(derivePresence(at(240), now)).toBe('recentlyOnline');
    expect(derivePresence(at(1_200), now)).toBe('offline');
  });

  it('marks a status stale only once it falls out of the recently-online window', () => {
    expect(isStatusStale(at(500), now)).toBe(false);
    expect(isStatusStale(at(700), now)).toBe(true);
  });

  it('falls back to the server verdict when the timestamp is unusable', () => {
    expect(derivePresence({...base, recordedAtUtc: 'not-a-date'}, now)).toBe('online');
  });

  it('falls back to the server verdict when the phone clock runs behind the server', () => {
    expect(derivePresence({...base, presence: 'recentlyOnline', recordedAtUtc: at(-90).recordedAtUtc}, now)).toBe(
      'recentlyOnline',
    );
  });

  it('uses documented defaults when an older server omits the windows', () => {
    const withoutWindows = {
      presence: 'online' as const,
      recordedAtUtc: new Date(now - 900 * 1000).toISOString(),
    } as Parameters<typeof derivePresence>[0];

    expect(derivePresence(withoutWindows, now)).toBe('offline');
  });
});
