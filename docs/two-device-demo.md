# Two-device demo

Love+ supports a local development demo and a remote two-phone deployment. Demo data is intended only for Development and must never contain real credentials or personal data.

## Remote arrangement

1. Deploy the backend using [deployment.md](deployment.md) and verify `/health/ready` over HTTPS.
2. Build and verify the Android release using [android-release.md](android-release.md).
3. Install the same APK on two phones and register two accounts through the app. Production never seeds accounts.
4. Pair the accounts with a ten-minute single-use code.
5. Disconnect development tooling and verify the behavior over ordinary mobile/Wi-Fi networks.

The diagnostics screen can be used to inspect API reachability, realtime connection state, last telemetry upload, queue depth, build environment, and permissions without exposing tokens, coordinates, or partner values.

## Local development demo

Set these only in a local development environment:

```sh
Demo__SeedEnabled=true
Demo__Password=<local demo password>
LOVEPLUS_API_BASE_URL=http://127.0.0.1:5080
```

The seeded accounts are:

- `alex@example.com` (Alex)
- `taylor@example.com` (Taylor)

The password is supplied through runtime configuration and is not stored in source control or mobile code. Demo seeding is ignored outside Development.

## Physical-device setup

For each connected Android target, expose Metro and the API using that device's own ADB serial:

```sh
adb -s <device-a-serial> reverse tcp:8081 tcp:8081
adb -s <device-a-serial> reverse tcp:5080 tcp:5080
adb -s <device-b-serial> reverse tcp:8081 tcp:8081
adb -s <device-b-serial> reverse tcp:5080 tcp:5080
```

`./scripts/start-usb-demo.sh` can apply the reverse tunnels to connected devices automatically.

## Verification matrix

1. Plug and unplug charging on each device and verify the partner card updates through SignalR.
2. Enable location on both devices and verify markers, accuracy, update time, and backend-computed distance.
3. Disable location on one device and verify new coordinates stop immediately and exact distance is hidden.
4. Deny location/activity permissions, including permanent denial, and verify the app remains usable.
5. Generate activity samples and verify low-confidence samples do not cause unstable activity changes.
6. Disable network, generate updates, reconnect, and verify queue ordering and replay behavior.
7. Log out and switch accounts; service state, widget state, queue data, and cached mood must be cleared.
8. Exercise the critical-battery path and verify duplicate suppression and recovery behavior.
9. Change moods in both directions and verify realtime propagation.
10. Send haptic heartbeat patterns in both directions and verify delivery/ack behavior.
11. Disable heartbeat receive and verify the sender receives the privacy-safe disabled result.
12. Force-stop the receiver and verify old heartbeat patterns are never replayed later.
13. Burst requests above the configured limit and verify rate limiting prevents continuous vibration.
14. Background one phone and verify status remains accurate while the foreground service is alive.
15. Return it to foreground and verify realtime reconnect/refetch occurs without an app restart.
16. Force-stop one phone and verify the partner transitions from online to recently-online/offline based on configured windows.
17. Disable networking for several minutes and verify queued telemetry cannot overwrite newer state after reconnect.
18. Exercise aggressive OEM battery optimization and document any device-specific behavior.
19. Reboot a phone and verify the partner is not falsely reported online before Love+ resumes publishing.
20. Switch accounts and verify no prior account's cached partner information survives.

## Emulator locations

Use the Android Emulator location controls to send different GPS positions. This exercises the normal fused-location path rather than a Love+-specific simulator endpoint.

## Maps

Set `LOVEPLUS_GOOGLE_MAPS_API_KEY` at build time when map rendering is needed. When absent, telemetry can continue while the map uses its safe empty state. Never commit the key.
