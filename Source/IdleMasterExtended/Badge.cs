using System;
using System.Diagnostics;
using System.ComponentModel;
using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HtmlAgilityPack;
using IdleMasterExtended.Properties;

namespace IdleMasterExtended
{
    public class Badge
    {
        public double AveragePrice { get; set; }

        public int AppId { get; set; }

        public string Name { get; set; }

        public string StringId
        {
            get { return AppId.ToString(); }
            set { AppId = string.IsNullOrWhiteSpace(value) ? 0 : int.Parse(value); }
        }

        public int RemainingCard { get; set; }

        public double HoursPlayed { get; set; }


        private Process idleProcess;

        public bool InIdle { get { return idleProcess != null && !idleProcess.HasExited; } }

        private Action stopCurrentProcess;

        internal bool SteamReady { get; private set; }

        internal void Idle(ISynchronizeInvoke owner, Action<int?, string> reportExit)
        {
            if (InIdle) return;

            var process = new Process
            {
                StartInfo = new ProcessStartInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "steam-idle.exe"), StringId)
                {
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                }
            };
            SteamReady = false;
            process.OutputDataReceived += (sender, args) =>
            {
                if (args.Data == "READY")
                    PostToOwner(owner, () => { if (idleProcess == process) SteamReady = true; });
            };
            bool expectedStop = false;
            process.Exited += (sender, args) =>
            {
                if (!PostToOwner(owner, () =>
                {
                    try
                    {
                        if (!expectedStop && idleProcess == process) reportExit(process.ExitCode, null);
                    }
                    finally
                    {
                        if (idleProcess == process)
                        {
                            SteamReady = false;
                            idleProcess = null;
                            stopCurrentProcess = null;
                        }
                        process.Dispose();
                    }
                })) process.Dispose();
            };

            try
            {
                process.Start();
            }
            catch (Win32Exception ex)
            {
                process.Dispose();
                reportExit(null, ex.Message);
                return;
            }

            idleProcess = process;
            stopCurrentProcess = () =>
            {
                if (process.HasExited) return;
                try
                {
                    process.Kill();
                    expectedStop = true;
                }
                catch (InvalidOperationException) when (process.HasExited)
                {
                    // The helper exited independently before Kill could stop it.
                }
            };
            process.BeginOutputReadLine();
            process.EnableRaisingEvents = true;
        }

        private static bool PostToOwner(ISynchronizeInvoke owner, Action action)
        {
            try
            {
                owner.BeginInvoke(action, null);
                return true;
            }
            catch (InvalidOperationException)
            {
                // A test dialog can close before the helper finishes draining stdout.
                return false;
            }
        }

        public void StopIdle()
        {
            stopCurrentProcess?.Invoke();
        }

        public async Task<bool?> CanCardDrops()
        {
            try
            {
                var document = new HtmlDocument();
                var response = await CookieClient.GetHttpAsync(SteamProfile.ProfileUrl + "/gamecards/" + StringId);
                // Response should be empty. User should be unauthorised.
                if (string.IsNullOrEmpty(response))
                {
                    return null;
                }
                document.LoadHtml(response);

                var hoursNode = document.DocumentNode.SelectSingleNode("//div[@class=\"badge_title_stats_playtime\"]");
                var hours = Regex.Match(hoursNode.InnerText, @"[0-9\.,]+").Value;

                var cardNode = hoursNode.ParentNode.SelectSingleNode(".//span[@class=\"progress_info_bold\"]");
                var cards = cardNode == null ? string.Empty : Regex.Match(cardNode.InnerText, @"[0-9]+").Value;

                UpdateStats(cards, hours);
                return RemainingCard != 0;
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "Badge -> CanCardDrops, for id = " + AppId);
            }
            return null;
        }

        public void UpdateStats(string remaining, string hours)
        {
            RemainingCard = string.IsNullOrWhiteSpace(remaining) ? 0 : int.Parse(remaining);
            HoursPlayed = string.IsNullOrWhiteSpace(hours) ? 0 : double.Parse(hours, new NumberFormatInfo());
        }

        public override bool Equals(object obj)
        {
            var badge = obj as Badge;
            return badge != null && Equals(AppId, badge.AppId);
        }

        public override int GetHashCode()
        {
            return AppId.GetHashCode();
        }

        public override string ToString()
        {
            return string.IsNullOrWhiteSpace(Name) ? StringId : Name;
        }

        public Badge(string id, string name, string remaining, string hours)
          : this()
        {
            StringId = id;
            Name = name;
            UpdateStats(remaining, hours);
        }

        // CONSTRUCTOR
        public Badge()
        { }
    }
}
