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

COPY --from=build /out/agent/ /opt/l4d2-agent/
COPY --from=build /out/probe/ /opt/steam-lobby-probe/
COPY deploy/steam-lobby-agent/l4d2-lobby-agent.ini /etc/supervisor.d/l4d2-lobby-agent.ini
COPY deploy/steam-lobby-agent/91-enable-steam-supervisor.sh /etc/cont-init.d/91-enable-steam-supervisor.sh
RUN rm -f /etc/cont-init.d/60-configure_gpu_driver.sh \
    && sed -i 's|if \[\[ ! -f /tmp/\.desktop-apps-updated \]\]; then|if false; then # skip desktop-apps-updated bootstrap|' /usr/bin/start-desktop.sh \
    && sed -i 's/if (!window\.isSecureContext) {/if (false) {/' /opt/frontend/noVNC/core/rfb.js \
    && chmod +x /etc/cont-init.d/91-enable-steam-supervisor.sh /opt/l4d2-agent/L4d2LobbyAgent /opt/steam-lobby-probe/SteamLobbyProbe \
    && mkdir -p /var/lib/l4d2-agent \
    && chmod 0750 /var/lib/l4d2-agent

ENV ASPNETCORE_URLS=http://0.0.0.0:8080 \
    STEAM_LOBBY_PROBE_PATH=/opt/steam-lobby-probe/SteamLobbyProbe \
    PROBE_TIMEOUT_SECONDS=10

EXPOSE 8080
