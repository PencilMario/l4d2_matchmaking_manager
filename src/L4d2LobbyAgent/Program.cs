using L4d2LobbyAgent.Probe;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(ProbeCommandOptions.FromEnvironment());
builder.Services.AddSingleton<IProbeProcessLauncher, ProcessProbeProcessLauncher>();
builder.Services.AddSingleton<IProbeCommandRunner, ProbeCommandRunner>();
builder.Services.AddSingleton<ISteamDesktopDetector, SteamDesktopDetector>();
builder.Services.AddSingleton<ProbeStatusService>();

var app = builder.Build();

app.MapGet("/healthz", () => Results.Ok(new { status = "alive" }));
app.MapGet("/v1/probe/status", async (ProbeStatusService service, CancellationToken cancellationToken) =>
{
    var response = await service.GetAsync(cancellationToken);
    return Results.Json(response, statusCode: response.Ready ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
});

app.Run();

public partial class Program;
