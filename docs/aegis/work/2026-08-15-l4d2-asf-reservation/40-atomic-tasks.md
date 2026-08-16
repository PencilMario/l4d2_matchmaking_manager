# 原子任务状态

- [x] 提取 L4d2Protocol，并保持 helper 协议向量不变。
- [x] 添加 async reservation client 的 loopback UDP 测试。
- [x] 添加 ASF JSON 配置和单 bot host session 状态测试。
- [x] 添加 raw ClientChatMsg 不附加 NUL 的 encoding 测试。
- [x] 以配置驱动 lifecycle 替换 20 秒 probe。
- [ ] 运行协议与插件的完整本地回归、Release build。
- [ ] 在 24561 上验证真实客户端的 ASF JoinData transport。
- [ ] 在 27083 上验证 cookie reservation 和真实客户端入服。
- [ ] 记录取证，并清理远端暂存部署。
