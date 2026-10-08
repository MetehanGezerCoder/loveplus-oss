import {QueryClient} from '@tanstack/react-query';

/**
 * One client for the whole app, created outside React so the auth store can clear it.
 * Sign-out and account switching must drop every cached partner value before the next
 * session renders; otherwise the previous account's status, distance and mood stay visible
 * on the shared device until the first refetch lands.
 */
export const queryClient = new QueryClient({
  defaultOptions: {queries: {retry: 2, staleTime: 15_000}},
});

export function clearCachedSessionData() {
  queryClient.cancelQueries().catch(() => undefined);
  queryClient.clear();
}
