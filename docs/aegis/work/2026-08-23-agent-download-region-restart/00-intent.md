# Task Intent

## Requested outcome

修改 Warm-up Agent 的 Steam 下载区域时，运行中的 Agent 先更新账号私有的区域目标文件，再重启 Steam，使新区域立即生效；停止中的 Agent 只保存配置，在下次启动时生效。

## Scope

- 增加 Agent 内部的下载区域应用操作。
- 让 Core 在运行中的 Agent 区域实际变化时调用该操作。
- 保留既有 Steam 重启、容器生命周期和账号卷边界。
- 更新内部 API、管理 API 与部署说明。

## Non-goals

- 不重建容器，不删除账号卷，不修改 Steam 登录凭据。
- 不为名称、VNC 或其他 Agent 字段变化增加 Steam 重启。
- 不改变调度器在暖服结束后的既有 Steam 恢复流程。

## Risk hints

- 容器环境变量在运行期间不可变；Agent 必须写入 Millennium 账号级目标文件。
- 将默认区域写入空值时，Bridge 不能回退到旧环境变量目标。
- 远程应用失败时 Core 不应先持久化新区域。
