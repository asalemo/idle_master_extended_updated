﻿using System;
using System.Windows.Forms;
using Steamworks;
using IdleMaster;

namespace steam_idle
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            uint appId;
            if (args.Length != 1 || !uint.TryParse(args[0], out appId) || appId == 0)
                return (int)HelperExitCode.InvalidAppId;
            Environment.SetEnvironmentVariable("SteamAppId", appId.ToString());

            try
            {
                if (!SteamAPI.Init())
                    return (int)HelperExitCode.SteamInitializationFailed;
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is BadImageFormatException ||
                                       ex is EntryPointNotFoundException)
            {
                return (int)HelperExitCode.SteamLibraryUnavailable;
            }

            if (Console.IsOutputRedirected) Console.WriteLine("READY");
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new FormSteamIdle(appId));
            return (int)HelperExitCode.Closed;
        }
    }
}