# Task Intent

## TaskIntentDraft

- Outcome: add a Core-wide Steam proxy setting and a global-settings tab.
- Scope: persist and validate one proxy URI; expose authenticated GET/PUT settings; inject proxy variables only into Warm-up Agent containers whose VNC service is enabled.
- Non-goals: per-agent proxy values, automatic restart/rebuild after global setting changes, proxying Core itself, or exposing proxy credentials in responses.
- Risk: the shared worktree contains concurrent frontend and Core changes; preserve all unrelated edits.

## BaselineReadSetHint

- `src/L4d2MatchmakingCore/Data/*`: PostgreSQL entity and EF model owner.
- `src/L4d2MatchmakingCore/Agents/*`: Warm-up Agent request, persistence, and Docker environment owner.
- `frontend/src/features/workspace/*`: current navigation and view owner.
- `frontend/src/state/useCoreSnapshot.ts`, `frontend/src/api/*`, and `frontend/src/components/*`: current frontend API/state/forms.
- Existing VNC contract fields (`keepVncAlive`, `WEB_UI_MODE`, `STEAM_LOGIN_UI_MODE`) must remain compatible.

## ImpactStatementDraft

- Persistence: add one singleton settings row with a stable key and nullable proxy URI.
- API: add authenticated `/v1/settings` GET/PUT; return a redacted effective setting, never proxy credentials.
- Runtime: resolve settings when creating/recreating an Agent; emit uppercase and lowercase proxy environment variables only when VNC is enabled and proxy is configured.
- UI: add a global settings navigation item and page; explain that existing VNC-enabled nodes require rebuild to apply changes.
