import {API_BASE_URL} from '../../shared/config/environment';
import {apiRequest} from '../../shared/api/apiClient';

export type ApiDiagnostics = {
  environment: string;
  apiVersion: string;
  usesRelationalDatabase: boolean;
  usesRedis: boolean;
  migrationsApplied: boolean;
  pushProvider: string;
  pushConfigured: boolean;
  requiresHttps: boolean;
  presenceOnlineSeconds: number;
  presenceRecentlyOnlineSeconds: number;
  clientStatusHeartbeatSeconds: number;
  serverTimeUtc: string;
};

export type HealthProbe = {
  reachable: boolean;
  status: string;
  httpStatus?: number;
  latencyMs?: number;
  /** Failure category only; never a response body that could carry account data. */
  failure?: string;
  checks: {name: string; status: string}[];
};

const PROBE_TIMEOUT_MS = 8_000;

export async function probeHealth(): Promise<HealthProbe> {
  const startedAt = Date.now();
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), PROBE_TIMEOUT_MS);
  try {
    const response = await fetch(`${API_BASE_URL}/health`, {
      method: 'GET',
      headers: {Accept: 'application/json'},
      signal: controller.signal,
    });
    const latencyMs = Date.now() - startedAt;
    const body = (await response.json().catch(() => null)) as
      | {status?: string; checks?: {name: string; status: string}[]}
      | null;
    return {
      reachable: true,
      status: body?.status ?? (response.ok ? 'healthy' : 'unhealthy'),
      httpStatus: response.status,
      latencyMs,
      checks: body?.checks ?? [],
    };
  } catch (error) {
    return {
      reachable: false,
      status: 'unreachable',
      latencyMs: Date.now() - startedAt,
      failure: error instanceof Error && error.name === 'AbortError' ? 'timeout' : 'network_unreachable',
      checks: [],
    };
  } finally {
    clearTimeout(timeout);
  }
}

export const diagnosticsApi = {
  server: () => apiRequest<ApiDiagnostics>('/api/diagnostics'),
};
