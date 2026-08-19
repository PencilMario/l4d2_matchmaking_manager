# 大厅成员 Steam 资料解析实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use aegis:executing-plans to implement this plan task-by-task.

**Goal:** 通过 Core 后端使用 Steam Web API 为大厅成员补全公开昵称和头像，并在全局设置中安全管理 API Key。

**Architecture:** Core 保存加密后的 Steam Web API Key，并在大厅查询成功取得完整成员后，由独立的 `SteamProfileService` 批量请求 `GetPlayerSummaries`。查询失败只返回原始成员 ID；成员契约增加可空 `AvatarUrl`，前端负责头像和默认图标展示。

**Tech Stack:** .NET 10, ASP.NET Minimal API, EF Core/PostgreSQL, `HttpClient`, AES-GCM, React 19, Vitest, Testing Library。

**Baseline / Authority Refs:** `docs/aegis/specs/2026-08-19-lobby-member-profiles-design.md`; `CONTEXT.md`; `GlobalSettingsService`; `RconCredentialProtector`; `LobbyQueryService`; `LobbyMemberSnapshot`。

**Compatibility Boundary:** Agent HTTP API、Steamworks 成员枚举、现有大厅查询错误语义保持不变。未配置 Key 或 Steam API 失败时，大厅查询仍成功并返回 Steam64 ID，昵称和头像为空；Key 不得出现在任何 GET 设置响应或前端状态中。

**Verification:** 运行 Core 测试、Steam Web API service 测试、契约测试、前端完整 Vitest 测试和生产构建；使用 `git diff --check` 检查副作用。

---

### Task 1: 扩展成员契约与加密设置

**Files:**
- Modify: `src/L4d2Matchmaking.Contracts/AgentContracts.cs`
- Modify: `src/L4d2MatchmakingCore/Data/Entities.cs`
- Modify: `src/L4d2MatchmakingCore/Data/MatchmakingDbContext.cs`
- Modify: `src/L4d2MatchmakingCore/Settings/GlobalSettingsDtos.cs`
- Modify: `src/L4d2MatchmakingCore/Settings/GlobalSettingsService.cs`
- Modify: `src/L4d2MatchmakingCore/Settings/GlobalSettingsEndpoints.cs`
- Modify: `src/L4d2MatchmakingCore/Servers/RconCredentialProtector.cs`
- Create: `src/L4d2MatchmakingCore/Data/Migrations/202608190002_SteamWebApiKey.cs`
- Test: `src/L4d2MatchmakingCore/tests/GlobalSettingsEndpointTests.cs`
- Test: `src/L4d2Matchmaking.Contracts/tests/LobbySnapshotTests.cs`

**Why this task exists:** Key 必须能由全局设置配置，但不能明文存储或返回；成员响应需要携带头像地址且兼容旧数据。

**Impact / Compatibility:** `LobbyMemberSnapshot` 的新增 `string? AvatarUrl` 放在末尾并提供默认值，旧构造调用保持编译和 JSON 兼容。设置响应新增 `SteamWebApiKeyConfigured`，保留 `SteamProxyUrl`。数据库新增可空密文字段。

**Verification:** 设置接口测试覆盖设置 Key 后 GET 只返回 configured 标记、数据库字段不是明文、清除 Key 后标记为 false；契约测试覆盖旧构造和带头像 JSON。

- [ ] **Step 1: Write failing tests**

在 `GlobalSettingsEndpointTests` 增加：PUT `{ steamWebApiKey: "test-key" }` 后 GET 返回 `steamWebApiKeyConfigured=true` 且响应文本不含 `test-key`；PUT `{ clearSteamWebApiKey: true }` 后返回 false。测试工厂提供固定 32-byte `CORE_RCON_ENCRYPTION_KEY`。

在 `LobbySnapshotTests` 增加带 `AvatarUrl` 的成员序列化/反序列化断言，并保留旧的两参数成员构造。

- [ ] **Step 2: Run tests and verify RED**

Run:

```powershell
dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter GlobalSettingsEndpointTests
dotnet test src/L4d2Matchmaking.Contracts/tests/L4d2Matchmaking.Contracts.Tests.csproj --filter LobbySnapshotTests
```

Expected: 编译或断言失败，因为 DTO、数据库字段和成员头像字段尚未存在。

- [ ] **Step 3: Implement minimal contract and storage changes**

将成员记录改为：

```csharp
public sealed record LobbyMemberSnapshot(string SteamId, string? PersonaName, string? AvatarUrl = null);
```

扩展设置请求为 `SteamWebApiKey` 与 `ClearSteamWebApiKey`，响应增加 `SteamWebApiKeyConfigured`；实体增加 `SteamWebApiKeyCiphertext`，数据库字段最大长度 2048。为 `RconCredentialProtector` 抽取可复用的 `Protect`/`Unprotect` 语义到通用凭据保护接口，错误码保留 RCON 兼容行为。

服务更新逻辑：新 Key 非空时加密替换；clear 为 true 时置空；两者同时出现时 clear 优先级明确为拒绝请求。GET 仅通过 ciphertext 是否为空计算 configured。新增 EF migration。

