import React, {useEffect, useMemo, useRef, useState} from 'react';
import {Animated, Pressable, ScrollView, StyleSheet, Text, View} from 'react-native';
import MapView, {Circle, Marker, Polyline, PROVIDER_GOOGLE, type Region} from 'react-native-maps';
import type {NativeStackScreenProps} from '@react-navigation/native-stack';
import {Screen} from '../../shared/ui/Screen';
import {colors} from '../../shared/ui/theme';
import {useCurrentPair} from '../pairing';
import {formatDistance, usePartnerStatus, type MoodType, type PartnerStatus} from '../realtime-status';
import {loveStatusNative} from '../../shared/native/LoveStatusNative';
import {MAPS_ENABLED} from '../../shared/config/environment';
import {selectableMoods, tr} from '../../shared/localization/tr';
import {useHeartbeat, type DeliveryState} from '../heartbeat';
import {apiRequest} from '../../shared/api/apiClient';

type Routes = {Dashboard: undefined; Permissions: undefined; Settings: undefined};
export function DashboardScreen({navigation}: NativeStackScreenProps<Routes, 'Dashboard'>) {
  const pair = useCurrentPair();
  const status = usePartnerStatus();
  // The cached DTO carries the presence the server computed when it was sent. Overriding it
  // with the freshness-derived value keeps every card below from claiming a partner is online
  // long after their phone stopped reporting.
  const partner = useMemo(
    () => (status.data ? {...status.data, presence: status.presence ?? status.data.presence} : status.data),
    [status.data, status.presence],
  );
  const distance = distanceCopy(partner);
  const heartbeat = useHeartbeat();
  const scrollRef = useRef<ScrollView>(null);
  const mapOffset = useRef(0);
  const [selectedMood, setSelectedMood] = useState<MoodType>('none');
  useEffect(() => { loveStatusNative.state().then(state => setSelectedMood(state.mood as MoodType)).catch(() => undefined); }, []);
  const chooseMood = async (mood: MoodType) => {
    if (mood === selectedMood) return;
    const previous = selectedMood;
    setSelectedMood(mood);
    try {
      await apiRequest('/api/status/mood', {method: 'POST', body: {mood, isMoodSharingEnabled: heartbeat.preferences.moodSharing}});
      await loveStatusNative.setMood(mood);
    } catch { setSelectedMood(previous); }
  };
  return (
    <Screen scrollRef={scrollRef}>
      <View style={styles.header}>
        <View><Text style={styles.brand}>Love+</Text><Text style={styles.subtitle}>Sadece ikinizin alanı ♥</Text></View>
        <Pressable style={styles.avatar} onPress={() => navigation.navigate('Settings')}><Text style={styles.avatarText}>⚙</Text></Pressable>
      </View>
      {partner?.isSimulated ? <Text style={styles.simulated}>SIMULATED DATA</Text> : null}
      <View style={styles.hero}>
        <View style={styles.heroTop}><View><Text style={styles.eyebrow}>ŞU AN</Text><Text style={styles.name}>{partner?.partnerDisplayName ?? pair.data?.partnerDisplayName ?? 'Partnerin'}</Text></View><Presence presence={partner?.presence} connection={status.connectionState} /></View>
        <Text style={styles.distance}>{distance}</Text>
        <Text style={styles.freshness}>{partner ? `${relativeTime(partner.recordedAtUtc)} güncellendi` : 'İlk gerçek cihaz verisi bekleniyor'}</Text>
        <View style={styles.metrics}>
          <Metric icon={partner?.isCharging ? '⚡' : '🔋'} label="Pil" value={partner ? `%${partner.batteryLevel}` : '—'} danger={Boolean(partner && partner.batteryLevel <= 5)} />
          <Metric icon="🚶" label="Aktivite" value={partner ? tr.activity[partner.activityType] : '—'} />
          <Metric icon={moodEmoji(partner?.mood)} label="Ruh hâli" value={moodLabel(partner?.mood)} />
        </View>
      </View>
      {heartbeat.preferences.showProximity ? <ProximityCard status={partner} onMap={() => scrollRef.current?.scrollTo({y: mapOffset.current, animated: true})} /> : null}
      <HeartbeatCard heartbeat={heartbeat} partner={partner} />
      <CoupleMap status={partner} onLayout={y => { mapOffset.current = y; }} />
      <View style={styles.sectionHeader}><Text style={styles.sectionTitle}>Bugün nasılsın?</Text><Text style={styles.sectionHint}>Partnerin anında görsün</Text></View>
      <View style={styles.moods}>{selectableMoods.map(mood => <Pressable key={mood} style={[styles.mood, selectedMood === mood && styles.moodSelected]} onPress={() => { chooseMood(mood).catch(() => undefined); }}><Text style={styles.moodEmoji}>{tr.mood[mood].emoji}</Text><Text style={styles.moodLabel}>{tr.mood[mood].label}</Text></Pressable>)}</View>
      {status.error ? <Text style={styles.error}>{status.error.message}</Text> : null}
      <View style={styles.actions}>
        <Pressable style={styles.primaryAction} onPress={() => navigation.navigate('Permissions')}><Text style={styles.primaryActionText}>📍  Paylaşım ve izinler</Text></Pressable>
        <Pressable style={styles.action} onPress={() => navigation.navigate('Settings')}><Text style={styles.actionText}>Ayarlar</Text></Pressable>
      </View>
    </Screen>
  );
}

