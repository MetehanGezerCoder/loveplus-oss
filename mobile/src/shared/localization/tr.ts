import type {ActivityType, MoodType, ProximityLevel} from '../../features/realtime-status';

export const tr = {
  activity: {
    unknown: 'Bilinmiyor', stationary: 'Sabit', walking: 'Yürüyor', running: 'Koşuyor', cycling: 'Bisiklette', inVehicle: 'Araçta',
  } satisfies Record<ActivityType, string>,
  mood: {
    none: {emoji: '💭', label: 'Seçilmedi'},
    happy: {emoji: '😊', label: 'Mutlu'},
    calm: {emoji: '😌', label: 'Sakin'},
    excited: {emoji: '🤩', label: 'Heyecanlı'},
    tired: {emoji: '😴', label: 'Yorgun'},
    sad: {emoji: '😔', label: 'Üzgün'},
    busy: {emoji: '💼', label: 'Meşgul'},
    inLove: {emoji: '😍', label: 'Aşık'},
    missingYou: {emoji: '🥺', label: 'Özledi'},
    sulky: {emoji: '😒', label: 'Tripli'},
  } satisfies Record<MoodType, {emoji: string; label: string}>,
  proximity: {
    unavailable: 'Yakınlık için iki güncel konum bekleniyor',
    samePlace: 'Aynı yerdesiniz',
    veryClose: 'Çok yakınsınız',
    nearby: 'Yakındasınız',
    sameArea: 'Aynı bölgedesiniz',
    far: 'Birbirinizden uzaktasınız',
  } satisfies Record<ProximityLevel, string>,
};

export const selectableMoods: MoodType[] = [
  'happy', 'calm', 'excited', 'tired', 'sad', 'busy', 'inLove', 'missingYou', 'sulky',
];