- [ ] **Step 4: Run tests and verify GREEN**

Run the two commands above. Expected: all targeted tests pass and response body does not contain the API Key.

- [ ] **Step 5: Commit**

```powershell
git add src/L4d2Matchmaking.Contracts src/L4d2MatchmakingCore
git commit -m "feat(settings): 安全保存 Steam Web API Key"
```

### Task 2: 实现 Steam Web API 资料解析服务

**Files:**
- Create: `src/L4d2MatchmakingCore/Profiles/SteamProfileService.cs`
- Create: `src/L4d2MatchmakingCore/Profiles/SteamProfileModels.cs`
- Modify: `src/L4d2MatchmakingCore/Program.cs`
- Modify: `src/L4d2MatchmakingCore/Settings/GlobalSettingsService.cs`
- Test: `src/L4d2MatchmakingCore/tests/SteamProfileServiceTests.cs`

**Why this task exists:** 非好友成员不能依赖 Steamworks 好友接口，必须通过 Steam Web API 获取公开资料；外部服务故障不应破坏大厅查询。

**Impact / Compatibility:** 服务只读取 Core 设置并使用注入的 `HttpClient`; 不把 API Key 放入日志、响应或 URL 以外的地方。Steam Web API 请求最多 100 个 Steam64 ID，外部错误统一转为空资料而非抛出到大厅查询。

**Verification:** 使用自定义 `HttpMessageHandler` 验证 URL、Key、批量参数、资料映射、成功缓存和失败回退；测试超时和非 200 响应不会抛出到调用方。

- [ ] **Step 1: Write failing tests**

创建 `SteamProfileServiceTests`，以 fake settings provider 和 fake HTTP handler 构造服务，先写以下断言：两个 ID 被一次请求；响应中的 `personaname` 和 `avatarmedium` 映射到结果；第二次相同查询不增加请求数；HTTP 500 返回两个空资料且不抛异常；超过 100 个 ID 分成多次请求。

- [ ] **Step 2: Run tests and verify RED**

```powershell
dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter SteamProfileServiceTests
```

Expected: 编译失败，因为 service 尚不存在。

- [ ] **Step 3: Implement minimal service**

实现 `ISteamProfileService.ResolveAsync(IReadOnlyCollection<string>, CancellationToken)`：从设置服务取得解密后的 Key；无 Key 或无 ID 返回空映射；按 100 个 ID 建立 `https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key=...&steamids=...&format=json` 请求；使用 `JsonDocument` 或强类型 DTO 读取 `response.players`。成功缓存 15 分钟，失败缓存 1 分钟；异常、取消以外的 HTTP/JSON 错误返回空映射。

在 `Program.cs` 注册 typed `HttpClient`，连接超时设为 5 秒；向设置服务增加仅供服务内部使用的解密读取方法，不改变公开 DTO。

- [ ] **Step 4: Run tests and verify GREEN**

```powershell
dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter SteamProfileServiceTests
```

Expected: all service tests pass, including cache and 100-ID batching.

- [ ] **Step 5: Commit**

```powershell
git add src/L4d2MatchmakingCore/Profiles src/L4d2MatchmakingCore/Program.cs src/L4d2MatchmakingCore/Settings
git commit -m "feat(lobbies): 增加 Steam 成员资料解析"
```

### Task 3: 合并资料到大厅查询结果

**Files:**
- Modify: `src/L4d2MatchmakingCore/Lobbies/LobbyQueryService.cs`
- Modify: `src/L4d2MatchmakingCore/tests/LobbyQueryEndpointTests.cs`
- Modify: `src/L4d2LobbyAgent/Steam/AgentSteamSessionService.cs` only if compile-time mapping requires no behavior change

**Why this task exists:** 将 Agent 已发现的 Steam64 ID 与 Core 外部资料解析结果合并，保持大厅查询主流程稳定。

**Impact / Compatibility:** 仅当 Agent 返回 `memberDataStatus=complete` 时补全；owner 也作为成员资料查询 ID；外部资料服务失败时返回原始快照。Agent API 不变。

**Verification:** LobbyQueryEndpointTests 使用 fake profile service 验证成员带头像、重复 ID 去重、资料失败仍 200；metadata-only 结果不触发 profile service。

- [ ] **Step 1: Write failing tests**

扩展 endpoint fake dependencies，写完整成员查询返回 `LobbyMemberSnapshot("765611...", null)` 并让 profile service 返回昵称和头像，断言 HTTP JSON 有对应字段；再写 profile service 抛出/返回空时 HTTP 仍成功；metadata-only 查询断言调用次数为 0。

- [ ] **Step 2: Run targeted tests and verify RED**

```powershell
dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter LobbyQueryEndpointTests
```

Expected: 编译失败或头像字段断言失败。

- [ ] **Step 3: Implement minimal merge**

给 `LobbyQueryService` 注入 `ISteamProfileService`。`TryQueryAsync` 获得 snapshot 后，若状态为 complete，收集成员 ID，调用 resolver，并用 `with` 创建新成员列表：资料存在则替换 `PersonaName`/`AvatarUrl`，不存在保留原昵称和空头像。resolver 内部异常需由 service 转为空映射，避免吞掉 Agent 自身的查询异常。

