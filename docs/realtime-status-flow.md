# Realtime status flow

1. The user explicitly enables tracking after foreground, activity-recognition, notification and background-location permission steps.
2. `LoveStatusService` requests balanced fused-location updates: 60-second target, 15-second minimum, 25-metre displacement and two-minute batching. It reads battery state and the latest activity transition.
3. The service assigns a device-persistent sequence number and sends the packet to authenticated `POST /api/status/`. Failed packets enter an Android Keystore-backed FIFO capped at 50 items; reconnect replays them in order.
4. The API ignores client identity fields and supplies user/device identity from access-token claims.
5. FluentValidation checks coordinates, accuracy, battery, activity, mood enum and sequence. The handler rejects packets outside the configured 24-hour offline window or more than two minutes in the future.
6. Active pair membership is resolved from PostgreSQL. Redis atomically rejects stale/duplicate sequence values and stores the accepted packet for the configured live TTL (12 minutes by default).
7. PostgreSQL updates the user's last-known PostGIS snapshot only after one minute or immediately for a critical battery status.
8. Distance is calculated server-side using a tested Haversine implementation against the partner's current Redis status. Configured proximity thresholds and both accuracy radii select `SamePlace`, `VeryClose`, `Nearby`, `SameArea`, `Far` or `Unavailable`.
9. A safe DTO without coordinates is published to the authenticated pair SignalR group. Each app ignores its own user ID, updates TanStack Query cache, and writes minimal encrypted widget state.
10. On the first status at or below 5%, the handler opens one critical-battery episode, persists the last-known location, writes a coordinate-free outbox record and emits SignalR/push DTOs. Further low readings reuse the open episode. A reading above 5% closes it.

## Presence

Presence is a function of the age of the newest accepted packet, never of whether the dashboard
Activity is foreground. `RealtimeStatusPolicy` classifies it as `Online` inside
`Realtime__OnlineSeconds`, `RecentlyOnline` inside `Realtime__RecentlyOnlineSeconds` and
`Offline` after that.

The Android foreground service publishes every `LoveStatusService.HEARTBEAT_INTERVAL_SECONDS`
(60 s), plus immediately on battery/charging change, meaningful movement, a sharing change, and
whenever the app returns to the foreground. Startup refuses an online window under twice that
cadence, so one dropped packet cannot report a connected partner as offline.

The published DTO carries `presenceOnlineSeconds` and `presenceRecentlyOnlineSeconds`. The client
caches the DTO between pushes and re-derives presence from `recordedAtUtc` on a 15-second tick,
so an idle screen ages out of "Çevrimiçi" on its own instead of repeating a verdict that was only
true when it was sent. A distance computed from a packet that has aged past the recently-online
window is presented as last known, not as the current distance.

The SignalR client retries forever with capped backoff, restarts itself if the hub closes, and
reconnects, refetches and republishes when the app returns to the foreground.

## Failure behavior

- Redis loss rejects live writes rather than bypassing sequence protection.
- PostgreSQL loss rejects the status before realtime publication.
- SignalR/push failure happens after persistence; the outbox record is the durable recovery boundary. The Phase 1 development adapter does not provide external push delivery.
- Access-token expiry causes REST refresh rotation in the app; the refreshed access token is copied to the native secure store and used by later SignalR reconnects.
- The widget never owns a network connection and Android limits periodic fallback refreshes to 30 minutes.
- Location sharing off removes the partner-facing coordinate and exact distance immediately, even when an internal last-known sample exists.

Mood changes use app-authenticated `POST /api/status/mood`. The live Redis record and optional snapshot are updated only after validation; SignalR then refreshes the partner card and local widget. Selecting the existing mood does not issue another request, and UI selection rolls back on failure.
