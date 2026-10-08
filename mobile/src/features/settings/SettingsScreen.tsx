import React, {useEffect, useState} from 'react';
import {Pressable, StyleSheet, Switch, Text, View} from 'react-native';
import {PrimaryButton} from '../../shared/ui/FormControls';
import {Screen} from '../../shared/ui/Screen';
import {colors} from '../../shared/ui/theme';
import {loveStatusNative} from '../../shared/native/LoveStatusNative';
import {useAuthStore} from '../auth';
import type {HapticIntensity, PhaseThreePreferences} from '../../shared/native/LoveStatusNative';
import type {TelemetryState} from '../../shared/native/LoveStatusNative';
import type {NativeStackScreenProps} from '@react-navigation/native-stack';

const defaults: PhaseThreePreferences = {receiveHeartbeats: true, sendHeartbeats: true, hapticIntensity: 'medium', receiveAnimation: true, showProximity: true, moodSharing: true};

type Routes = {Settings: undefined; Permissions: undefined; Diagnostics: undefined};

export function SettingsScreen({navigation}: NativeStackScreenProps<Routes, 'Settings'>) {
  const {session, logout, busy} = useAuthStore();
  const [preferences, setPreferences] = useState(defaults);
  const [telemetry, setTelemetry] = useState<TelemetryState | null>(null);
  useEffect(() => {
    const refresh = () => {
      loveStatusNative.phaseThreePreferences().then(setPreferences).catch(() => undefined);
      loveStatusNative.state().then(setTelemetry).catch(() => undefined);
    };
    refresh();
    return navigation.addListener('focus', refresh);
  }, [navigation, session?.user.id]);
  const update = async (next: PhaseThreePreferences) => {
    const previous = preferences;
    setPreferences(next);
    try { await loveStatusNative.updatePhaseThreePreferences(next); } catch { setPreferences(previous); }
  };
  const secureLogout = async () => {
    await loveStatusNative.stop(true).catch(() => undefined);
    await logout();
  };
  return (
    <Screen>
      <Text style={styles.title}>Ayarlar</Text>
      <Text style={styles.groupTitle}>Kalp atışı</Text>
      <SettingRow label="Kalp atışı al" value={preferences.receiveHeartbeats} onChange={value => update({...preferences, receiveHeartbeats: value})} />
      <SettingRow label="Kalp atışı gönder" value={preferences.sendHeartbeats} onChange={value => update({...preferences, sendHeartbeats: value})} />
      <SettingRow label="Gelince ekranda canlandır" value={preferences.receiveAnimation} onChange={value => update({...preferences, receiveAnimation: value})} />
      <Text style={styles.settingLabel}>Titreşim şiddeti</Text>
      <View style={styles.intensities}>{(['low', 'medium', 'high'] as HapticIntensity[]).map(intensity => <Pressable key={intensity} style={[styles.intensity, preferences.hapticIntensity === intensity && styles.intensitySelected]} onPress={() => { update({...preferences, hapticIntensity: intensity}).then(() => loveStatusNative.previewHeartbeat()).catch(() => undefined); }}><Text style={styles.intensityText}>{intensity === 'low' ? 'Düşük' : intensity === 'medium' ? 'Orta' : 'Yüksek'}</Text></Pressable>)}</View>
      <Text style={styles.groupTitle}>Gizlilik ve görünüm</Text>
      <Pressable style={styles.settingRow} onPress={() => navigation.navigate('Permissions')}><View><Text style={styles.settingLabel}>Konum paylaşımı</Text><Text style={styles.settingNote}>{telemetry?.locationSharing ? 'Açık' : 'Kapalı'} · İzinleri yönet</Text></View><Text style={styles.chevron}>›</Text></Pressable>
      <SettingRow label="Ruh hâlimi paylaş" value={preferences.moodSharing} onChange={value => update({...preferences, moodSharing: value})} />
      <SettingRow label="Yakınlık kartını göster" value={preferences.showProximity} onChange={value => update({...preferences, showProximity: value})} />
      <Text style={styles.groupTitle}>Bağlantı</Text>
      <Pressable style={styles.settingRow} onPress={() => navigation.navigate('Diagnostics')}><View><Text style={styles.settingLabel}>Tanılama</Text><Text style={styles.settingNote}>API, canlı bağlantı, telemetri ve izin durumu</Text></View><Text style={styles.chevron}>›</Text></Pressable>
      <Text style={styles.label}>Hesap</Text><Text style={styles.value}>{session?.user.displayName}</Text><Text style={styles.value}>{session?.user.email}</Text>
      <Text style={styles.privacy}>Çıkış, bu cihaz oturumunu ve refresh token zincirini sunucuda iptal eder; yerel oturum verisini siler ve takip servisini durdurur.</Text>
      <PrimaryButton loading={busy} onPress={() => { secureLogout().catch(() => undefined); }}>Güvenli çıkış</PrimaryButton>
    </Screen>
  );
}

function SettingRow({label, value, onChange}: {label: string; value: boolean; onChange: (value: boolean) => void}) {
  return <View style={styles.settingRow}><Text style={styles.settingLabel}>{label}</Text><Switch value={value} onValueChange={onChange} trackColor={{true: colors.primary}} /></View>;
}

const styles = StyleSheet.create({
  title: {fontSize: 30, color: colors.text, fontWeight: '800', marginBottom: 22}, groupTitle: {color: colors.primarySoft, fontWeight: '900', letterSpacing: 1, marginTop: 12, marginBottom: 7}, settingRow: {flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', minHeight: 52, borderBottomWidth: 1, borderBottomColor: colors.border}, settingLabel: {color: colors.text, fontSize: 15, fontWeight: '700'}, settingNote: {color: colors.muted, fontSize: 11, marginTop: 3}, chevron: {color: colors.primarySoft, fontSize: 28}, intensities: {flexDirection: 'row', gap: 8, marginVertical: 12}, intensity: {flex: 1, alignItems: 'center', padding: 11, borderRadius: 12, borderWidth: 1, borderColor: colors.border}, intensitySelected: {backgroundColor: colors.primary, borderColor: colors.primary}, intensityText: {color: colors.text, fontWeight: '800'}, label: {color: colors.primarySoft, fontWeight: '800', letterSpacing: 1, marginTop: 22}, value: {color: colors.text, fontSize: 17, marginTop: 8}, privacy: {color: colors.muted, lineHeight: 22, marginVertical: 28},
});
