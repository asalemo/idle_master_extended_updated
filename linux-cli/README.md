# Linux single-game test

This branch contains an experimental C# CLI for **x64 Linux**. It opens a Steamworks session for one game, reads the active AppID back from the local Steam client and keeps processing callbacks until you stop it. It requires a running native Linux Steam client, a game in your library and the .NET 8 SDK. Use the same Linux login for Steam and the terminal.

## Run

```sh
git clone --branch prototype/linux-cli --single-branch https://github.com/asalemo/idle_master_extended_updated.git
cd idle_master_extended_updated/linux-cli
./run.sh 440
```

Replace **440** with your own game's numeric store AppID. On first run the launcher downloads the Linux x64 Steamworks redistributable from a pinned revision of the [Steamworks SDK mirror](https://github.com/rlabrecque/SteamworksSDK/tree/df2baabf574a738ef1ea90a7e89339107fc0a279) and verifies its SHA-256 before building. It requires curl and sha256sum. Subsequent runs reuse the verified file. For offline setup, place the same libsteam_api.so in this directory first.

1. Wait for the **Steam confirmed AppID** message. It means the local Steamworks API returned the requested number.
2. Look at your status in the Linux Steam client or Friends list and check the game title. Visibility depends on your Steam status and privacy settings.
3. Press **Ctrl+C**. The CLI calls SteamAPI_Shutdown and prints **Steam session closed**. Check that your previous status returns in Steam. SIGTERM uses the same shutdown path.

The default output uses English or French according to your locale. To include native Steam loader diagnostics, run `IDLE_MASTER_VERBOSE=1 ./run.sh 440`. Setup errors suggest one action without a managed stack trace. Exit code 0 indicates normal shutdown or help, 2 indicates invalid usage or platform, 3 a dependency or setup problem and 4 a refused or mismatched Steam session. If Steam refuses initialization, check that Steam is online under the same Linux account and that you own the selected title. WSL with only a Windows Steam client is insufficient for an end-to-end presence test.

[.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) · [Steamworks initialization and shutdown](https://partner.steamgames.com/doc/api/steam_api)
