import {useQuery} from '@tanstack/react-query';
import {pairingApi} from './pairingApi';
import {getCurrentPairRefetchInterval} from './pairingRefresh';

export const currentPairQueryKey = ['pairing', 'current'] as const;

export function useCurrentPair() {
  return useQuery({
    queryKey: currentPairQueryKey,
    queryFn: pairingApi.current,
    refetchInterval: query => getCurrentPairRefetchInterval(query.state.status, query.state.data),
  });
}
