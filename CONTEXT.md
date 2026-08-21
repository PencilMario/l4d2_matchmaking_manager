# Project Context: L4D1 Matchmaking Manager

这是面向 Left 4 Dead 1（Steam AppID `500`）的专用分支。旧的 `L4d2...` 解决方案、程序集与
namespace 名暂时保留，作为兼容性命名；它们不代表运行时仍支持 AppID 550。

## 运行边界

- Steam Lobby Probe、Agent 和发布目录中的 `steam_appid.txt` 默认且仅面向 AppID 500。
- Agent 使用 Steam Runtime/兼容新版 Steam API 库；L4D1 自带旧 `steam_api.dll` 缺少本项目
  所需 ManualDispatch 与 flat API 导出，不能直接加载。
- 标准 Lobby 使用从真实 L4D1 公共 Lobby 捕获的 `Farm`/`coop` 元数据模板；默认 4 个槽位。
- `versus` 可选择 8 个槽位，但服务器连接与 ReplyJoinData 仍需要目标环境真机验收。
- L4D2 reservation 常量和握手没有被冒充为 L4D1 实现。新建或启用
  `requiresReservation=true` 会返回 `l4d1_reservation_not_supported`。
- A2S 必须返回 AppID 500；其他游戏或不可识别的服务器不会进入暖服调度。

## 术语

| Term | Definition |
| --- | --- |
| Core Controller | 管理目标服务器、Agent、A2S 观察和 Lobby 生命周期的控制服务。 |
| Warm-up Agent | 持有一个独立 Steam 账号、创建并保持 L4D1 Lobby 的容器。 |
| Target Server | AppID 500 的 L4D1 dedicated server，以主机名/IPv4 和游戏端口标识。 |
| Shared Game Library | 所有 Agent 共用的 AppID 500 游戏内容和 Steam runtime，不保存账号凭据。 |
| Account Data Volume | 单个 Agent 独有的 Steam 登录、Steam Guard、userdata 与配置。 |
| Player Target | A2S 玩家数达到后停止暖服的阈值，默认 4。 |

`docs/aegis/work` 中的 L4D2 文档是上游研究历史与负面证据，不是本分支当前部署说明。
