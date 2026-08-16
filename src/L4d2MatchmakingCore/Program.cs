using L4d2MatchmakingCore.Agents;
using L4d2MatchmakingCore.Auth;
using L4d2MatchmakingCore.Configuration;
using L4d2MatchmakingCore.Data;
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
builder.Services.AddScoped<TargetServerService>();
var agentContainerOptions = builder.Environment.IsEnvironment("Testing")
    ? new AgentContainerOptions("l4d2-steam-lobby-agent:local", "/mnt/steam-library", "l4d2-matchmaking", 18083, 18183)
    : AgentContainerOptions.FromConfiguration(builder.Configuration);
builder.Services.AddSingleton(agentContainerOptions);
builder.Services.AddSingleton<IAgentContainerRuntime, DockerAgentContainerRuntime>();
builder.Services.AddScoped<WarmupAgentContainerService>();
builder.Services.AddScoped<WarmupAgentService>();
builder.Services.AddHttpClient<IAgentControlClient, AgentControlClient>();
builder.Services.AddScoped<IHealthyAgentSelector, HealthyAgentSelector>();

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

app.Run();

public partial class Program;
