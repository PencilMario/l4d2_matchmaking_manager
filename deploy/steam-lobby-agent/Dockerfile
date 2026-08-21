FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src
COPY src/L4d2Matchmaking.Contracts/L4d2Matchmaking.Contracts.csproj src/L4d2Matchmaking.Contracts/
COPY research/L4d2Protocol/L4d2Protocol.csproj research/L4d2Protocol/
COPY research/SteamLobbyProbe/SteamLobbyProbe.csproj research/SteamLobbyProbe/
COPY src/L4d2LobbyAgent/L4d2LobbyAgent.csproj src/L4d2LobbyAgent/
RUN dotnet restore src/L4d2LobbyAgent/L4d2LobbyAgent.csproj -r linux-x64
RUN dotnet restore research/SteamLobbyProbe/SteamLobbyProbe.csproj -r linux-x64

COPY src/L4d2Matchmaking.Contracts/ src/L4d2Matchmaking.Contracts/
COPY research/L4d2Protocol/ research/L4d2Protocol/
COPY research/SteamLobbyProbe/ research/SteamLobbyProbe/
COPY src/L4d2LobbyAgent/ src/L4d2LobbyAgent/
RUN dotnet publish src/L4d2LobbyAgent/L4d2LobbyAgent.csproj -c Release -r linux-x64 --self-contained true --no-restore -o /out/agent
RUN dotnet publish research/SteamLobbyProbe/SteamLobbyProbe.csproj -c Release -r linux-x64 --self-contained true --no-restore -o /out/probe

# The maintained image owns the desktop, noVNC, Steam lifecycle and supervisor.
FROM josh5/steam-headless@sha256:1803d3d1bb51899dcc40a41625442a33108f1d205332eb09aa8dc662cfae0bd5

RUN apt-get update \
    && apt-get install -y --no-install-recommends libssl3t64:i386 \
    && rm -rf /var/lib/apt/lists/*

# The Debian steam-installer confirmation dialog is black under this headless desktop.
# Keep its download/install path, but accept the one-time installation automatically.
RUN set -eu \
    && patch_file="$(mktemp /tmp/steam-installer-patch.XXXXXX)" \
    && grep -Fq 'if command -v zenity >/dev/null; then' /usr/games/steam \
    && grep -Fq 'if ! "$zenityish" \' /usr/games/steam \
    && grep -Fq 'steam-launcher/bootstraplinux_ubuntu12_32.tar.xz' /usr/games/steam \
    && awk 'index($0, "    if ! \"$zenityish\" \\") == 1 { starts++; skipping = 1; next } skipping && /^    fi$/ { ends++; skipping = 0; next } !skipping { print } END { if (starts != 1 || ends != 1 || skipping != 0) exit 20 }' /usr/games/steam > "${patch_file}" \
    && ! grep -Fq 'if ! "$zenityish" \' "${patch_file}" \
    && grep -Fq 'if command -v zenity >/dev/null; then' "${patch_file}" \
    && grep -Fq 'steam-launcher/bootstraplinux_ubuntu12_32.tar.xz' "${patch_file}" \
    && grep -Fq 'mkdir -p "$STEAMDIR/deb-installer"' "${patch_file}" \
    && bash -n "${patch_file}" \
    && install -m 0755 "${patch_file}" /usr/games/steam \
    && rm -f "${patch_file}"

ARG MILLENNIUM_ARCHIVE_URL=https://github.com/SteamClientHomebrew/Millennium/releases/download/v3.4.1/millennium-v3.4.1-linux-x86_64.tar.gz
ARG MILLENNIUM_ARCHIVE_SHA256=5f2f6f73915523a7b3f7ecc500dd3e6ed0e5c88a1b1db6584c40f173aa9d13d4
RUN set -eu \
    && archive_file="$(mktemp /tmp/millennium-v3.4.1.XXXXXX.tar.gz)" \
    && extract_directory="$(mktemp -d /tmp/millennium-v3.4.1.XXXXXX)" \
    && curl --fail --location --retry 3 --output "${archive_file}" "${MILLENNIUM_ARCHIVE_URL}" \
    && printf '%s  %s\n' "${MILLENNIUM_ARCHIVE_SHA256}" "${archive_file}" | sha256sum --check --status \
    && tar --extract --gzip --file "${archive_file}" --directory "${extract_directory}" \
    && test -d "${extract_directory}/usr/lib/millennium" \
    && install -d -m 0755 /usr/lib/millennium \
    && cp -a "${extract_directory}/usr/lib/millennium/." /usr/lib/millennium/ \
    && chmod 0755 /usr/lib/millennium/* \
    && rm -f "${archive_file}" \
    && rm -rf "${extract_directory}"

COPY --from=build /out/agent/ /opt/l4d2-agent/
COPY --from=build /out/probe/ /opt/steam-lobby-probe/
COPY deploy/steam-lobby-agent/l4d2-lobby-agent.ini /etc/supervisor.d/l4d2-lobby-agent.ini
COPY deploy/steam-lobby-agent/91-enable-steam-supervisor.sh /etc/cont-init.d/91-enable-steam-supervisor.sh
COPY deploy/steam-lobby-agent/steamwebhelper-guard.sh /usr/local/bin/steamwebhelper-guard
COPY deploy/steam-lobby-agent/steamwebhelper-guard-shim.sh /usr/local/lib/steamwebhelper-guard-shim.sh
COPY deploy/steam-lobby-agent/steamwebhelper-guard.ini /etc/supervisor.d/steamwebhelper-guard.ini
COPY deploy/steam-lobby-agent/millennium/steam-region-bridge /opt/steam-region-bridge
COPY deploy/steam-lobby-agent/millennium/steam-ui-memory-saver /opt/steam-ui-memory-saver
COPY deploy/steam-lobby-agent/l4d2-steam-status.sh /usr/local/bin/l4d2-steam-status
COPY deploy/steam-lobby-agent/l4d2-steam-status.desktop /usr/share/applications/l4d2-steam-status.desktop
RUN rm -f /etc/cont-init.d/60-configure_gpu_driver.sh \
    && sed -i 's|if \[\[ ! -f /tmp/\.desktop-apps-updated \]\]; then|if false; then # skip desktop-apps-updated bootstrap|' /usr/bin/start-desktop.sh \
    && sed -i 's/if (!window\.isSecureContext) {/if (false) {/' /opt/frontend/noVNC/core/rfb.js \
    && chmod +x /etc/cont-init.d/91-enable-steam-supervisor.sh /usr/local/bin/l4d2-steam-status /usr/local/bin/steamwebhelper-guard /usr/local/lib/steamwebhelper-guard-shim.sh /opt/l4d2-agent/L4d2LobbyAgent /opt/steam-lobby-probe/SteamLobbyProbe \
    && chmod 0755 /opt/steam-region-bridge \
    && chmod 0755 /opt/steam-ui-memory-saver \
    && mkdir -p /var/lib/l4d2-agent \
    && chmod 0750 /var/lib/l4d2-agent

ENV ASPNETCORE_URLS=http://0.0.0.0:8080 \
    STEAM_LOBBY_PROBE_PATH=/opt/steam-lobby-probe/SteamLobbyProbe \
    PROBE_TIMEOUT_SECONDS=10 \
    STEAM_REGION_BRIDGE_SOURCE=/opt/steam-region-bridge \
    STEAM_UI_MEMORY_SAVER_SOURCE=/opt/steam-ui-memory-saver \
    STEAM_DOWNLOAD_REGION_STATUS_FILE=/home/default/.steam/steam/config/steam-download-region

EXPOSE 8080
