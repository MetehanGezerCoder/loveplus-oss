#!/usr/bin/env bash
set -euo pipefail

project_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
android_debug_bridge="${ADB_BIN:-$(command -v adb || true)}"

if [[ -z "$android_debug_bridge" ]]; then
  echo "adb bulunamadı. Android platform-tools PATH içinde olmalı." >&2
  exit 1
fi

device_serials=()
while IFS= read -r serial; do
  [[ -n "$serial" ]] && device_serials+=("$serial")
done < <($android_debug_bridge devices | awk 'NR > 1 && $2 == "device" { print $1 }')
if [[ "${#device_serials[@]}" -eq 0 ]]; then
  echo "USB debugging açık bir Android cihaz bağla veya bir emülatör başlat." >&2
  exit 1
fi

dotnet_bin="${DOTNET_BIN:-}"
if [[ -z "$dotnet_bin" ]]; then
  dotnet_candidates=(
    "$(command -v dotnet || true)"
    "/usr/local/share/dotnet/dotnet"
    "/usr/local/share/dotnet/x64/dotnet"
    "$HOME/.dotnet/dotnet"
  )
  dotnet_candidates+=(/private/tmp/loveplus-dotnet-10.*/dotnet)
  for candidate in "${dotnet_candidates[@]}"; do
    [[ -x "$candidate" ]] || continue
    candidate_version="$($candidate --version 2>/dev/null || true)"
    if [[ "${candidate_version%%.*}" =~ ^[0-9]+$ ]] && (( ${candidate_version%%.*} >= 10 )); then
      dotnet_bin="$candidate"
      break
    fi
  done
fi

if [[ -z "$dotnet_bin" || ! -x "$dotnet_bin" ]]; then
  echo ".NET 10 SDK bulunamadı. DOTNET_BIN ile dotnet 10 yolunu ver." >&2
  exit 1
fi

demo_password="${LOVEPLUS_DEMO_PASSWORD:-}"
if [[ -z "$demo_password" ]]; then
  read -r -s -p "Love+ demo parolası: " demo_password
  echo
fi
if (( ${#demo_password} < 10 )); then
  echo "Demo parolası en az 10 karakter olmalı." >&2
  exit 1
fi

if ! lsof -nP -iTCP:8081 -sTCP:LISTEN >/dev/null 2>&1; then
  if ! command -v npm >/dev/null 2>&1; then
    echo "npm bulunamadı; Metro başlatılamıyor." >&2
    exit 1
  fi
  (
    cd "$project_dir/mobile"
    nohup npm start > /tmp/loveplus-metro.log 2>&1 &
  )
  echo "Metro başlatıldı: /tmp/loveplus-metro.log"
fi

installed_api_url=""
android_build_config="$project_dir/mobile/android/app/build/generated/source/buildConfig/debug/com/loveplus/BuildConfig.java"
if [[ -f "$android_build_config" ]]; then
  installed_api_url="$(sed -n 's/.*API_BASE_URL = "\([^"]*\)";.*/\1/p' "$android_build_config" | head -n 1)"
fi
if [[ "$installed_api_url" != "http://127.0.0.1:5080" ]]; then
  echo "USB demo APK hazırlanıyor (API: http://127.0.0.1:5080)..."
  (
    cd "$project_dir/mobile/android"
    env LOVEPLUS_API_BASE_URL=http://127.0.0.1:5080 ./gradlew assembleDebug -PreactNativeArchitectures=arm64-v8a
  )
fi

usb_demo_apk="$project_dir/mobile/android/app/build/outputs/apk/debug/app-debug.apk"
if [[ ! -f "$usb_demo_apk" ]]; then
  echo "USB demo APK oluşturulamadı: $usb_demo_apk" >&2
  exit 1
fi

for serial in "${device_serials[@]}"; do
  $android_debug_bridge -s "$serial" reverse tcp:5080 tcp:5080
  $android_debug_bridge -s "$serial" reverse tcp:8081 tcp:8081
  $android_debug_bridge -s "$serial" install -r "$usb_demo_apk" >/dev/null
  $android_debug_bridge -s "$serial" shell am force-stop com.loveplus
  $android_debug_bridge -s "$serial" shell monkey -p com.loveplus -c android.intent.category.LAUNCHER 1 >/dev/null 2>&1
  echo "ADB tüneli hazır: $serial"
done

jwt_signing_key="$(openssl rand -base64 64)"
echo "Love+ demo hazır (${#device_serials[@]} cihaz). Bu terminali açık tut; API: http://127.0.0.1:5080"
echo "Telemetri gerçek Android cihazdan gelir; RAM altyapısı uygulama durumunu uydurmaz."

cd "$project_dir"
exec env \
  ASPNETCORE_ENVIRONMENT=Development \
  ASPNETCORE_URLS=http://127.0.0.1:5080 \
  Demo__SeedEnabled=true \
  Demo__UseInMemoryInfrastructure=true \
  Demo__Password="$demo_password" \
  Jwt__SigningKey="$jwt_signing_key" \
  "$dotnet_bin" run --project src/LovePlus.Api/LovePlus.Api.csproj --no-launch-profile
