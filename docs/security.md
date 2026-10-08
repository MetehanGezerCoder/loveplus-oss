# Security and privacy

## Implemented controls

- API derives user, device-session and pair identity from validated JWT claims; status bodies contain no trusted identity fields.
- Every partner query and SignalR connection resolves the current active pair in the database.
- The hub is authenticated and receive-only. There is no client-controlled group join method.
- Access tokens live for 15 minutes by default. Refresh secrets are random, SHA-256 hashed at rest, single-use, rotated, and revoke the device session when reuse is detected.
- Passwords use ASP.NET Core `PasswordHasher` rather than reversible encryption.
- Pairing codes are random, stored only as hashes, valid for ten minutes, single-use, and executed in a serializable transaction.
- Coordinates, tokens, passwords and personal status content are not logged.
- Status validation checks coordinates, accuracy, battery, enum, freshness and a monotonically increasing sequence.
- Redis sequence replacement is atomic. PostgreSQL constraints protect unique identities, active membership, token/code hashes and open critical-battery episodes.
- Precise coordinates never enter SignalR critical alerts, push DTOs, outbox payloads, notifications or widget state.
- Android status credentials and the bounded offline location queue use encrypted preferences backed by Android Keystore.
- Basic rate limits protect authentication and REST status endpoints. SignalR is receive-only.
- Heartbeat uses app-token-only HTTP commands, active-pair lookup, per-user and per-pair limits, a 16 KiB SignalR ceiling, 8-second/32-entry waveform bounds and short-lived `EventId` idempotency.
- Heartbeat patterns never enter PostgreSQL, outbox, Android offline status queues or application logs. Redis stores only routing/ACK metadata with TTL.
- Android validates the waveform again, never repeats it, remembers recent event IDs per user in encrypted preferences and cancels active vibration when receive is disabled or the bridge is invalidated.
- Mood sharing defaults on for older clients, but the partner DTO replaces mood with `None` immediately when disabled. Location sharing off returns no prior coordinate or exact distance.

## Fail-closed production startup (v0.5.0)

`ProductionConfigurationGuard` refuses to start a Production process that is only accidentally safe. It rejects the Development in-memory store, an enabled demo seed, an enabled telemetry simulator, a missing PostgreSQL/Redis connection string, a signing key or password still containing an example placeholder, and an undeclared push provider. A deployment that wants to run without notifications must say so with `Push__Provider=disabled`; silence is not accepted.

It also rejects a presence window narrower than twice the client telemetry cadence, because that combination reports a connected partner as offline after one missed packet — an honesty failure, not just a tuning problem.

## Transport and proxy trust

- The API rejects any request its trusted proxy reports as cleartext (`Api__RequireHttps`). It does not redirect: TLS terminates at the edge, and a redirect to a port the container cannot know would loop.
- `X-Forwarded-For`/`X-Forwarded-Proto` are honoured only for addresses or CIDR ranges named in `Api__TrustedProxies`. Without that declaration the socket address is used, because a spoofable header would let a caller choose its own authentication rate-limit partition.
- Release Android builds refuse to compile against a non-HTTPS API URL, and refuse `127.0.0.1`, `10.0.2.2` and `localhost` outright. `usesCleartextTraffic` stays false outside debug.
- SignalR authenticates WebSocket upgrades with an `access_token` query parameter, which is standard for the transport but does place a short-lived token in a URL. Keep reverse-proxy access logging off, or strip query strings from it.

## Push notifications

- The FCM adapter sends only a battery percentage and a boolean saying whether a last-known location exists. Coordinates, moods, distances and heartbeat waveforms never enter a push payload.
- Push registration tokens are bound to the authenticated device session; a client cannot register a token for another user. A token that appears under a new session retires its previous row, and tokens FCM reports as unregistered are disabled automatically.
- A closed phone may later receive an ordinary notification. It never receives a replayed heartbeat waveform, and the sender is never shown "delivered" without a receiver acknowledgement.

## Diagnostics

`GET /api/diagnostics` is authenticated and returns environment, version, which adapters are in use, whether migrations ran, the push provider, and the presence windows. It contains no connection string, key, token, coordinate or partner value. The Android diagnostics screen adds build type, telemetry service state, the time of the last successful upload, the failure category of the last attempt, the offline queue depth and permission state — again with no payload contents.

## Deployment checklist

- Supply secrets from a managed secret store; never bake `.env`, JWT keys, FCM credentials or signing keys into an image/APK.
- Require HTTPS at the edge and for the production mobile API URL. Configure trusted proxies before using forwarded client IPs for rate limiting.
- Register a real `IPushNotificationSender`; production startup deliberately fails without one.
- Enable PostgreSQL/Redis TLS and private networking; rotate passwords and JWT keys with an overlap strategy.
- Add outbox processing with retry/dead-letter metrics before relying on push delivery.
- Define and enforce KVKK/GDPR consent, retention, export, unpairing and erasure policies.
- Revoke active SignalR connections when pair membership or account state changes.
- Replace the single-process presence tracker with distributed presence before horizontally scaling the API.
- Add mobile certificate pinning only with a tested key-rotation and recovery plan.
- Run dependency, SAST, secret and container scans in CI. Review the RN 0.80/Metro transitive advisories before release.
- Keep release signing material outside the repository and enable Play Integrity/attestation only as a risk signal, not an authorization substitute.

`FLAG_SECURE` and screenshot controls are deterrents, not absolute exfiltration prevention. Love+ must not claim that media or screens can never be captured or downloaded.
