import { describe, expect, it } from 'vitest';
import { formatAttemptPhase } from './statusMapping';
import { labelWarmupPhase } from '../state/display';

describe('暖服任务阶段映射', () => {
  it.each([
    ['Selecting', '选择目标'],
    ['AwaitingFirstMember', '等待外部成员进入'],
    ['Active', '观测服务器人数'],
  ])('将后端阶段 %s 映射为 %s', (phase, label) => {
    expect(formatAttemptPhase(phase)).toBe(label);
    expect(labelWarmupPhase(phase)).toBe(label);
  });
});
