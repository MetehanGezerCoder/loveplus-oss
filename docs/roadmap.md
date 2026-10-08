# Love+ roadmap

## P0 — Foundation

- [x] Authentication, device sessions and refresh-token rotation/reuse detection
- [x] Pairing code and pair-level authorization
- [x] PostgreSQL/PostGIS migration and Redis adapter
- [x] Redacted application logging and consistent Problem Details errors
- [x] Application, integration and architecture tests
- [x] Dockerised HTTPS deployment, startup migrations and fail-closed Production configuration (v0.5.0)
- [x] Standalone signed release APK path with build-time HTTPS enforcement (v0.5.0)
- [x] Production FCM adapter, device push-token registration and token retirement (v0.5.0, server side)
- [ ] Android FCM token acquisition (needs a Firebase project and `google-services.json`)
- [ ] CI, secret scanning, dependency scanning and container scanning
- [ ] KVKK consent records, retention enforcement, export and deletion workers
- [ ] Outbox processor with retry and dead-letter metrics
- [ ] Pairing-code creator's screen live-updates when the partner redeems the code — currently
      requires a force-close/reopen; `useCurrentPair()` has no `refetchInterval` and nothing
      wires `AppState` to React Query's `focusManager`. Found during the real two-phone pairing
      test on 2026-08-16; see `CLAUDE.md` P2 item 15 for fix options.

## P1 — First vertical slice (Phase 1A/1B delivered)

- [x] Permission-gated Android background location/battery collection
- [x] Activity Recognition transition infrastructure
- [x] Authenticated SignalR status receive connection
- [x] Backend distance projection
- [x] Idempotent critical battery alert contract
- [x] Android home-screen widget backed by encrypted local state
- [x] Bounded offline queue and ordered replay
- [x] Presence decoupled from Activity foreground state, with client-side freshness re-derivation (v0.5.0)
- [x] SignalR reconnect that never surrenders, plus foreground-triggered reconnect and republish (v0.5.0)
- [x] In-app diagnostics for API, live connection, telemetry, queue depth, build and permissions (v0.5.0)
- [ ] Physical-device battery consumption, Doze and OEM-kill test matrix (steps 15–21 in two-device-demo.md, not yet executed)
- [ ] End-to-end FCM delivery from a real Firebase project
- [ ] iOS background status equivalent

## P2 — Private communication (Planned)

- [ ] Private chat and delivery receipts
- [x] Android foreground/process-alive Haptic Heartbeat with app acknowledgement (v0.4.0 Phase 3)
- [ ] Push-assisted notification when the receiving app is fully closed
- [ ] Push-to-talk
- [ ] Flash-Cam request flow
- [ ] Client-side media encryption and encrypted cloud vault

## P3 — Safety

- [ ] SOS and Safe Walk sessions
- [ ] Arrival timer and escalation rules
- [ ] Fall/inactivity detection with false-positive cancellation

## P4 — Memories and business

- [ ] Special days and reminders
- [ ] Shared-place detection and memory timeline
- [ ] “Bugün yaklaştınız”, historical distance comparison and automatic shared-place cards
- [ ] Subscription tiers, quotas and store purchases
- [ ] Admin/support portal and abuse controls

## Platform backlog

- [ ] iOS Core Haptics equivalent and WidgetKit status widget
- [ ] Production FCM routing for critical battery and closed-app heartbeat notification (never delayed waveform replay)
