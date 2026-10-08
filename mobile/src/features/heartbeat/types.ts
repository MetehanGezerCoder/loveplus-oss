export type HeartbeatAcknowledgementState = 'played' | 'hapticDisabled' | 'unsupported';

export type HeartbeatPattern = {
  eventId: string;
  senderUserId: string;
  senderDisplayName: string;
  pattern: number[];
  totalDurationMilliseconds: number;
  sentAtUtc: string;
  expiresAtUtc: string;
  schemaVersion: number;
};

export type HeartbeatAcknowledgement = {
  eventId: string;
  state: HeartbeatAcknowledgementState;
  acknowledgedAtUtc: string;
};

export type SendHeartbeatResult = {
  eventId: string;
  accepted: boolean;
  duplicate: boolean;
  expiresAtUtc: string;
};
