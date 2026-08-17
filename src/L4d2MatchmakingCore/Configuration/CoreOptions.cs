using Microsoft.Extensions.Configuration;

namespace L4d2MatchmakingCore.Configuration;

public sealed record CoreOptions(
    string ApiToken,
    string DatabaseConnectionString,
    string? RconEncryptionKey,
    int SchedulerMaxStartsPerTick)
{
    public const int DefaultSchedulerMaxStartsPerTick = 16;

    public static CoreOptions FromConfiguration(IConfiguration configuration)
    {
        var token = configuration["CORE_API_TOKEN"] ?? configuration["Core:ApiToken"];
        var tokenFile = configuration["CORE_API_TOKEN_FILE"] ?? configuration["Core:ApiTokenFile"];
        if (string.IsNullOrWhiteSpace(token) && !string.IsNullOrWhiteSpace(tokenFile))
        {
            if (!File.Exists(tokenFile))
                throw new InvalidOperationException("core_api_token_file_not_found");
            token = File.ReadAllText(tokenFile).Trim();
        }

        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("core_api_token_not_configured");

        var databaseConnectionString =
            configuration["CORE_DATABASE_CONNECTION_STRING"] ??
            configuration.GetConnectionString("Matchmaking");
        if (string.IsNullOrWhiteSpace(databaseConnectionString))
            throw new InvalidOperationException("core_database_connection_not_configured");

        var rconEncryptionKey = ReadOptionalSecret(
            configuration,
            "CORE_RCON_ENCRYPTION_KEY",
            "CORE_RCON_ENCRYPTION_KEY_FILE",
            "Core:RconEncryptionKey",
            "Core:RconEncryptionKeyFile",
            "core_rcon_encryption_key_file_not_found");
        var schedulerMaxStartsPerTick = ReadPositiveInt(
            configuration,
            "CORE_SCHEDULER_MAX_STARTS_PER_TICK",
            "Core:SchedulerMaxStartsPerTick",
            DefaultSchedulerMaxStartsPerTick,
            "core_scheduler_max_starts_per_tick_invalid");

        return new CoreOptions(token, databaseConnectionString, rconEncryptionKey, schedulerMaxStartsPerTick);
    }

    private static string? ReadOptionalSecret(
        IConfiguration configuration,
        string valueKey,
        string fileKey,
        string alternateValueKey,
        string alternateFileKey,
        string missingFileError)
    {
        var value = configuration[valueKey] ?? configuration[alternateValueKey];
        var file = configuration[fileKey] ?? configuration[alternateFileKey];
        if (string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(file))
        {
            if (!File.Exists(file))
                throw new InvalidOperationException(missingFileError);
            value = File.ReadAllText(file).Trim();
        }

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static int ReadPositiveInt(
        IConfiguration configuration,
        string environmentKey,
        string configurationKey,
        int defaultValue,
        string invalidMessage)
    {
        var value = configuration[environmentKey] ?? configuration[configurationKey];
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;
        if (!int.TryParse(value, out var parsed) || parsed < 1)
            throw new InvalidOperationException(invalidMessage);
        return parsed;
    }
}
