using L4d2MatchmakingCore.Agents;
using L4d2MatchmakingCore.A2s;
using L4d2MatchmakingCore.Auth;
using L4d2MatchmakingCore.Configuration;
using L4d2MatchmakingCore.Data;
using L4d2MatchmakingCore.Lobbies;
using L4d2MatchmakingCore.Profiles;
using L4d2MatchmakingCore.Scheduling;
using L4d2MatchmakingCore.Servers;
using L4d2MatchmakingCore.Settings;
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
builder.Services.AddSingleton<ISecretProtector>(services => services.GetRequiredService<IRconCredentialProtector>());
builder.Services.AddScoped<TargetServerService>();
builder.Services.AddScoped<TargetServerObservationService>();
builder.Services.AddScoped<GlobalSettingsService>();
builder.Services.AddHttpClient<ISteamProfileService, SteamProfileService>(client =>
{
    client.BaseAddress = new Uri("https://api.steampowered.com");
    client.Timeout = TimeSpan.FromSeconds(5);
});
var agentContainerOptions = builder.Environment.IsEnvironment("Testing")
    ? new AgentContainerOptions("l4d2-steam-lobby-agent:local", "/mnt/steam-library", "l4d2-matchmaking", 18083, 18183, "/mnt/steam-library/libsteam_api.so")
    : AgentContainerOptions.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(agentContainerOptions);
builder.Services.AddSingleton<IAgentContainerRuntime, DockerAgentContainerRuntime>();
builder.Services.AddSingleton<AgentVncSessionService>();
builder.Services.AddSingleton<AgentLifecycleCoordinator>();
builder.Services.AddHttpClient("agent-vnc", client => client.Timeout = Timeout.InfiniteTimeSpan);
builder.Services.AddSingleton<IAgentVncProxyTargetResolver, AgentVncProxyTargetResolver>();
builder.Services.AddSingleton<IAgentVncProxy, AgentVncProxy>();
builder.Services.AddScoped<WarmupAgentContainerService>();
builder.Services.AddScoped<WarmupAgentService>();
builder.Services.AddHttpClient<IAgentControlClient, AgentControlClient>();
builder.Services.AddScoped<IHealthyAgentSelector, HealthyAgentSelector>();
builder.Services.AddScoped<LobbyQueryService>();
builder.Services.AddSingleton<ISourceA2sClient>(new SourceA2sClient(TimeSpan.FromSeconds(3)));
builder.Services.AddSingleton<TargetServerObservationStore>();
builder.Services.AddSingleton<WarmupDecisionEngine>();
builder.Services.AddScoped<SharedLibraryMaintenanceService>();
builder.Services.AddScoped<WarmupSchedulerService>();
builder.Services.AddScoped<WarmupStatusService>();
builder.Services.AddScoped<WarmupAttemptDrainService>();
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddHostedService<AgentVncSessionCleanupService>();
    builder.Services.AddHostedService<WarmupSchedulerBackgroundService>();
    builder.Services.AddHostedService<TargetServerObservationCollector>();
}

var app = builder.Build();
app.UseWebSockets();
if (!app.Environment.IsEnvironment("Testing"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider
        .GetRequiredService<MatchmakingDbContext>()
        .Database
        .MigrateAsync();
}

app.MapGet("/healthz", () => Results.Ok(new { status = "alive" }));
var targetServerGroup = app.MapGroup("/v1/servers").RequireAuthorization();
targetServerGroup.MapTargetServerObservationEndpoints();
targetServerGroup.MapTargetServerEndpoints();
app.MapGroup("/v1/agents")
    .RequireAuthorization()
    .MapWarmupAgentEndpoints();
app.MapLobbyQueryEndpoints();
app.MapWarmupStatusEndpoints();
app.MapGlobalSettingsEndpoints();

app.Run();

public partial class Program;
