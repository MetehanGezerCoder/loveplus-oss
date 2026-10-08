import {getCurrentPairRefetchInterval, UNPAIRED_PAIR_REFETCH_INTERVAL_MS} from './pairingRefresh';

const paired = {
  pairId: 'pair-1',
  partnerUserId: 'user-2',
  partnerDisplayName: 'Taylor',
  pairedAtUtc: '2026-10-08T00:00:00Z',
};

describe('pairing refresh policy', () => {
  it('polls only after a successful unpaired response', () => {
    expect(getCurrentPairRefetchInterval('success', null)).toBe(UNPAIRED_PAIR_REFETCH_INTERVAL_MS);
  });

  it('stops polling as soon as a pair exists', () => {
    expect(getCurrentPairRefetchInterval('success', paired)).toBe(false);
  });

  it('does not poll while loading or after an error', () => {
    expect(getCurrentPairRefetchInterval('pending', undefined)).toBe(false);
    expect(getCurrentPairRefetchInterval('error', null)).toBe(false);
  });
});
