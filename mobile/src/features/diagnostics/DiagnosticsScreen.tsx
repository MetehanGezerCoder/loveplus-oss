import React from 'react';
import {Linking, Pressable, StyleSheet, Text, View} from 'react-native';
import {PrimaryButton} from '../../shared/ui/FormControls';
import {Screen} from '../../shared/ui/Screen';
import {colors} from '../../shared/ui/theme';
import {useDiagnostics} from './useDiagnostics';

type Tone = 'ok' | 'warn' | 'bad' | 'muted';

export function DiagnosticsScreen() {
  const report = useDiagnostics();
  const {health, server, native, permissions, connection} = report;

  return (
    <Screen>
      <Text style={styles.title}>Tanılama</Text>
      <Text style={styles.body}>
        Bir şey çalışmadığında nedenini burada görürsün. Bu ekran token, koordinat veya partner
        verisi göstermez.
      </Text>

      <Section title="Sunucu bağlantısı">
        <Row
          label="API erişimi"
          value={
            health
              ? health.reachable
                ? `${health.status} · ${health.latencyMs} ms`
                : health.failure === 'timeout'
                  ? 'Zaman aşımı'
                  : 'Ulaşılamıyor'
              : '—'
          }
          tone={health ? (health.reachable && health.status === 'healthy' ? 'ok' : 'bad') : 'muted'}
        />
        <Row label="API adresi" value={native?.apiBaseUrl ?? '—'} tone="muted" />
        {health?.checks.map(check => (
          <Row
            key={check.name}
            label={`  ${check.name}`}
            value={check.status}
            tone={check.status === 'healthy' ? 'ok' : 'bad'}
          />
        ))}
        <Row
          label="Canlı bağlantı (SignalR)"
          value={
            connection.state === 'connected'
              ? 'Bağlı'
              : connection.state === 'connecting'
                ? 'Bağlanıyor'
                : 'Kopuk'
          }
          tone={connection.state === 'connected' ? 'ok' : connection.state === 'connecting' ? 'warn' : 'bad'}
        />
        <Row label="Son bağlanma" value={relative(connection.lastConnectedAt)} tone="muted" />
        {connection.state !== 'connected' && connection.lastFailureReason ? (
          <Row label="Son hata" value={connection.lastFailureReason} tone="muted" />
        ) : null}
      </Section>

      <Section title="Sunucu yapılandırması">
        {report.serverError ? (
          <Text style={styles.note}>{report.serverError}</Text>
        ) : (
          <>
            <Row label="Ortam" value={server?.environment ?? '—'} tone="muted" />
            <Row
              label="PostgreSQL/PostGIS"
              value={server ? (server.usesRelationalDatabase ? 'Kullanımda' : 'Bellek içi (demo)') : '—'}
              tone={server ? (server.usesRelationalDatabase ? 'ok' : 'warn') : 'muted'}
            />
            <Row
              label="Redis"
              value={server ? (server.usesRedis ? 'Kullanımda' : 'Bellek içi (demo)') : '—'}
              tone={server ? (server.usesRedis ? 'ok' : 'warn') : 'muted'}
            />
            <Row
              label="Migration"
              value={server ? (server.migrationsApplied ? 'Uygulandı' : 'İlişkisel değil') : '—'}
              tone={server?.migrationsApplied ? 'ok' : 'muted'}
            />
            <Row
              label="HTTPS zorunlu"
              value={server ? (server.requiresHttps ? 'Evet' : 'Hayır') : '—'}
              tone={server ? (server.requiresHttps ? 'ok' : 'warn') : 'muted'}
            />
            <Row
              label="Bildirim sağlayıcı"
              value={
                server
                  ? server.pushConfigured
                    ? server.pushProvider
                    : `${server.pushProvider} · yapılandırılmadı`
                  : '—'
              }
              tone={server ? (server.pushConfigured ? 'ok' : 'warn') : 'muted'}
            />
            <Row
              label="Çevrimiçi penceresi"
              value={server ? `${server.presenceOnlineSeconds} sn (beat ${server.clientStatusHeartbeatSeconds} sn)` : '—'}
              tone="muted"
            />
          </>
        )}
      </Section>

      <Section title="Bu cihaz">
        {report.nativeError ? (
          <Text style={styles.note}>{report.nativeError}</Text>
        ) : (
          <>
            <Row
              label="Sürüm"
              value={native ? `${native.versionName} (${native.versionCode}) · ${native.buildType}` : '—'}
              tone="muted"
            />
            <Row
              label="Telemetri servisi"
              value={native ? (native.telemetryEnabled ? 'Açık' : 'Kapalı') : '—'}
              tone={native?.telemetryEnabled ? 'ok' : 'warn'}
            />
            <Row
              label="Son başarılı gönderim"
              value={relative(native?.lastUploadAtMs ?? null)}
              tone={uploadTone(native?.lastUploadAtMs, native?.heartbeatIntervalSeconds)}
            />
            <Row label="Son gönderim sonucu" value={native?.lastUploadResult ?? '—'} tone="muted" />
            <Row
              label="Bekleyen kuyruk"
              value={native ? `${native.queuedStatusCount} kayıt` : '—'}
              tone={native && native.queuedStatusCount > 0 ? 'warn' : 'muted'}
            />
            <Row
              label="Harita anahtarı"
              value={native ? (native.mapsEnabled ? 'Var' : 'Yok · harita boş görünür') : '—'}
              tone={native?.mapsEnabled ? 'ok' : 'muted'}
            />
          </>
        )}
      </Section>

      <Section title="İzinler">
        <Row label="Konum (hassas)" value={yesNo(permissions?.fineLocation)} tone={permissionTone(permissions?.fineLocation)} />
        <Row
          label="Arka planda konum"
          value={yesNo(permissions?.backgroundLocation)}
          tone={permissionTone(permissions?.backgroundLocation)}
        />
        <Row
          label="Aktivite tanıma"
          value={yesNo(permissions?.activityRecognition)}
          tone={permissionTone(permissions?.activityRecognition)}
        />
        <Row label="Bildirimler" value={yesNo(permissions?.notifications)} tone={permissionTone(permissions?.notifications)} />
        <Pressable onPress={() => Linking.openSettings().catch(() => undefined)}>
          <Text style={styles.link}>Sistem ayarlarını aç ›</Text>
        </Pressable>
      </Section>

      <PrimaryButton
        loading={report.loading}
        onPress={() => {
          report.refresh().catch(() => undefined);
        }}>
        Yeniden denetle
      </PrimaryButton>
    </Screen>
  );
}

