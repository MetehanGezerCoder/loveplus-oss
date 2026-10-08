# Haptic Heartbeat

Haptic Heartbeat sends one short, relative Android vibration pattern independently of chat. The sender taps the dashboard heart, reviews the recorded count and explicitly presses send.

## Flow

1. Mobile normalizes tap gaps and creates `EventId`.
2. App-token-authenticated `POST /api/heartbeat/` accepts only `{ eventId, pattern }`.
3. `SendHeartbeatCommand` derives sender, active pair and partner from claims/database state; validates 32 entries, 1.5-second entry and 8-second total bounds; checks live presence and per-user/per-pair rate limits.
4. The pattern is published once to the active partner's user-scoped SignalR group. It is not written to PostgreSQL, outbox, Redis payload storage, Android offline queues or logs.
5. Android validates again, checks user-specific receive preference/support/duplicate ID and plays `VibrationEffect.createWaveform(..., repeat = -1)` using bounded amplitude.
6. Receiver posts `Played`, `HapticDisabled` or `Unsupported` to `/api/heartbeat/{eventId}/ack`. Sender shows delivered only after ACK; timeout is seven seconds.

Redis retains only routing, expiry and first-ACK metadata for 20 seconds by default. Duplicate send and ACK are safe. Multiple connected partner devices receive the event, each device plays an `EventId` once, and the first accepted ACK determines the displayed outcome.

## Limits and background behavior

Defaults live under `Heartbeat` configuration: three sends per 30 seconds for both user and pair, 32 entries, 8 seconds total and 1.5 seconds per entry. The API also has an HTTP fixed-window limiter and 16 KiB SignalR payload ceiling.

Foreground and a living background process can receive while SignalR remains connected. A fully closed/force-stopped app is offline: without production FCM, Love+ does not claim delivery and never replays an old waveform later. The push adapter may send a generic private notification, never a delayed vibration pattern.

Delivery therefore depends on the hub actually being connected, which is why the v0.5.0 reconnect
work matters here as much as it does for presence. The client now retries forever with capped
backoff, restarts a closed hub, and reconnects when the app returns to the foreground; before
that, a receiver returning from Doze could hold a permanently closed connection and every send
to it correctly — but avoidably — returned "partner is not connected right now".

This has been fixed in code and covered by unit tests, but the behaviour has **not** yet been
measured on hardware across Doze, One UI battery limits, process death and network switching
(`docs/two-device-demo.md`, steps 15–21). Until that run happens, do not describe background
heartbeat delivery as reliable.

Android encrypted preferences are keyed by user. Disabling receive cancels any active vibration immediately. Device support fallback is an in-app pulse when visible; emulator behavior is not evidence of physical haptic fidelity. Android DND/system vibration policy remains authoritative.