function ProximityCard({status, onMap}: {status?: PartnerStatus | null; onMap: () => void}) {
  const level = status?.proximityLevel ?? 'unavailable';
  return <View style={styles.proximityCard}>
    <View style={styles.proximityIcon}><Text style={styles.proximityIconText}>⌖</Text></View>
    <View style={styles.proximityCopy}><Text style={styles.proximityTitle}>{tr.proximity[level]}</Text><Text style={styles.proximityMeta}>{status ? `${status.partnerDisplayName} · ${presenceLabel(status.presence)} · ${relativeTime(status.recordedAtUtc)}` : 'Partner verisi bekleniyor'}</Text><Text style={styles.proximityText}>{status?.distanceMeters != null ? `${status.distanceIsApproximate ? 'Yaklaşık ' : ''}${formatDistance(status.distanceMeters)} · GPS ±${Math.round(status.location?.accuracy ?? 0)} m` : 'Gizlilik ayarları ve GPS doğruluğu dikkate alınır.'}</Text></View>
    <Pressable onPress={onMap} accessibilityLabel="Haritaya git"><Text style={styles.proximityLink}>Harita ↓</Text></Pressable>
  </View>;
}

function HeartbeatCard({heartbeat, partner}: {heartbeat: ReturnType<typeof useHeartbeat>; partner?: PartnerStatus | null}) {
  const scale = useRef(new Animated.Value(1)).current;
  useEffect(() => {
    if (!heartbeat.receivedPulse) return;
    Animated.sequence([
      Animated.spring(scale, {toValue: 1.22, useNativeDriver: true, speed: 28}),
      Animated.spring(scale, {toValue: 1, useNativeDriver: true, speed: 20}),
    ]).start();
  }, [heartbeat.receivedPulse, scale]);
  const disabled = heartbeat.delivery === 'sending' || heartbeat.delivery === 'pending';
  return <View style={styles.heartbeatCard}>
    <View style={styles.sectionHeader}><View><Text style={styles.sectionTitle}>Haptic Heartbeat</Text><Text style={styles.heartbeatHint}>{partner ? `${partner.partnerDisplayName} · ${presenceLabel(partner.presence)}` : 'Partner bağlantısı bekleniyor'}</Text></View><DeliveryBadge state={heartbeat.delivery} /></View>
    <Pressable disabled={disabled} onPress={heartbeat.tap} style={styles.heartPad} accessibilityLabel="Kalp atışı ritmi ekle">
      <Animated.Text style={[styles.heart, {transform: [{scale}]}]}>💗</Animated.Text>
      <Text style={styles.tapCount}>{heartbeat.timestamps.length ? `${heartbeat.timestamps.length} vuruş` : 'Kalbe dokun'}</Text>
    </Pressable>
    <Text style={styles.heartbeatMessage}>{heartbeat.message}</Text>
    {heartbeat.hapticsSupported === false ? <Text style={styles.hapticFallback}>Bu cihazda titreşim yok; gelen kalp ekranda canlandırılır.</Text> : null}
    <View style={styles.heartbeatActions}>
      <Pressable style={styles.heartbeatSecondary} onPress={heartbeat.reset}><Text style={styles.heartbeatSecondaryText}>Sıfırla</Text></Pressable>
      <Pressable disabled={disabled || !heartbeat.preferences.sendHeartbeats} style={[styles.heartbeatSend, disabled && styles.buttonDisabled]} onPress={() => { heartbeat.send().catch(() => undefined); }}><Text style={styles.heartbeatSendText}>Kalp atışını gönder ♥</Text></Pressable>
    </View>
  </View>;
}

