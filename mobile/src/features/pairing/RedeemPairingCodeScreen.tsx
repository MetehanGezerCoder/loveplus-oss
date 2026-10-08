import React, {useState} from 'react';
import {StyleSheet, Text} from 'react-native';
import {useMutation, useQueryClient} from '@tanstack/react-query';
import {Field, PrimaryButton} from '../../shared/ui/FormControls';
import {Screen} from '../../shared/ui/Screen';
import {colors} from '../../shared/ui/theme';
import {currentPairQueryKey} from './useCurrentPair';
import {pairingApi} from './pairingApi';

export function RedeemPairingCodeScreen() {
  const [code, setCode] = useState('');
  const queryClient = useQueryClient();
  const redeem = useMutation({
    mutationFn: pairingApi.redeem,
    onSuccess: async pair => {
      queryClient.setQueryData(currentPairQueryKey, pair);
      await queryClient.invalidateQueries({queryKey: currentPairQueryKey});
    },
  });
  return (
    <Screen contentStyle={styles.container}>
      <Text style={styles.title}>Partnerinin kodunu gir</Text>
      <Text style={styles.body}>Eşleşmeden sonra canlı durum verileri yalnızca bu çift ilişkisi içinde paylaşılır.</Text>
      <Field value={code} onChangeText={value => setCode(value.toUpperCase())} placeholder="ABCD-1234" autoCapitalize="characters" maxLength={9} />
      {redeem.error ? <Text style={styles.error}>{redeem.error.message}</Text> : null}
      <PrimaryButton loading={redeem.isPending} disabled={code.replace('-', '').length !== 8} onPress={() => redeem.mutate(code)}>Eşleş</PrimaryButton>
    </Screen>
  );
}

const styles = StyleSheet.create({
  container: {justifyContent: 'center'}, title: {fontSize: 28, color: colors.text, fontWeight: '800'}, body: {color: colors.muted, lineHeight: 22, marginTop: 10, marginBottom: 24}, error: {color: colors.danger, marginBottom: 8},
});
