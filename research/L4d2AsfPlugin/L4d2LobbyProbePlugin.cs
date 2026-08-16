using System.Collections.Concurrent;
using ArchiSteamFarm;
using ArchiSteamFarm.Core;
using ArchiSteamFarm.Plugins.Interfaces;
using ArchiSteamFarm.Steam;
using SteamKit2;

namespace L4d2AsfPlugin;

public sealed class L4d2LobbyProbePlugin : IBotConnection, IBotSteamClient
{
    private const uint AppId = 550;
    private readonly ConcurrentDictionary<ulong, L4d2HostSession> activeSessions = new();
    private readonly ConcurrentDictionary<ulong, byte> startedBots = new();
    private L4d2PluginConfiguration? configuration;

    public string Name => nameof(L4d2LobbyProbePlugin);

    public Version Version => typeof(L4d2LobbyProbePlugin).Assembly.GetName().Version
        ?? throw new InvalidOperationException(nameof(Version));

    public Task OnLoaded()
    {
        var directory = Path.GetDirectoryName(typeof(L4d2LobbyProbePlugin).Assembly.Location);
        if (string.IsNullOrWhiteSpace(directory))
        {
            ASF.ArchiLogger.LogGenericError($"{Name}: unable to locate the plugin directory.");
            return Task.CompletedTask;
        }

        if (!L4d2PluginConfiguration.TryLoad(directory, out var loadedConfiguration, out var error))
        {
            ASF.ArchiLogger.LogGenericError($"{Name}: configuration disabled: {error}");
            return Task.CompletedTask;
        }

        configuration = loadedConfiguration;
        if (loadedConfiguration.Endpoint == null)
        {
            ASF.ArchiLogger.LogGenericInfo($"{Name}: disabled because no enabled bot is configured.");
            return Task.CompletedTask;
        }

        ASF.ArchiLogger.LogGenericInfo(
            $"{Name} loaded: endpoint={loadedConfiguration.Endpoint}, reservation={loadedConfiguration.ReservationEnabled}.");
        return Task.CompletedTask;
    }

    public Task OnBotSteamCallbacksInit(Bot bot, CallbackManager callbackManager) => Task.CompletedTask;

    public Task<IReadOnlyCollection<ClientMsgHandler>?> OnBotSteamHandlersInit(Bot bot)
    {
        ArgumentNullException.ThrowIfNull(bot);

        var config = configuration;
        if (config == null || !config.IsEnabledFor(bot.BotName))
            return Task.FromResult<IReadOnlyCollection<ClientMsgHandler>?>(null);

        var chatHandler = new L4d2LobbyChatHandler(
                () => activeSessions.GetValueOrDefault(bot.SteamID),
                audit => ASF.ArchiLogger.LogGenericInfo(
                    $"{Name}: join_data={audit.Event} bot={bot.BotName} sender={audit.SenderSteamId} " +
                    $"lobby={audit.LobbySteamId} state={audit.State} reply_bytes={audit.ReplyLength}."));
        IReadOnlyCollection<ClientMsgHandler> handlers = [chatHandler];
        return Task.FromResult<IReadOnlyCollection<ClientMsgHandler>?>(handlers);
    }

