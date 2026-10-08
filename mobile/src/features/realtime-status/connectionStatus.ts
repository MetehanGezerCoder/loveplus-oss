import {create} from 'zustand';

export type ConnectionState = 'connecting' | 'connected' | 'offline';

type RealtimeConnectionStore = {
  state: ConnectionState;
  lastConnectedAt: number | null;
  lastFailureAt: number | null;
  /** Failure category only. Hub payloads carry partner data and never enter diagnostics. */
  lastFailureReason: string | null;
  report: (state: ConnectionState, failureReason?: string) => void;
};

/**
 * The hub is owned by the dashboard, but the diagnostics screen has to describe it too.
 * Sharing the observable state avoids opening a second connection just to inspect the first.
 */
export const useRealtimeConnection = create<RealtimeConnectionStore>(set => ({
  state: 'connecting',
  lastConnectedAt: null,
  lastFailureAt: null,
  lastFailureReason: null,
  report: (state, failureReason) =>
    set(current => ({
      state,
      lastConnectedAt: state === 'connected' ? Date.now() : current.lastConnectedAt,
      lastFailureAt: state === 'offline' ? Date.now() : current.lastFailureAt,
      lastFailureReason: state === 'offline' ? failureReason ?? current.lastFailureReason : current.lastFailureReason,
    })),
}));
