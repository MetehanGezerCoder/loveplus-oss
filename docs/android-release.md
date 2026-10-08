# Android release build and signing

The artifact that reaches a second phone is a **release** APK: it embeds the JavaScript bundle,
so it starts without Metro, a Mac, USB debugging or ADB reverse tunnels, and it refuses
cleartext HTTP.

## Signing material

Love+ keeps signing material out of the repository. The Gradle build reads it from either
`mobile/android/keystore.properties` (git-ignored) or `LOVEPLUS_RELEASE_*` environment
variables. Nothing is defaulted: without signing material the release build still runs and
produces an **unsigned** APK with a warning, rather than silently signing with a key committed
to source.

Create a keystore once, outside the repository, and keep it in your password manager:

```sh
keytool -genkeypair -v \
  -keystore ~/loveplus-release.keystore \
  -alias loveplus \
  -keyalg RSA -keysize 4096 -validity 10000
```

`keytool` prompts for the store and key passwords. Choose them yourself and never type them
into a file that is committed, an APK, a log or Slack.

Then create `mobile/android/keystore.properties`:

```properties
storeFile=/Users/you/loveplus-release.keystore
storePassword=<the store password you chose>
keyAlias=loveplus
keyPassword=<the key password you chose>
```

Or export the equivalent environment variables:

```sh
export LOVEPLUS_RELEASE_STORE_FILE=$HOME/loveplus-release.keystore
export LOVEPLUS_RELEASE_STORE_PASSWORD=...
export LOVEPLUS_RELEASE_KEY_ALIAS=loveplus
export LOVEPLUS_RELEASE_KEY_PASSWORD=...
```

**Losing this keystore means Meliha's phone can never install an update in place** — Android
rejects an APK signed by a different key for the same package. Back it up.

## Build

```sh
cd mobile
npm ci
LOVEPLUS_API_BASE_URL=https://loveplus.example.com \
LOVEPLUS_GOOGLE_MAPS_API_KEY=<optional> \
  ./android/gradlew -p android :app:assembleRelease -PreactNativeArchitectures=arm64-v8a
```

Output: `mobile/android/app/build/outputs/apk/release/app-release.apk`.

The build fails at configuration time when `LOVEPLUS_API_BASE_URL` is not `https://`, or when
it points at `127.0.0.1`, `10.0.2.2` or `localhost`. Those addresses belong to the local USB
and emulator workflow and must never leave this machine inside an APK.

## Verify before sending

```sh
APK=mobile/android/app/build/outputs/apk/release/app-release.apk
shasum -a 256 "$APK"

# Embedded API URL and version
"$ANDROID_HOME"/build-tools/35.0.0/aapt2 dump badging "$APK" | grep -E "package|versionName"
unzip -p "$APK" classes.dex | strings | grep -m1 "https://loveplus"

# The JavaScript bundle must be inside the APK
unzip -l "$APK" | grep assets/index.android.bundle

# Signature
"$ANDROID_HOME"/build-tools/35.0.0/apksigner verify --print-certs "$APK"

# No secret may be present
unzip -p "$APK" assets/index.android.bundle | grep -ciE "BEGIN PRIVATE KEY|service_account" || echo "clean"
```

## Standalone debug artifact (before a keystore exists)

If you need a self-contained APK before creating a release keystore, a debug build can embed
the bundle too:

```sh
LOVEPLUS_BUNDLE_IN_DEBUG=true \
LOVEPLUS_API_BASE_URL=https://loveplus.example.com \
  ./android/gradlew -p android :app:assembleDebug -PreactNativeArchitectures=arm64-v8a
```

Be honest about what this is. A debug build is signed with the machine's throwaway debug key
and its manifest permits cleartext traffic. It is a bridge for testing, not the artifact to
hand over. Prefer the release build.

## Installing on a second phone

Send the APK by any file transfer. On the receiving phone, allow "install unknown apps" for the
app doing the transfer, then open the file. Android will warn about an app from an unknown
source; that warning is expected for a privately distributed APK.

An in-place update requires the same signing key and a higher `versionCode`
(`mobile/android/app/build.gradle`).
