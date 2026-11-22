using System.Globalization;
using System.Runtime.InteropServices;

internal static class Program
{
    private static string Text(string en, string fr) =>
        CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fr" ? fr : en;

    private static int Error(int code, string message)
    {
        Console.Error.WriteLine(message);
        return code;
    }

    private static int Main(string[] args)
    {
        const string usage = "dotnet IdleMaster.Linux.dll <AppID>";
        if (args is ["--help"] or ["-h"])
        {
            Console.WriteLine(usage);
            return 0;
        }

        if (args.Length != 1 || !uint.TryParse(args[0], NumberStyles.None,
                CultureInfo.InvariantCulture, out uint appId) || appId == 0)
            return Error(2, Text("Enter one positive numeric AppID.", "Indique un seul AppID numérique positif.") + "  " + usage);

        if (!OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
            return Error(2, Text("Run this prototype on x64 Linux.", "Lance ce prototype sous Linux x64."));

        try
        {
            return Run(appId);
        }
        catch (DllNotFoundException)
        {
            return Error(3, Text("Cannot load libsteam_api.so. Follow the native library setup in linux-cli/README.md.",
                "Impossible de charger libsteam_api.so. Suis la procédure dans linux-cli/README.md."));
        }
        catch (Exception ex) when (ex is EntryPointNotFoundException or BadImageFormatException)
        {
            return Error(3, Text("Incompatible Steam library. Use the documented Linux x64 build.",
                "Bibliothèque Steam incompatible. Utilise la version Linux x64 indiquée dans le guide."));
        }
    }

    private static int Run(uint appId)
    {
        using var stop = new ManualResetEventSlim(false);
        void RequestStop(PosixSignalContext signal)
        {
            signal.Cancel = true;
            stop.Set();
        }
        using var interrupt = PosixSignalRegistration.Create(PosixSignal.SIGINT, RequestStop);
        using var terminate = PosixSignalRegistration.Create(PosixSignal.SIGTERM, RequestStop);

        // Steam reads the native process environment during initialization.
        if (Steam.SetEnvironment("SteamAppId", appId.ToString(CultureInfo.InvariantCulture), 1) != 0)
            return Error(3, Text("Could not set the game ID.", "Impossible de définir l’AppID."));

        if (Steam.Initialize() != 0)
            return Error(4, Text($"Steam could not initialize AppID {appId}. Open the Linux Steam client with the same user and check that your account owns the game.",
                $"Steam n’a pas pu initialiser l’AppID {appId}. Ouvre Steam sous le même utilisateur Linux et vérifie que ton compte possède ce jeu."));

        try
        {
            var utils = Steam.SteamAPI_SteamUtils_v011();
            if (utils == IntPtr.Zero)
                return Error(4, Text("Steam could not provide the active game ID.", "Steam n’a pas fourni l’AppID actif."));

            uint actual = Steam.SteamAPI_ISteamUtils_GetAppID(utils);
            if (actual != appId)
                return Error(4, Text($"Requested AppID {appId}, but Steam returned {actual}. Stopping.",
                    $"AppID demandé : {appId}. Steam indique {actual}. Arrêt du test."));

            Console.WriteLine(Text($"Steam confirmed AppID {actual} locally. Check your Steam status, then press Ctrl+C to stop.",
                $"Steam a confirmé localement l’AppID {actual}. Vérifie ton statut dans Steam, puis appuie sur Ctrl+C."));
            while (!stop.Wait(50))
                Steam.SteamAPI_RunCallbacks();
        }
        finally
        {
            Steam.SteamAPI_Shutdown();
            Console.WriteLine(Text("Steam session closed.", "Session Steam fermée."));
        }
        return 0;
    }
}

internal static class Steam
{
    private const string Library = "libsteam_api.so";

    [DllImport("libc", EntryPoint = "setenv")]
    internal static extern int SetEnvironment(string name, string value, int overwrite);

    internal static int Initialize()
    {
        if (Environment.GetEnvironmentVariable("IDLE_MASTER_VERBOSE") == "1")
            return SteamAPI_InitFlat(IntPtr.Zero);

        // The native SDK prints loader diagnostics directly to stderr during initialization.
        using var sink = File.OpenHandle("/dev/null", FileMode.Open, FileAccess.Write);
        int original = dup(2);
        if (original < 0) return SteamAPI_InitFlat(IntPtr.Zero);
        try
        {
            dup2(sink.DangerousGetHandle().ToInt32(), 2);
            return SteamAPI_InitFlat(IntPtr.Zero);
        }
        finally
        {
            dup2(original, 2);
            close(original);
        }
    }

    [DllImport("libc")]
    private static extern int dup(int fd);

    [DllImport("libc")]
    private static extern int dup2(int fd, int destination);

    [DllImport("libc")]
    private static extern int close(int fd);

    [DllImport(Library)]
    private static extern int SteamAPI_InitFlat(IntPtr errorMessage);

    [DllImport(Library)]
    internal static extern IntPtr SteamAPI_SteamUtils_v011();

    [DllImport(Library)]
    internal static extern uint SteamAPI_ISteamUtils_GetAppID(IntPtr utils);

    [DllImport(Library)]
    internal static extern void SteamAPI_RunCallbacks();

    [DllImport(Library)]
    internal static extern void SteamAPI_Shutdown();
}
