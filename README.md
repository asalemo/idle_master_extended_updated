# Idle Master Extended Updated

Idle Master checks your Steam badges for remaining trading card drops and starts a small helper for each selected game. Steam determines when cards drop. This fork targets **Windows x64** and includes changes to game switching, startup responsiveness and session recovery.

[Français](README.fr.md) · [Download the Windows release candidate](https://github.com/asalemo/idle_master_extended_updated/releases/tag/v1.11.0-rc.3) · [User guide](https://github.com/asalemo/idle_master_extended_updated/wiki)

## Get started

1. Download the **win-x64 ZIP** from the release and extract the entire archive. The GitHub source archives are for developers.
2. Open Steam and sign in. Run `IdleMasterExtended.exe` from the extracted folder with `steam-idle.exe`, the DLLs, `runtimes` and `Languages` beside it.
3. Use the sign-in link to configure your Steam Community session. Choose an idling mode in **File → Settings**.

## Sign-in and language in RC3

In RC3, select **Sign in**, complete sign-in on Steam’s official page, then select **Use this Steam account**. The window shows your public SteamID. This private Microsoft Edge WebView2 session lasts until sign-out or application exit. Manual session fields remain available in Settings.

In **File → Settings**, choose **Windows language** or a language by its native name, then restart the application.
Steam sign-in, single-game testing and Diagnostics are available in English, French, German, Spanish, Italian and Portuguese (Portugal and Brazil). Other interface languages use English for missing translations.

## What to expect

Fast mode runs up to 30 eligible games in parallel while they are below two recorded hours. It checks progress every five minutes. Once a game qualifies for individual idling, or only one remains, it stays active between checks.

**Starting** means the helper has launched. **Currently in-game** appears after the helper confirms local Steam API initialization. If it exits, the Diagnostics menu identifies the game, AppID and reason. Failed helpers wait one minute between attempts. **File → Retry now** retries pending games immediately.

The profile name and orphan-process cleanup run in the background. An unreadable profile or temporary connection failure preserves the saved session and schedules another startup check in one minute.

## RC2 on Windows

Open **File → Test a game** to search installed titles. Starting a test pauses the automatic queue. Stop the test or close its window, then resume the queue when ready. **File → Timings** shows the last badge request and processing times and the first helper confirmation. Repeated failures are grouped in **Diagnostics**, with retry and copy actions. Helpers run without a window.

[Test a single game](https://github.com/asalemo/idle_master_extended_updated/wiki/Test-a-single-game) · [Read errors in Diagnostics](https://github.com/asalemo/idle_master_extended_updated/wiki/Diagnostics-and-recovery)

## Build and contribute

Build the helper first, then the desktop solution in **Release | Any CPU** using Visual Studio with .NET desktop development and the .NET Framework 4.8 targeting pack. The resulting application requires x64 Windows. See the [build guide](https://github.com/asalemo/idle_master_extended_updated/wiki/Building-from-source) for paths, packaging and validation.

Based on [Idle Master Extended by Jonas Nilson](https://github.com/jonas-med-ett-s/idle_master_extended) and [Idle Master by jshackles](https://github.com/jshackles/idle_master), originally based on work by Stumpokapow. Uses Steamworks.NET and Open Iconic. See the [license](LICENSE).
