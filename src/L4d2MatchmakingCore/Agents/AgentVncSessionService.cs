using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace L4d2MatchmakingCore.Agents;

public sealed class AgentVncSessionService(
    IAgentContainerRuntime runtime,
    TimeSpan? lifetime = null)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ConcurrentDictionary<Guid, AgentVncSession> _sessions = [];
    private readonly TimeSpan _lifetime = lifetime ?? TimeSpan.FromMinutes(15);

    public async Task<AgentVncSession> OpenAsync(
        Guid agentId,
        string containerId,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var now = DateTimeOffset.UtcNow;
            if (_sessions.TryGetValue(agentId, out var existing))
            {
                if (existing.IsActive && existing.ExpiresAt > now)
                {
                    var renewalVncState = await runtime.GetVncStateAsync(containerId, cancellationToken);
                    if (renewalVncState != AgentVncState.Running)
                        throw new InvalidOperationException(renewalVncState == AgentVncState.Partial
                            ? "agent_vnc_partial_state"
                            : "agent_vnc_not_running");
                    existing.Revocation.Cancel();
                    var replacement = CreateSession(agentId, containerId, now, existing.StartedVnc);
                    _sessions[agentId] = replacement;
                    return replacement;
                }

                existing.Revocation.Cancel();
                await RetireAsync(existing, cancellationToken);
            }

            var vncState = await runtime.GetVncStateAsync(containerId, cancellationToken);
            if (vncState == AgentVncState.Partial)
                throw new InvalidOperationException("agent_vnc_partial_state");
            var startedVnc = vncState == AgentVncState.Stopped;
            if (startedVnc)
                await runtime.StartVncAsync(containerId, cancellationToken);

            var session = CreateSession(agentId, containerId, now, startedVnc);
            _sessions[agentId] = session;
            return session;
        }
        finally
        {
            _gate.Release();
        }
    }

    public bool TryGet(Guid agentId, string token, out AgentVncSession? session)
    {
        if (_sessions.TryGetValue(agentId, out var candidate) &&
            candidate.IsActive &&
            candidate.ExpiresAt > DateTimeOffset.UtcNow &&
            TokenMatches(candidate.Token, token))
        {
            session = candidate;
            return true;
        }

        session = null;
        return false;
    }

    public async Task<AgentVncSession?> ExchangeTokenAsync(
        Guid agentId,
        string token,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!_sessions.TryGetValue(agentId, out var existing) ||
                !existing.IsActive ||
                existing.ExpiresAt <= DateTimeOffset.UtcNow ||
                !TokenMatches(existing.Token, token))
            {
                return null;
            }

            var exchanged = existing with { Token = CreateToken() };
            _sessions[agentId] = exchanged;
            return exchanged;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task CloseAsync(Guid agentId, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!_sessions.TryGetValue(agentId, out var session))
                return;
            session.Revocation.Cancel();
            await RetireAsync(session, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task CleanupExpiredAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            Exception? failure = null;
            foreach (var session in _sessions.Values
                         .Where(session => !session.IsActive || session.ExpiresAt <= now)
                         .ToList())
            {
                session.Revocation.Cancel();
                try
                {
                    await RetireAsync(session, cancellationToken);
                }
                catch (Exception exception)
                {
                    failure ??= exception;
                }
            }
            if (failure is not null)
                throw failure;
        }
        finally
        {
            _gate.Release();
        }
    }

    private AgentVncSession CreateSession(
        Guid agentId,
        string containerId,
        DateTimeOffset now,
        bool startedVnc)
    {
        var revocation = new CancellationTokenSource();
        revocation.CancelAfter(_lifetime);
        return new AgentVncSession(agentId, containerId, CreateToken(), now.Add(_lifetime), startedVnc)
        {
            Revocation = revocation,
        };
    }

    private async Task RetireAsync(AgentVncSession session, CancellationToken cancellationToken)
    {
        if (session.StartedVnc)
            await runtime.StopVncAsync(session.ContainerId, cancellationToken);
        _sessions.TryRemove(new KeyValuePair<Guid, AgentVncSession>(session.AgentId, session));
    }

    private static string CreateToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    private static bool TokenMatches(string expected, string actual) =>
        CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(expected),
            System.Text.Encoding.UTF8.GetBytes(actual));
}

public sealed record AgentVncSession(
    Guid AgentId,
    string ContainerId,
    string Token,
    DateTimeOffset ExpiresAt,
    bool StartedVnc)
{
    internal CancellationTokenSource Revocation { get; init; } = new();
    public bool IsActive => !Revocation.IsCancellationRequested;
    public CancellationToken LifetimeToken => Revocation.Token;
}

public sealed class AgentVncSessionCleanupService(AgentVncSessionService sessions) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await sessions.CleanupExpiredAsync(DateTimeOffset.UtcNow, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch
            {
                // A later cleanup tick retries a transient Docker failure.
            }
        }
    }
}
