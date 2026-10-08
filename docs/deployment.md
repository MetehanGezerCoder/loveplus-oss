# Remote deployment

This guide describes a reusable remote deployment for two phones on separate networks, with no development machine, Metro server, USB cable, or ADB reverse tunnel required.

Everything here is configuration and infrastructure. No secret belongs in the repository, container image, APK, issue, or chat log.

## What you must provide

| Dependency | Why it is needed | What to do once |
| --- | --- | --- |
| A host with a public IP and Docker | Runs the API, PostgreSQL/PostGIS, Redis and TLS proxy | A small VPS is enough for a small deployment |
| A domain name with an A/AAAA record pointing at that host | Caddy needs a hostname for TLS and the APK needs a stable HTTPS API URL | Point e.g. `loveplus.example.com` at the host |
| An Android release keystore | Release APKs must be signed by a key you control | See [android-release.md](android-release.md) |
| Optional Firebase project | Push notifications when the receiving app is fully closed | Configure only when needed |
| Optional Google Maps API key | Renders the shared map | Provide at build time; never commit it |

## 1. Configure

```sh
cp .env.production.example .env.production
```

Fill in every blank value and generate secrets locally:

```sh
openssl rand -base64 64   # JWT_SIGNING_KEY
openssl rand -base64 24   # POSTGRES_PASSWORD
openssl rand -base64 24   # REDIS_PASSWORD
```

`.env.production` is git-ignored. Keep it on the host and in a password manager or secret store.

Production fails closed when required configuration is insecure or incomplete, including placeholder signing keys/passwords, missing data-store configuration, development seed/simulator modes, undeclared push providers, or an invalid realtime presence window.

## 2. Start

```sh
docker compose --env-file .env.production -f docker-compose.prod.yml config
docker compose --env-file .env.production -f docker-compose.prod.yml up -d --build
docker compose --env-file .env.production -f docker-compose.prod.yml ps
```

PostgreSQL and Redis should remain on the internal Compose network. Only the TLS proxy should expose public HTTP/HTTPS ports.

Verify from another machine:

```sh
curl -fsS https://loveplus.example.com/health
curl -fsS https://loveplus.example.com/health/ready
curl -is https://loveplus.example.com/api/status/partner | head -1
```

The protected partner endpoint must not return `200` without authentication.

## 3. Create and pair accounts

Production does not seed accounts. Register two accounts through the app, then pair them:

1. User A installs the APK and registers.
2. User A creates a ten-minute single-use pairing code.
3. User B installs the same APK, registers, and redeems the code.

Pairing codes are stored only as hashes, expire after ten minutes, and are single use.

## 4. Build and distribute Android

See [android-release.md](android-release.md). The short version:

```sh
cd mobile
LOVEPLUS_API_BASE_URL=https://loveplus.example.com \
LOVEPLUS_GOOGLE_MAPS_API_KEY=<optional> \
  ./android/gradlew -p android :app:assembleRelease
```

Release builds reject cleartext/local-loopback API URLs. Signing material must stay outside the repository.

## Push notifications

SignalR handles live heartbeat/status delivery while the app process is connected. A fully closed or force-stopped phone cannot receive realtime events.

The backend contains an FCM HTTP v1 adapter and device-token registration path. If Firebase is not configured, run production with an explicit disabled provider rather than an implicit fallback:

```text
PUSH_PROVIDER=disabled
```

## Proxy trust

`API_TRUSTED_PROXIES` controls which addresses may supply forwarded client/protocol headers. Narrow the configured range to your actual reverse-proxy network whenever possible.

## Backups

PostgreSQL contains durable account, pair, device-session, and throttled location snapshot data. Redis holds ephemeral live status.

```sh
docker compose --env-file .env.production -f docker-compose.prod.yml exec -T postgres \
  pg_dump -U "$POSTGRES_USER" "$POSTGRES_DB" | gzip > loveplus-$(date +%F).sql.gz
```

Treat database backups as sensitive personal data. Encrypt them at rest and apply a retention policy.

## Scaling note

Review the realtime presence implementation before running multiple API replicas. Any in-memory presence/rate state must be moved to a shared backing store before horizontal scaling.
