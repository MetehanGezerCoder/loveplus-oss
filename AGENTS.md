# Love+ repository guide

## Start here

- Read `README.md`, this file, and the relevant document under `docs/` before substantial work.
- Treat `main` as the source of truth. Keep changes focused, reviewable, and covered by the relevant tests.
- Never add real credentials, personal data, production endpoints, signing material, or private test data to the repository.

## Structure

- `src/LovePlus.Domain`: entities and domain value types; no application or infrastructure references.
- `src/LovePlus.Application`: CQRS handlers, validation, DTOs and dependency contracts.
- `src/LovePlus.Infrastructure`: EF Core/PostGIS, Redis, identity crypto and external adapters.
- `src/LovePlus.Api`: HTTP/SignalR boundary, authentication, rate limiting and health checks.
- `src/LovePlus.Api/Startup`: production configuration guard, push provider selection, startup migration and the diagnostics contract.
- `tests`: application, HTTP integration and architecture tests.
- `mobile`: React Native TypeScript app and Android Kotlin service/widget.
- `deploy`: reverse-proxy configuration for remote deployment.
- `docs`: architecture, security, deployment, Android release, local setup, realtime flow and roadmap.

## Architecture and code rules

- Dependencies point inward: Api -> Infrastructure/Application -> Domain.
- A mobile feature may expose cross-feature code only through its `index.ts`.
- Use CQRS handlers for use cases; keep HTTP claims and transport types at the API boundary.
- Store and compare timestamps in UTC. Pass cancellation tokens through I/O paths.
- Use no-tracking queries for reads and avoid N+1 queries.
- Keep Redis status ephemeral; throttle PostgreSQL snapshots.
- Heartbeat patterns are ephemeral transport payloads: never write them to PostgreSQL, outbox, offline queues or application logs.
- Derive heartbeat sender, pair and receiver from authenticated state; clients never choose a target user or pair.
- Proximity is calculated on the server from live samples and configured thresholds; mobile telemetry never supplies a trusted distance.
- Presence is derived from the age of the newest telemetry packet, never from whether an Activity is foreground. The DTO carries the windows that produced its verdict so a cached copy can be re-evaluated instead of repeating a stale claim.
- The Android telemetry cadence (`LoveStatusService.HEARTBEAT_INTERVAL_SECONDS`) and the server online window (`Realtime__OnlineSeconds`) are one decision in two places; startup fails when the window is under twice the cadence.
- Production startup is fail-closed. A new external dependency must be declared in configuration and validated in `ProductionConfigurationGuard`, never defaulted to a silently degraded mode.

## Commands

```sh
dotnet restore LovePlus.slnx
dotnet build LovePlus.slnx --no-restore
dotnet test LovePlus.slnx --no-restore
dotnet ef database update --project src/LovePlus.Infrastructure --startup-project src/LovePlus.Infrastructure
docker compose config
docker compose up -d
docker compose --env-file .env.production -f docker-compose.prod.yml config
cd mobile && npm ci
npm run typecheck
npm run lint
npm test -- --runInBand
JAVA_HOME=$(/usr/libexec/java_home -v 17) ANDROID_HOME="$ANDROID_HOME" ./android/gradlew -p android testDebugUnitTest :app:assembleDebug
LOVEPLUS_API_BASE_URL=https://<host> ./scripts/build-release-apk.sh
```

## Security rules

- Never trust `UserId`, `PairId` or `DeviceId` from a request body; derive them from authenticated claims.
- Never log coordinates, passwords, bearer/refresh tokens, media metadata or personal message contents.
- Never store refresh or pairing tokens in plaintext. Do not commit `.env`, signing material or provider credentials.
- Enforce pair membership in every partner-data query and SignalR group join.
- Widgets and notifications must not expose precise coordinates.
- Turning off location or mood sharing must immediately hide the previous partner-facing value.
- Heartbeat waveform bounds and duplicate protection must exist on both the backend and Android client.
- Production adapters must fail closed when credentials/providers are not configured.
- Signing keystores, `keystore.properties`, `.env.production` and Firebase service accounts stay outside the repository; the release build reads them from disk or environment and produces an unsigned APK rather than using a committed key.
- A release Android build must not compile against a non-HTTPS, `127.0.0.1`, `10.0.2.2` or `localhost` API URL.
- Diagnostics surfaces may report configuration and failure categories, never tokens, coordinates, payloads or partner values.

## Open-source contribution rules

- Prefer neutral demo identities such as `alex@example.com` and `taylor@example.com`; do not add real names or e-mail addresses to fixtures or screenshots.
- Use example domains in documentation. Do not document a maintainer's private server topology unless it is essential and intentionally public.
- Keep PRs narrow and explain security/privacy impact when touching auth, pairing, sessions, realtime, notifications, location, diagnostics, or retention.
- Update `README.md`, `CONTRIBUTING.md`, `SECURITY.md`, or `docs/` when contributor or operator behavior changes.
- AI-assisted changes are welcome, but generated output must be reviewed and validated like any other contribution.

## Do not

- Do not add microservices, Kubernetes or Terraform without a clear project need and prior discussion.
- Do not bypass validation, rate limits, pair isolation or refresh-token reuse detection.
- Do not leave illustrative TODO implementations on critical paths.

## Definition of done

A change is complete only when relevant build/test/typecheck/lint commands pass, migrations and contracts remain aligned, security boundaries are covered by tests, no secret or personal data is added, and documentation reflects any operational change.
