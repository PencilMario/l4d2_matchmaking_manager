using L4d2MatchmakingCore.Agents;
using L4d2MatchmakingCore.A2s;
using L4d2MatchmakingCore.Auth;
using L4d2MatchmakingCore.Configuration;
using L4d2MatchmakingCore.Data;
using L4d2MatchmakingCore.Lobbies;
using L4d2MatchmakingCore.Scheduling;
using L4d2MatchmakingCore.Servers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var coreOptions = CoreOptions.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(coreOptions);
builder.Services.AddDbContext<MatchmakingDbContext>(options =>
    options.UseNpgsql(coreOptions.DatabaseConnectionString));
builder.Services
    .AddAuthentication(StaticBearerAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, StaticBearerAuthenticationHandler>(
        StaticBearerAuthenticationHandler.SchemeName,
        static _ => { });
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IRconCredentialProtector, RconCredentialProtector>();
builder.Services.AddScoped<TargetServerService>();
var agentContainerOptions = builder.Environment.IsEnvironment("Testing")
    ? new AgentContainerOptions("l4d2-steam-lobby-agent:local", "/mnt/steam-library", "l4d2-matchmaking", 18083, 18183, "/mnt/steam-library/libsteam_api.so")
    : AgentContainerOptions.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(agentContainerOptions);
builder.Services.AddSingleton<IAgentContainerRuntime, DockerAgentContainerRuntime>();
builder.Services.AddScoped<WarmupAgentContainerService>();
builder.Services.AddScoped<WarmupAgentService>();
builder.Services.AddHttpClient<IAgentControlClient, AgentControlClient>();
builder.Services.AddScoped<IHealthyAgentSelector, HealthyAgentSelector>();
builder.Services.AddSingleton<ISourceA2sClient>(new SourceA2sClient(TimeSpan.FromSeconds(3)));
builder.Services.AddSingleton<WarmupDecisionEngine>();
builder.Services.AddScoped<SharedLibraryMaintenanceService>();
builder.Services.AddScoped<WarmupSchedulerService>();
builder.Services.AddScoped<WarmupStatusService>();
if (!builder.Environment.IsEnvironment("Testing"))
    builder.Services.AddHostedService<WarmupSchedulerBackgroundService>();

var app = builder.Build();
if (!app.Environment.IsEnvironment("Testing"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider
        .GetRequiredService<MatchmakingDbContext>()
        .Database
        .MigrateAsync();
}

app.MapGet("/healthz", () => Results.Ok(new { status = "alive" }));
app.MapGroup("/v1/servers")
    .RequireAuthorization()
    .MapTargetServerEndpoints();
app.MapGroup("/v1/agents")
    .RequireAuthorization()
    .MapWarmupAgentEndpoints();
app.MapLobbyQueryEndpoints();
app.MapWarmupStatusEndpoints();

app.Run();

public partial class Program;
