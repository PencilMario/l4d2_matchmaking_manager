# Steam Region Bridge

This legacy Millennium plugin changes Steam's download region through
`SteamClient.Settings.SetSetting`, using `CMsgClientSettings.download_region`
field `8009`. It never edits `config.vdf`.

On load it waits for Steam's `vecValidDownloadRegions`, applies the numeric
`STEAM_DOWNLOAD_REGION_ID` target when configured, and records the actual
current ID in the account-local `steam-download-region` state file. An empty
target only records the current Steam setting.
