# 任务意图

## 目标

复现 L4D2 真实客户端的 UDP reservation 握手，使授权测试服务器
`202.105.108.88:27084` 接受 lobby cookie `0x0186000047CF0FD8`，并提供可重复运行的
诊断命令、自动化协议测试和中文文档。

## 范围

- 在 `research/SteamLobbyProbe` 中增加不依赖 Steam API 的 reservation 协议模块。
- 实现 challenge 请求、challenge 回包解析、ICE level 1 加密、reservation 请求和状态回包解析。
- 先发送 `settingsLength=0` 的 16 字节最小明文 payload。
- 仅在服务器 `0 humans` 且 `unreserved` 时执行 live reservation。
- 用 RCON `status` 在请求前后核对服务器持有的 cookie。

## 非目标

- 本阶段不生成 binary KeyValues game settings。
- 不修改游戏服务器配置、插件或地图。
- 不改现有 Steam lobby metadata、owner、JoinData 或 MatchFramework 探测路径。
- RCON 密码不写入项目文件、日志或命令行接口。

## 成功标准

- 固定 ICE 测试向量逐字节匹配独立 Python ICE 实现。
- live 命令收到 exact L4D2 `S2A_RESERVE_RESPONSE`，或在未收到回包时明确要求用服务器状态外部验证，
  不把超时误判为拒绝。
- RCON `status` 显示 `(reserved 186000047cf0fd8)`。
