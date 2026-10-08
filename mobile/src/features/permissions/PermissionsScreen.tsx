import React, {useEffect, useState} from 'react';
import {Alert, AppState, Linking, PermissionsAndroid, Platform, StyleSheet, Switch, Text, View} from 'react-native';
import {PrimaryButton} from '../../shared/ui/FormControls';
import {Screen} from '../../shared/ui/Screen';
import {colors} from '../../shared/ui/theme';
import {loveStatusNative, type TelemetryState} from '../../shared/native/LoveStatusNative';

const initial: TelemetryState = {enabled: true, locationSharing: false, activityEnabled: false, shareLastKnown: true, mood: 'none'};

export function PermissionsScreen() {
  const [state, setState] = useState(initial);
  const [busy, setBusy] = useState(false);
  useEffect(() => { loveStatusNative.state().then(setState).catch(() => undefined); }, []);
  useEffect(() => {
    const subscription = AppState.addEventListener('change', value => {
      if (value === 'active') loveStatusNative.state().then(setState).catch(() => undefined);
    });
    return () => subscription.remove();
  }, []);

  const persist = async (next: TelemetryState) => {
    setBusy(true);
    try { await loveStatusNative.updateSharing(next); setState(next); }
    finally { setBusy(false); }
  };
  const enableLocation = async () => {
    if (Platform.OS !== 'android') return;
    setBusy(true);
    try {
      const fine = await PermissionsAndroid.request(PermissionsAndroid.PERMISSIONS.ACCESS_FINE_LOCATION);
      if (fine === PermissionsAndroid.RESULTS.NEVER_ASK_AGAIN) return openSettings('Konum izni sistem ayarlarından açılmalı.');
      if (fine !== PermissionsAndroid.RESULTS.GRANTED) throw new Error('Konum izni verilmedi; pil paylaşımı çalışmaya devam edecek.');
      if (Number(Platform.Version) >= 29) {
        Alert.alert('Arka planda konum', 'Ekran kapalıyken de paylaşmak için sonraki sistem ekranında Konum > Her zaman izin ver seçeneğini aç.', [
          {text: 'Şimdilik değil', style: 'cancel'},
          {text: 'Sistem ayarlarını aç', onPress: () => Linking.openSettings().catch(() => undefined)},
        ]);
        return;
      }
      await persist({...state, locationSharing: true});
    } catch (error) { Alert.alert('Konum paylaşımı', error instanceof Error ? error.message : 'İzin kontrol edilemedi.'); }
    finally { setBusy(false); }
  };
  const confirmLocation = async () => {
    const granted = Number(Platform.Version) < 29 || await PermissionsAndroid.check(PermissionsAndroid.PERMISSIONS.ACCESS_BACKGROUND_LOCATION);
    if (!granted) return openSettings('“Her zaman izin ver” seçeneği henüz açık değil.');
    await persist({...state, locationSharing: true});
  };
  const toggleLocation = async (value: boolean) => value ? enableLocation() : persist({...state, locationSharing: false});
  const toggleActivity = async (value: boolean) => {
    if (value && Number(Platform.Version) >= 29) {
      const result = await PermissionsAndroid.request(PermissionsAndroid.PERMISSIONS.ACTIVITY_RECOGNITION);
      if (result === PermissionsAndroid.RESULTS.NEVER_ASK_AGAIN) return openSettings('Aktivite izni sistem ayarlarından açılmalı.');
      if (result !== PermissionsAndroid.RESULTS.GRANTED) return Alert.alert('Aktivite kapalı', 'İzin verilmedi; partnerin aktiviteyi “Bilinmiyor” olarak görür.');
    }
    await persist({...state, activityEnabled: value});
  };

  return (
    <Screen>
      <Text style={styles.title}>Ne paylaşmak istersin?</Text>
      <Text style={styles.body}>Pil durumu gerçek cihazdan otomatik paylaşılır. Konum ve aktivite tamamen senin kontrolünde.</Text>
      <PermissionRow title="Gerçek pil durumu" note="Pil yüzdesi ve şarj değişince güncellenir" value locked />
      <PermissionRow title="Konum paylaşımı" note={state.locationSharing ? 'Meliha/Metehan haritada görebilir' : 'Kapalı · koordinat gönderilmez'} value={state.locationSharing} onChange={value => { toggleLocation(value).catch(() => undefined); }} disabled={busy} />
      <PermissionRow title="Aktivite" note="Yürüyor, koşuyor, bisiklette veya araçta" value={state.activityEnabled} onChange={value => { toggleActivity(value).catch(() => undefined); }} disabled={busy} />
      <PermissionRow title="Son konumu göster" note="Paylaşımı kapatınca son bilinen noktayı koru" value={state.shareLastKnown} onChange={value => { persist({...state, shareLastKnown: value}).catch(() => undefined); }} disabled={busy} />
      {Platform.OS === 'android' && Number(Platform.Version) >= 29 && !state.locationSharing ? <PrimaryButton loading={busy} onPress={() => { confirmLocation().catch(() => undefined); }}>İzin verdim, konumu aç</PrimaryButton> : null}
      <Text style={styles.privacy}>Dengeli gönderim: pil/şarj değişiminde, anlamlı hareket olduğunda ve 90 saniyelik heartbeat ile. Offline veriler şifreli, sıralı ve en fazla 50 kayıt tutulur.</Text>
    </Screen>
  );
}

function PermissionRow({title, note, value, onChange, disabled, locked}: {title: string; note: string; value: boolean; onChange?: (value: boolean) => void; disabled?: boolean; locked?: boolean}) {
  return <View style={styles.row}><View style={styles.copy}><Text style={styles.label}>{title}</Text><Text style={styles.note}>{note}</Text></View><Switch value={value} onValueChange={onChange} disabled={disabled || locked} trackColor={{true: colors.primary}} /></View>;
}
function openSettings(message: string) { Alert.alert('Sistem ayarı gerekli', message, [{text: 'Vazgeç'}, {text: 'Ayarları aç', onPress: () => Linking.openSettings().catch(() => undefined)}]); }

const styles = StyleSheet.create({
  title: {fontSize: 28, color: colors.text, fontWeight: '900', marginTop: 12}, body: {color: colors.muted, fontSize: 16, lineHeight: 23, marginTop: 10, marginBottom: 16},
  row: {flexDirection: 'row', alignItems: 'center', backgroundColor: colors.surface, borderRadius: 18, borderWidth: 1, borderColor: colors.border, padding: 17, marginTop: 12}, copy: {flex: 1, paddingRight: 12}, label: {color: colors.text, fontWeight: '800', fontSize: 16}, note: {color: colors.muted, marginTop: 5, lineHeight: 19}, privacy: {color: colors.muted, lineHeight: 21, marginTop: 24},
});