- [ ] **Step 4: Run tests and verify GREEN**

```powershell
dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj --filter LobbyQueryEndpointTests
```

Expected: existing and new endpoint tests pass.

- [ ] **Step 5: Commit**

```powershell
git add src/L4d2MatchmakingCore/Lobbies src/L4d2MatchmakingCore/tests
git commit -m "feat(lobbies): 合并成员 Steam 昵称和头像"
```

### Task 4: 全局设置和大厅成员前端展示

**Files:**
- Modify: `frontend/src/api/models.ts`
- Modify: `frontend/src/api/core-client.ts`
- Modify: `frontend/src/features/settings/SettingsWorkspace.tsx`
- Modify: `frontend/src/features/lobbies/LobbyQueryView.tsx`
- Modify: `frontend/src/features/lobbies/LobbyQueryView.test.tsx`
- Create: `frontend/src/features/settings/SettingsWorkspace.test.tsx`

**Why this task exists:** 用户需要在现有全局设置页管理 Key，并在大厅查询成员列表直接看到头像与昵称。

**Impact / Compatibility:** Key 输入为 password，页面只显示 configured 状态；成员无头像时必须稳定显示默认图标，不能造成布局跳动或破坏现有 ID 展示。

**Verification:** 前端测试覆盖设置加载/保存/清除和成员头像/默认图标；`npm test -- --run` 与 `npm run build` 全量通过。

- [ ] **Step 1: Write failing tests**

扩展 settings 测试：加载 configured=true 时显示“已配置”而不显示 Key，提交新 Key 传给 API，清除按钮传 `clearSteamWebApiKey=true`。扩展大厅查询测试：返回 `avatarUrl` 时渲染头像 `alt` 文本，空值时渲染默认用户图标且仍显示 Steam64 ID。

- [ ] **Step 2: Run tests and verify RED**

```powershell
npm test -- --run src/features/settings/SettingsWorkspace.test.tsx src/features/lobbies/LobbyQueryView.test.tsx
```

Expected: 新断言失败，因为 API 类型、设置控件和成员头像尚不存在。

- [ ] **Step 3: Implement minimal UI and API types**

扩展 `GlobalSettings` 与 update payload；设置组件使用 `type=password` 和 configured 状态，清除操作调用专用 payload。成员行使用固定 32px 容器，头像 URL 使用 `img`，无 URL 使用 lucide `User` 图标；保留昵称、Steam64 ID 和未知玩家回退。

- [ ] **Step 4: Run tests and build**

```powershell
npm test -- --run src/features/settings/SettingsWorkspace.test.tsx src/features/lobbies/LobbyQueryView.test.tsx
npm test -- --run
npm run build
```

Expected: targeted and full frontend tests pass, Vite production build exits 0.

- [ ] **Step 5: Commit**

```powershell
git add frontend/src
git commit -m "feat(frontend): 展示成员 Steam 头像和昵称"
```

### Task 5: 集成验证与分支收尾

**Files:**
- Test: all Core and frontend tests
- Inspect: all changed files and migration

**Why this task exists:** 该功能跨设置、数据库、外部网络和用户界面，需要确认旧查询路径和密钥边界没有回归。

**Verification:**

- [ ] **Step 1: Run Core full suite**

```powershell
dotnet test src/L4d2MatchmakingCore/tests/L4d2MatchmakingCore.Tests.csproj
```

- [ ] **Step 2: Run contracts and related Agent suites**

```powershell
dotnet test src/L4d2Matchmaking.Contracts/tests/L4d2Matchmaking.Contracts.Tests.csproj
dotnet test src/L4d2LobbyAgent/tests/L4d2LobbyAgent.Tests.csproj
```

- [ ] **Step 3: Run frontend suite and build**

```powershell
npm test -- --run
npm run build
```

- [ ] **Step 4: Inspect security and compatibility**

确认 `git diff --check` 通过；确认 GET `/v1/settings` 和所有日志不包含 Steam API Key；确认未配置 Key、Steam API 失败和资料私有时大厅查询仍返回成员 Steam64 ID。

- [ ] **Step 5: Commit verification evidence**

```powershell
git add docs/aegis/work/2026-08-19-lobby-member-profiles
git commit -m "test(lobbies): 完成成员资料解析回归验证"
```

**Repair Track:** 修复当前大厅成员只有 Steam64 ID、Core 无资料补全入口的问题；canonical owner 是 Core 的设置服务、SteamProfileService 和 LobbyQueryService 合并边界。

**Retirement Track:** 旧的 `PersonaName=null` 回退仍保留，作为未配置、私有资料和外部失败时的兼容路径；Steamworks 好友名称读取不会新增，也没有旧外部解析分支需要删除。未来若外部资料服务稳定性和隐私策略改变，再评估是否缩短回退缓存或增加主动刷新。

**Residual Risk:** 未执行真实 Steam Web API 生产请求，不验证具体账号隐私状态、API 配额和线上头像 CDN 可用性；自动化测试使用 fake HTTP handler 覆盖协议和失败边界。
