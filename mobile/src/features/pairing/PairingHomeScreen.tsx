import React from 'react';
import {Pressable, StyleSheet, Text, View} from 'react-native';
import type {NativeStackScreenProps} from '@react-navigation/native-stack';
import {Screen} from '../../shared/ui/Screen';
import {colors} from '../../shared/ui/theme';

type Routes = {PairingHome: undefined; CreatePairingCode: undefined; RedeemPairingCode: undefined; Settings: undefined};

export function PairingHomeScreen({navigation}: NativeStackScreenProps<Routes, 'PairingHome'>) {
  return (
    <Screen contentStyle={styles.container}>
      <Text style={styles.brand}>Love+</Text>
      <Text style={styles.title}>İki kişilik alanını bağla</Text>
      <Text style={styles.body}>Kod 10 dakika geçerlidir, yalnızca bir kez kullanılır ve yalnızca iki hesabı birbirine bağlar.</Text>
      <View style={styles.cards}>
        <Pressable style={styles.card} onPress={() => navigation.navigate('CreatePairingCode')}>
          <Text style={styles.cardTitle}>Kod oluştur</Text><Text style={styles.cardBody}>Partnerine süreli bir kod gönder.</Text>
        </Pressable>
        <Pressable style={styles.card} onPress={() => navigation.navigate('RedeemPairingCode')}>
          <Text style={styles.cardTitle}>Kod gir</Text><Text style={styles.cardBody}>Partnerinin paylaştığı kodu kullan.</Text>
        </Pressable>
      </View>
      <Pressable onPress={() => navigation.navigate('Settings')}><Text style={styles.settings}>Ayarlar ve güvenli çıkış</Text></Pressable>
    </Screen>
  );
}

const styles = StyleSheet.create({
  container: {justifyContent: 'center'}, brand: {fontSize: 40, color: colors.primary, fontWeight: '900'},
  title: {fontSize: 28, color: colors.text, fontWeight: '800', marginTop: 20}, body: {fontSize: 16, lineHeight: 23, color: colors.muted, marginTop: 10},
  cards: {gap: 14, marginTop: 28}, card: {backgroundColor: colors.surface, borderRadius: 20, borderWidth: 1, borderColor: colors.border, padding: 20},
  cardTitle: {fontSize: 20, color: colors.text, fontWeight: '800'}, cardBody: {color: colors.muted, marginTop: 6}, settings: {color: colors.primarySoft, textAlign: 'center', marginTop: 26},
});
