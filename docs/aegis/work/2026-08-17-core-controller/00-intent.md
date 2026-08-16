# 单主机暖服核心控制器：任务意图

## 请求结果

在 Docker 中交付管理 L4D2 服务器和数十至数百单账号 Steam Agent 的核心控制服务，并扩展 Agent 为持久 Steam 会话控制器。核心通过受认证 REST API 管理配置、调度暖服和代理只读大厅查询。

## 已确认范围

- 单 Docker 主机、PostgreSQL、Docker Engine API 与内部 Agent HTTP 合约。
- 预留服务器独占、空服预检、120 秒首位玩家等待、30 秒成员空窗、12 分钟尝试窗口、A2S 人数目标、优先级和非预留并发上限。
- Agent 每次创建大厅独立随机 C1-C14 完整 campaign metadata profile。
- 共享 L4D2/Steam runtime/API 库，独立账号登录卷，AppID 550 禁止常规自动更新。
- 对外 lobby 查询由 Agent 的任意状态读取能力提供，核心只认证并转发。

## 明确非目标

Web UI、跨主机编排、RCON、服务器侧 reservation lease 双重验证、公开 Agent 控制端口与公开 noVNC。
