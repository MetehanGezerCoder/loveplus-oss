import type {Pair} from './pairingApi';

export const UNPAIRED_PAIR_REFETCH_INTERVAL_MS = 5_000;

export type PairQueryStatus = 'pending' | 'error' | 'success';

export function getCurrentPairRefetchInterval(
  status: PairQueryStatus,
  pair: Pair | null | undefined,
): number | false {
  return status === 'success' && pair === null ? UNPAIRED_PAIR_REFETCH_INTERVAL_MS : false;
}
