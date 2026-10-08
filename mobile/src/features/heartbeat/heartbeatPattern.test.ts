import {buildHeartbeatPattern} from './heartbeatPattern';

describe('buildHeartbeatPattern', () => {
  it('records relative tap gaps without wall-clock timestamps', () => {
    expect(buildHeartbeatPattern([1000, 1300, 1510])).toEqual([0, 80, 220, 80, 130, 80]);
  });

  it('clamps unsafe long gaps and limits taps', () => {
    const taps = Array.from({length: 20}, (_, index) => index * 10_000);
    const pattern = buildHeartbeatPattern(taps);
    expect(pattern).toHaveLength(32);
    expect(Math.max(...pattern)).toBe(1500);
  });
});
