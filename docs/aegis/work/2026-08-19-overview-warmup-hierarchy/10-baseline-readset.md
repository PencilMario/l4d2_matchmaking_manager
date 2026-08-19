# 基线阅读集

- `docs/aegis/specs/2026-08-19-overview-warmup-hierarchy-design.md`：已批准的展示、兼容和非目标边界。
- `CONTEXT.md`：Target Server 与 Warm-up Agent 的规范术语和一对多关系。
- `frontend/src/components/OverviewView.tsx`：系统概览暖服区域的现有渲染所有者。
- `frontend/src/components/OverviewView.test.tsx`：当前大厅 ID 展示的回归测试。
- `frontend/src/types/index.ts`：`TargetServer` 与 `WarmupAttempt` 的稳定前端数据契约。
- `frontend/src/services/api.ts`：`/v1/warmups` 与 A2S 观测到前端状态的既有映射，明确不改。
