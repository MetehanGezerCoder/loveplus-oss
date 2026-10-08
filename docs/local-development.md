# Local development

This is the developer loop against a local backend. For the deployment two people actually use
from separate networks, see [deployment.md](deployment.md) and
[android-release.md](android-release.md).

## Environment

Copy `.env.example` to `.env`, replace all placeholder passwords, and generate a local JWT key with `openssl rand -base64 64`. Load the file into the current shell before running .NET commands:

```sh
set -a
source .env
set +a
```

For the private two-person demo, follow [two-device-demo.md](two-device-demo.md). The optional development seed creates only the two accounts and ready pair; it never creates synthetic device status.

## Infrastructure

```sh
docker compose config
docker compose up -d
docker compose ps
```

PostgreSQL/PostGIS listens on 5432, Redis on 6379 and MinIO on 9000/9001. MinIO is provisioned for later encrypted-media work and is not used by v0.4.0 code.

## Database and API

```sh
dotnet restore LovePlus.slnx
dotnet tool install --global dotnet-ef --version 10.0.4
dotnet ef database update --project src/LovePlus.Infrastructure --startup-project src/LovePlus.Infrastructure
dotnet run --project src/LovePlus.Api
```

The migration design-time factory reads `ConnectionStrings__Postgres`; it contains no fallback password. Check `/health`, `/health/live`, `/health/ready`, `/openapi/v1.json` and `/scalar/v1` in Development.

The API also applies pending migrations on startup (`Database__MigrateOnStartup`, default true).
Set it to `false` when you want to run `dotnet ef` by hand.

### Presence configuration

`Realtime__ClientStatusHeartbeatSeconds` (default 60) describes how often the Android
foreground service posts telemetry. `Realtime__OnlineSeconds` (default three times the cadence)
is the window in which a partner is reported online. Startup fails when the window is under
twice the cadence, because that reports a connected partner as offline after a single missed
packet. Changing the Android cadence in `LoveStatusService.HEARTBEAT_INTERVAL_SECONDS` means
changing this setting too.

## Mobile and Android

```sh
cd mobile
npm ci
npm run typecheck
npm run lint
npm start
```

In another terminal:

```sh
JAVA_HOME=$(/usr/libexec/java_home -v 17)
export JAVA_HOME
export ANDROID_HOME="$HOME/Library/Android/sdk"
npm run android
```

Android emulator uses `http://10.0.2.2:5080` in debug. For a physical device, set `LOVEPLUS_API_BASE_URL` to a reachable development HTTPS endpoint before building. Never enable release cleartext traffic.

### Physical-device USB demo without Docker

The v0.4.0 USB demo APK targets `http://127.0.0.1:5080`. Connect one or more Android devices with USB debugging enabled (a physical device and an emulator can run together), then run. The script rebuilds the ARM64 debug APK with the USB-loopback API URL when needed, installs it on every connected target, refreshes both reverse tunnels and opens Love+:

```sh
./scripts/start-usb-demo.sh
```

The script asks for the private demo password without saving it, starts Metro when needed, creates `adb reverse` tunnels for ports 5080 and 8081 on every connected device, and runs the API with Development-only in-memory persistence. It seeds only the two accounts and their pair. Battery, charging, location, activity, mood and presence still enter through the Android status pipeline; no status values are seeded or simulated. Keep the terminal open during the demo. Data and sessions reset when the API stops.

The in-memory mode is rejected outside `Development`. Normal local and production paths continue to require PostgreSQL/PostGIS and Redis.

## Tests

```sh
dotnet test LovePlus.slnx
cd mobile
npm run typecheck
npm run lint
npm test -- --runInBand
./android/gradlew -p android testDebugUnitTest :app:assembleDebug
```

Integration tests replace PostgreSQL with an in-memory provider and do not require Docker. Real adapter smoke testing still requires the Compose services.
