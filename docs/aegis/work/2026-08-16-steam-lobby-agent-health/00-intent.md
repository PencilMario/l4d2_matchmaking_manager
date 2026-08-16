# 任务意图

实现一台 Ubuntu Docker Steam Lobby Agent。它保留一个 Steam Desktop 账号的登录数据，并在每次 HTTP 请求时
无副作用地运行 `SteamLobbyProbe health-check`，实时报告 Steam API、AppID 550、登录态和大厅列表回调是否正常。

不实现暖服、reservation、L4D2 游戏客户端、核心控制面、数据库、NATS 或第三方公开 API。
