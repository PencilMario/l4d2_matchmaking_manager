# Global Steam Proxy Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use aegis:subagent-driven-development or aegis:executing-plans to implement this plan task-by-task.

**Goal:** Add a Core-wide Steam proxy setting and global settings tab, with proxy variables applied only to VNC-enabled Warm-up Agents.

**Architecture:** Store one nullable proxy URI in a singleton database settings row. A settings service owns normalization and the authenticated `/v1/settings` contract. `WarmupAgentContainerService` reads the service when building a container and adds proxy variables only when the agent's VNC flag is true. The frontend loads/saves the setting through its existing API client and adds a settings workspace view.

**Tech Stack:** ASP.NET Core minimal APIs, EF Core/PostgreSQL, Docker.DotNet, React, TypeScript, Vitest/Testing Library, xUnit.

**Baseline / Authority Refs:** `CONTEXT.md`; `docs/frontend-redesign-spec.md`; existing `WarmupAgent*`, `MatchmakingDbContext`, `AgentContainerOptions`, `WorkspaceNav`, `WorkspaceShell`, and frontend API/state files.

**Compatibility Boundary:** Do not change existing routes or authentication. Preserve VNC fields and container ports. Empty/global-disabled proxy produces no proxy variables. Global changes do not rebuild existing containers.

**Verification:** targeted xUnit tests for normalization, endpoint persistence, and container environment; targeted Vitest tests for settings form and navigation; full .NET and frontend test suites where the concurrent worktree permits.

## Tasks

1. Add the singleton settings entity, EF model/migration, service, DTOs, and authenticated GET/PUT endpoint. Write failing tests first for empty values, valid URI acceptance, invalid URI rejection, and persistence.
2. Add the settings dependency to agent container creation. Write failing tests asserting a VNC-enabled agent receives all six proxy variable spellings and a disabled-VNC agent receives none. Keep credentials out of response DTOs and logs.
3. Add frontend settings types/API/state and a “全局设置” navigation view. Write failing component tests for loading, editing, saving, empty proxy, and the VNC-only application note. Preserve current agent form and VNC behavior.
4. Run targeted tests, then regression suites, TypeScript build, and inspect the final diff for changes outside the feature boundary. Do not commit while concurrent agents have uncommitted changes.