function DeliveryBadge({state}: {state: DeliveryState}) {
  const label: Record<DeliveryState, string> = {idle: 'Hazır', recording: 'Kaydediliyor', sending: 'Gönderiliyor', pending: 'Onay bekliyor', delivered: 'Teslim edildi', disabled: 'Titreşim kapalı', timeout: 'Zaman aşımı', error: 'Gönderilemedi'};
  return <View style={[styles.deliveryBadge, state === 'delivered' && styles.deliverySuccess]}><Text style={styles.deliveryText}>{label[state]}</Text></View>;
}

function CoupleMap({status, onLayout}: {status?: PartnerStatus | null; onLayout: (y: number) => void}) {
  const partner = status?.location;
  const own = status?.counterpartLocation;
  const region = useMemo<Region | null>(() => {
    const points = [partner, own].filter(Boolean) as NonNullable<typeof partner>[];
    if (!points.length) return null;
    const latitudes = points.map(x => x.latitude); const longitudes = points.map(x => x.longitude);
    const minLat = Math.min(...latitudes); const maxLat = Math.max(...latitudes); const minLon = Math.min(...longitudes); const maxLon = Math.max(...longitudes);
    return {latitude: (minLat + maxLat) / 2, longitude: (minLon + maxLon) / 2, latitudeDelta: Math.max(0.008, (maxLat - minLat) * 1.8), longitudeDelta: Math.max(0.008, (maxLon - minLon) * 1.8)};
  }, [own, partner]);
  return <View style={styles.mapCard} onLayout={event => onLayout(event.nativeEvent.layout.y)}>
    <View style={styles.sectionHeader}><Text style={styles.sectionTitle}>İkinizin haritası</Text><Text style={styles.sectionHint}>{partner?.isApproximate ? 'Yaklaşık konum' : 'Gerçek cihaz konumu'}</Text></View>
    {MAPS_ENABLED && region && partner ? <MapView style={styles.map} provider={PROVIDER_GOOGLE} region={region} toolbarEnabled={false}>
      <Marker coordinate={partner} title={status?.partnerDisplayName} description={`${relativeTime(partner.recordedAtUtc)} · ±${Math.round(partner.accuracy)} m`} pinColor={colors.primary} />
      <Circle center={partner} radius={partner.accuracy} strokeColor="#ff6fae99" fillColor="#ff6fae22" />
      {own ? <Marker coordinate={own} title="Sen" description={`${relativeTime(own.recordedAtUtc)} · ±${Math.round(own.accuracy)} m`} pinColor="#7D9FFF" /> : null}
      {own ? <Circle center={own} radius={own.accuracy} strokeColor="#7D9FFF99" fillColor="#7D9FFF22" /> : null}
      {own ? <Polyline coordinates={[partner, own]} strokeColor={colors.primarySoft} strokeWidth={3} lineDashPattern={[8, 7]} /> : null}
    </MapView> : <View style={styles.mapEmpty}><Text style={styles.mapEmptyIcon}>🗺️</Text><Text style={styles.mapEmptyTitle}>{!MAPS_ENABLED ? 'Harita güvenli şekilde yapılandırılmayı bekliyor' : mapEmptyTitle(status)}</Text><Text style={styles.mapEmptyText}>{!MAPS_ENABLED ? 'Konum verileri çalışır; harita anahtarı eklenince iki telefon burada görünür.' : 'Paylaşım açıldığında iki telefon burada birlikte görünür.'}</Text></View>}
  </View>;
}

