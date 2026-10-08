# Architecture

Love+ v0.4.0 is a modular monolith. Module boundaries are represented by namespaces and CQRS contracts; high-volume modules can later be extracted without exposing repositories across modules.

## Dependency direction

```text
LovePlus.Api -> LovePlus.Infrastructure -> LovePlus.Application -> LovePlus.Domain
              LovePlus.Api --------------------^             -> LovePlus.Domain
```

- Domain contains entities and value types.
- Application contains commands, queries, validators, safe DTOs and ports.
- Infrastructure owns EF Core/PostGIS, Redis, password/JWT cryptography and external adapters.
- API owns authenticated claims, HTTP contracts, rate limits, SignalR groups and health endpoints.

The Application project references EF Core abstractions through `IApplicationDbContext`; it never references the concrete infrastructure context.

## Implemented modules

- Identity: users, device sessions and rotating refresh tokens.
- Pairing: pair lifecycle, active memberships and one-time codes.
- Realtime Status: ephemeral Redis state, durable last-known snapshot and distance projection.
- Notifications: critical battery event, outbox record, SignalR delivery and push provider port.
- Heartbeat: authenticated HTTP commands, validation, ephemeral idempotency/rate metadata, user-scoped SignalR delivery and receiver acknowledgement.
- Proximity: server-side distance projection, configurable levels and GPS-accuracy-aware wording.

Messaging, Vault, Safety, Memories, Special Days and Subscriptions are roadmap boundaries only; Heartbeat has no dependency on a future messaging module.

## Data ownership and retention

- Redis holds full live-status packets for a configured short TTL (12 minutes by default).
- PostgreSQL updates one last-known status row per user at most once per minute, except critical battery transitions, which persist immediately.
- PostGIS `geography(point)` columns and GiST indexes prepare future consented spatial queries.
- Critical-battery episodes remain open until battery recovery; a partial unique index prevents duplicate open episodes.
- Pair membership is explicit so unpairing and deletion workflows can later preserve audit state while removing personal data.
- Outbox payloads contain identifiers and availability flags, never exact coordinates.
- Heartbeat patterns are never durable. Redis holds only event routing/idempotency/ACK metadata for 20 seconds by default; neither the pattern nor a delayed-delivery job is stored.

## Mobile boundaries

Feature folders export their public API from `index.ts`. TanStack Query owns server state; Zustand owns the small authentication/bootstrap state. The native bridge exposes configuration, tracking, bounded haptic playback and minimal widget updates. The widget does not connect to SignalR and stores no coordinates.

## Heartbeat routing decision

`POST /api/heartbeat/` is a CQRS boundary. Claims identify the sender; the active pair identifies the partner. The API publishes to the partner's user group, not to a client-selected target. All currently connected partner devices receive the event; each device deduplicates `EventId`, and the first accepted ACK establishes the sender-facing result. Disconnected devices do not receive delayed waveforms. In-memory presence is adequate for the USB demo; a scaled multi-instance deployment must replace it with a distributed presence adapter.

## Production boundaries

`src/LovePlus.Api/Startup` owns the deployment boundary: `ProductionConfigurationGuard` (fail-closed configuration validation), `PushNotificationSetup` (declared provider selection), `DatabaseMigrator` (startup migration with a reported outcome) and the `RuntimeDiagnostics` contract.

The development push adapter performs no external send and logs only the event identifier. Production must declare `Push__Provider`: either `firebase` with a service account, which selects the FCM HTTP v1 adapter, or an explicit `disabled`. An undeclared provider fails startup, because a silent no-op would let the product imply a delivery it never made.

Production deployment also requires TLS at the edge, a managed secret store, database backups, Redis authentication/TLS, observability redaction and a real retention/deletion worker. See [deployment.md](deployment.md).
