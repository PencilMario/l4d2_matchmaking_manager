using System.Diagnostics;

namespace L4d2LobbyAgent.Probe;

public sealed class SteamDesktopDetector : ISteamDesktopDetector
{
    public bool IsRunning()
    {
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                if (string.Equals(process.ProcessName, "steam", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }
}
