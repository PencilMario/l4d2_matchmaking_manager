# Gemini Frontend Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use aegis:executing-plans to implement this plan task-by-task.

**Goal:** Replace the current frontend presentation with the approved Gemini design archive while preserving the repository's real Core API contract and polling behavior.

**Architecture:** Keep `CoreClient` and `useCoreSnapshot` as the data owners. Add display adapters and shared status/error mapping for the design components, then transplant the archive's sidebar, header, views, drawers, dialogs, and utility components into the existing Vite/React application. The API boundary remains `/healthz` and `/v1/*` with Bearer authentication.

**Tech Stack:** React 19, TypeScript, Vite, lucide-react, Vitest, Testing Library, Playwright, CSS modules by project stylesheet layers.

**Baseline / Authority Refs:** `CONTEXT.md`, `docs/frontend-redesign-spec.md`, `frontend/src/api/core-client.ts`, `frontend/src/api/models.ts`, `frontend/src/state/useCoreSnapshot.ts`, and the approved archive `D:\Windows\Download\l4d2-匹配管理.zip`.

**Compatibility Boundary:** Do not change API paths, request verbs, JSON fields, Bearer auth, 401 handling, or five-second polling. Do not modify backend projects. Preserve the user's untracked `docs/frontend-redesign-spec.md`.

**Verification:** `npm run build`; `npm test -- --run`; `npm run test:e2e`; inspect 1280x720, 1440x900, and 1920x1080 Playwright screenshots for overflow and overlap; `rg` must find no Three.js imports or forbidden English UI labels.

---

### Task 1: Establish the design-to-Core display boundary

**Files:**
- Create: `frontend/src/state/display.ts` and `frontend/src/state/core-errors.ts` updates as needed.
- Modify: `frontend/src/api/models.ts`, `frontend/src/api/core-client.ts` only where the current contract needs missing operations already supported by the backend.
- Test: `frontend/src/api/core-client.test.ts`, `frontend/src/state/useCoreSnapshot.test.tsx`.

**Why this task exists:** The archive's UI models do not match the repository's actual DTOs. A single adapter/mapping owner prevents raw API statuses from leaking into JSX and preserves refresh semantics.

**Impact / Compatibility:** Current API methods and response models remain canonical. Mapping supplies display names, counts, permissions, time/date formatting, stale/uncertain labels, and 409 error text. No optimistic removal is allowed for failed destructive operations.

**Verification:** Existing API and snapshot tests pass; a 401 clears the session; independent snapshot requests still run through `Promise.all`; a failed refresh keeps the previous data.

### Task 2: Transplant shared shell and login/loading surfaces

**Files:**
- Modify: `frontend/src/App.tsx`, `frontend/src/main.tsx`.
- Replace: `frontend/src/features/workspace/WorkspaceShell.tsx`, `WorkspaceNav.tsx`, `CoreStatusBar.tsx`.
- Create/modify: `frontend/src/components/common/*` and `frontend/src/styles/*`.
- Test: `frontend/src/App.test.tsx`.

**Why this task exists:** The archive's fixed sidebar, header, login, and loading views define the visual identity of every route.

**Impact / Compatibility:** Keep token input validation, unauthorized callback, first-load gating, manual refresh, sign-out, and persistent data during polling. Retire the old shell markup and styles after the archive shell is integrated.

**Verification:** Login submit is disabled for empty input; loading text is shown before the first complete snapshot; refresh does not blank content; 401 returns to login; keyboard focus and Esc behavior remain usable.

### Task 3: Transplant overview, server, agent, and lobby views

**Files:**
- Replace: `frontend/src/features/servers/*`, `frontend/src/features/agents/*`, `frontend/src/features/warmups/*`, `frontend/src/features/lobbies/*`.
- Modify: `frontend/src/features/workspace/WorkspaceShell.tsx` and display adapters.
- Test: existing feature tests plus focused tests for server 409 handling, quarantined-agent actions, stale lobby results, and incomplete members.

**Why this task exists:** These are the user-facing workflows covered by the approved design: overview summaries and warmups, server table/drawer, agent table/modal, and lobby lookup.

**Impact / Compatibility:** Wire archive controls to `CoreClient` methods. Use existing PUT/POST/DELETE capabilities where no dedicated toggle/rebuild endpoint exists. Preserve drawer form values and table rows on `target_server_drain_failed`; quarantined agents cannot start or recreate; lobby failures keep prior results and mark them stale.

**Verification:** Create/edit/delete server, enable/disable through update, agent create/edit/start/stop/recreate/delete, and lobby success/failure paths pass tests and manual interaction checks.

### Task 4: Replace styles and validate responsive behavior

**Files:**
- Modify: `frontend/src/styles/tokens.css`, `layout.css`, `tables.css`, `forms.css`, `feedback.css`, `global.css`.
- Modify: `frontend/index.html` for document language/title if needed.
- Test: `frontend/e2e/management-workspace.spec.ts`.

**Why this task exists:** The archive's Tailwind-first appearance must be reproduced inside the repository's stylesheet organization without changing application behavior.

**Impact / Compatibility:** Keep the project's CSS layer split. Use lucide icons, Chinese accessible labels/tooltips, compact desktop-first layout, reduced-motion support, and no rounded cards nested inside page sections. Retire obsolete visual-stage/Three.js references if present.

**Verification:** Build, unit tests, and E2E tests pass. Screenshots at all three required widths have no horizontal overflow, clipped text, overlap, or layout shift. `rg -i "three|postprocessing|Target Server Registry|RESOURCE WORKSPACE|CORE API TOKEN|LAST SYNC|OWNER|MEMBERS|DATA|KEY|VALUE" frontend` returns no runtime/design violations except API field names in type/model code where necessary.

### Task 5: Final review and evidence

**Files:**
- Create: `docs/aegis/work/2026-08-19-gemini-frontend-migration/50-evidence.md`.

**Why this task exists:** Completion requires evidence that the design was transplanted without breaking the API compatibility boundary.

**Verification:** Record exact build/test/E2E commands, relevant output, screenshot paths, and remaining residual risks. Review `git diff --stat`, `git status --short`, and changed files for unrelated edits.