function Presence({presence, connection}: {presence?: PartnerStatus['presence']; connection: string}) {
  const online = presence === 'online'; const label = presence ? presenceLabel(presence) : connection === 'connecting' ? 'Bağlanıyor' : 'Çevrimdışı';
  return <View style={styles.presence}><View style={[styles.dot, online ? styles.dotOnline : styles.dotOffline]} /><Text style={styles.presenceText}>{label}</Text></View>;
}
function Metric({icon, label, value, danger = false}: {icon: string; label: string; value: string; danger?: boolean}) { return <View style={styles.metric}><Text style={styles.metricIcon}>{icon}</Text><Text style={styles.metricLabel}>{label}</Text><Text style={[styles.metricValue, danger && styles.danger]}>{value}</Text></View>; }
function distanceCopy(status?: PartnerStatus | null) {
  if (!status) return 'Birbirinizi bekliyorsunuz…';
  // A distance calculated from a packet that has aged out is no longer a fact about where
  // two people are, so it is reported as last-known rather than as the current distance.
  if (status.presence === 'offline' && status.distanceMeters != null) {
    return `Son bilinen mesafe ${formatDistance(status.distanceMeters)} · ${status.partnerDisplayName} şu an bildirmiyor`;
  }
  if (status.distanceMeters !== undefined && status.distanceMeters !== null) return status.distanceMeters < 30 ? 'Birbirinize çok yakınsınız ♥' : `Aranızda ${status.distanceIsApproximate ? 'yaklaşık ' : ''}${formatDistance(status.distanceMeters)} var`;
  const copy: Record<string, string> = {partnerSharingOff: `${status.partnerDisplayName} konum paylaşımını kapattı`, ownSharingOff: 'Konum paylaşımın kapalı', partnerLocationStale: `${status.partnerDisplayName} konumu güncel değil`, ownLocationStale: 'Konumun güncel değil'};
  return copy[status.distanceAvailability] ?? 'Mesafe için iki konum bekleniyor';
}
function mapEmptyTitle(status?: PartnerStatus | null) { return status?.distanceAvailability === 'partnerSharingOff' ? `${status.partnerDisplayName} konumunu gizli tutuyor` : 'Konum paylaşımı henüz hazır değil'; }
function moodEmoji(mood?: MoodType) { return tr.mood[mood ?? 'none']?.emoji ?? '💭'; }
function moodLabel(mood?: MoodType) { return tr.mood[mood ?? 'none']?.label ?? 'Seçilmedi'; }
function relativeTime(utc: string) { const seconds = Math.max(0, Math.round((Date.now() - new Date(utc).getTime()) / 1000)); if (seconds < 60) return `${seconds} sn önce`; const minutes = Math.floor(seconds / 60); if (minutes < 60) return `${minutes} dk önce`; return `${Math.floor(minutes / 60)} sa önce`; }
function presenceLabel(presence: PartnerStatus['presence']) { return presence === 'online' ? 'Çevrimiçi' : presence === 'recentlyOnline' ? 'Yakın zamanda' : 'Çevrimdışı'; }

