using System.Globalization;

namespace L4d2MatchmakingCore.Scheduling;

public sealed record WarmupPauseWindow(string Start, string End);

public static class WarmupPauseWindowRules
{
    private static readonly TimeSpan ShanghaiOffset = TimeSpan.FromHours(8);

    public static IReadOnlyList<WarmupPauseWindow> Normalize(IReadOnlyList<WarmupPauseWindow> windows)
    {
        if (windows is null)
            throw new ArgumentException("invalid_warmup_pause_window");

        var parsed = new List<(WarmupPauseWindow Window, int Start, int End)>();
        foreach (var window in windows)
        {
            if (window is null ||
                !TimeOnly.TryParseExact(window.Start, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start) ||
                !TimeOnly.TryParseExact(window.End, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var end))
            {
                throw new ArgumentException("invalid_warmup_pause_window");
            }

            var startMinutes = start.Hour * 60 + start.Minute;
            var endMinutes = end.Hour * 60 + end.Minute;
            if (startMinutes == endMinutes)
                throw new ArgumentException("invalid_warmup_pause_window");

            parsed.Add((
                new WarmupPauseWindow(
                    start.ToString("HH:mm", CultureInfo.InvariantCulture),
                    end.ToString("HH:mm", CultureInfo.InvariantCulture)),
                startMinutes,
                endMinutes));
        }

        return parsed
            .OrderBy(item => item.Start)
            .ThenBy(item => item.End)
            .Select(item => item.Window)
            .ToArray();
    }

    public static bool IsActive(DateTimeOffset utcNow, IReadOnlyList<WarmupPauseWindow> windows)
    {
        var local = utcNow.ToUniversalTime().ToOffset(ShanghaiOffset);
        var localMinutes = local.Hour * 60 + local.Minute;
        foreach (var window in Normalize(windows))
        {
            var start = ParseMinutes(window.Start);
            var end = ParseMinutes(window.End);
            if (start < end && localMinutes >= start && localMinutes < end)
                return true;
            if (start > end && (localMinutes >= start || localMinutes < end))
                return true;
        }

        return false;
    }

    private static int ParseMinutes(string value)
    {
        TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time);
        return time.Hour * 60 + time.Minute;
    }
}
