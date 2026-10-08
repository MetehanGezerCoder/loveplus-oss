# Love+

Love+ is an open-source, privacy-first reference application for secure two-person mobile experiences. It combines a .NET 10 backend, PostgreSQL/PostGIS, Redis, SignalR, and a React Native Android client to demonstrate pairing, realtime presence, privacy-aware location sharing, haptic heartbeat delivery, diagnostics, and production-safe deployment patterns.

The project is intentionally small enough to study and contribute to, while still exercising real-world authentication, authorization, realtime, mobile background work, deployment, and privacy boundaries.

> Status: active pre-1.0 development. The current milestone is v0.5.0.

## Why this project exists

Love+ is both a working application and a reusable reference for developers building privacy-sensitive mobile products. The codebase focuses on patterns that are easy to get wrong in production: pair isolation, refresh-token rotation, background telemetry, stale-presence handling, realtime authorization, location privacy, fail-closed configuration, and secret-safe Android release builds.

AI-assisted development is welcome. `AGENTS.md` is the repository guide for Codex/Claude-style coding agents, and contributions must still pass the same human-reviewed build, test, security, and documentation requirements.

## What works

- .NET 10 modular-monolith API with Clean Architecture boundaries, CQRS/MediatR and FluentValidation
- Registration, login, short-lived JWT access tokens, hashed refresh-token rotation/reuse detection, device sessions and secure logout
- Ten-minute single-use pairing codes and database-enforced active-pair membership
- Redis status snapshots with TTL and atomic sequence rejection
- Throttled PostgreSQL/PostGIS last-known snapshots, Haversine live distance and idempotent critical-battery events/outbox records
- Authenticated receive-only SignalR hub; clients can join only the group derived from their active pair
- React Native screens for bootstrap, auth, pairing, dashboard, permissions, settings and diagnostics
- Android foreground location/battery/activity service, encrypted 50-item offline queue and native widget state
- `InLove`, `MissingYou` and `Sulky` moods with Turkish localization, privacy toggle and widget fallback
- Authenticated CQRS Heartbeat send/acknowledge flow, SignalR user delivery, short-lived idempotency/rate metadata and native Android waveform playback
- Server-configured proximity levels that consider both devices' GPS accuracy and never expose old coordinates after sharing is disabled
- Application, HTTP integration, architecture and SignalR authorization/isolation tests

### New in v0.5.0

- **Deployable backend**: `Dockerfile`, `docker-compose.prod.yml` and a Caddy TLS front door; migrations run on startup and are reported, not assumed
- **Fail-closed Production startup**: placeholder secrets, the in-memory demo store, an enabled seed/simulator and an undeclared push provider all refuse to boot
- **Standalone release APK**: the JS bundle is embedded, release signing is read from outside the repository, and a release build cannot be compiled against a cleartext or loopback API URL
- **Honest presence**: the partner DTO carries the windows that produced its verdict, so a cached status ages out of "online" on the client instead of repeating a stale claim
- **Reconnect that does not give up**: the SignalR hub retries forever with capped backoff and reconnects when the app returns to the foreground
- **Diagnostics**: `GET /api/diagnostics` plus an in-app screen for API reachability, live-connection state, last telemetry upload, queue depth, build environment and permissions
- **Push boundary**: a real FCM HTTP v1 adapter with device-token registration and automatic retirement of unregistered tokens

Chat, private media, SOS, Safe Walk, subscriptions and iOS haptics remain out of scope.

## Prerequisites

- .NET 10 SDK
- Docker with Compose v2
- Node.js 18+ and npm
- Android Studio/SDK 35, NDK `27.1.12297006` and JDK 17 for Android builds

## Local setup

```sh
cp .env.example .env
```

Replace every `replace-with-...` value. Generate a local JWT key without committing it:

```sh
openssl rand -base64 64
```

Load the variables and start dependencies:

```sh
set -a
source .env
set +a
docker compose config
docker compose up -d
```

Apply the migration and run the API:

```sh
dotnet tool install --global dotnet-ef --version 10.0.4
dotnet restore LovePlus.slnx
dotnet ef database update --project src/LovePlus.Infrastructure --startup-project src/LovePlus.Infrastructure
dotnet run --project src/LovePlus.Api
```

OpenAPI/Scalar is exposed only in Development. Health endpoints are `/health`, `/health/live` and `/health/ready`.

Run the Android client:

```sh
cd mobile
npm ci
npm run typecheck
npm run lint
npm start
# separate terminal
npm run android
```

The emulator development URL defaults to `http://10.0.2.2:5080`. Set `LOVEPLUS_API_BASE_URL=https://...` when building another environment. Release builds reject non-HTTPS and local-loopback configuration at build time, and the native bridge rejects it again at runtime.

## Verification

```sh
dotnet build LovePlus.slnx
dotnet test LovePlus.slnx --no-restore
cd mobile
npm run typecheck
npm run lint
npm test -- --runInBand
```

CI runs the backend build/tests and mobile typecheck/lint/tests on pushes and pull requests to `main`.

## Documentation

- [Architecture](docs/architecture.md)
- [Security](docs/security.md)
- [Local development](docs/local-development.md)
- [Deployment](docs/deployment.md)
- [Android release](docs/android-release.md)
- [Realtime status flow](docs/realtime-status-flow.md)
- [Haptic Heartbeat](docs/haptic-heartbeat.md)
- [Proximity experience](docs/proximity-experience.md)
- [Roadmap](docs/roadmap.md)

## Contributing

Contributions are welcome. Start with [CONTRIBUTING.md](CONTRIBUTING.md) and `AGENTS.md`. Security-sensitive reports should follow [SECURITY.md](SECURITY.md), not a public issue.

## License

Love+ is released under the [MIT License](LICENSE).