const styles = StyleSheet.create({
  header: {flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginTop: 8, marginBottom: 20}, brand: {fontSize: 38, fontWeight: '900', color: colors.primary}, subtitle: {fontSize: 14, color: colors.muted, marginTop: 1}, avatar: {width: 42, height: 42, borderRadius: 15, backgroundColor: colors.surface, alignItems: 'center', justifyContent: 'center', borderWidth: 1, borderColor: colors.border}, avatarText: {fontSize: 18, color: colors.text}, simulated: {backgroundColor: '#FFB000', color: '#1C1000', fontWeight: '900', textAlign: 'center', padding: 7, borderRadius: 10, marginBottom: 10},
  hero: {backgroundColor: colors.surface, borderRadius: 26, padding: 20, borderWidth: 1, borderColor: colors.border}, heroTop: {flexDirection: 'row', justifyContent: 'space-between', alignItems: 'flex-start'}, eyebrow: {fontSize: 11, letterSpacing: 1.6, color: colors.primarySoft, fontWeight: '900'}, name: {fontSize: 30, color: colors.text, fontWeight: '900', marginTop: 5}, presence: {flexDirection: 'row', alignItems: 'center', gap: 7, backgroundColor: colors.surfaceRaised, borderRadius: 20, paddingVertical: 7, paddingHorizontal: 10}, presenceText: {color: colors.muted, fontSize: 12, fontWeight: '700'}, dot: {width: 8, height: 8, borderRadius: 4}, dotOnline: {backgroundColor: colors.success}, dotOffline: {backgroundColor: colors.muted}, distance: {fontSize: 18, color: colors.primarySoft, fontWeight: '800', marginTop: 16}, freshness: {color: colors.muted, marginTop: 5, fontSize: 12},
  metrics: {flexDirection: 'row', gap: 9, marginTop: 18}, metric: {flex: 1, backgroundColor: colors.surfaceRaised, borderRadius: 17, padding: 12}, metricIcon: {fontSize: 20}, metricLabel: {color: colors.muted, fontSize: 10, textTransform: 'uppercase', letterSpacing: 0.6, marginTop: 7}, metricValue: {color: colors.text, fontWeight: '800', fontSize: 14, marginTop: 4}, danger: {color: colors.danger},
  proximityCard: {flexDirection: 'row', alignItems: 'center', gap: 12, marginTop: 14, padding: 15, backgroundColor: colors.surface, borderRadius: 19, borderWidth: 1, borderColor: colors.border}, proximityIcon: {width: 42, height: 42, borderRadius: 14, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.surfaceRaised}, proximityIconText: {fontSize: 25, color: colors.primary}, proximityCopy: {flex: 1}, proximityTitle: {color: colors.text, fontSize: 16, fontWeight: '900'}, proximityMeta: {color: colors.primarySoft, fontSize: 10, fontWeight: '700', marginTop: 3}, proximityText: {color: colors.muted, fontSize: 11, marginTop: 3}, proximityLink: {color: colors.primarySoft, fontWeight: '800', fontSize: 11},
  heartbeatCard: {marginTop: 16, backgroundColor: colors.surface, borderRadius: 24, padding: 18, borderWidth: 1, borderColor: colors.border}, heartbeatHint: {color: colors.muted, fontSize: 11, marginTop: 3}, heartPad: {height: 150, backgroundColor: colors.surfaceRaised, borderRadius: 22, alignItems: 'center', justifyContent: 'center'}, heart: {fontSize: 70}, tapCount: {color: colors.primarySoft, fontWeight: '800', marginTop: 4}, heartbeatMessage: {color: colors.muted, textAlign: 'center', minHeight: 20, marginTop: 11}, hapticFallback: {color: colors.primarySoft, fontSize: 11, textAlign: 'center', marginTop: 4}, heartbeatActions: {flexDirection: 'row', gap: 9, marginTop: 10}, heartbeatSecondary: {paddingVertical: 13, paddingHorizontal: 18, borderWidth: 1, borderColor: colors.border, borderRadius: 14}, heartbeatSecondaryText: {color: colors.muted, fontWeight: '800'}, heartbeatSend: {flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.primary, borderRadius: 14}, heartbeatSendText: {color: '#fff', fontWeight: '900'}, buttonDisabled: {opacity: 0.5}, deliveryBadge: {backgroundColor: colors.surfaceRaised, borderRadius: 12, paddingVertical: 6, paddingHorizontal: 9}, deliverySuccess: {backgroundColor: '#173B2B'}, deliveryText: {color: colors.primarySoft, fontSize: 10, fontWeight: '800'},
  mapCard: {marginTop: 16, backgroundColor: colors.surface, borderRadius: 24, padding: 14, borderWidth: 1, borderColor: colors.border, overflow: 'hidden'}, sectionHeader: {flexDirection: 'row', alignItems: 'baseline', justifyContent: 'space-between', marginBottom: 12}, sectionTitle: {fontSize: 18, color: colors.text, fontWeight: '900'}, sectionHint: {fontSize: 11, color: colors.muted}, map: {height: 220, borderRadius: 18}, mapEmpty: {height: 190, borderRadius: 18, backgroundColor: colors.surfaceRaised, alignItems: 'center', justifyContent: 'center', padding: 28}, mapEmptyIcon: {fontSize: 38}, mapEmptyTitle: {color: colors.text, fontSize: 16, fontWeight: '800', marginTop: 10, textAlign: 'center'}, mapEmptyText: {color: colors.muted, textAlign: 'center', marginTop: 6, lineHeight: 19},
  moods: {flexDirection: 'row', flexWrap: 'wrap', gap: 8, marginBottom: 8}, mood: {width: '31%', backgroundColor: colors.surface, borderColor: colors.border, borderWidth: 1, borderRadius: 16, padding: 11, alignItems: 'center'}, moodSelected: {borderColor: colors.primary, backgroundColor: '#2F1928'}, moodEmoji: {fontSize: 24}, moodLabel: {color: colors.muted, fontSize: 11, fontWeight: '700', marginTop: 5},
  actions: {flexDirection: 'row', gap: 10, marginTop: 14, marginBottom: 20}, primaryAction: {flex: 1.5, backgroundColor: colors.primary, borderRadius: 15, padding: 15, alignItems: 'center'}, primaryActionText: {color: '#fff', fontWeight: '900'}, action: {flex: 0.7, borderColor: colors.border, borderWidth: 1, borderRadius: 15, padding: 15, alignItems: 'center'}, actionText: {color: colors.primarySoft, fontWeight: '800'}, error: {color: colors.danger, marginTop: 14},
});
