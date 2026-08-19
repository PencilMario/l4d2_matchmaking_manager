# 系统概览暖服层级展示 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use aegis:subagent-driven-development (recommended) or aegis:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在系统概览中按目标服务器展示并发暖服节点，使同一服务器的全部暖服状态可在同一层级内核对。

**Architecture:** `OverviewView` 按既有 `WarmupAttempt.targetServerId` 聚合任务，并从既有 `targets` 取得名称和 A2S 人数。每组输出一个服务器父行和固定展开的节点子行；不新增状态、请求或后端字段。

**Tech Stack:** React 19、TypeScript、Tailwind CSS、Lucide React、Vitest、Testing Library、Vite。

**Baseline / Authority Refs:** `docs/aegis/specs/2026-08-19-overview-warmup-hierarchy-design.md`；`CONTEXT.md`；`frontend/src/components/OverviewView.tsx`；`frontend/src/types/index.ts`；`frontend/src/services/api.ts`。

**Compatibility Boundary:** `/v1/warmups` API、`WarmupAttempt`/`TargetServer` 类型、轮询、大厅 ID 文本、空状态和定位服务器导航保持稳定；不改 Core 或其他页面。

**Verification:** 目标 Vitest、完整前端测试、生产构建、`git diff --check`，以及宽屏/窄屏手工检查。

---

### Task 1: 按目标服务器渲染暖服层级

**Files:** `frontend/src/components/OverviewView.tsx`；`frontend/src/components/OverviewView.test.tsx`

**Why this task exists:** 扁平表会重复目标服务器，无法快速判断一个服务器上并发运行了多少个暖服节点。

**Impact / Compatibility:** 只改 `OverviewView` 的 DOM 结构和展示顺序，保留现有 props、格式化工具、状态徽标、大厅 ID 文本和导航回调。

**Repair Track:** 不适用；这是新增显示层次，未替换错误逻辑所有者。

**Retirement Track:** 旧的 `attempts.map` 扁平行由父/子行渲染取代；无 API fallback、迁移或重复分支。

- [ ] **Step 1: Write the failing test**

在 `OverviewView.test.tsx` 增加一台在线目标服务器和两条共享 `targetServerId` 的 active 任务，断言同一服务器只出现一个父项、父项显示节点数和 `0/12`，并显示两个节点标签和两个大厅 ID：

```tsx
expect(screen.getAllByText('服务器[1]')).toHaveLength(1);
expect(screen.getByText('当前调度节点 2')).toBeInTheDocument();
expect(screen.getByText('0/12')).toBeInTheDocument();
expect(screen.getByText('暖服节点[1]')).toBeInTheDocument();
expect(screen.getByText('暖服节点[2]')).toBeInTheDocument();
```

- [ ] **Step 2: Verify RED**

在 `frontend` 执行 `npm test -- --run src/components/OverviewView.test.tsx`。预期新断言失败，因为当前实现只有扁平任务行，没有父项统计和节点标签。

- [ ] **Step 3: Implement minimal grouping**

在 `OverviewView` 内用 `Map<string, WarmupAttempt[]>` 按 `targetServerId` 分组；每组内部和分组本身均按 `startedAt` 降序排序。通过 `targets.find` 取得父项服务器；模式取唯一值，多种时显示“混合模式”。该模型只存在于渲染函数内，不改 API 或类型。

```tsx
const groups = new Map<string, WarmupAttempt[]>();
attempts.forEach((attempt) => groups.set(attempt.targetServerId, [...(groups.get(attempt.targetServerId) ?? []), attempt]));
const groupedWarmups = [...groups.entries()].map(([targetServerId, items]) => ({
  targetServerId,
  target: targets.find((target) => target.id === targetServerId),
  attempts: items.toSorted((a, b) => Date.parse(b.startedAt) - Date.parse(a.startedAt)),
})).toSorted((a, b) => Date.parse(b.attempts[0].startedAt) - Date.parse(a.attempts[0].startedAt));
```

- [ ] **Step 4: Render parent and child rows**

将现有 `attempts.map` 替换为分组迭代。父行显示服务器名称/端点、运行模式、`当前调度节点 {count}`、`currentPlayers/maxPlayers` 和定位操作。子行使用左侧边框、缩进及低对比度背景显示 `暖服节点[index + 1]`、节点名、状态、阶段、大厅 ID、剩余时间和开始时间。继续使用现有 `Badge`、格式化函数和 `Tooltip`。目标缺失时名称回退到任务端点，人数显示 `--`；空状态保留。

```tsx
{groupedWarmups.map((group) => (
  <React.Fragment key={group.targetServerId}>
    <tr>{/* server parent row */}</tr>
    {group.attempts.map((attempt, index) => <tr key={attempt.id}>{/* child row */}</tr>)}
  </React.Fragment>
))}
```

- [ ] **Step 5: Verify GREEN**

在 `frontend` 执行 `npm test -- --run src/components/OverviewView.test.tsx`，预期现有大厅 ID 测试和新增聚合测试全部通过。

- [ ] **Step 6: Regression and manual verification**

在 `frontend` 执行 `npm test -- --run` 和 `npm run build`；在仓库根目录执行 `git diff --check`。预期测试、构建和差异检查均成功。手工检查桌面与窄屏：父项只出现一次，节点全部可见，窄屏横向滚动而非文字重叠。

- [ ] **Step 7: Commit**

执行 `git add frontend/src/components/OverviewView.tsx frontend/src/components/OverviewView.test.tsx docs/aegis/work/2026-08-19-overview-warmup-hierarchy; git commit -m "feat(frontend): 按服务器分组展示暖服节点"`。

**Residual Risk:** 组件测试不覆盖真实 Core 延迟或缺失 A2S 观测；缺失目标服务器回退由组件逻辑和手工检查覆盖。移动端依赖现有横向滚动容器。
