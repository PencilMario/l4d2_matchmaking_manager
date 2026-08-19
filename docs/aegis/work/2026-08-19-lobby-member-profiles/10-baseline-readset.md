# Baseline Read Set

- `docs/aegis/specs/2026-08-19-lobby-member-profiles-design.md`: approved behavior and security boundaries.
- `src/L4d2MatchmakingCore/Settings/GlobalSettingsService.cs`: current global settings owner.
- `src/L4d2MatchmakingCore/Settings/GlobalSettingsDtos.cs`: current settings API contract.
- `src/L4d2MatchmakingCore/Servers/RconCredentialProtector.cs`: AES-GCM protection pattern to reuse.
- `src/L4d2MatchmakingCore/Data/Entities.cs`: CoreSettings and LobbyMemberSnapshot owners.
- `src/L4d2MatchmakingCore/Lobbies/LobbyQueryService.cs`: canonical lobby query merge owner.
- `frontend/src/features/settings/SettingsWorkspace.tsx`: settings UI owner.
- `frontend/src/features/lobbies/LobbyQueryView.tsx`: lobby member UI owner.
