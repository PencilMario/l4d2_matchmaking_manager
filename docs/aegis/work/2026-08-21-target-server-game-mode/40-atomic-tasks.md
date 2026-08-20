# 原子任务与检查点

## TodoCheckpointDraft

- 当前目标：为目标服务器增加可选 `gameMode` 预设，并把 `coop` metadata 传到暖服 Agent。
- 已完成：Core nullable 持久化/校验/API、Scheduler 与 Agent contract 传递、Steam metadata 覆盖、两套前端表单/详情、文档、回归测试。
- 当前切片：最终验证与证据整理。
- 下一步：保留工作区无关改动，提交验证结果给用户；不执行 commit/push。
- 阻塞项：无自动化阻塞；真实 Steam runtime 与生产数据库升级未在本机执行。

## 原子任务状态

| 任务 | 状态 | 证据 |
| --- | --- | --- |
| Core TargetServer 字段、DTO、规范化和 nullable migration | completed | `TargetServerEndpointTests` 11/11；Core 全量 143 passed/3 skipped |
| Scheduler → AgentOperationRequest.GameMode | completed | `WarmupSchedulerServiceTests` 全量包含 coop 传递断言；Core 全量通过 |
| Agent metadata `coop=4/coop/19`，默认/versus 保持 `8/versus/35` | completed | L4d2Protocol 13/13；SteamLobbyProbe 15/15 |
| active App 与 workspace 前端 select、回填和 payload | completed | 目标 Vitest 2/2；全量 Vitest 53/53；build 通过 |
| API 文档与 Aegis 记录 | completed | 三份 API 文档、索引和本目录已更新 |
| 解决方案级回归验证 | completed | solution build 0 warning/0 error；solution test 全项目 0 failure |

## DriftCheckDraft

- 范围：仍只改变目标服务器模式数据流；未新增自定义 metadata 编辑入口。
- 兼容性：数据库列/API/Agent 字段均可空；旧 positional constructors 和默认 versus metadata 保留。
- Owner：规范化由 Core；metadata 由 `RealSessionSettings`；reservation binary payload 未改动。
- Retirement track：没有删除旧 versus fallback；未来只有在所有旧 Agent 消失后才可考虑移除兼容默认分支。
- 决策：`continue` → 验证证据已齐，进入用户交付；不宣称真实 Steam/生产运行时已验收。
