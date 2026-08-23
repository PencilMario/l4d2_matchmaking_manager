# 玩家进入统计证据记录

## TodoCheckpointDraft

- 当前 todo：交付隔离 worktree 变更，并保留已知环境缺口与后续检查。
- 已完成：设计/意图/基线/影响文档；共享契约；成功 ReplyJoinData 采集；Agent 有界上报；Core token、幂等接收、迁移；调度快照；数据库分组统计、上海零桶、180 天清理；现用管理页；部署/API 文档。
- 活跃切片：V3 最终证据收敛已完成。
- 阻塞项：Docker/Testcontainers 环境不可用，PostgreSQL migration/运行时聚合 smoke test 未执行。
- 下一步：由用户决定是否合并 worktree，以及在具备 Docker/真实 Steam 环境后执行外部验证。

## Fresh verification evidence

| Check | Result | Scope / note |
| --- | --- | --- |
| `dotnet test L4d2MatchmakingManager.sln --no-restore --logger "console;verbosity=minimal"` | exit 0 | Contracts 7 passed; Protocol 13 passed; Probe 24 passed; Agent 25 passed; Core 183 passed, 3 skipped. |
| `dotnet build L4d2MatchmakingManager.sln --no-restore` | exit 0 | 0 warnings, 0 errors. |
| Core `WarmupAgentEndpointTests` | 15 passed | Latest recreate compensation boundary: replacement cleanup, retained replacement ID on delete failure, and persisted pending boundary when compensation save fails. |
| Core `PlayerEntryStatisticsQueryTests` | 7 passed | Shanghai cross-midnight, Monday week split, filters, zero Agent row, default dimensions and Npgsql SQL translation assertion. |
| Core `PlayerEntryRetentionServiceTests` | 1 passed | Deletes only events older than 180 days; relational path uses `ExecuteDeleteAsync`, InMemory fallback remains test-only. |
| `npm run test -- --run` in `frontend` | exit 0 | 23 test files, 70 tests passed. Includes statistics API serialization, Shanghai datetime conversion, page states, zero-result sections, request ordering, mounted App/Sidebar journey. |
| `npm run build` in `frontend` | exit 0 | TypeScript and Vite production build passed. |
| `pwsh -NoProfile -File .\deploy\matchmaking-core\Test-ComposeContract.ps1` | exit 0 | Compose/environment contract checks passed. |
| `git diff --check` | exit 0 | No whitespace errors. |

The Npgsql translation regression generated and asserted `AT TIME ZONE 'UTC'` in the UTC-hour grouping SQL. The query now performs count, UTC-hour, Agent/name-snapshot and region grouping in the database; Core only maps grouped hours to Shanghai buckets and fills zeros.

## Review repair evidence

- The empty-result frontend regression first failed because `totalEntries === 0` replaced trend and dimension sections; after the minimal render-condition repair, the statistics component suite passed 9/9.
- The Core ingestion concurrency regression used a test-only `SaveChanges` gate to force simultaneous same-ID commits. Before repair it raised a duplicate-key exception; after batch ID loading, detach/reconcile and bounded retry, `PlayerEntryEventIngestionTests` passed 4/4.
- The recreate persistence regression forced the final token-hash save to fail after the replacement container started. Before repair the replacement remained; after cleanup/old-hash restoration/compensation save, the focused endpoint suite passed 13/13.
- The recreate boundary regression now also covers replacement deletion failure and a second compensation-save failure; the focused endpoint suite passed 15/15, and the persisted record does not revert to the old running/container boundary.
- The frontend request-order regression completed the later request first and the earlier request second. The latest-request sequence guard kept the later result; the component suite passed 9/9.
- `PlayerEntryEventCaptureTests` directly exercises the callback send seam: successful sends enqueue once, failed sends enqueue zero, and sink failures do not alter the native send result; Probe suite passed 24/24.

## Advisory review closure

- Review result: 0 Critical, 7 Important repaired, 4 Minor retained.
- Important 1 (empty frontend sections), 2 (ingestion N+1/concurrent key race), 3 (recreate token persistence), 4 (frontend request ordering), 6 (native callback coverage), and the latest recreate orphan/persisted-boundary issue were repaired with the regression tests and fresh runs listed above.
- Important 5 is an environment evidence gap, not a code failure: Docker is not installed, so live PostgreSQL migration/aggregation remains explicitly unverified.
- Minor findings are retained as bounded follow-ups: deleted historical Target Servers are not discoverable in the current selector; uploader discard logs do not include full transport status/retry counters; the frontend does not cancel an in-flight request on unmount; and the standalone `research/SteamLobbyProbe/Program.cs` diagnostic CLI path bypasses the production reporting coordinator. The latter is diagnostic-only and does not represent the Core-managed Agent path. None changes the confirmed metric or compatibility boundary.
- A fresh follow-up reviewer request for the latest boundary repair returned no findings before the reviewer was stopped after repeated timeouts; it is not treated as approval. The handoff review readback and the focused 15/15 endpoint regression remain the evidence for that repair.
- The review is advisory evidence only; it is not an authoritative merge/completion signal.

## Blocked integration evidence

