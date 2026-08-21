using L4d2LobbyAgent.Probe;
using L4d2Matchmaking.Contracts;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(AgentSessionOptions.FromEnvironment());
builder.Services.AddSingleton<ISteamSessionActor>(services =>
    SteamSessionActor.Create(services.GetRequiredService<AgentSessionOptions>().SteamApiLibraryPath));
builder.Services.AddSingleton<IAgentSteamSessionService, AgentSteamSessionService>();
builder.Services.AddSingleton<ISteamDesktopController, SupervisorSteamDesktopController>();
builder.Services.AddSingleton<ISteamDesktopDetector, SteamDesktopDetector>();
builder.Services.AddSingleton<ISteamDownloadRegionReader>(_ => SteamDownloadRegionReader.FromEnvironment());
builder.Services.AddSingleton<IAgentReadinessMarker>(_ => FileAgentReadinessMarker.FromEnvironment());
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
    if (request.Mode == AgentLobbyMode.Reserved)
        return Results.BadRequest("l4d1_reservation_not_supported");

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
app.MapPost("/v1/steam/restart", async (
    ISteamDesktopController controller,
    CancellationToken cancellationToken) =>
{
    await controller.RestartAsync(cancellationToken);
    return Results.Accepted();
});
app.MapGet("/v1/lobbies/{lobbyId}", async (
    string lobbyId,
    IAgentSteamSessionService service,
    bool? includeMembers,
    CancellationToken cancellationToken) =>
{
    if (!ulong.TryParse(lobbyId, out var parsedLobbyId) || parsedLobbyId == 0)
        return Results.BadRequest();

    try
    {
        return Results.Ok(await service.QueryLobbyAsync(parsedLobbyId, includeMembers ?? true, cancellationToken));
    }
    catch (SteamRuntimeException exception) when (exception.Code == "lobby_data_unavailable")
    {
        return Results.Json("lobby_data_unavailable", statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch (SteamRuntimeException exception) when (exception.Code == "lobby_operation_preservation_failed")
    {
        return Results.Json("lobby_operation_preservation_failed", statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.Run();

public partial class Program;
