import React from 'react';
import {StyleSheet, Text} from 'react-native';
import {useMutation} from '@tanstack/react-query';
import {PrimaryButton} from '../../shared/ui/FormControls';
import {Screen} from '../../shared/ui/Screen';
import {colors} from '../../shared/ui/theme';
import {pairingApi} from './pairingApi';

export function CreatePairingCodeScreen() {
  const create = useMutation({mutationFn: pairingApi.createCode});
  return (
    <Screen contentStyle={styles.container}>
      <Text style={styles.title}>Eşleştirme kodun</Text>
      <Text style={styles.body}>Yeni kod üretildiğinde önceki kullanılmamış kod geçersizleşir.</Text>
      {create.data ? <Text selectable style={styles.code}>{create.data.code}</Text> : null}
      {create.data ? <Text style={styles.expiry}>{new Date(create.data.expiresAtUtc).toLocaleTimeString()} saatine kadar geçerli</Text> : null}
      {create.error ? <Text style={styles.error}>{create.error.message}</Text> : null}
      <PrimaryButton loading={create.isPending} onPress={() => create.mutate()}>{create.data ? 'Yeni kod oluştur' : 'Kod oluştur'}</PrimaryButton>
    </Screen>
  );
}

const styles = StyleSheet.create({
  container: {justifyContent: 'center'}, title: {fontSize: 28, color: colors.text, fontWeight: '800'}, body: {color: colors.muted, lineHeight: 22, marginTop: 10, marginBottom: 28},
  code: {fontSize: 38, letterSpacing: 4, color: colors.primary, fontWeight: '900', textAlign: 'center'}, expiry: {color: colors.muted, textAlign: 'center', marginVertical: 16}, error: {color: colors.danger, marginBottom: 8},
});
