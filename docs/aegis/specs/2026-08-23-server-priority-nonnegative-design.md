# 调度优先级非负约束设计

## 目标

调整目标服务器的调度优先级规则：优先级必须是大于等于 0 的整数。新建和更新请求提交负数时直接拒绝，不自动转换为 0。

## 方案

在 `TargetServerService.Normalize` 这个现有配置归一化入口统一校验优先级。创建和更新路径都会经过该入口，因此负数统一抛出现有的 `invalid_target_server_configuration` 参数错误，并由现有 HTTP 端点转换为 `400 Bad Request`。

前端 `TargetServerForm` 的优先级输入增加 `min={0}`，并在提交校验中要求整数且不小于 0。错误信息明确说明“必须为大于等于 0 的整数”；帮助文案不再宣称允许负数。

## 边界与兼容性

- `priority` 为 `null` 时继续使用默认值 0。
- 0 和正整数的现有排序、调度和 API 返回语义不变。
- 不新增或执行数据库 migration；当前实例已有数据最低为 0，历史负数数据不在本次范围内。
- 后端错误码仍为 `invalid_target_server_configuration`，不新增 API 错误契约。
- 不改变调度器的优先级排序算法或同优先级轮询逻辑。

## 验证

- Core API 测试验证创建和更新负数返回 400，且合法的 0 仍可保存。
- 前端表单测试验证负数提交被拦截、不会调用提交回调，并验证输入的最小值属性及合法值提交。
- 运行 Core 目标服务器端点测试和前端表单测试，并运行前端构建。

## 工作草案

- **TaskIntentDraft:** 收紧 Server Priority 输入域，保护调度配置一致性；范围为 Core API、管理前端、术语/表单文案和回归测试。
- **BaselineReadSetHint:** `CONTEXT.md`、`TargetServerService.cs`、`TargetServerEndpoints.cs`、`TargetServerForm.tsx`、`TargetServerEndpointTests.cs` 及前端测试配置。
- **ImpactStatementDraft:** 配置写入层和管理表单受影响；调度排序、数据库模型、已有 0 及正数数据保持不变；不做数据迁移。
