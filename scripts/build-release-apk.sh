#!/usr/bin/env bash
# Builds the standalone Love+ release APK and verifies the properties that decide whether it is
# safe to hand to a second phone: an HTTPS API URL, an embedded JavaScript bundle, a real
# signature, and no secret inside the artifact.
#
#   LOVEPLUS_API_BASE_URL=https://loveplus.example.com ./scripts/build-release-apk.sh
#
# Signing material is read by Gradle from mobile/android/keystore.properties or the
# LOVEPLUS_RELEASE_* environment variables. This script never creates or stores a password.
set -euo pipefail

project_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
api_base_url="${LOVEPLUS_API_BASE_URL:-}"
architectures="${LOVEPLUS_ABIS:-arm64-v8a}"

if [[ -z "$api_base_url" ]]; then
  echo "LOVEPLUS_API_BASE_URL gerekli. Örnek: https://loveplus.example.com" >&2
  exit 1
fi
if [[ "$api_base_url" != https://* ]]; then
  echo "LOVEPLUS_API_BASE_URL https:// ile başlamalı (verilen: $api_base_url)." >&2
  exit 1
fi
case "$api_base_url" in
  *127.0.0.1*|*10.0.2.2*|*localhost*)
    echo "$api_base_url yerel geliştirme adresi; release APK'ya konamaz." >&2
    exit 1
    ;;
esac

export JAVA_HOME="${JAVA_HOME:-$(/usr/libexec/java_home -v 17)}"
export ANDROID_HOME="${ANDROID_HOME:-$HOME/Library/Android/sdk}"
export ANDROID_SDK_ROOT="$ANDROID_HOME"

build_tools_dir="$(find "$ANDROID_HOME/build-tools" -maxdepth 1 -mindepth 1 -type d | sort -V | tail -n 1)"
if [[ -z "$build_tools_dir" ]]; then
  echo "Android build-tools bulunamadı: $ANDROID_HOME/build-tools" >&2
  exit 1
fi

echo "==> Release APK derleniyor (API: $api_base_url, ABI: $architectures)"
(
  cd "$project_dir/mobile/android"
  env LOVEPLUS_API_BASE_URL="$api_base_url" \
    ./gradlew :app:assembleRelease -PreactNativeArchitectures="$architectures" --console=plain
)

# Gradle names the artifact app-release.apk when a signing config exists and
# app-release-unsigned.apk when it does not; both are reported, only the signed one passes.
release_dir="$project_dir/mobile/android/app/build/outputs/apk/release"
apk=""
for candidate in "$release_dir/app-release.apk" "$release_dir/app-release-unsigned.apk"; do
  [[ -f "$candidate" ]] && apk="$candidate" && break
done
if [[ -z "$apk" ]]; then
  echo "Release APK üretilemedi: $release_dir" >&2
  exit 1
fi

echo
echo "==> Doğrulama"
failures=0

# Piping straight into `grep -q` risks a false failure here: grep can close the pipe as soon
# as it finds a match, the upstream writer then gets SIGPIPE, and `pipefail` turns that into a
# non-zero pipeline status even though grep actually matched. Capture output first, then grep
# the captured text so no live pipe can be closed early.
apk_listing="$(unzip -l "$apk")"
if grep -q "assets/index.android.bundle" <<<"$apk_listing"; then
  echo "  [ok]   JavaScript bundle APK içinde (Metro gerekmiyor)"
else
  echo "  [FAIL] assets/index.android.bundle yok; APK Metro'ya bağımlı" >&2
  failures=$((failures + 1))
fi

# BuildConfig.API_BASE_URL is compiled into the dex, not into resources.
dex_strings="$(unzip -p "$apk" classes.dex 2>/dev/null | strings)"
if grep -qF "$api_base_url" <<<"$dex_strings"; then
  echo "  [ok]   API adresi gömülü: $api_base_url"
else
  echo "  [FAIL] API adresi APK içinde bulunamadı: $api_base_url" >&2
  failures=$((failures + 1))
fi

bundle_contents="$(unzip -p "$apk" assets/index.android.bundle 2>/dev/null)"
# No "does the bundle contain 127.0.0.1/10.0.2.2" scan here on purpose: the real enforcement is
# the Gradle-time guard in android/app/build.gradle, which refuses a release build whose
# LOVEPLUS_API_BASE_URL is local BEFORE Metro even runs. ConnectionErrorScreen.tsx legitimately
# carries both substrings permanently (it detects a misconfigured build at runtime and warns the
# user), so a bundle-content grep for them would false-positive on every correct release build.

if "$build_tools_dir/apksigner" verify --print-certs "$apk" >/tmp/loveplus-apksigner.txt 2>&1; then
  echo "  [ok]   İmza doğrulandı"
  grep -m1 "Signer #1 certificate DN" /tmp/loveplus-apksigner.txt | sed 's/^/         /' || true
else
  echo "  [FAIL] APK imzasız veya imza doğrulanamadı. docs/android-release.md'ye bak." >&2
  failures=$((failures + 1))
fi
rm -f /tmp/loveplus-apksigner.txt

if grep -qiE "BEGIN (RSA )?PRIVATE KEY|\"type\": ?\"service_account\"" <<<"$bundle_contents"; then
  echo "  [FAIL] Bundle içinde özel anahtar/servis hesabı izi bulundu" >&2
  failures=$((failures + 1))
else
  echo "  [ok]   Bundle içinde özel anahtar/servis hesabı izi yok"
fi

badging_output="$("$build_tools_dir/aapt2" dump badging "$apk" 2>/dev/null)"
version_line="$(grep -m1 "^package:" <<<"$badging_output")"
echo "  [info] $version_line"
echo "  [info] SHA-256: $(shasum -a 256 "$apk" | awk '{print $1}')"
echo "  [info] Dosya:   $apk"

if (( failures > 0 )); then
  echo
  echo "$failures doğrulama başarısız. Bu APK dağıtılmamalı." >&2
  exit 1
fi

echo
echo "Release APK doğrulandı."
