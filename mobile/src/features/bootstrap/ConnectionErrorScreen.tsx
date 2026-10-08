import React, {useCallback, useEffect, useState} from 'react';
import {Pressable, StyleSheet, Text, View} from 'react-native';
import {PrimaryButton} from '../../shared/ui/FormControls';
import {Screen} from '../../shared/ui/Screen';
import {colors} from '../../shared/ui/theme';
import {loveStatusNative, type NativeDiagnostics} from '../../shared/native/LoveStatusNative';
import {probeHealth, type HealthProbe} from '../diagnostics';
import {useAuthStore} from '../auth';

type Props = {
  message?: string;
  onRetry: () => void;
  retrying?: boolean;
};

/**
 * Shown when the signed-in app cannot reach its API. Previously this state fell through to the
 * pairing screen, which told the couple they had no partner when the real problem was a wrong
 * build environment or an unreachable backend.
 */
export function ConnectionErrorScreen({message, onRetry, retrying = false}: Props) {
  const logout = useAuthStore(state => state.logout);
  const [health, setHealth] = useState<HealthProbe | null>(null);
  const [native, setNative] = useState<NativeDiagnostics | null>(null);
  const [probing, setProbing] = useState(true);

  const probe = useCallback(async () => {
    setProbing(true);
    const [healthResult, nativeResult] = await Promise.all([
      probeHealth(),
      loveStatusNative.diagnostics().catch(() => null),
    ]);
    setHealth(healthResult);
    setNative(nativeResult);
    setProbing(false);
  }, []);

  useEffect(() => {
    probe().catch(() => undefined);
  }, [probe]);

  const localAddress =
    native?.apiBaseUrl.includes('127.0.0.1') ||
    native?.apiBaseUrl.includes('10.0.2.2') ||
    native?.apiBaseUrl.includes('localhost');

  return (
    <Screen contentStyle={styles.content}>
      <Text style={styles.icon}>📡</Text>
      <Text style={styles.title}>Sunucuya ulaşılamıyor</Text>
      <Text style={styles.body}>
        {message ?? 'Love+ sunucusuna bağlanamadı. Partner durumu, mesafe ve kalp atışı bu yüzden görünmüyor.'}
      </Text>

      <View style={styles.card}>
        <Row label="API adresi" value={native?.apiBaseUrl ?? '—'} />
        <Row
          label="Sağlık kontrolü"
          value={
            probing
              ? 'Deneniyor…'
              : health?.reachable
                ? `${health.status} · ${health.latencyMs} ms`
                : health?.failure === 'timeout'
                  ? 'Zaman aşımı'
                  : 'Ulaşılamıyor'
          }
          tone={health?.reachable && health.status === 'healthy' ? 'ok' : 'bad'}
        />
        <Row label="Uygulama sürümü" value={native ? `${native.versionName} (${native.versionCode})` : '—'} />
      </View>

      {localAddress ? (
        <Text style={styles.hint}>
          Bu yapı yerel geliştirme adresine ({native?.apiBaseUrl}) derlenmiş. Uzaktan kullanım için
          https adresine derlenmiş bir sürüm gerekir.
        </Text>
      ) : (
        <Text style={styles.hint}>
          İnternet bağlantını kontrol et. Bağlantı varsa sunucu geçici olarak kapalı olabilir.
        </Text>
      )}

      <PrimaryButton
        loading={retrying || probing}
        onPress={() => {
          probe().catch(() => undefined);
          onRetry();
        }}>
        Yeniden dene
      </PrimaryButton>
      <Pressable
        onPress={() => {
          logout().catch(() => undefined);
        }}>
        <Text style={styles.secondary}>Çıkış yap ve baştan giriş dene</Text>
      </Pressable>
    </Screen>
  );
}

function Row({label, value, tone}: {label: string; value: string; tone?: 'ok' | 'bad'}) {
  return (
    <View style={styles.row}>
      <Text style={styles.rowLabel}>{label}</Text>
      <Text
        style={[
          styles.rowValue,
          tone === 'ok' && styles.ok,
          tone === 'bad' && styles.bad,
        ]}
        numberOfLines={2}>
        {value}
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  content: {justifyContent: 'center'},
  icon: {fontSize: 46, textAlign: 'center'},
  title: {fontSize: 26, color: colors.text, fontWeight: '900', textAlign: 'center', marginTop: 12},
  body: {color: colors.muted, fontSize: 15, lineHeight: 22, textAlign: 'center', marginTop: 10},
  card: {
    backgroundColor: colors.surface,
    borderRadius: 18,
    borderWidth: 1,
    borderColor: colors.border,
    padding: 16,
    marginTop: 20,
  },
  row: {flexDirection: 'row', justifyContent: 'space-between', gap: 12, paddingVertical: 6},
  rowLabel: {color: colors.text, fontSize: 13, flexShrink: 1},
  rowValue: {color: colors.muted, fontSize: 13, fontWeight: '700', textAlign: 'right', flexShrink: 1},
  ok: {color: colors.success},
  bad: {color: colors.danger},
  hint: {color: colors.primarySoft, fontSize: 13, lineHeight: 20, marginTop: 16},
  secondary: {color: colors.muted, textAlign: 'center', marginTop: 18, fontWeight: '700'},
});
