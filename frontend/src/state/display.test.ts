import { describe, expect, it } from 'vitest';
import { CoreApiError } from '../api/core-client';
import { describeError } from './display';

describe('Core error display', () => {
  it('explains when applying a running Agent download region failed', () => {
    const result = describeError(new CoreApiError(409, 'warmup_agent_download_region_apply_failed'));

    expect(result.code).toBe('warmup_agent_download_region_apply_failed');
    expect(result.message).toBe('Steam 下载区域应用失败，节点配置未修改，请检查节点状态后重试。');
  });
});
