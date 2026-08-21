# Steam UI Memory Saver

This Millennium plugin is enabled only for the Agent's no-VNC runtime state.
After the Agent readiness marker exists, it replaces the desktop Steam Library
and Library Downloads route contents with an empty React component. Steam's
main process, Steam API, login session, and `steamwebhelper` remain running.

The plugin does not activate when `STEAM_LOGIN_UI_MODE=always`, so VNC recovery
keeps the normal Steam UI.
