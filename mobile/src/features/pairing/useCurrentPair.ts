import {useQuery} from '@tanstack/react-query';
import {pairingApi} from './pairingApi';

export const currentPairQueryKey = ['pairing', 'current'] as const;
export function useCurrentPair() {
  return useQuery({queryKey: currentPairQueryKey, queryFn: pairingApi.current});
}
