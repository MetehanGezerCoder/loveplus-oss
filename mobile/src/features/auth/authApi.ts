import {apiRequest} from '../../shared/api/apiClient';
import {sessionStorage, type StoredSession} from '../../shared/storage/sessionStorage';

export type Credentials = {email: string; password: string};

export const authApi = {
  register(input: Credentials & {displayName: string}) {
    return apiRequest<StoredSession>('/api/auth/register', {
      method: 'POST',
      retryAuth: false,
      body: {
        ...input,
        deviceId: sessionStorage.deviceId(),
        deviceName: 'Love+ Android',
      },
    });
  },
  login(input: Credentials) {
    return apiRequest<StoredSession>('/api/auth/login', {
      method: 'POST',
      retryAuth: false,
      body: {
        ...input,
        deviceId: sessionStorage.deviceId(),
        deviceName: 'Love+ Android',
      },
    });
  },
  logout() {
    return apiRequest<void>('/api/auth/logout', {method: 'POST'});
  },
};
