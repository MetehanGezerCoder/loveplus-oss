const TAP_DURATION_MS = 80;
const MAX_GAP_MS = 1500;
export const MAX_TAPS = 16;

export function buildHeartbeatPattern(timestamps: number[]): number[] {
  if (!timestamps.length) return [];
  const safe = timestamps.slice(0, MAX_TAPS);
  const pattern = [0, TAP_DURATION_MS];
  for (let index = 1; index < safe.length; index += 1) {
    pattern.push(Math.max(30, Math.min(MAX_GAP_MS, safe[index]! - safe[index - 1]! - TAP_DURATION_MS)));
    pattern.push(TAP_DURATION_MS);
  }
  return pattern;
}

export function createEventId() {
  const hex = () => Math.floor(Math.random() * 0x10000).toString(16).padStart(4, '0');
  return `${hex()}${hex()}-${hex()}-4${hex().slice(1)}-a${hex().slice(1)}-${hex()}${hex()}${hex()}`;
}