function Section({title, children}: React.PropsWithChildren<{title: string}>) {
  return (
    <View style={styles.section}>
      <Text style={styles.sectionTitle}>{title}</Text>
      {children}
    </View>
  );
}

function Row({label, value, tone = 'muted'}: {label: string; value: string; tone?: Tone}) {
  return (
    <View style={styles.row}>
      <Text style={styles.rowLabel}>{label}</Text>
      <Text style={[styles.rowValue, toneStyles[tone]]} numberOfLines={2}>
        {value}
      </Text>
    </View>
  );
}

function yesNo(value?: boolean) {
  return value === undefined ? '—' : value ? 'Verildi' : 'Verilmedi';
}

function permissionTone(value?: boolean): Tone {
  return value === undefined ? 'muted' : value ? 'ok' : 'warn';
}

/** Late telemetry is the signal that a partner is about to look offline, so it is flagged early. */
function uploadTone(lastUploadAtMs?: number, heartbeatSeconds?: number): Tone {
  if (!lastUploadAtMs) return 'warn';
  const budgetMs = (heartbeatSeconds ?? 60) * 3 * 1000;
  return Date.now() - lastUploadAtMs <= budgetMs ? 'ok' : 'bad';
}

function relative(timestamp: number | null | undefined) {
  if (!timestamp) return 'Henüz yok';
  const seconds = Math.max(0, Math.round((Date.now() - timestamp) / 1000));
  if (seconds < 60) return `${seconds} sn önce`;
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes} dk önce`;
  return `${Math.floor(minutes / 60)} sa önce`;
}

const toneStyles = StyleSheet.create({
  ok: {color: colors.success},
  warn: {color: colors.primarySoft},
  bad: {color: colors.danger},
  muted: {color: colors.muted},
});

const styles = StyleSheet.create({
  title: {fontSize: 28, color: colors.text, fontWeight: '900', marginTop: 8},
  body: {color: colors.muted, fontSize: 14, lineHeight: 21, marginTop: 8, marginBottom: 8},
  section: {
    backgroundColor: colors.surface,
    borderRadius: 18,
    borderWidth: 1,
    borderColor: colors.border,
    padding: 16,
    marginTop: 14,
  },
  sectionTitle: {color: colors.primarySoft, fontWeight: '900', letterSpacing: 0.6, marginBottom: 8},
  row: {flexDirection: 'row', justifyContent: 'space-between', alignItems: 'flex-start', paddingVertical: 6, gap: 12},
  rowLabel: {color: colors.text, fontSize: 13, flexShrink: 1},
  rowValue: {fontSize: 13, fontWeight: '700', textAlign: 'right', flexShrink: 1},
  note: {color: colors.primarySoft, fontSize: 13, lineHeight: 20},
  link: {color: colors.primarySoft, fontWeight: '800', fontSize: 13, marginTop: 10},
});
