import React, {useState} from 'react';
import {Pressable, StyleSheet, Text} from 'react-native';
import type {NativeStackScreenProps} from '@react-navigation/native-stack';
import {Field, PrimaryButton} from '../../shared/ui/FormControls';
import {Screen} from '../../shared/ui/Screen';
import {colors} from '../../shared/ui/theme';
import {useAuthStore} from './useAuthStore';

type Props = NativeStackScreenProps<{Login: undefined; Register: undefined}, 'Register'>;

export function RegisterScreen({navigation}: Props) {
  const [displayName, setDisplayName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const {register, busy, error, clearError} = useAuthStore();
  return (
    <Screen contentStyle={styles.container}>
      <Text style={styles.title}>Love+ alanını oluştur</Text>
      <Text style={styles.subtitle}>Konum paylaşımı kayıtla başlamaz; ayrıca ve açıkça izin verirsin.</Text>
      <Field value={displayName} onChangeText={value => { clearError(); setDisplayName(value); }} placeholder="Görünen ad" />
      <Field value={email} onChangeText={value => { clearError(); setEmail(value); }} placeholder="E-posta" keyboardType="email-address" autoCapitalize="none" />
      <Field value={password} onChangeText={value => { clearError(); setPassword(value); }} placeholder="En az 10 karakter parola" secureTextEntry />
      {error ? <Text style={styles.error}>{error}</Text> : null}
      <PrimaryButton loading={busy} disabled={!displayName || !email || password.length < 10} onPress={() => { register({displayName, email, password}).catch(() => undefined); }}>Kayıt ol</PrimaryButton>
      <Pressable onPress={() => navigation.goBack()}><Text style={styles.link}>Zaten hesabın var mı? Giriş yap</Text></Pressable>
    </Screen>
  );
}

const styles = StyleSheet.create({
  container: {justifyContent: 'center'},
  title: {fontSize: 28, color: colors.text, fontWeight: '800'},
  subtitle: {fontSize: 16, color: colors.muted, marginTop: 8, marginBottom: 24, lineHeight: 23},
  link: {color: colors.primarySoft, textAlign: 'center', marginTop: 20, fontWeight: '700'},
  error: {color: colors.danger, marginBottom: 8},
});
