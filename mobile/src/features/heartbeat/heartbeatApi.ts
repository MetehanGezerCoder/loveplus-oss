import {apiRequest} from '../../shared/api/apiClient';
import type {HeartbeatAcknowledgementState, SendHeartbeatResult} from './types';

export const heartbeatApi = {
  send: (eventId: string, pattern: number[]) => apiRequest<SendHeartbeatResult>('/api/heartbeat/', {
    method: 'POST', body: {eventId, pattern},
  }),
  acknowledge: (eventId: string, state: HeartbeatAcknowledgementState) =>
    apiRequest<{accepted: boolean; duplicate: boolean}>(`/api/heartbeat/${eventId}/ack`, {
      method: 'POST', body: {state},
    }),
};
