﻿using System;
using System.Threading;
using Steamworks;
using IdleMaster;

namespace steam_idle
{
    static class Program
    {
        [STAThread]
        static int Main(string[] args)
        {
            IdleMaster.WindowsErrorReporting.DisableErrorWindows();
            try { return Run(args); }
            catch (Exception) { return (int)HelperExitCode.RuntimeFailure; }
        }

        private static int Run(string[] args)
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

            try
            {
                if (Console.IsOutputRedirected) Console.WriteLine("READY");
                while (true)
                {
                    SteamAPI.RunCallbacks();
                    Thread.Sleep(100);
                }
            }
            finally { SteamAPI.Shutdown(); }
        }
    }
}