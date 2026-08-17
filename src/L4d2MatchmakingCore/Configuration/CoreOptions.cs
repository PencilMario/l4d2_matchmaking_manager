using Microsoft.Extensions.Configuration;

namespace L4d2MatchmakingCore.Configuration;

public sealed record CoreOptions(string ApiToken, string DatabaseConnectionString, string? RconEncryptionKey)
{
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

        return new CoreOptions(token, databaseConnectionString, rconEncryptionKey);
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
}
