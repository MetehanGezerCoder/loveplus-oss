import React, {useEffect, useState} from 'react';
import {ActivityIndicator, StyleSheet, Text, View} from 'react-native';
import {colors} from '../../shared/ui/theme';
import {useAuthStore} from '../auth';
import {loveStatusNative} from '../../shared/native/LoveStatusNative';

/** After this long a spinner stops being informative and starts looking like a hang. */
const SLOW_START_MS = 6_000;

export function BootstrapScreen({label}: {label?: string}) {
  const bootstrap = useAuthStore(state => state.bootstrap);
  const [slow, setSlow] = useState(false);

  useEffect(() => {
    bootstrap();
    if (useAuthStore.getState().session) {
      loveStatusNative.ensureBatteryTelemetry().catch(() => undefined);
    }
  }, [bootstrap]);

  useEffect(() => {
    const timer = setTimeout(() => setSlow(true), SLOW_START_MS);
    return () => clearTimeout(timer);
  }, []);

  return (
    <View style={styles.container}>
      <Text style={styles.brand}>Love+</Text>
      <ActivityIndicator color={colors.primary} size="large" />
      {label ? <Text style={styles.label}>{label}</Text> : null}
      {slow ? (
        <Text style={styles.slow}>Sunucu yanıtı beklenenden uzun sürüyor. Bağlantı denetleniyor…</Text>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: colors.background,
    gap: 24,
    padding: 32,
  },
  brand: {fontSize: 44, fontWeight: '900', color: colors.primary},
  label: {color: colors.muted, fontSize: 14},
  slow: {color: colors.primarySoft, fontSize: 13, textAlign: 'center', lineHeight: 20},
});
