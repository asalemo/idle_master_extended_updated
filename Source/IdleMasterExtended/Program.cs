using System;
using System.Windows.Forms;

namespace IdleMasterExtended
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            IdleMaster.WindowsErrorReporting.DisableErrorWindows();
            System.Net.WebRequest.DefaultCachePolicy = new System.Net.Cache.RequestCachePolicy(
                System.Net.Cache.RequestCacheLevel.NoCacheNoStore);

            Application.ThreadException += (o, a) => Logger.Exception(a.Exception);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            try { Application.Run(new frmMain()); }
            catch (Exception ex) { Logger.Exception(ex, "Desktop application stopped."); }
        }
    }
}