- `dotnet test ... --filter FullyQualifiedName~PostgresPersistenceTests` completed with 0 failures and 3 skipped because Docker is not installed/running in this environment.
- The skipped tests cover PostgreSQL migration/application and database-generated behavior. The SQL translation assertion is direct provider evidence, but it does not prove execution against a live PostgreSQL instance.
- Manual follow-up when Docker is available:
  1. Run `dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --no-restore --filter FullyQualifiedName~PostgresPersistenceTests` and require all 3 tests to pass.
  2. Start the compose stack and verify migration creates `PlayerEntryEvents` and its indexes.
  3. POST one valid batch with an Agent reporting token, repeat it, then query the management endpoint; expect `202`, duplicate count without a second row, and Shanghai trend/daily results.
  4. Run `EXPLAIN` for the statistics range query and confirm the event time/dimension indexes are used for representative data.

## Requirements and compatibility evidence

- Only successful `SendLobbyChatMsg` paths call the event factory; failed sends, invalid/failed ReplyJoinData requests and missing operation context do not create events.
- The native callback's production send branch calls `ReplyJoinDataSendCoordinator`; its direct seam tests cover success, failure and sink-exception behavior without loading a native Steam library.
- Steam callback work remains synchronous and bounded to `TryEnqueue`; reporting network I/O is owned by the Agent hosted uploader.
- Reporting tokens are per-Agent, SHA-256 hashed in Core, and authenticated by a separate scheme. Management bearer authentication remains limited to management endpoints.
- Event records contain no Steam ID. Agent, explicit configured region, Target Server and effective mode are immutable snapshots; unspecified mode is normalized to `versus`, and no configured region is `默认`.
- Event ingestion is idempotent by `EventId`; same-content retries count as duplicates and conflicting content returns 409.
- Event ingestion's normal path loads existing IDs once per batch; a concurrent unique-key race is reconciled by a set-based reload instead of surfacing a 500.
- Existing operation JSON remains compatible because the statistics context is optional and old operations simply do not emit incomplete events.
- Existing Warm-up Agent/Target Server/Steam protocol tests remain in the full solution run.

## Known residual risks and deferred boundaries

- Docker-blocked PostgreSQL execution remains unverified; live migration, Npgsql grouping execution and query plans require the follow-up above.
- The frontend Target Server selector lists current Target Server configuration. A deleted historical Target Server can still be queried if its UUID is known, but it is not discoverable from the current selector; exposing historical server dimension options is deferred because it is outside the confirmed Agent/region/mode request.
- Agent reporting is intentionally best-effort: queue full, timeout, transport/5xx exhaustion and process restart can lose events. A durable local spool is a separate reliability decision.
- No 1280x720 browser screenshot/E2E session was run in this environment; component tests cover loading, empty, error/retry, filters, accessible SVG/table text and App mounting, while final visual spacing remains a manual deployment check.
- A final persistence failure during Agent recreate now removes the replacement container and restores the previous reporting hash before compensating the database; if the compensation save or container cleanup also fails, operational reconciliation remains required.
- The current Target Server selector still cannot discover a deleted historical server, and the page keeps an in-flight fetch after unmount; both are bounded UX/operational follow-ups rather than metric correctness failures.
- The standalone `research/SteamLobbyProbe/Program.cs` remains a diagnostic/native-protocol CLI and does not emit reporting events; production Agent traffic goes through `SteamNativeRuntime` and `ReplyJoinDataSendCoordinator`.

## DriftCheckDraft

- 范围：仍限定为成功 ReplyJoinData 统计、Agent/显式区域/目标模式/大厅/Target Server 筛选、上海时间查询和现用管理页。
- 兼容性：旧操作请求、Steam callback、暖服调度、管理认证和既有页面入口均保留；新增上报是可选旁路。
- 新 owner/fallback：事件 sink/uploader 负责传输，`PlayerEntryEvent` 负责原始事实，`PlayerEntryStatisticsQuery` 负责聚合；InMemory 删除 fallback 仅供测试 provider，不承载生产逻辑。
- 退休轨迹：无旧统计 owner；运行时下载区域仍只是健康显示 fallback，不进入事件；管理 bearer 与 reporting bearer 不合并。未来若采用 durable spool 或长期聚合归档，需另立可靠性/保留策略任务。
- 决策：continue；交接中的 advisory review 已闭合代码层面的 Important 修复，本轮 follow-up reviewer 无 raw readback，因此不增加审查通过声明；不存在超出 Docker 环境缺口的代码层面 needs-verification 漂移。

## Evidence boundary and confidence

- 使用了当前 worktree 的设计/计划、源文件、聚焦测试输出和全量验证输出；大型历史日志、历史会话和无关外部搜索结果未加载。Review 统计沿用交接中已有的 advisory readback；本轮 follow-up reviewer 未返回 raw review payload。
- 直接证据覆盖编译、单元/组件回归、SQL 翻译、前端构建、Compose contract、迁移发现和生产 callback seam；未覆盖 live Docker/PostgreSQL 运行及真实 Steam native ABI 加载。
- 当前置信度：B。核心功能和跨层契约有直接回归证据，Docker 缺失和未做浏览器视觉/E2E 是有界残余风险。
- 本记录是 verified evidence/advisory input，不是更高层的 authoritative completion 信号。
