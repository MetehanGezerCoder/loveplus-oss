export type ActivityType = 'unknown' | 'stationary' | 'walking' | 'running' | 'cycling' | 'inVehicle';
export type BatteryState = 'unknown' | 'charging' | 'discharging' | 'full' | 'notCharging';
export type MoodType = 'none' | 'happy' | 'calm' | 'excited' | 'tired' | 'sad' | 'busy' | 'inLove' | 'missingYou' | 'sulky';
export type PresenceState = 'online' | 'recentlyOnline' | 'offline';
export type ProximityLevel = 'unavailable' | 'samePlace' | 'veryClose' | 'nearby' | 'sameArea' | 'far';
export type DistanceAvailability =
  | 'available'
  | 'partnerLocationUnavailable'
  | 'ownLocationUnavailable'
  | 'partnerLocationStale'
  | 'ownLocationStale'
  | 'partnerSharingOff'
  | 'ownSharingOff';

export type SharedLocation = {
  latitude: number;
  longitude: number;
  accuracy: number;
  speed?: number;
  heading?: number;
  altitude?: number;
  recordedAtUtc: string;
  isLastKnown: boolean;
  isApproximate: boolean;
};

export type PartnerStatus = {
  userId: string;
  partnerDisplayName: string;
  batteryLevel: number;
  isCharging: boolean;
  batteryState: BatteryState;
  activityType: ActivityType;
  mood: MoodType;
  presence: PresenceState;
  distanceMeters?: number;
  distanceIsApproximate: boolean;
  distanceAvailability: DistanceAvailability;
  location?: SharedLocation;
  counterpartLocation?: SharedLocation;
  isLocationSharingEnabled: boolean;
  shareLastKnownLocation: boolean;
  recordedAtUtc: string;
  isSimulated: boolean;
  proximityLevel: ProximityLevel;
  isMoodSharingEnabled: boolean;
  // Windows the server used to classify presence, so a cached DTO can be re-evaluated
  // instead of repeating a verdict that has since expired.
  presenceOnlineSeconds: number;
  presenceRecentlyOnlineSeconds: number;
};

export type CriticalBattery = {
  eventId: string;
  userId: string;
  batteryLevel: number;
  lastKnownLocationAvailable: boolean;
  occurredAtUtc: string;
};
