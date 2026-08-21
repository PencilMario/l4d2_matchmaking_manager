# Steam Installer ????????

> **For agentic workers:** REQUIRED SUB-SKILL: Use aegis:subagent-driven-development (recommended) or aegis:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** ? Agent ?????????? Debian `steam-installer` ? Zenity ??????? Steam ???????

**Architecture:** Dockerfile ???????? `/usr/games/steam` ?????????????? Debian installer ?????????????? shell ???????????Supervisor ???????????

**Tech Stack:** Dockerfile, POSIX shell, PowerShell contract tests, SSH/Docker remote verification.

**Baseline / Authority Refs:** `deploy/steam-lobby-agent/Dockerfile`; `deploy/steam-lobby-agent/91-enable-steam-supervisor.sh`; `deploy/steam-lobby-agent/Test-ComposeContract.ps1`; `deploy/steam-lobby-agent/README.md`; remote `steam-installer` runtime evidence.

**Compatibility Boundary:** Preserve Steam download/install/launch logic and existing `STEAM_LOGIN_UI_MODE` behavior. Fail image build if the pinned base image no longer contains the expected installer confirmation block. Do not mutate existing volumes or containers.

**Verification:** Red/green PowerShell contract test; `bash -n` on the patch block; existing deployment contract tests; remote image build; one-shot first-start container showing no `Steam installer` window and a real Steam download/launch path.

---

### Task 1: Add the failing installer patch contract

**Files:**
- Create: `deploy/steam-lobby-agent/Test-SteamInstallerPatch.ps1`
- Modify: none before RED verification

**Why this task exists:** Protects the behavior that the Dockerfile owns the patch and refuses to silently build against an incompatible upstream script.

**Impact / Compatibility:** Read-only source contract; no runtime behavior changes.

**Verification:** `pwsh -NoProfile -File deploy/steam-lobby-agent/Test-SteamInstallerPatch.ps1` must fail before the Dockerfile patch exists.

- [ ] **Step 1: Write the failing test**

Assert Dockerfile contains a `steam-installer` patch step, expected guard text, `bash -n`, and executable preservation.

- [ ] **Step 2: Run test to verify it fails**

Run: `pwsh -NoProfile -File deploy/steam-lobby-agent/Test-SteamInstallerPatch.ps1`
Expected: non-zero exit because Dockerfile has no installer patch.

### Task 2: Implement the guarded Dockerfile patch

**Files:**
- Modify: `deploy/steam-lobby-agent/Dockerfile`

**Repair Track:**
- Root cause: Debian `steam-installer` blocks `/usr/games/steam` on a `zenity` confirmation dialog that renders black in the headless desktop.
- Canonical owner: base-image script at image build time.
- Smallest change: guarded text transformation of `/usr/games/steam` after the base image is selected.
- Verification: contract test, shell syntax check, remote first-start behavior.

**Retirement Track:**
- The runtime `xdotool` click workaround is not added and remains unnecessary.
- The original `zenity` confirmation branch is removed from the derived image only; the upstream package remains the source of installation logic.
- If the base image changes its script, the guard fails and forces a new adaptation.

- [ ] **Step 1: Add guarded patch command**

Use a temporary file and an exact `awk` transformation. The command must verify one expected block, remove only the `if command -v zenity...fi` confirmation branch, run `bash -n`, install mode `0755`, and remove the temporary file.

- [ ] **Step 2: Run the contract test**

Run: `pwsh -NoProfile -File deploy/steam-lobby-agent/Test-SteamInstallerPatch.ps1`
Expected: exit 0.

### Task 3: Regression and remote verification

**Files:**
- Modify: `deploy/steam-lobby-agent/Test-SteamInstallerPatch.ps1` only if the test needs an evidence assertion.
- Create: `docs/aegis/work/2026-08-21-steam-installer-autopatch/50-evidence.md`

**Why this task exists:** The requested outcome is an actual first-start behavior, not only a Dockerfile text change.

**Impact / Compatibility:** Build and one-shot runtime only; preserve existing agents and volumes.

- [ ] **Step 1: Run local deployment contracts**

Run: `pwsh -NoProfile -File deploy/steam-lobby-agent/Test-SteamInstallerPatch.ps1` and `pwsh -NoProfile -File deploy/steam-lobby-agent/Test-ComposeContract.ps1`.

- [ ] **Step 2: Build remotely**

From `sirphomesv`, build a distinct image tag from the current repository checkout. Do not stop or recreate existing managed containers.

- [ ] **Step 3: Verify the derived script remotely**

Use a disposable container from the new image to assert `/usr/games/steam` has no `zenity --question`, still has the Steam download URL and installation path, and passes `bash -n`.

- [ ] **Step 4: Verify first-start behavior**

Create a disposable container or isolated temporary volume using the new image with Steam enabled. Confirm `wmctrl -l` does not show `Steam installer`; inspect `/home/default/.steam/deb-installer` or bootstrap logs for the download and confirm Steam starts.

- [ ] **Step 5: Record evidence and residual risks**

Write exact commands, exit codes, changed files, remote image tag, and any unverified assumptions. Do not claim all managed Agents were migrated unless explicitly recreated.