    public async Task OnBotLoggedOn(Bot bot)
    {
        ArgumentNullException.ThrowIfNull(bot);

        var config = configuration;
        if (config?.Endpoint == null || !config.IsEnabledFor(bot.BotName) || !startedBots.TryAdd(bot.SteamID, 0))
            return;

        var matchmaking = bot.GetHandler<SteamMatchmaking>();
        if (matchmaking == null)
        {
            startedBots.TryRemove(bot.SteamID, out _);
            ASF.ArchiLogger.LogGenericError($"{Name}: {bot.BotName} has no SteamMatchmaking handler.");
            return;
        }

        SteamID? lobbyId = null;
        try
        {
            var createJob = matchmaking.CreateLobby(AppId, config.LobbyType, config.MaxMembers);
            if (createJob == null)
            {
                ASF.ArchiLogger.LogGenericError($"{Name}: {bot.BotName} could not submit CreateLobby.");
                startedBots.TryRemove(bot.SteamID, out _);
                return;
            }

            var created = await createJob.ToLongRunningTask().ConfigureAwait(false);
            if (created.Result != EResult.OK)
            {
                ASF.ArchiLogger.LogGenericError($"{Name}: CreateLobby result={created.Result} bot={bot.BotName}.");
                startedBots.TryRemove(bot.SteamID, out _);
                return;
            }

            lobbyId = created.LobbySteamID;
            var setData = await matchmaking.SetLobbyData(
                AppId,
                lobbyId,
                config.LobbyType,
                config.MaxMembers,
                metadata: LobbyMetadata.CreateSessionMetadata()).ToLongRunningTask().ConfigureAwait(false);
            if (setData.Result != EResult.OK)
            {
                ASF.ArchiLogger.LogGenericError(
                    $"{Name}: SetLobbyData result={setData.Result} lobby={lobbyId.ConvertToUInt64()}.");
                await LeaveLobbyAsync(matchmaking, lobbyId, bot.BotName).ConfigureAwait(false);
                startedBots.TryRemove(bot.SteamID, out _);
                return;
            }

            var session = new L4d2HostSession(
                lobbyId,
                bot.SteamID,
                config.Endpoint,
                config.ReservationEnabled
                    ? L4d2HostSessionState.ReservationPending
                    : L4d2HostSessionState.Ready);
            activeSessions[bot.SteamID] = session;

            ASF.ArchiLogger.LogGenericInfo(
                $"{Name}: lobby={lobbyId.ConvertToUInt64()} join_uri=steam://joinlobby/{AppId}/{lobbyId.ConvertToUInt64()}/{bot.SteamID} state={session.State}.");

            if (config.ReservationEnabled)
                await ReserveAsync(bot, matchmaking, session, config).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            if (lobbyId is SteamID createdLobby)
                await LeaveLobbyAsync(matchmaking, createdLobby, bot.BotName).ConfigureAwait(false);

            activeSessions.TryRemove(bot.SteamID, out _);
            startedBots.TryRemove(bot.SteamID, out _);
            ASF.ArchiLogger.LogGenericException(exception);
        }
    }

    public Task OnBotDisconnected(Bot bot, EResult reason)
    {
        ArgumentNullException.ThrowIfNull(bot);
        activeSessions.TryRemove(bot.SteamID, out _);
        startedBots.TryRemove(bot.SteamID, out _);
        return Task.CompletedTask;
    }

    private async Task ReserveAsync(
        Bot bot,
        SteamMatchmaking matchmaking,
        L4d2HostSession session,
        L4d2PluginConfiguration config)
    {
        try
        {
            var result = await ReservationClient.ReserveAsync(
                session.Endpoint,
                session.LobbyId.ConvertToUInt64(),
                RealSessionSettings.EncodeReservationSettings(),
                config.ReservationTimeout,
                config.HostVersion).ConfigureAwait(false);

            switch (result.Outcome)
            {
                case ReservationOutcome.Accepted:
                    activeSessions[bot.SteamID] = session with { State = L4d2HostSessionState.ReservationResponseReceived };
                    break;
                case ReservationOutcome.Timeout:
                    activeSessions[bot.SteamID] = session with { State = L4d2HostSessionState.ReservationStatusRequired };
                    ASF.ArchiLogger.LogGenericInfo(
                        $"{Name}: reservation=Timeout lobby={session.LobbyId.ConvertToUInt64()} verification=server_status_required.");
                    return;
                default:
                    activeSessions.TryRemove(bot.SteamID, out _);
                    startedBots.TryRemove(bot.SteamID, out _);
                    ASF.ArchiLogger.LogGenericError(
                        $"{Name}: reservation={result.Outcome} lobby={session.LobbyId.ConvertToUInt64()}; leaving lobby.");
                    await LeaveLobbyAsync(matchmaking, session.LobbyId, bot.BotName).ConfigureAwait(false);
                    return;
            }

            ASF.ArchiLogger.LogGenericInfo(
                $"{Name}: reservation={result.Outcome} lobby={session.LobbyId.ConvertToUInt64()}.");
        }
        catch (Exception exception)
        {
            activeSessions.TryRemove(bot.SteamID, out _);
            startedBots.TryRemove(bot.SteamID, out _);
            await LeaveLobbyAsync(matchmaking, session.LobbyId, bot.BotName).ConfigureAwait(false);
            ASF.ArchiLogger.LogGenericException(exception);
        }
    }

    private async Task LeaveLobbyAsync(SteamMatchmaking matchmaking, SteamID lobbyId, string botName)
    {
        try
        {
            var left = await matchmaking.LeaveLobby(AppId, lobbyId).ToLongRunningTask().ConfigureAwait(false);
            ASF.ArchiLogger.LogGenericInfo(
                $"{Name}: LeaveLobby result={left.Result} lobby={lobbyId.ConvertToUInt64()} bot={botName}.");
        }
        catch (Exception exception)
        {
            ASF.ArchiLogger.LogGenericException(exception);
        }
    }
}
