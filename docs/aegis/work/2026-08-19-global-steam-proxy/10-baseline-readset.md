# Baseline Read Set

The current shared worktree has uncommitted changes from other agents. Those changes are treated as user-owned and remain untouched unless this feature directly depends on them.

Compatibility boundaries:

- Existing API paths and VNC behavior remain available.
- Empty proxy means no proxy environment variables are added.
- A disabled-VNC Agent never receives proxy variables, even when the global setting is configured.
- Updating the global setting does not stop or rebuild running containers.
- Existing frontend polling/authentication behavior remains unchanged.
