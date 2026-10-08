import {apiRequest} from '../../shared/api/apiClient';

export type Pair = {
  pairId: string;
  partnerUserId: string;
  partnerDisplayName: string;
  pairedAtUtc: string;
};

export type PairingCode = {code: string; expiresAtUtc: string};

export const pairingApi = {
  current: () => apiRequest<Pair | null>('/api/pairing/current'),
  createCode: () => apiRequest<PairingCode>('/api/pairing/code', {method: 'POST'}),
  redeem: (code: string) => apiRequest<Pair>('/api/pairing/redeem', {method: 'POST', body: {code}}),
};
