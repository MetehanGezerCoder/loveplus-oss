import {MMKV} from 'react-native-mmkv';

const storage = new MMKV({id: 'loveplus-session'});

export type StoredSession = {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  refreshToken: string;
  refreshTokenExpiresAtUtc: string;
  telemetryToken: string;
  telemetryTokenExpiresAtUtc: string;
  deviceSessionId: string;
  user: {id: string; email: string; displayName: string};
};

const sessionKey = 'auth.session';
const deviceIdKey = 'auth.device-id';

export const sessionStorage = {
  read(): StoredSession | null {
    const value = storage.getString(sessionKey);
    if (!value) {
      return null;
    }
    try {
      const parsed = JSON.parse(value) as StoredSession;
      if (!parsed.telemetryToken || !parsed.user?.id) {
        storage.delete(sessionKey);
        return null;
      }
      return parsed;
    } catch {
      storage.delete(sessionKey);
      return null;
    }
  },
  write(session: StoredSession) {
    storage.set(sessionKey, JSON.stringify(session));
  },
  clear() {
    storage.delete(sessionKey);
  },
  deviceId() {
    const existing = storage.getString(deviceIdKey);
    if (existing) {
      return existing;
    }
    const created = `rn-${Date.now()}-${Math.random().toString(36).slice(2)}`;
    storage.set(deviceIdKey, created);
    return created;
  },
};
