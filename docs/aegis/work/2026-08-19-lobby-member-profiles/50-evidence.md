# 验证证据

日期：2026-08-19

## 自动化结果

- `frontend`: `npm test -- --run`，13 个测试文件、27 个测试通过。
- `frontend`: `npm run build`，TypeScript 检查和 Vite 生产构建退出码 0。
- `.NET`: `dotnet test L4d2MatchmakingManager.sln --no-restore`，Contracts 2/2、Core 111 通过且 3 个既有测试跳过、Lobby Agent 16/16，以及研究项目测试全部通过。
- `git diff --check`：通过。

## 覆盖范围

- 全局设置支持保存、替换和清除 Steam Web API Key；页面只显示是否已配置，输入框为密码类型且保存成功后清空。
- 大厅成员保留 Steam64 ID，并在存在头像时显示头像；无头像时显示固定尺寸默认用户图标。
- Steam API 无 Key、HTTP/JSON/超时失败和私有资料均保留大厅查询成功语义。
- Steam Web API Key 使用现有 AES-GCM 保护器加密保存，不进入设置 GET 响应或前端状态。

## 残余风险

- 未执行真实 Steam Web API 生产请求，未验证线上 API 配额、账号隐私状态和头像 CDN 可用性。
- 浏览器实际截图和线上部署环境未验证；自动化 UI 测试覆盖实际 `App` 使用的组件行为。

## 信心边界

本证据为直接自动化验证，支持 B 级信心：核心功能和回归路径均有测试，但真实外部 Steam 服务和部署环境仍未验证。
