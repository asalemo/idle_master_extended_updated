# Idle Master Extended Updated

Idle Master checks your Steam badges for remaining trading card drops and starts a small helper for each selected game. Steam determines when cards drop. This fork targets **Windows x64** and includes changes to game switching, startup responsiveness and session recovery.

[Français](README.fr.md) · [Download the Windows release candidate](https://github.com/asalemo/idle_master_extended_updated/releases/tag/v1.11.0-rc.8) · [User guide](https://github.com/asalemo/idle_master_extended_updated/wiki)

[Test the RC8 card counter](https://github.com/asalemo/idle_master_extended_updated/wiki/Getting-started#test-the-rc8-card-counter)

## Get started

[RC8 guide: sign in to Steam, change language and test one game](https://github.com/asalemo/idle_master_extended_updated/wiki/Getting-started) · [Test both sign-in methods and check what remains after exit](https://github.com/asalemo/idle_master_extended_updated/wiki/RC8-sign-in-privacy-test)

1. Download the **win-x64 ZIP** from the release and extract the entire archive. The GitHub source archives are for developers.
2. Open Steam and sign in. Run `IdleMasterExtended.exe` from the extracted folder with `steam-idle.exe`, the DLLs, `runtimes` and `Languages` beside it.
3. Use the sign-in link to configure your Steam Community session. Choose an idling mode in **File → Settings**.

## Sign-in options in RC8

Select **Sign In**, then choose **Sign in with Steam** or **Enter session details manually**. The manual option accepts sessionid, steamLoginSecure and an optional parental cookie for this run. Preferences persist. Diagnostics and activity counters stay in memory. Browser sign-in uses a temporary private WebView2 profile and attempts to remove it when the browser closes. A forced shutdown can leave temporary files behind.

## Interface language

In **File → Settings**, choose **Windows language** or a language by its native name, then restart the application.
Steam sign-in, single-game testing and Diagnostics are available in English, French, German, Spanish, Italian and Portuguese (Portugal and Brazil). Other interface languages use English for missing translations.

## Queue monitoring in RC8

Open **File → Game queue** to inspect active, waiting and deferred titles, skip the active game, suspend its automatic rotation or move a title to the front. The solo view shows the last confirmed read and rotation progress. Session totals count decreases per AppID, completed games and failed reads. With default sorting, eligible solo games can start while later badge pages load. Global card-count sorting waits for the full library.

## Adjustable rotation delay in RC8

Open **File → Settings** and set **Switch after unchanged drops (min)** to a value from 1 to 1440. The default is 120 minutes. For a short test, select 3 minutes and run the automatic queue with one active game and at least one other eligible game. Diagnostics reports the elapsed time when the queue switches. Restore 120 minutes after testing if desired.

## Automatic queue in RC8

When idling one game, the configured delay (120 minutes by default) of successful checks with the same positive card count trigger a switch at the next check. Diagnostics shows the game, remaining count and elapsed time. Deferred games stay eligible for another turn. With only one eligible game, the helper stays active. A change of count, a failed read or a pause resets the observation period.

[Test game rotation and check elapsed time in Diagnostics](https://github.com/asalemo/idle_master_extended_updated/wiki/Verify-RC8-game-rotation)

## What to expect

Fast mode runs up to 30 eligible games in parallel while they are below two recorded hours. It checks progress every five minutes. Once a game qualifies for individual idling, or only one remains, it stays active between checks.

**Starting** means the helper has launched. **Currently in-game** appears after the helper confirms local Steam API initialization. If it exits, the Diagnostics menu identifies the game, AppID and reason. Failed helpers wait one minute between attempts. **File → Retry now** retries pending games immediately.

The profile name and orphan-process cleanup run in the background. An unreadable profile or temporary connection failure preserves the current session and schedules another startup check in one minute.

## RC2 on Windows

Open **File → Test a game** to search installed titles. Starting a test pauses the automatic queue. Stop the test or close its window, then resume the queue when ready. **File → Timings** shows the last badge request and processing times and the first helper confirmation. Repeated failures are grouped in **Diagnostics**, with retry and copy actions. Helpers run without a window.

[Test a single game](https://github.com/asalemo/idle_master_extended_updated/wiki/Test-a-single-game) · [Read errors in Diagnostics](https://github.com/asalemo/idle_master_extended_updated/wiki/Diagnostics-and-recovery)

## Build and contribute

Build the helper first, then the desktop solution in **Release | Any CPU** using Visual Studio with .NET desktop development and the .NET Framework 4.8 targeting pack. The resulting application requires x64 Windows. See the [build guide](https://github.com/asalemo/idle_master_extended_updated/wiki/Building-from-source) for paths, packaging and validation.

Based on [Idle Master Extended by Jonas Nilson](https://github.com/jonas-med-ett-s/idle_master_extended) and [Idle Master by jshackles](https://github.com/jshackles/idle_master), originally based on work by Stumpokapow. Uses Steamworks.NET and Open Iconic. See the [license](LICENSE).

[Compare the Steam badge and test three-minute rotation (result template)](https://github.com/asalemo/idle_master_extended_updated/wiki/Getting-started#compare-the-steam-badge-and-verify-automatic-rotation-in-three-minutes)
