# Approved Design

后端在 `TargetServerService.Normalize` 中把 `priority ?? 0` 归一化为有效优先级，并在结果写入前拒绝小于 0 的值。该方法由创建和更新共同调用，所以两条写入路径保持一致；现有端点错误映射继续返回 `400 invalid_target_server_configuration`。

前端 `TargetServerForm` 为优先级数字输入增加 `min={0}`，提交校验由“整数”收紧为“整数且 >= 0”，并更新帮助和错误文案。合法值 0 和正整数维持原有提交形态。

数据库不做迁移或清洗。项目当前实例数据最低为 0，历史负数兼容处理不是本任务的一部分。
