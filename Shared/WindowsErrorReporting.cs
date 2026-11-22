using System;
using System.Runtime.InteropServices;

namespace IdleMaster
{
    internal static class WindowsErrorReporting
    {
        private const uint FailCriticalErrors = 0x0001;
        private const uint NoOpenFileErrorBox = 0x8000;
        private const uint NoReportingUI = 32;

        internal static void DisableErrorWindows()
        {
            SetErrorMode(GetErrorMode() | FailCriticalErrors | NoOpenFileErrorBox);
            uint flags;
            if (WerGetFlags(GetCurrentProcess(), out flags) >= 0)
                WerSetFlags(flags | NoReportingUI);
        }

        [DllImport("kernel32.dll")]
        private static extern uint GetErrorMode();
        [DllImport("kernel32.dll")]
        private static extern uint SetErrorMode(uint mode);
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll")]
        private static extern int WerGetFlags(IntPtr process, out uint flags);
        [DllImport("kernel32.dll")]
        private static extern int WerSetFlags(uint flags);
    }
}
