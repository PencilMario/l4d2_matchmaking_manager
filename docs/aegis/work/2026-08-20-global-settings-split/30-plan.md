# Global Settings Split Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use aegis:subagent-driven-development (recommended) or aegis:executing-plans to implement this plan task-by-task.

**Goal:** 让 VNC 代理和 Steam Web API Key 成为互不覆盖的独立全局设置。

**Architecture:** Core 在现有组合设置路由之外增加两个认证的专用资源路由：`/v1/settings/vnc-proxy` 和 `/v1/settings/steam-web-api-key`。两个资源仍使用同一条 `CoreSettings` 记录，但每个更新方法只修改自己的列；前端保留组合 GET 用于初始化，两个表单分别调用专用 PUT。

**Tech Stack:** ASP.NET Core Minimal API、EF Core/PostgreSQL、MSTest、React 19、TypeScript、Vitest。

**Baseline / Authority Refs:** `docs/matchmaking-core-frontend-api.md`、`docs/matchmaking-core-api.md`、`CONTEXT.md`、本任务的 `00-intent.md` 与 `10-baseline-readset.md`。

**Compatibility Boundary:** 旧 `/v1/settings` GET/PUT、数据库列、密钥加密格式、Agent 代理环境变量注入保持可用；任何读取接口都不得返回密钥明文。

**Verification:** 后端设置端点测试、前端组件测试、CoreClient API 测试、`dotnet test`、`npm test`、`npm run build`、`git diff --check`。

## Repair Track

- 根因：组合设置请求同时承载两个独立配置，前端保存单项时仍提交另一项的当前/空值。
- canonical owner：`GlobalSettingsService` 的分字段更新方法和两个专用端点；前端两个表单的专用 API 回调。
- 最小修复：新增专用 GET/PUT 资源，不改数据库结构；两个前端表单停止调用组合 PUT。

## Retirement Track

- 旧 `/v1/settings` 组合接口保留给旧客户端，不再作为新前端单项保存的主路径。
- 旧组合接口只有在旧客户端兼容期内继续保留；未来所有客户端迁移到专用资源后，才评估删除组合 PUT。

### Task 1: 后端独立设置资源

先新增失败的 endpoint 测试，验证代理与 API Key 可分别写入且互不清除；再增加 DTO、service 方法和路由。

### Task 2: 前端专用 API 与表单

先修改组件测试，验证两个保存按钮只调用对应回调；再更新 `ApiService`、`CoreClient`、两套设置组件及工作台 wiring。

### Task 3: 文档与回归

补充 API 契约、运行全部相关测试和前端构建，记录残余兼容风险。
