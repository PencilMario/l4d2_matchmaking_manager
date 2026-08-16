using L4d2LobbyAgent.Probe;
using L4d2Matchmaking.Contracts;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(AgentSessionOptions.FromEnvironment());
builder.Services.AddSingleton<ISteamSessionActor>(services =>
    SteamSessionActor.Create(services.GetRequiredService<AgentSessionOptions>().SteamApiLibraryPath));
builder.Services.AddSingleton<IAgentSteamSessionService, AgentSteamSessionService>();
builder.Services.AddSingleton<ISteamDesktopDetector, SteamDesktopDetector>();
builder.Services.AddSingleton<ProbeStatusService>();

var app = builder.Build();

app.MapGet("/healthz", () => Results.Ok(new { status = "alive" }));
app.MapGet("/v1/probe/status", async (ProbeStatusService service, CancellationToken cancellationToken) =>
{
    var response = await service.GetAsync(cancellationToken);
    return Results.Json(response, statusCode: response.Ready ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
});
app.MapPost("/v1/operations", async (
    AgentOperationRequest request,
    IAgentSteamSessionService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.StartAsync(request, cancellationToken);
    return result.AlreadyExists
        ? Results.Ok(result.Operation)
        : Results.Accepted($"/v1/operations/{request.OperationId}", result.Operation);
});
app.MapGet("/v1/operations/{operationId:guid}", async (
    Guid operationId,
    IAgentSteamSessionService service,
    CancellationToken cancellationToken) =>
{
    var operation = await service.GetAsync(operationId, cancellationToken);
    return operation is null ? Results.NotFound() : Results.Ok(operation);
});
app.MapDelete("/v1/operations/{operationId:guid}", async (
    Guid operationId,
    IAgentSteamSessionService service,
    CancellationToken cancellationToken) =>
    await service.StopAsync(operationId, cancellationToken) ? Results.NoContent() : Results.NotFound());
app.MapGet("/v1/lobbies/{lobbyId}", async (
    string lobbyId,
    IAgentSteamSessionService service,
    CancellationToken cancellationToken) =>
{
    if (!ulong.TryParse(lobbyId, out var parsedLobbyId) || parsedLobbyId == 0)
        return Results.BadRequest();

    return Results.Ok(await service.ReadLobbyAsync(parsedLobbyId, cancellationToken));
});

app.Run();

public partial class Program;
