using System.Collections.Concurrent;
using L4d2Matchmaking.Contracts;

public sealed class SteamSessionActor : ISteamSessionActor
{
    private readonly ISteamNativeRuntime _runtime;
    private readonly ICampaignSelector _campaignSelector;
    private readonly ConcurrentQueue<IActorCommand> _commands = new();
    private readonly AutoResetEvent _signal = new(false);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Thread _thread;
    private readonly Dictionary<Guid, AgentOperationSnapshot> _operations = [];
    private AgentOperationSnapshot? _activeOperation;
    private AgentLobbyMode? _activeMode;
    private Exception? _callbackFailure;
    private int _disposed;

    internal SteamSessionActor(ISteamNativeRuntime runtime, ICampaignSelector campaignSelector)
    {
        _runtime = runtime;
        _campaignSelector = campaignSelector;
        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "steam-session-actor",
        };
        _thread.Start();
    }

    public static ISteamSessionActor Create(string steamApiLibraryPath) =>
        new SteamSessionActor(
            new SteamNativeRuntime(steamApiLibraryPath),
            new RandomCampaignSelector());

    public Task<AgentHealthSnapshot> ObserveHealthAsync(CancellationToken cancellationToken) =>
        Enqueue(static actor => actor.ObserveHealth(), cancellationToken);

    public Task<AgentOperationStartResult> StartAsync(
        AgentOperationRequest request,
        CancellationToken cancellationToken) =>
        Enqueue(actor => actor.Start(request), cancellationToken);

    public Task<AgentOperationSnapshot?> GetOperationAsync(
        Guid operationId,
        CancellationToken cancellationToken) =>
        Enqueue(actor => actor._operations.GetValueOrDefault(operationId), cancellationToken);

    public Task StopAsync(Guid operationId, CancellationToken cancellationToken) =>
        Enqueue(actor => actor.Stop(operationId), cancellationToken);

    public Task<LobbySnapshot> ReadLobbyAsync(ulong lobbyId, CancellationToken cancellationToken) =>
        Enqueue(actor => actor.ReadLobby(lobbyId), cancellationToken);

    public Task<LobbySnapshot> QueryLobbyAsync(
        ulong lobbyId,
        bool includeMembers,
        CancellationToken cancellationToken) =>
        Enqueue(actor => actor.QueryLobby(lobbyId, includeMembers), cancellationToken);

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        try
        {
            await Enqueue(
                actor =>
                {
                    actor.StopActiveOperation();
                    return true;
                },
                CancellationToken.None,
                allowDisposed: true).ConfigureAwait(false);
        }
        finally
        {
            _shutdown.Cancel();
            _signal.Set();
            _thread.Join(TimeSpan.FromSeconds(5));
            _signal.Dispose();
            _shutdown.Dispose();
            _runtime.Dispose();
        }
    }

    private AgentHealthSnapshot ObserveHealth()
    {
        ThrowIfCallbackFailed();
        return _runtime.ObserveHealth();
    }

    private AgentOperationStartResult Start(AgentOperationRequest request)
    {
        ThrowIfCallbackFailed();
        if (_operations.TryGetValue(request.OperationId, out var existing))
            return new AgentOperationStartResult(existing, true);
        if (_activeOperation is not null)
            throw new InvalidOperationException("operation_in_progress");

        var lobby = _runtime.CreateLobby(request, _campaignSelector.Select());
        var snapshot = new AgentOperationSnapshot(
            request.OperationId,
            "active",
            lobby,
            null,
            DateTimeOffset.UtcNow);
        _operations.Add(request.OperationId, snapshot);
        _activeOperation = snapshot;
        _activeMode = request.Mode;
        return new AgentOperationStartResult(snapshot, false);
    }

    private void Stop(Guid operationId)
    {
        if (!_operations.TryGetValue(operationId, out var operation))
            throw new KeyNotFoundException("operation_not_found");
        if (_activeOperation?.OperationId != operationId)
            return;

        StopActiveOperation();
    }

    private void StopActiveOperation()
    {
        if (_activeOperation is not { } operation)
            return;

        if (operation.Lobby is { } lobby && ulong.TryParse(lobby.LobbyId, out var lobbyId) && lobbyId != 0)
            _runtime.LeaveLobby(lobbyId);

        var stopped = operation with
        {
            State = "stopped",
            ObservedAt = DateTimeOffset.UtcNow,
        };
        _operations[operation.OperationId] = stopped;
        _activeOperation = null;
        _activeMode = null;
    }

    private LobbySnapshot ReadLobby(ulong lobbyId)
    {
        ThrowIfCallbackFailed();
        if (lobbyId == 0)
            throw new ArgumentOutOfRangeException(nameof(lobbyId));
        return _runtime.ReadLobby(lobbyId);
    }

    private LobbySnapshot QueryLobby(ulong lobbyId, bool includeMembers)
    {
        ThrowIfCallbackFailed();
        if (lobbyId == 0)
            throw new ArgumentOutOfRangeException(nameof(lobbyId));

        var metadata = _runtime.ReadLobby(lobbyId);
        if (!includeMembers)
            return MetadataOnly(metadata, LobbyMemberDataStatus.MetadataOnlyNoQueryAgent);

        if (_activeOperation is not null && _activeMode != AgentLobbyMode.Standard)
            return MetadataOnly(metadata, LobbyMemberDataStatus.MetadataOnlyAgentStateChanged);

        var currentSteamId = _runtime.GetCurrentSteamId();
        if (currentSteamId == 0)
            throw new SteamRuntimeException("steam_not_logged_on");

        var heldLobbyId = 0UL;
        if (_activeOperation?.Lobby is { } heldLobby)
            ulong.TryParse(heldLobby.LobbyId, out heldLobbyId);

        if (heldLobbyId == lobbyId)
        {
            return Complete(metadata, _activeOperation?.Lobby);
        }

        if (_activeOperation is not null && heldLobbyId == 0)
        {
            return MetadataOnly(metadata, LobbyMemberDataStatus.MetadataOnlyAgentStateChanged);
        }

        var joined = _runtime.JoinLobby(lobbyId);
        if (joined == NativeLobbyJoinResult.Denied)
            return MetadataOnly(metadata, LobbyMemberDataStatus.MetadataOnlyJoinDenied);
        if (joined == NativeLobbyJoinResult.Timeout)
            return MetadataOnly(metadata, LobbyMemberDataStatus.MetadataOnlyJoinTimeout);

        try
        {
            return Complete(_runtime.ReadLobby(lobbyId));
        }
        finally
        {
            try
            {
                _runtime.LeaveLobby(lobbyId);
            }
            catch (Exception exception) when (_activeOperation is not null && _activeMode == AgentLobbyMode.Standard)
            {
                FailActiveOperation("lobby_operation_preservation_failed");
                throw new SteamRuntimeException("lobby_operation_preservation_failed", exception);
            }

            if (_activeOperation is not null && _activeMode == AgentLobbyMode.Standard &&
                !_runtime.IsCurrentUserLobbyMember(heldLobbyId, currentSteamId))
            {
                FailActiveOperation("lobby_operation_preservation_failed");
                throw new SteamRuntimeException("lobby_operation_preservation_failed");
            }
        }
    }

    private static LobbySnapshot MetadataOnly(LobbySnapshot snapshot, string status) =>
        snapshot with { Members = [], MemberDataStatus = status };

    private static LobbySnapshot Complete(LobbySnapshot snapshot, LobbySnapshot? heldLobby = null)
    {
        if (heldLobby is null || heldLobby.Members.Count == 0)
            return snapshot with { MemberDataStatus = LobbyMemberDataStatus.Complete };

        var members = snapshot.Members
            .Concat(heldLobby.Members)
            .GroupBy(member => member.SteamId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        return snapshot with { Members = members, MemberDataStatus = LobbyMemberDataStatus.Complete };
    }

    private void FailActiveOperation(string failure)
    {
        if (_activeOperation is not { } operation)
            return;

        var failed = operation with
        {
            State = "failed",
            Failure = failure,
            ObservedAt = DateTimeOffset.UtcNow,
        };
        _operations[operation.OperationId] = failed;
        _activeOperation = null;
        _activeMode = null;
    }

    private Task<T> Enqueue<T>(
        Func<SteamSessionActor, T> command,
        CancellationToken cancellationToken,
        bool allowDisposed = false)
    {
        if (!allowDisposed)
            ThrowIfDisposed();
        cancellationToken.ThrowIfCancellationRequested();
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _commands.Enqueue(new ActorCommand<T>(command, completion));
        _signal.Set();
        return completion.Task;
    }

    private Task Enqueue(Action<SteamSessionActor> command, CancellationToken cancellationToken) =>
        Enqueue(actor =>
        {
            command(actor);
            return true;
        }, cancellationToken);

    private void Run()
    {
        while (!_shutdown.IsCancellationRequested)
        {
            TryPumpCallbacks();
            while (_commands.TryDequeue(out var command))
                command.Execute(this);
            _signal.WaitOne(50);
        }

        while (_commands.TryDequeue(out var command))
            command.Cancel();
    }

    private void TryPumpCallbacks()
    {
        try
        {
            _runtime.PumpCallbacks();
        }
        catch (Exception exception)
        {
            _callbackFailure ??= exception;
        }
    }

    private void ThrowIfCallbackFailed()
    {
        if (_callbackFailure is not null)
            throw new InvalidOperationException("steam_callback_loop_failed", _callbackFailure);
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(SteamSessionActor));
    }

    private interface IActorCommand
    {
        void Execute(SteamSessionActor actor);
        void Cancel();
    }

    private sealed class ActorCommand<T>(
        Func<SteamSessionActor, T> command,
        TaskCompletionSource<T> completion) : IActorCommand
    {
        public void Execute(SteamSessionActor actor)
        {
            try
            {
                completion.SetResult(command(actor));
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        }

        public void Cancel() => completion.TrySetCanceled();
    }
}
