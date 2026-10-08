import {create} from 'zustand';
import {sessionStorage, type StoredSession} from '../../shared/storage/sessionStorage';
import {authApi, type Credentials} from './authApi';
import {clearCachedSessionData} from '../../shared/api/queryClient';
import {loveStatusNative} from '../../shared/native/LoveStatusNative';

type AuthState = {
  bootstrapped: boolean;
  session: StoredSession | null;
  busy: boolean;
  error: string | null;
  bootstrap: () => void;
  login: (input: Credentials) => Promise<void>;
  register: (input: Credentials & {displayName: string}) => Promise<void>;
  logout: () => Promise<void>;
  clearError: () => void;
};

export const useAuthStore = create<AuthState>(set => ({
  bootstrapped: false,
  session: null,
  busy: false,
  error: null,
  bootstrap: () => set({session: sessionStorage.read(), bootstrapped: true}),
  login: async input => {
    set({busy: true, error: null});
    try {
      const session = await authApi.login(input);
      // Two people share a phone during pairing. Nothing cached under the previous account
      // may survive into the new session.
      clearCachedSessionData();
      sessionStorage.write(session);
      await loveStatusNative.ensureBatteryTelemetry().catch(() => undefined);
      set({session, busy: false});
    } catch (error) {
      set({busy: false, error: error instanceof Error ? error.message : 'Giriş yapılamadı.'});
    }
  },
  register: async input => {
    set({busy: true, error: null});
    try {
      const session = await authApi.register(input);
      clearCachedSessionData();
      sessionStorage.write(session);
      await loveStatusNative.ensureBatteryTelemetry().catch(() => undefined);
      set({session, busy: false});
    } catch (error) {
      set({busy: false, error: error instanceof Error ? error.message : 'Kayıt tamamlanamadı.'});
    }
  },
  logout: async () => {
    set({busy: true, error: null});
    try {
      await authApi.logout();
    } catch {
      // Local credentials must still be removed when the server is unreachable.
    } finally {
      await loveStatusNative.stop(true).catch(() => undefined);
      sessionStorage.clear();
      clearCachedSessionData();
      set({session: null, busy: false});
    }
  },
  clearError: () => set({error: null}),
}));
