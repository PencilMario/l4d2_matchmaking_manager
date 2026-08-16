# 原子任务状态

- [x] 添加无副作用、JSON 化的 Probe `health-check` 命令。
- [x] 添加每次实时执行 Probe 的串行 HTTP Agent。
- [x] 添加 Ubuntu Steam Desktop Docker 镜像与持久登录 volume。
- [x] 运行本地回归、构建镜像并部署到授权主机验证。
- [ ] 经 Steam 登录和 AppID 550 Linux API library 配置完成三次真实 Probe 成功检查。

## 检查点（2026-08-16）

- 当前任务：完成真实 Steam 会话下的三次只读 Probe 成功检查。
- 已完成：Probe/Agent 实现与回归、Ubuntu Docker 构建、远端 loopback 部署、未配置库的受控失败验证。
- 证据：`50-evidence.md`；远端容器为 `steam-lobby-agent-steam-lobby-agent-1`。
- 外部阻塞：管理员须在 noVNC 中完成 Steam 登录和 Steam Guard，安装 AppID 550 的 Linux 内容但不启动游戏。
- 下一步：定位 `libsteam_api.so`，写入仅部署目录使用的 `.env`，`docker compose up -d --no-build` 后连续三次调用 `/v1/probe/status`。
- DriftCheckDraft：范围仍限于单账号、只读 Lobby Probe；没有引入游戏客户端、暖服、数据库或公开 API。决定为 `pause-for-user`。
