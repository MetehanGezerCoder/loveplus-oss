import {NativeModules} from 'react-native';
import {API_BASE_URL} from '../config/environment';
import {sessionStorage, type StoredSession} from '../storage/sessionStorage';

type ApiOptions = Omit<RequestInit, 'body'> & {body?: unknown; retryAuth?: boolean};
let refreshPromise: Promise<StoredSession> | null = null;

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
  ) {
    super(message);
  }
}

export async function apiRequest<T>(path: string, options: ApiOptions = {}): Promise<T> {
  const session = sessionStorage.read();
  const response = await fetch(`${API_BASE_URL}${path}`, {
    ...options,
    headers: {
      Accept: 'application/json',
      'Content-Type': 'application/json',
      ...(session ? {Authorization: `Bearer ${session.accessToken}`} : {}),
      ...options.headers,
    },
    body: options.body === undefined ? undefined : JSON.stringify(options.body),
  });

  if (response.status === 401 && session && options.retryAuth !== false) {
    await rotateSession(session);
    return apiRequest<T>(path, {...options, retryAuth: false});
  }
  if (!response.ok) {
    const problem = (await response.json().catch(() => null)) as {detail?: string; title?: string} | null;
    throw new ApiError(response.status, problem?.detail ?? problem?.title ?? 'İstek tamamlanamadı.');
  }
  if (response.status === 204) {
    return undefined as T;
  }
  return response.json() as Promise<T>;
}

async function rotateSession(current: StoredSession) {
  refreshPromise ??= fetch(`${API_BASE_URL}/api/auth/refresh`, {
    method: 'POST',
    headers: {'Content-Type': 'application/json'},
    body: JSON.stringify({
      refreshToken: current.refreshToken,
      deviceId: sessionStorage.deviceId(),
    }),
  })
    .then(async response => {
      if (!response.ok) {
        sessionStorage.clear();
        throw new ApiError(response.status, 'Oturumunuz sona erdi.');
      }
      const next = (await response.json()) as StoredSession;
      sessionStorage.write(next);
      const native = NativeModules.LoveStatus as {configure?: (url: string, token: string, deviceId: string, userId: string) => Promise<void>} | undefined;
      await native?.configure?.(API_BASE_URL, next.telemetryToken, sessionStorage.deviceId(), next.user.id).catch(() => undefined);
      return next;
    })
    .finally(() => {
      refreshPromise = null;
    });
  return refreshPromise;
}
