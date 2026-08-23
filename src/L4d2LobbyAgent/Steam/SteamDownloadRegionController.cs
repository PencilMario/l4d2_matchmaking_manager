public interface ISteamDownloadRegionController
{
    Task ApplyAsync(string? regionId, CancellationToken cancellationToken);
}

public sealed class SteamDownloadRegionController(
    string targetPath,
    ISteamDesktopController desktopController) : ISteamDownloadRegionController
{
    public static SteamDownloadRegionController FromEnvironment(ISteamDesktopController desktopController)
    {
        var homeDirectory = Environment.GetEnvironmentVariable("USER_HOME")
            ?? Environment.GetEnvironmentVariable("HOME")
            ?? "/home/default";
        return new(
            Path.Combine(homeDirectory, ".config", "millennium", "steam-region-bridge-region"),
            desktopController);
    }

    public async Task ApplyAsync(string? regionId, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(targetPath)
            ?? throw new InvalidOperationException("steam_download_region_target_path_invalid");
        Directory.CreateDirectory(directory);

        var temporaryPath = string.Concat(targetPath, ".tmp.", Guid.NewGuid().ToString("N"));
        try
        {
            var content = string.IsNullOrWhiteSpace(regionId)
                ? string.Empty
                : regionId.Trim() + Environment.NewLine;
            await File.WriteAllTextAsync(temporaryPath, content, cancellationToken);
            File.Move(temporaryPath, targetPath, true);
            await desktopController.RestartAsync(cancellationToken);
        }
        finally
        {
            try
            {
                File.Delete(temporaryPath);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
