using System.Globalization;

internal sealed record SteamAppConfiguration(uint AppId)
{
    internal const uint DefaultAppId = 500;
    internal const string AppIdEnvironmentVariable = "STEAM_APP_ID";

    internal static SteamAppConfiguration FromEnvironment() =>
        new(ParseAppId(Environment.GetEnvironmentVariable(AppIdEnvironmentVariable)));

    internal static uint ParseAppId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return DefaultAppId;

        if (!uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var appId) || appId == 0)
            throw new InvalidOperationException("steam_app_id_invalid");

        return appId;
    }

    internal void ApplyToProcess()
    {
        var value = AppId.ToString(CultureInfo.InvariantCulture);
        Environment.SetEnvironmentVariable("SteamAppId", value);
        Environment.SetEnvironmentVariable("SteamGameId", value);
    }
}
