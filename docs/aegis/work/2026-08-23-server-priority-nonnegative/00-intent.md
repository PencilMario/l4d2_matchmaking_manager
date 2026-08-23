# Task Intent

## Requested Outcome

调度优先级不可为负数。后端创建/更新直接拒绝负数，前端不再允许负数输入。

## Scope

- Core 目标服务器配置归一化和 HTTP 契约测试。
- 管理前端目标服务器表单的输入限制、校验和测试。
- `CONTEXT.md` 与现有前端帮助文案。

## Non-goals

- 不新增数据库 migration。
- 不修改现有数据库数据。
- 不修改调度器排序、轮询或并发逻辑。
- 不改变优先级为 `null` 时默认值 0 的行为。

## Acceptance Criteria

1. Core 创建接口收到负数优先级时返回 `400` 和 `invalid_target_server_configuration`。
2. Core 更新接口收到负数优先级时返回 `400` 和 `invalid_target_server_configuration`。
3. 前端优先级输入声明最小值为 0，提交负数时显示错误且不调用提交回调。
4. 优先级 0 仍能正常提交和保存。
