import React, {useState} from 'react';
import {Pressable, StyleSheet, Text, View} from 'react-native';
import type {NativeStackScreenProps} from '@react-navigation/native-stack';
import {Field, PrimaryButton} from '../../shared/ui/FormControls';
import {Screen} from '../../shared/ui/Screen';
import {colors} from '../../shared/ui/theme';
import {useAuthStore} from './useAuthStore';

type Props = NativeStackScreenProps<{Login: undefined; Register: undefined}, 'Login'>;

const demoPeople = [
  {name: 'Alex', email: 'alex@example.com', emoji: '🧑🏻'},
  {name: 'Taylor', email: 'taylor@example.com', emoji: '👩🏻'},
] as const;

export function LoginScreen({navigation}: Props) {
  const [email, setEmail] = useState(__DEV__ ? demoPeople[0].email : '');
  const [password, setPassword] = useState('');
  const {login, busy, error, clearError} = useAuthStore();
  return (
    <Screen contentStyle={styles.container}>
      <View style={styles.heart}><Text style={styles.heartText}>♥</Text></View>
      <Text style={styles.brand}>Love+</Text>
      <Text style={styles.title}>Sadece ikiniz için.</Text>
      <Text style={styles.subtitle}>Gerçek cihazlarınızdan gelen pil, konum ve günlük durumunuz; yalnızca partnerinizle paylaşılır.</Text>
      {__DEV__ ? (
        <View style={styles.people}>
          {demoPeople.map(person => (
            <Pressable key={person.email} style={[styles.person, email === person.email && styles.personSelected]} onPress={() => { clearError(); setEmail(person.email); }}>
              <Text style={styles.personEmoji}>{person.emoji}</Text><Text style={styles.personName}>{person.name}</Text>
            </Pressable>
          ))}
        </View>
      ) : null}
      <Field value={email} onChangeText={value => { clearError(); setEmail(value); }} placeholder="E-posta" keyboardType="email-address" autoCapitalize="none" />
      <Field value={password} onChangeText={value => { clearError(); setPassword(value); }} placeholder="Parolanız" secureTextEntry />
      {error ? <Text style={styles.error}>{error}</Text> : null}
      <PrimaryButton loading={busy} disabled={!email || password.length < 10} onPress={() => { login({email, password}).catch(() => undefined); }}>Özel alanımıza gir</PrimaryButton>
      {!__DEV__ ? <Pressable onPress={() => navigation.navigate('Register')}><Text style={styles.link}>Yeni bir Love+ alanı oluştur</Text></Pressable> : null}
      <Text style={styles.privacy}>🔒 Konum paylaşımı sen açmadan başlamaz.</Text>
    </Screen>
  );
}

const styles = StyleSheet.create({
  container: {justifyContent: 'center'}, heart: {width: 58, height: 58, borderRadius: 20, backgroundColor: colors.primary, alignItems: 'center', justifyContent: 'center', transform: [{rotate: '-5deg'}]}, heartText: {fontSize: 34, color: '#fff'}, brand: {fontSize: 42, fontWeight: '900', color: colors.primary, marginTop: 18},
  title: {fontSize: 29, color: colors.text, fontWeight: '900', marginTop: 4}, subtitle: {fontSize: 16, color: colors.muted, marginTop: 8, marginBottom: 20, lineHeight: 23},
  people: {flexDirection: 'row', gap: 12, marginBottom: 14}, person: {flex: 1, flexDirection: 'row', alignItems: 'center', gap: 9, borderRadius: 16, padding: 13, borderWidth: 1, borderColor: colors.border, backgroundColor: colors.surface}, personSelected: {borderColor: colors.primary, backgroundColor: colors.surfaceRaised}, personEmoji: {fontSize: 23}, personName: {fontSize: 16, color: colors.text, fontWeight: '800'},
  link: {color: colors.primarySoft, textAlign: 'center', marginTop: 20, fontWeight: '700'}, error: {color: colors.danger, marginBottom: 8}, privacy: {color: colors.muted, textAlign: 'center', marginTop: 20, fontSize: 12},
});
