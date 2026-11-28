using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using IdleMaster;
using System.Management;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using IdleMasterExtended.Properties;
using Steamworks;
using HtmlDocument = HtmlAgilityPack.HtmlDocument;
using System.Globalization;
using System.Runtime.InteropServices;

namespace IdleMasterExtended
{
    public partial class frmMain : Form
    {
        private Statistics statistics = new Statistics();
        public List<Badge> AllBadges { get; set; }

        public IEnumerable<Badge> CanIdleBadges
        {
            get { return AllBadges.Where(b => b.RemainingCard != 0).Where(b => (!Settings.Default.IdleOnlyPlayed) || (b.HoursPlayed) > 0.0); }
        }

        public bool IsCookieReady;
        public bool IsSteamReady;
        public int MaxSimultanousCards = 30;
        public int TimeLeft = 900;
        public int TimeSet = 300;
        public int RetryCount = 0;
        public int ReloadCount = 0;
        public int CardsRemaining { get { return CanIdleBadges.Sum(b => b.RemainingCard); } }
        public int GamesRemaining { get { return CanIdleBadges.Count(); } }
        public Badge CurrentBadge;

        private string lastBadgeTiming;
        private TimeSpan? firstConfirmation;
        private readonly Stopwatch firstHelperClock = new Stopwatch();
        private bool pausedForTest;
        private int idleSession;
        private bool IsCurrentThemeCustom;
        private bool IsCurrentIconsWhite;

        public frmMain()
        {
            InitializeComponent();
            Controls.Add(observation);
            helperDetails = new ToolTip(components);
            helperMonitor = new System.Windows.Forms.Timer(components) { Interval = 1000 };
            helperMonitor.Tick += (sender, args) => RefreshHelpers();
            retryHelpers.Click += (sender, args) =>
            {
                foreach (var badge in retryAt.Keys.ToList()) retryAt[badge] = DateTime.MinValue;
                RefreshHelpers();
            };
            fileToolStripMenuItem.DropDownItems.Add(retryHelpers);
            retryHelpers.Visible = false;
            diagnostics.Visible = false;
            mnuTop.Items.Add(diagnostics);
            AllBadges = new List<Badge>();
        }

        #region BADGES
        public async Task LoadBadgesAsync(bool startup = false)
        {
            int session = idleSession;
            Badge earlyGame = null;
            TimeSpan nextEarlyRead = TimeSpan.Zero;
            picReadingPage.Visible = true;

            if (Settings.Default.IdlingModeWhitelist)
            {
                AllBadges.Clear();

                foreach (var whitelistID in Settings.Default.whitelist)
                {
                    int applicationID;
                    if (int.TryParse(whitelistID, out applicationID)
                        && !AllBadges.Any(badge => badge.AppId.Equals(applicationID)))
                    {
                        AllBadges.Add(new Badge(whitelistID, "App ID: " + whitelistID, "-1", "0"));
                    }
                }
            }
            else
            {
                try
                {
                    var fetch = new Stopwatch();
                    var parse = Stopwatch.StartNew();
                    var byId = AllBadges.ToDictionary(b => b.StringId);
                    parse.Stop();
                    int totalBadgePages = 1;

                    for (var currentBadgePage = 1; currentBadgePage <= totalBadgePages; currentBadgePage++)
                    {
                        // Give an active game a read slot between background library pages.
                        if (earlyGame != null && earlyGame.SteamReady && sessionClock.Elapsed >= nextEarlyRead)
                        {
                            var read = await earlyGame.CanCardDrops();
                            if (session != idleSession || IsDisposed) return;
                            if (read.HasValue)
                            {
                                statistics.Observe(earlyGame.AppId, earlyGame.RemainingCard);
                                if (!read.Value) { ForgetHelper(earlyGame); earlyGame = null; }
                            }
                            else statistics.Failed();
                            nextEarlyRead = sessionClock.Elapsed + TimeSpan.FromMinutes(read.HasValue ? 5 : 1);
                        }
                        fetch.Start();
                        string response = await GetBadgePageAsync(currentBadgePage);
                        fetch.Stop();
                        parse.Start();
                        var htmlDocument = new HtmlDocument();
                        htmlDocument.LoadHtml(response);
                        if (session != idleSession) return;
                        if (currentBadgePage == 1)
                        {
                            totalBadgePages = ExtractTotalBadgePages(htmlDocument);
                        }

                        lblDrops.Text = string.Format(localization.strings.reading_badge_page + " {0}/{1}, " + localization.strings.please_wait, currentBadgePage, totalBadgePages);
                        ProcessBadgesOnPage(htmlDocument, byId);
                        parse.Stop();
                        if (startup && earlyGame == null && currentBadgePage < totalBadgePages && desiredHelpers.Count == 0)
                        {
                            var first = EarlyCandidate(QueueCandidates(), Settings.Default.sort, Settings.Default.OnlyOneGameIdle,
                                Settings.Default.fastMode || Settings.Default.OneThenMany);
                            if (first != null)
                            {
                                if (orphanCleanup == null) orphanCleanup = Task.Run((Action)CleanOrphanHelpers);
                                await orphanCleanup;
                                if (session != idleSession || IsDisposed || pausedForTest) return;
                                if (IsSteamReady && SteamProfile.HasSession)
                                {
                                    StartHelper(first);
                                    earlyGame = first;
                                }
                            }
                        }
                    }
                    lastBadgeTiming = string.Format(localization.strings.ResourceManager.GetString("badge_timing"),
                        totalBadgePages, fetch.Elapsed.TotalMilliseconds, parse.Elapsed.TotalMilliseconds);
                }
                catch (Exception ex)
                {
                    if (session != idleSession) return;
                    if (earlyGame != null) StopIdle();
                    statistics.Failed();
                    Logger.Exception(ex, "Could not load Steam badges.");
                    ResetFormDesign();
                    ReloadCount = 1;
                    tmrBadgeReload.Enabled = true;
                    return;
                }
            }

            ResetRetryCountAndUpdateApplicationState();
        }

        private async Task<string> GetBadgePageAsync(int pageNumber)
        {
            var profileLink = SteamProfile.ProfileUrl + "/badges";
            var pageURL = string.Format("{0}/?p={1}", profileLink, pageNumber);
            var response = await CookieClient.GetHttpAsync(pageURL);
            CheckIfResponseIsNullWithRetryCount(response);
            return response;
        }

        private static int ExtractTotalBadgePages(HtmlDocument document)
        {
            int lastPage = 1;
            var links = document.DocumentNode.SelectNodes("//a[@class='pagelink']");
            if (links == null) return lastPage;
            foreach (var link in links)
            {
                string href = WebUtility.HtmlDecode(link.GetAttributeValue("href", ""));
                var page = Regex.Match(href, @"[?&]p=(\d+)(?:&|#|$)");
                if (int.TryParse(page.Groups[1].Value, out int value)) lastPage = Math.Max(lastPage, value);
            }
            return lastPage;
        }

        public void SortBadges(string method)
        {
            lblDrops.Text = localization.strings.sorting_results;
            switch (method)
            {
                case "mostcards":
                    AllBadges = AllBadges.OrderByDescending(b => b.RemainingCard).ToList();
                    break;
                case "leastcards":
                    AllBadges = AllBadges.OrderBy(b => b.RemainingCard).ToList();
                    break;
                default:
                    return;
            }
        }

        private void CheckIfResponseIsNullWithRetryCount(string response)
        {
            // Response should be empty. User should be unauthorised.
            if (string.IsNullOrEmpty(response))
            {
                RetryCount++;
                throw new Exception("Response is null or empty. Added (+1) to RetryCount");
            }
        }
        
        /// <summary>
        /// Processes all badges on page
        /// </summary>
        /// <param name="document">HTML document (1 page) from x</param>
        private void ProcessBadgesOnPage(HtmlDocument document, Dictionary<string, Badge> byId)
        {
            var rows = document.DocumentNode.SelectNodes("//div[@class='badge_row is_link']");
            if (rows == null && document.DocumentNode.SelectSingleNode("//div[@class='badges_sheet']") != null) return;
            if (rows == null) throw new InvalidDataException("Unrecognized Steam badge page.");
            foreach (var badge in rows)
            {
                var appIdNode = badge.SelectSingleNode(".//a[@class=\"badge_row_overlay\"]").Attributes["href"].Value;
                var appid = Regex.Match(appIdNode, @"gamecards/(\d+)/").Groups[1].Value;

                if (string.IsNullOrWhiteSpace(appid) || Settings.Default.blacklist.Contains(appid) || appid == "368020" || appid == "335590" || appIdNode.Contains("border=1"))
                {
                    continue;
                }

                var hoursNode = badge.SelectSingleNode(".//div[@class=\"badge_title_stats_playtime\"]");
                var hours = hoursNode == null ? string.Empty : Regex.Match(hoursNode.InnerText, @"[0-9\.,]+").Value;

                var nameNode = badge.SelectSingleNode(".//div[@class=\"badge_title\"]");
                var name = WebUtility.HtmlDecode(nameNode.FirstChild.InnerText).Trim();

                var cards = Badge.ReadRemainingCards(badge);
                if (cards == null) continue;

                if (byId.TryGetValue(appid, out var badgeInMemory))
                {
                    badgeInMemory.UpdateStats(cards, hours);
                    statistics.Observe(badgeInMemory.AppId, badgeInMemory.RemainingCard);
                }
                else
                {
                    var added = new Badge(appid, name, cards, hours);
                    AllBadges.Add(added);
                    statistics.Observe(added.AppId, added.RemainingCard);
                    byId.Add(appid, added);
                }
            }
        }

        internal static Badge EarlyCandidate(IEnumerable<Badge> candidates, string sort, bool solo, bool preferPlayed)
        {
            if (!string.IsNullOrEmpty(sort) && sort != "default") return null;
            if (solo) return candidates.FirstOrDefault();
            return preferPlayed ? candidates.FirstOrDefault(b => b.HoursPlayed >= 2) : null;
        }

        private int? heldGame;
        private int? priorityGame;
        private string observationReason;
        private DateTime? failedAt;
        private readonly Label observation = new Label { AutoSize = false, AutoEllipsis = true };
        private static string TextFor(string key) => localization.strings.ResourceManager.GetString(key);

        private readonly Stopwatch sessionClock = Stopwatch.StartNew();
        private static TimeSpan RotationThreshold => TimeSpan.FromMinutes(Settings.Default.EffectiveRotationMinutes);
        private readonly DropProgress dropProgress = new DropProgress();
        private readonly List<int> deferredGames = new List<int>();

        private List<Badge> QueueCandidates()
        {
            var eligible = CanIdleBadges.ToList();
            if (deferredGames.Count == 0) return eligible;
            var fresh = eligible.Where(b => !deferredGames.Contains(b.AppId)).ToList();
            if (fresh.Count > 0) return fresh;
            // Revisit deferred titles in the order they stalled.
            return eligible.OrderBy(b => deferredGames.IndexOf(b.AppId)).Take(1).ToList();
        }

        public async Task CheckCardDrops(Badge badge)
        {
            int session = idleSession;
            int previousCount = badge.RemainingCard;
            bool? hasDrops = await badge.CanCardDrops();
            if (session != idleSession || CurrentBadge != badge) return;
            if (!hasDrops.HasValue)
            {
                dropProgress.Reset();
                failedAt = DateTime.Now;
                statistics.Failed();
                observationReason = TextFor("rotation_read_failed");
                TimeLeft = 60;
                return;
            }
            failedAt = null;
            statistics.Observe(badge.AppId, badge.RemainingCard);
            if (previousCount != badge.RemainingCard) observationReason = TextFor("rotation_count_changed");
            if (!hasDrops.Value)
            {
                NextIdle();
                return;
            }
            if (TryAdvanceStalledGame(badge)) return;
            TimeLeft = dropProgress.NextCheckSeconds(badge.RemainingCard == 1 ? 300 : 900, sessionClock.Elapsed, RotationThreshold);

            lblCurrentRemaining.Text = badge.RemainingCard == -1 ? "" : badge.RemainingCard + " " + localization.strings.card_drops_remaining;
            pbIdle.Maximum = CardsRemaining > pbIdle.Maximum ? CardsRemaining : pbIdle.Maximum;
            pbIdle.Value = pbIdle.Maximum - CardsRemaining;
            lblHoursPlayed.Text = badge.HoursPlayed + " " + localization.strings.hrs_on_record;
            UpdateStateInfo();
        }

        private bool TryAdvanceStalledGame(Badge badge)
        {
            if (!badge.SteamReady || !badge.InIdle)
            {
                dropProgress.Reset();
                return false;
            }

            var elapsed = dropProgress.Observe(badge.RemainingCard, sessionClock.Elapsed, RotationThreshold);
            if (!elapsed.HasValue || heldGame == badge.AppId) return false;
            bool rotate = CanIdleBadges.Any(b => b.AppId != badge.AppId);
            if (rotate || !dropProgress.Reported)
            {
                var key = rotate ? "cards_stalled_next" : "cards_stalled_only";
                string message = string.Format(localization.strings.ResourceManager.GetString(key),
                    badge.RemainingCard, (int)elapsed.Value.TotalHours, elapsed.Value.Minutes);
                RecordDiagnostic(badge, message, 1, allowRetry: false);
                dropProgress.Reported = true;
            }
            if (!rotate) return false;

            deferredGames.Remove(badge.AppId);
            deferredGames.Add(badge.AppId);
            NextIdle();
            return true;
        }

        private void ResetRetryCountAndUpdateApplicationState()
        {
            RetryCount = 0;
            SortBadges(Settings.Default.sort);

            picReadingPage.Visible = false;
            UpdateStateInfo();

            if (CardsRemaining == 0)
            {
                IdleComplete();
            }
        }
        #endregion

        #region IDLING
        private Task orphanCleanup;
        private int startRequest;

        private static void CleanOrphanHelpers()
        {
            try
            {
                if (Process.GetProcessesByName("steam-idle").Length == 0) return;
                string owner = WindowsIdentity.GetCurrent().Name;
                using (var current = Process.GetCurrentProcess())
                using (var search = new ManagementObjectSearcher("SELECT * FROM Win32_Process WHERE Name='steam-idle.exe'"))
                using (var rows = search.Get())
                {
                    foreach (ManagementObject row in rows)
                    using (row)
                    {
                        try
                        {
                            int parentId = Convert.ToInt32(row["ParentProcessId"]);
                            if (parentId == current.Id) continue;
                            try
                            {
                                using (var parent = Process.GetProcessById(parentId))
                                    if (parent.ProcessName == current.ProcessName) continue;
                            }
                            catch (ArgumentException) { /* The parent has already exited. */ }

                            string[] account = { "", "" };
                            using (var helper = Process.GetProcessById(Convert.ToInt32(row["ProcessId"])))
                            {
                                if (helper.ProcessName == "steam-idle" &&
                                    Convert.ToInt32(row.InvokeMethod("GetOwner", account)) == 0 &&
                                    account[1] + "\\" + account[0] == owner)
                                    helper.Kill();
                            }
                        }
                        catch (ArgumentException) { /* The helper has already exited. */ }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "Could not clean up orphaned helpers.");
            }
        }

        private async void StartIdle()
        {
            if (pausedForTest) return;
            int session = idleSession;
            int request = ++startRequest;
            if (orphanCleanup == null) orphanCleanup = Task.Run((Action)CleanOrphanHelpers);
            await orphanCleanup;
            if (IsDisposed || Disposing || session != idleSession || request != startRequest) return;

            // Check if user is authenticated and if any badge left to idle
            // There should be check for IsCookieReady, but property is set in timer tick, so it could take some time to be set.
            if (!SteamProfile.HasSession || !IsSteamReady)
            {
                StopIdle();
                tmrReadyToGo.Enabled = true;
            }
            else
            {
                if (ReloadCount != 0)
                {
                    return;
                }

                var candidates = QueueCandidates();
                if (candidates.Count > 0)
                {
                    EnableCardDropCheckTimer();
                    lblCurrentStatus.Enabled = false;

                    tmrStatistics.Enabled = true;
                    tmrStatistics.Start();

                    var preferred = CanIdleBadges.FirstOrDefault(b => b.AppId == priorityGame);
                    if (preferred != null)
                    {
                        priorityGame = null;
                        deferredGames.Remove(preferred.AppId);
                        StartSoloIdle(preferred);
                    }
                    else if (Settings.Default.OnlyOneGameIdle)
                    {
                        StartSoloIdle(candidates.First());
                    }
                    else
                    {
                        var ready = candidates.FirstOrDefault(b => b.HoursPlayed >= 2);
                        if (Settings.Default.fastMode)
                        {
                            if (ready != null || candidates.Count == 1)
                                StartSoloIdle(ready ?? candidates.First());
                            else
                                StartMultipleIdle();
                        }
                        else if (Settings.Default.OneThenMany && ready != null)
                            StartSoloIdle(ready);
                        else if (Settings.Default.OneThenMany || candidates.Count(b => b.HoursPlayed < 2) >= 2)
                            StartMultipleIdle();
                        else
                            StartSoloIdle(candidates.First());
                    }
                }
                else
                {
                    IdleComplete();
                }

                UpdateStateInfo();
            }
        }

        public void StopIdle()
        {
            idleSession++;
            heldGame = null;
            failedAt = null;
            observationReason = null;
            observation.Visible = false;
            dropProgress.Reset();
            firstHelperClock.Reset();
            helperMonitor.Stop();
            desiredHelpers.Clear();
            retryAt.Clear();
            retryHelpers.Visible = false;
            tmrStartNext.Stop();
            try
            {
                lblGameName.Visible = false;
                picApp.Image = null;
                picApp.Visible = false;
                GamesState.Visible = false;
                btnPause.Visible = false;
                btnSkip.Visible = false;
                lblCurrentStatus.Text = localization.strings.not_ingame;
                lblHoursPlayed.Visible = false;
                picIdleStatus.Visible = false;

                // Stop the card drop check timer
                DisableCardDropCheckTimer();

                // Stop the statistics timer
                tmrStatistics.Stop();

                // Hide the status bar
                ssFooter.Visible = false;

                // Resize the form
                var graphics = CreateGraphics();
                var scale = graphics.DpiY * 2.000;
                Height = Convert.ToInt32(scale);

                // Kill the idling process
                foreach (var badge in AllBadges.Where(b => b.InIdle))
                    badge.StopIdle();
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "frmMain -> StopIdle -> An attempt to stop the idling processes resulted in an exception.");
            }
        }

        private void NextIdle()
        {
            // Stop idling the current game
            StopIdle();

            // Check if user is authenticated and if any badge left to idle
            // There should be check for IsCookieReady, but property is set in timer tick, so it could take some time to be set.
            if (!SteamProfile.HasSession || !IsSteamReady)
            {
                StopIdle();
                tmrReadyToGo.Enabled = true;
            }
            else
            {
                if (CanIdleBadges.Any())
                {
                    // Give the user notification that the next game will start soon
                    lblCurrentStatus.Text = localization.strings.loading_next;

                    // Make a short but random amount of time pass
                    var rand = new Random();
                    var wait = rand.Next(3, 9);
                    wait = wait * 1000;

                    tmrStartNext.Interval = wait;
                    tmrStartNext.Enabled = true;

                    UpdateStateInfo();
                }
                else
                {
                    IdleComplete();
                }
            }
        }

        private readonly ToolStripMenuItem diagnostics = new ToolStripMenuItem();

        private readonly ToolTip helperDetails;
        private readonly System.Windows.Forms.Timer helperMonitor;
        private readonly HashSet<Badge> desiredHelpers = new HashSet<Badge>();
        private readonly Dictionary<Badge, DateTime> retryAt = new Dictionary<Badge, DateTime>();
        private readonly ToolStripMenuItem retryHelpers = new ToolStripMenuItem();

        private void RefreshHelpers()
        {
            if (desiredHelpers.Count == 0) return;
            foreach (var badge in retryAt.Where(p => p.Value <= DateTime.UtcNow).Select(p => p.Key).ToList())
            {
                retryAt.Remove(badge);
                if (desiredHelpers.Contains(badge)) StartHelper(badge);
            }
            retryHelpers.Visible = retryAt.Count > 0;
            int ready = desiredHelpers.Count(b => b.SteamReady && b.InIdle);
            if (ready > 0 && firstHelperClock.IsRunning)
            {
                firstHelperClock.Stop();
                firstConfirmation = firstHelperClock.Elapsed;
            }
            if (retryAt.Count > 0)
            {
                var next = retryAt.OrderBy(p => p.Value).First();
                var seconds = Math.Max(0, (int)Math.Ceiling((next.Value - DateTime.UtcNow).TotalSeconds));
                lblCurrentStatus.Text = string.Format(localization.strings.ResourceManager.GetString("helper_retry_status"), next.Key, seconds);
            }
            else
                lblCurrentStatus.Text = ready == desiredHelpers.Count ? localization.strings.currently_ingame
                    : localization.strings.ResourceManager.GetString("helper_starting");
            UpdateStateInfo();
        }

        private void ForgetHelper(Badge badge)
        {
            desiredHelpers.Remove(badge);
            retryAt.Remove(badge);
            badge.StopIdle();
        }

        private void StartHelper(Badge badge)
        {
            if (desiredHelpers.Count == 0)
            {
                firstConfirmation = null;
                firstHelperClock.Restart();
            }
            desiredHelpers.Add(badge);
            helperMonitor.Start();
            if (retryAt.ContainsKey(badge)) return;
            int session = idleSession;
            badge.Idle(this, (code, detail) =>
            {
                if (Disposing || IsDisposed || session != idleSession || !desiredHelpers.Contains(badge)) return;
                if (badge == CurrentBadge) { dropProgress.Reset(); observationReason = TextFor("rotation_helper_reset"); }
                retryAt[badge] = DateTime.UtcNow.AddMinutes(1);
                RecordHelperFailure(badge, code, detail);
                UpdateStateInfo();
            });
        }

        internal static string HelperFailureReason(int? code, string detail)
        {
            string key;
            switch ((HelperExitCode?)code)
            {
                case null: key = "helper_launch_failed"; break;
                case HelperExitCode.SteamInitializationFailed: key = "helper_steam_failed"; break;
                case HelperExitCode.SteamLibraryUnavailable: key = "helper_library_failed"; break;
                case HelperExitCode.InvalidAppId: key = "helper_invalid_id"; break;
                case HelperExitCode.RuntimeFailure: key = "helper_runtime_failure"; break;
                case HelperExitCode.Closed: key = "helper_closed"; break;
                default: key = "helper_unexpected_exit"; break;
            }
            return string.Format(localization.strings.ResourceManager.GetString(key), detail, code);
        }

        private sealed class DiagnosticItem : ToolStripMenuItem
        {
            internal int AppId;
            internal string Reason;
            internal int Count;
        }

        private void RecordHelperFailure(Badge badge, int? code, string detail)
        {
            string reason = HelperFailureReason(code, detail);
            RecordDiagnostic(badge, reason, 1);
        }

        private void RecordDiagnostic(Badge badge, string reason, int occurrences, bool allowRetry = true)
        {
            int appId = badge == null ? 0 : badge.AppId;
            var item = diagnostics.DropDownItems.OfType<DiagnosticItem>()
                .FirstOrDefault(i => i.AppId == appId && i.Reason == reason);
            if (item == null)
            {
                item = new DiagnosticItem { AppId = appId, Reason = reason };
                if (badge != null && allowRetry)
                {
                    var retry = item.DropDownItems.Add(localization.strings.ResourceManager.GetString("helper_retry_now"));
                    item.DropDownOpening += (sender, args) => retry.Enabled = retryAt.ContainsKey(badge);
                    retry.Click += (sender, args) =>
                    {
                        if (!retryAt.ContainsKey(badge)) return;
                        retryAt[badge] = DateTime.MinValue;
                        RefreshHelpers();
                    };
                }
                item.DropDownItems.Add(localization.strings.ResourceManager.GetString("copy_details"), null, (sender, args) =>
                {
                    try { Clipboard.SetDataObject(item.Text, true, 0, 0); }
                    catch (ExternalException) { Logger.Report(localization.strings.ResourceManager.GetString("clipboard_busy")); }
                });
                if (diagnostics.DropDownItems.Count == 50)
                {
                    var oldest = diagnostics.DropDownItems[49];
                    diagnostics.DropDownItems.Remove(oldest);
                    oldest.Dispose();
                }
            }
            else diagnostics.DropDownItems.Remove(item);

            item.Count += occurrences;
            string attempts = string.Format(localization.strings.ResourceManager.GetString("failure_count"), item.Count);
            string source = badge == null ? "" : $"{badge} (AppID {appId}) — ";
            item.Text = $"{DateTime.Now:T}  {source}{reason} ({attempts})";
            diagnostics.DropDownItems.Insert(0, item);
            diagnostics.Text = localization.strings.ResourceManager.GetString("diagnostics_title") + $" ({diagnostics.DropDownItems.Count})";
            diagnostics.Visible = true;
        }

        public void StartSoloIdle(Badge badge)
        {
            // Set the currentAppID value
            foreach (var previous in desiredHelpers.Where(b => b != badge).ToList()) ForgetHelper(previous);
            if (CurrentBadge != badge)
            {
                dropProgress.Reset();
                failedAt = null;
                heldGame = null;
                observationReason = null;
            }
            CurrentBadge = badge;

            // Place user "In game" for card drops
            StartHelper(CurrentBadge);

            // Update game name
            lblCurrentStatus.Enabled = false;
            lblGameName.Visible = true;
            lblGameName.Text = CurrentBadge.Name;

            GamesState.Visible = false;
            gameToolStripMenuItem.Enabled = true;

            // Update game image
            picApp.ErrorImage = null;
            picApp.ImageLocation = "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/" + CurrentBadge.StringId + "/header.jpg";
            picApp.Visible = true;

            // Update label controls
            lblCurrentRemaining.Text = badge.RemainingCard == -1 ? "" : CurrentBadge.RemainingCard + " " + localization.strings.card_drops_remaining;
            RefreshHelpers();
            lblHoursPlayed.Visible = !Settings.Default.IdlingModeWhitelist;
            lblHoursPlayed.Text = CurrentBadge.HoursPlayed + " " + localization.strings.hrs_on_record;

            // Set progress bar values and show the footer
            pbIdle.Maximum = CardsRemaining > pbIdle.Maximum ? CardsRemaining : pbIdle.Maximum;
            ssFooter.Visible = true;

            // Start the animated "working" gif
            picIdleStatus.Visible = true; // Settings.Default.customTheme ? Resources.imgSpinInv : Resources.imgSpin;

            // Start the timer that will check if drops remain
            EnableCardDropCheckTimer();

            // Reset the timer
            TimeLeft = 2; // Establish the first count promptly, including for short rotation delays.

            // Set the correct buttons on the form for pause / resume
            ShowInterruptiveButtons();

            observation.Visible = !Settings.Default.IdlingModeWhitelist;
            observation.SetBounds(lblHoursPlayed.Left, lblHoursPlayed.Bottom + 3, ClientSize.Width - 20, Font.Height * 4);
            var scale = CreateGraphics().DpiY * 3.9 + (observation.Visible ? observation.Height + 5 : 0);
            Height = Convert.ToInt32(scale);
        }

        public void StartMultipleIdle()
        {
            dropProgress.Reset();
            observation.Visible = false;
            CurrentBadge = null;
            // Start the idling processes
            UpdateIdleProcesses();

            // Update label controls
            lblCurrentRemaining.Text = localization.strings.update_games_status;
            RefreshHelpers();
            lblCurrentStatus.Enabled = false;

            lblGameName.Visible = false;
            lblHoursPlayed.Visible = false;
            ssFooter.Visible = true;
            gameToolStripMenuItem.Enabled = false;

            // Start the animated "working" gif
            picIdleStatus.Visible = true; //Image = Settings.Default.customTheme ? Resources.imgSpinInv : Resources.imgSpin;

            // Start the timer that will check if drops remain
            EnableCardDropCheckTimer();

            // Reset the timer
            TimeLeft = Settings.Default.fastMode ? 300 : 360;

            // Show game
            GamesState.Visible = true;
            picApp.Visible = false;
            RefreshGamesStateListView();

            ShowInterruptiveButtons();

            var scale = CreateGraphics().DpiY * 3.86;
            Height = Convert.ToInt32(scale);
        }

        public void IdleComplete()
        {
            // Deactivate the timer control and inform the user that the program is finished
            lblCurrentStatus.Text = localization.strings.idling_complete;
            lblCurrentStatus.Enabled = true;

            lblGameName.Visible = false;
            btnPause.Visible = false;
            btnSkip.Visible = true;
            // TODO: Refresh button?

            // Resize the form
            var graphics = CreateGraphics();
            var scale = graphics.DpiY * 2.000;
            Height = Convert.ToInt32(scale);

            if (Settings.Default.ShutdownWindowsOnDone)
            {
                Settings.Default.ShutdownWindowsOnDone = false;
                Settings.Default.Save();

                StartShutdownProcess();

                if (MessageBox.Show(localization.strings.ResourceManager.GetString("shutdown_pending"),
                                    localization.strings.ResourceManager.GetString("session_completed"), MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.Cancel)
                {
                    AbortShutdownProcess();
                }
                else
                {
                    Form1_Closing(this, null);
                }
            }
        }

        private void ShowQueue()
        {
            using (var dialog = new Form { Text = TextFor("queue_title"), Width = 620, Height = 420,
                StartPosition = FormStartPosition.CenterParent })
            using (var timer = new System.Windows.Forms.Timer { Interval = 1000 })
            {
                var list = new ListBox { Dock = DockStyle.Fill, DisplayMember = "Name" };
                var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true };
                var totals = new Label { Dock = DockStyle.Top, Height = 30 };
                var skip = new Button { Text = TextFor("queue_skip"), AutoSize = true };
                var hold = new Button { AutoSize = true };
                var front = new Button { Text = TextFor("queue_front"), AutoSize = true };
                actions.Controls.AddRange(new Control[] { skip, hold, front });
                dialog.Controls.Add(list);
                dialog.Controls.Add(actions);
                dialog.Controls.Add(totals);
                list.FormattingEnabled = true;
                list.Format += (o, e) =>
                {
                    var badge = (Badge)e.ListItem;
                    var state = desiredHelpers.Contains(badge) ? TextFor("queue_active")
                        : deferredGames.Contains(badge.AppId) ? TextFor("queue_deferred") : TextFor("queue_waiting");
                    e.Value = state + " · " + badge.Name + " (" + badge.AppId + ")";
                };
                Action refresh = () =>
                {
                    var selected = list.SelectedItem as Badge;
                    var eligible = CanIdleBadges.ToList();
                    var ordered = eligible.OrderBy(b => desiredHelpers.Contains(b) ? 0 : b.AppId == priorityGame ? 1
                        : deferredGames.Contains(b.AppId) ? 4
                        : (Settings.Default.fastMode || Settings.Default.OneThenMany) && b.HoursPlayed >= 2 ? 2 : 3)
                        .ThenBy(b => deferredGames.IndexOf(b.AppId)).ToArray();
                    list.BeginUpdate();
                    try
                    {
                        list.Items.Clear();
                        list.Items.AddRange(ordered);
                        list.SelectedItem = selected ?? CurrentBadge;
                    }
                    finally { list.EndUpdate(); }
                    bool active = CurrentBadge != null && desiredHelpers.Contains(CurrentBadge);
                    skip.Enabled = active && eligible.Count > 1;
                    hold.Enabled = active && !Settings.Default.IdlingModeWhitelist;
                    hold.Text = TextFor(active && heldGame == CurrentBadge.AppId ? "queue_release" : "queue_hold");
                    totals.Text = string.Format(TextFor("queue_totals"), statistics.getSessionCardIdled(), statistics.Completed, statistics.Failures);
                };
                skip.Click += (o, e) =>
                {
                    if (CurrentBadge == null || !desiredHelpers.Contains(CurrentBadge) || !CanIdleBadges.Any(b => b != CurrentBadge)) return;
                    deferredGames.Remove(CurrentBadge.AppId);
                    deferredGames.Add(CurrentBadge.AppId);
                    NextIdle();
                    refresh();
                };
                hold.Click += (o, e) =>
                {
                    if (CurrentBadge == null || !desiredHelpers.Contains(CurrentBadge)) return;
                    heldGame = heldGame == CurrentBadge.AppId ? (int?)null : CurrentBadge.AppId;
                    refresh();
                };
                front.Click += (o, e) =>
                {
                    var selected = list.SelectedItem as Badge;
                    if (selected == null || !CanIdleBadges.Contains(selected)) return;
                    deferredGames.Remove(selected.AppId);
                    priorityGame = selected.AppId;
                    if (desiredHelpers.Count > 0 && CurrentBadge != selected) NextIdle();
                    refresh();
                };
                refresh();
                timer.Tick += (o, e) => refresh();
                timer.Start();
                dialog.ShowDialog(this);
            }
        }

        private void RefreshObservation()
        {
            if (!observation.Visible || CurrentBadge == null) return;
            var last = statistics.LastRead(CurrentBadge.AppId);
            string rotation = heldGame == CurrentBadge.AppId ? TextFor("queue_held")
                : !CanIdleBadges.Any(b => b.AppId != CurrentBadge.AppId) ? TextFor("queue_only")
                : string.Format(TextFor("queue_elapsed"), (int)dropProgress.Elapsed(sessionClock.Elapsed).TotalMinutes,
                    Settings.Default.EffectiveRotationMinutes);
            observation.Text = string.Format(TextFor("queue_last"), last.HasValue ? last.Value.ToString("T") : "—")
                + Environment.NewLine + rotation + Environment.NewLine
                + (failedAt.HasValue ? string.Format(TextFor("queue_retry"), failedAt.Value.ToString("T"), TimeLeft) : observationReason);
        }

        internal void UpdateStateInfo()
        {
            RefreshObservation();
            if (ReloadCount == 0)
            {
                int numberOfCardsInIdle = CanIdleBadges.Count(b => b.SteamReady && b.InIdle);

                lblIdle.Text = string.Format(
                    "{0} " + localization.strings.games_left_to_idle
                    + ", {1} " + localization.strings.idle_now
                    + ".", (CardsRemaining > 0 ? GamesRemaining : numberOfCardsInIdle), numberOfCardsInIdle);
                lblDrops.Text = CardsRemaining + " " + localization.strings.card_drops_remaining;
                lblIdle.Visible = GamesRemaining != 0;
                lblDrops.Visible = CardsRemaining > 0;
            }
        }

        public void UpdateIdleProcesses()
        {
            var candidates = QueueCandidates().Where(b => Settings.Default.IdlingModeWhitelist || b.HoursPlayed < 2).ToList();
            foreach (var badge in desiredHelpers.Where(b => !candidates.Contains(b)).ToList())
                ForgetHelper(badge);

            int slots = Math.Max(0, MaxSimultanousCards - desiredHelpers.Count);
            foreach (var badge in candidates.Where(b => !desiredHelpers.Contains(b)).Take(slots))
                StartHelper(badge);

            RefreshGamesStateListView();

            if (desiredHelpers.Count == 0)
                NextIdle();

            UpdateStateInfo();
        }

        private void RefreshGamesStateListView()
        {
            GamesState.Items.Clear();
            foreach (var badge in CanIdleBadges.Where(b => b.InIdle))
            {
                var line = new ListViewItem(badge.Name);
                line.SubItems.Add(badge.HoursPlayed.ToString());
                GamesState.Items.Add(line);
            }

            GamesState.Columns[GamesState.Columns.IndexOf(Hours)].Width = Settings.Default.IdlingModeWhitelist ? 0 : 45;

            // JN: Recolor the listview
            GamesState.BackColor = Settings.Default.customTheme ? Settings.Default.colorBgd : Settings.Default.colorBgdOriginal;
            GamesState.ForeColor = Settings.Default.customTheme ? Settings.Default.colorTxt : Settings.Default.colorTxtOriginal;
        }
        #endregion

        #region MISC
        private static void PreventSleep() => NativeMethods.SetThreadExecutionState(NativeMethods.ExecutionState.EsContinuous | NativeMethods.ExecutionState.EsSystemRequired);
        private static void AllowSleep() => NativeMethods.SetThreadExecutionState(NativeMethods.ExecutionState.EsContinuous);

        private static void CreateShutdownProcess(String parameters)
        {
            var psi = new ProcessStartInfo("shutdown", parameters);
            psi.CreateNoWindow = true;
            psi.UseShellExecute = false;
            Process.Start(psi);
        }

        private static void AbortShutdownProcess()
        {
            CreateShutdownProcess("/a");
        }

        private static void StartShutdownProcess()
        {
            CreateShutdownProcess("/s /c \"Idle Master Extended is about to shutdown Windows.\" /t 300");
        }

        private void CopyResource(string resourceName, string file)
        {
            using (var resource = GetType().Assembly.GetManifestResourceStream(resourceName))
            {
                if (resource == null)
                {
                    return;
                }
                using (Stream output = File.OpenWrite(file))
                {
                    resource.CopyTo(output);
                }
            }
        }

        private async Task GetLatestVersionAsync()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            string jsonResponse;
            using (var webClient = new WebClient())
            {
                webClient.Headers.Add("user-agent", "Idle Master Extended application");
                webClient.Encoding = Encoding.UTF8;

                try
                {
                    jsonResponse = await webClient.DownloadStringTaskAsync("https://api.github.com/repos/asalemo/idle_master_extended_updated/releases/latest");
                }
                catch (WebException)
                {
                    return;
                }
            }
            if (IsDisposed || Disposing) return;
            string githubReleaseTagKey = "tag_name";

            if (jsonResponse.Contains(githubReleaseTagKey))
            {
                string jsonResponseShortened = jsonResponse
                    .Substring(jsonResponse
                    .IndexOf(githubReleaseTagKey));
                
                string[] releaseTagKeyValue = jsonResponseShortened
                    .Substring(0, jsonResponseShortened.IndexOf(','))
                    .Replace("\"", string.Empty)
                    .Split(':');

                if (releaseTagKeyValue[1].StartsWith("v"))
                {
                    string githubReleaseTag = releaseTagKeyValue[1];        // "vX.Y.Z-rc1"
                    string[] tagElements = githubReleaseTag.Split('-');     // "vX.Y.Z"
                    string versionNumber = tagElements[0].Substring(1);     // "X.Y.Z"
                    string[] versionElements = versionNumber.Split('.');    // [X, Y, Z]

                    if (int.TryParse(versionElements[0], out int latestMajorVersion)
                        && int.TryParse(versionElements[1], out int latestMinorVersion)
                        && int.TryParse(versionElements[2], out int latestPatchVersion))
                    {
                        System.Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                        if (latestMajorVersion > version.Major
                            || (latestMajorVersion == version.Major && latestMinorVersion > version.Minor)
                            || latestMajorVersion == version.Major && latestMinorVersion == version.Minor && latestPatchVersion > version.Build)
                        {
                            lnkLatestRelease.Text = string.Format("(Latest: v{0}.{1}.{2})", latestMajorVersion, latestMinorVersion, latestPatchVersion);
                        }
                        else
                        {
                            lnkLatestRelease.Text = string.Format("(Current: v{0}.{1}.{2})", version.Major, version.Minor, version.Build);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Performs reset to initial state
        /// </summary>
        private void ResetClientStatus()
        {
            deferredGames.Clear();
            priorityGame = null;
            statistics = new Statistics();
            SteamBrowserSession.Disconnect();
            ManualSession.Current = null;

            // Stop the steam-idle process
            StopIdle();

            // Clear the badges list
            AllBadges.Clear();

            // Resize the form
            var graphics = CreateGraphics();
            var scale = graphics.DpiY * 1.625;
            Height = Convert.ToInt32(scale);

            // Set timer intervals
            tmrCheckSteam.Interval = 500;
            tmrCheckCookieData.Interval = 500;

            // Hide signed user name
            if (Settings.Default.showUsername)
            {
                lblSignedOnAs.Text = String.Empty;
                lblSignedOnAs.Visible = false;
            }

            // Hide spinners
            picReadingPage.Visible = false;

            // Hide lblDrops and lblIdle
            lblDrops.Visible = false;
            lblIdle.Visible = false;

            // Set IsCookieReady to false
            IsCookieReady = false;

            // Re-enable tmrReadyToGo
            tmrReadyToGo.Enabled = true;
        }
        #endregion

        #region FORM
        private void ResetFormDesign()
        {
            picReadingPage.Visible = false;
            picIdleStatus.Visible = false;
            lblDrops.Text = localization.strings.badge_didnt_load.Replace("__num__", "10");
            lblIdle.Text = "";

            // Set the form height
            var graphics = CreateGraphics();
            var scale = graphics.DpiY * 1.625;
            Height = Convert.ToInt32(scale);
            ssFooter.Visible = false;
        }

        private void frmMain_Load(object sender, EventArgs e)
        {
            Settings.Default.LoadPreferences();

            InterfaceLanguage.Apply(Settings.Default.language);

            fileToolStripMenuItem.DropDownItems.Add(TextFor("queue_title"), null, (senderMenu, args) => ShowQueue());
            retryHelpers.Text = localization.strings.ResourceManager.GetString("helper_retry_now");
            fileToolStripMenuItem.DropDownItems.Insert(0, new ToolStripMenuItem(
                localization.strings.ResourceManager.GetString("test_game"), null, (menu, args) =>
                {
                    using (var test = new GameTestForm(PauseForGameTest, RecordHelperFailure))
                        test.ShowDialog(this);
                }));
            fileToolStripMenuItem.DropDownItems.Add(localization.strings.ResourceManager.GetString("timings"), null, (menu, args) =>
            {
                var pending = localization.strings.ResourceManager.GetString("timing_pending");
                var helper = firstConfirmation.HasValue ? string.Format("{0:F1} s", firstConfirmation.Value.TotalSeconds) : pending;
                MessageBox.Show(this, (lastBadgeTiming ?? pending) + Environment.NewLine +
                    string.Format(localization.strings.ResourceManager.GetString("helper_timing"), helper),
                    localization.strings.ResourceManager.GetString("timings"));
            });
            // Localize form elements
            fileToolStripMenuItem.Text = localization.strings.file;
            gameToolStripMenuItem.Text = localization.strings.game;
            helpToolStripMenuItem.Text = localization.strings.help;
            settingsToolStripMenuItem.Text = localization.strings.settings;
            blacklistToolStripMenuItem.Text = localization.strings.blacklist;
            exitToolStripMenuItem.Text = localization.strings.exit;
            pauseIdlingToolStripMenuItem.Text = localization.strings.pause_idling;
            resumeIdlingToolStripMenuItem.Text = localization.strings.resume_idling;
            skipGameToolStripMenuItem.Text = localization.strings.skip_current_game;
            blacklistCurrentGameToolStripMenuItem.Text = localization.strings.blacklist_current_game;
            statisticsToolStripMenuItem.Text = localization.strings.statistics;
            changelogToolStripMenuItem.Text = localization.strings.release_notes;
            officialGroupToolStripMenuItem.Text = localization.strings.official_group;
            aboutToolStripMenuItem.Text = localization.strings.about;
            lnkSignIn.Text = "(" + localization.strings.sign_in + ")";
            lnkResetCookies.Text = "(" + localization.strings.sign_out + ")";
            // TODO: lnkLatestRelease = "(" + localization.strings.latest_release + ")";
            toolStripStatusLabel1.Text = localization.strings.next_check;
            toolStripStatusLabel1.ToolTipText = localization.strings.next_check;

            lblSignedOnAs.Text = localization.strings.signed_in_as;
            GamesState.Columns[0].Text = localization.strings.name;
            GamesState.Columns[1].Text = localization.strings.hours;

            // Set the form height
            var graphics = CreateGraphics();
            var scale = graphics.DpiY * 1.625;
            Height = Convert.ToInt32(scale);

            // Set the location of certain elements so that they scale correctly for different DPI settings
            var point = new Point(Convert.ToInt32(graphics.DpiX * 1.14), Convert.ToInt32(lblGameName.Location.Y));
            lblGameName.Location = point;
            point = new Point(Convert.ToInt32(graphics.DpiX * 2.35), Convert.ToInt32(lnkSignIn.Location.Y));
            lnkSignIn.Location = point;
            point = new Point(Convert.ToInt32(graphics.DpiX * 2.15), Convert.ToInt32(lnkResetCookies.Location.Y));
            lnkResetCookies.Location = point;
            point = new Point(Convert.ToInt32(graphics.DpiX * 2.15), Convert.ToInt32(lnkLatestRelease.Location.Y));
            lnkLatestRelease.Location = point;

            SetTheme();
            _ = GetLatestVersionAsync();

            //Prevent Sleep
            if (Settings.Default.NoSleep)
            {
                PreventSleep();
            }
        }

        private void frmMain_FormClose(object sender, FormClosedEventArgs e)
        {
            deferredGames.Clear();
            priorityGame = null;
            statistics = new Statistics();
            SteamBrowserSession.Disconnect();
            ManualSession.Current = null;
            StopIdle();
        }

        private void frmMain_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                if (Settings.Default.minToTray)
                {
                    notifyIcon1.Visible = true;
                    Hide();
                }
            }
            else if (WindowState == FormWindowState.Normal)
            {
                notifyIcon1.Visible = false;
            }
        }

        private void Form1_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            //Restore Sleep Settings on close
            if (Settings.Default.NoSleep == true)
            {
                AllowSleep();
            }
            this.Close();
        }
        #endregion

        #region BUTTONS
        private void ShowInterruptiveButtons()
        {
            // Set the correct buttons on the form for pause / resume
            btnResume.Visible = false;
            resumeIdlingToolStripMenuItem.Enabled = false;

            btnPause.Visible = true;
            pauseIdlingToolStripMenuItem.Enabled = true;

            if (Settings.Default.fastMode || Settings.Default.IdlingModeWhitelist)
            {
                btnSkip.Visible = false;
                skipGameToolStripMenuItem.Enabled = false;
            }
            else
            {
                btnSkip.Visible = true;
                skipGameToolStripMenuItem.Enabled = true;
            }
        }

        private async void btnSkip_Click(object sender, EventArgs e)
        {
            if (!IsSteamReady)
                return;

            StopIdle();
            AllBadges.RemoveAll(b => Equals(b, CurrentBadge));

            if (!CanIdleBadges.Any())
            {
                // If there are no more games to idle, reload the badges
                picReadingPage.Visible = true;
                lblIdle.Visible = false;
                lblDrops.Visible = true;
                lblDrops.Text = localization.strings.reading_badge_page + ", " + localization.strings.please_wait;
                await LoadBadgesAsync();
            }

            StartIdle();
        }

        private void btnPause_Click(object sender, EventArgs e)
        {
            if (!IsSteamReady)
                return;

            // Stop the steam-idle process
            StopIdle();

            // Indicate to the user that idling has been paused
            lblCurrentStatus.Text = localization.strings.idling_paused;

            // Set the correct button visibility
            observationReason = TextFor("rotation_pause_reset");
            btnResume.Visible = true;
            btnPause.Visible = false;
            pauseIdlingToolStripMenuItem.Enabled = false;
            resumeIdlingToolStripMenuItem.Enabled = true;

            // Focus the resume button
            btnResume.Focus();
        }

        private void PauseForGameTest()
        {
            pausedForTest = true;
            tmrReadyToGo.Stop();
            tmrBadgeReload.Stop();
            ReloadCount = 0;
            StopIdle();
            lblCurrentStatus.Text = localization.strings.idling_paused;
            btnResume.Visible = true;
            resumeIdlingToolStripMenuItem.Enabled = true;
            pauseIdlingToolStripMenuItem.Enabled = false;
        }

        private void btnResume_Click(object sender, EventArgs e)
        {
            if (pausedForTest)
            {
                pausedForTest = false;
                tmrReadyToGo.Interval = 100;
                tmrReadyToGo.Start();
            }
            else StartIdle();

            pauseIdlingToolStripMenuItem.Enabled = true;
            resumeIdlingToolStripMenuItem.Enabled = false;
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            Show();
            WindowState = FormWindowState.Normal;
        }
        #endregion

        #region MENU ITEMS
        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void blacklistToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var frm = new frmBlacklist();
            frm.ShowDialog();

            if (CurrentBadge != null && Settings.Default.blacklist.Cast<string>().Any(appid => appid == CurrentBadge.StringId))
                btnSkip.PerformClick();
        }

        private void whitelistToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var frm = new frmWhitelist(this);
            frm.ShowDialog();
        }

        private void blacklistCurrentGameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Default.blacklist.Add(CurrentBadge.StringId);
            Settings.Default.Save();

            btnSkip.PerformClick();
        }
        private void changelogToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start("https://github.com/asalemo/idle_master_extended_updated/releases");
        }

        private void statisticsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var frm = new frmStatistics(statistics);
            frm.ShowDialog();
        }

        private void officialGroupToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start("https://steamcommunity.com/groups/idlemastery");
        }

        private void donateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start("https://github.com/JonasNilson/idle_master_extended/wiki/Donate");
        }

        private void wikiToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Process.Start("https://github.com/asalemo/idle_master_extended_updated/wiki");
        }

        private void settingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Show the form
            String previous = Settings.Default.sort;
            Boolean previous_behavior = Settings.Default.OnlyOneGameIdle;
            Boolean previous_behavior2 = Settings.Default.OneThenMany;
            Boolean previous_behavior3 = Settings.Default.fastMode;
            Boolean previous_behavior4 = Settings.Default.IdlingModeWhitelist;
            int previousDelay = Settings.Default.EffectiveRotationMinutes;
            using (var form = new frmSettings()) form.ShowDialog(this);
            if (previousDelay != Settings.Default.EffectiveRotationMinutes && CurrentBadge != null)
                TimeLeft = Math.Min(TimeLeft, 2);

            if (previous != Settings.Default.sort || previous_behavior != Settings.Default.OnlyOneGameIdle || previous_behavior2 != Settings.Default.OneThenMany
                || previous_behavior3 != Settings.Default.fastMode || previous_behavior4 != Settings.Default.IdlingModeWhitelist)
            {
                StopIdle();
                AllBadges.Clear();
                tmrReadyToGo.Enabled = true;
            }

            if (Settings.Default.showUsername && IsCookieReady)
            {
                _ = RefreshUsernameAsync();
            }
        }

        private void pauseIdlingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            btnPause.PerformClick();
        }

        private void resumeIdlingToolStripMenuItem_Click(object sender, EventArgs e)
        {
            btnResume.PerformClick();
        }

        private void skipGameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            btnSkip.PerformClick();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var frm = new frmAbout();
            frm.ShowDialog();
        }
        #endregion

        #region LINKS
        private void lblGameName_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("https://store.steampowered.com/app/" + CurrentBadge.AppId);
        }

        private void lblCurrentStatus_LinkClicked(object sender, EventArgs e)
        {
            Process.Start("https://github.com/JonasNilson/idle_master_extended/wiki/Idling-complete");
        }

        private void lblCurrentRemaining_Click(object sender, EventArgs e)
        {
            if (TimeLeft > 2)
            {
                TimeLeft = 2;
            }
        }

        private void lnkResetCookies_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            ResetClientStatus();
        }

        private void lnkSignIn_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            var choices = new ContextMenuStrip();
            choices.Items.Add(localization.strings.ResourceManager.GetString("browser_signin"), null,
                async (s, args) => await SignInAsync(false));
            choices.Items.Add(localization.strings.ResourceManager.GetString("manual_signin"), null,
                async (s, args) => await SignInAsync(true));
            choices.Closed += (s, args) =>
            {
                if (IsHandleCreated && !IsDisposed) BeginInvoke(new Action(choices.Dispose));
                else choices.Dispose();
            };
            choices.Show(lnkSignIn, new Point(0, lnkSignIn.Height));
        }

        private async Task SignInAsync(bool manual)
        {
            try
            {
                if (manual)
                {
                    using (var form = new frmSettingsAdvanced())
                        if (form.ShowDialog(this) != DialogResult.OK) return;
                }
                else if (!await SteamBrowserSession.ConnectAsync(this)) return;
                if (IsDisposed) return;
                StopIdle();
                AllBadges.Clear();
                ReloadCount = 0;
                tmrBadgeReload.Stop();
                tmrReadyToGo.Interval = 100;
                tmrReadyToGo.Start();
                lblSignedOnAs.Text = string.Format(localization.strings.ResourceManager.GetString("browser_account"), SteamProfile.GetSteamId());
                lblSignedOnAs.Visible = Settings.Default.showUsername;
            }
            catch (Exception)
            {
                Logger.Report(localization.strings.ResourceManager.GetString("browser_unavailable"));
            }
        }

        private void lnkLatestRelease_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Process.Start("https://github.com/asalemo/idle_master_extended_updated/releases");
        }
        #endregion

        #region TIMERS
        private void tmrBadgeReload_Tick(object sender, EventArgs e)
        {
            ReloadCount = ReloadCount + 1;
            lblDrops.Text = localization.strings.badge_didnt_load.Replace("__num__", (10 - ReloadCount).ToString());

            if (ReloadCount == 10)
            {
                tmrBadgeReload.Enabled = false;
                tmrReadyToGo.Enabled = true;
                ReloadCount = 0;
            }
        }

        private void tmrStatistics_Tick(object sender, EventArgs e)
        {
            statistics.increaseMinutesIdled();

        }

        private void tmrStartNext_Tick(object sender, EventArgs e)
        {
            tmrStartNext.Enabled = false;
            StartIdle();
        }

        private async Task RefreshUsernameAsync()
        {
            string id = SteamProfile.GetSteamId();
            string label = await SteamProfile.GetSignedAsAsync();
            if (IsDisposed || Disposing || !Settings.Default.showUsername || SteamProfile.GetSteamId() != id) return;
            lblSignedOnAs.Text = label;
            lblSignedOnAs.Visible = true;
        }

        private async void tmrReadyToGo_Tick(object sender, EventArgs e)
        {
            if (pausedForTest || !IsCookieReady || !IsSteamReady)
                return;

            if (Settings.Default.showUsername)
            {
                _ = RefreshUsernameAsync();
            }

            lblDrops.Visible = true;
            lblDrops.Text = localization.strings.reading_badge_page + ", " + localization.strings.please_wait;
            lblIdle.Visible = false;
            picReadingPage.Visible = true;

            tmrReadyToGo.Enabled = false;

            int session = idleSession;
            var login = ManualSession.Current;
            var status = await CookieClient.CheckSessionAsync();
            if (IsDisposed || Disposing || session != idleSession || login != ManualSession.Current) return;
            tmrReadyToGo.Interval = 100;
            if (status == SessionStatus.Connected)
            {
                await LoadBadgesAsync(startup: true);
                if (session == idleSession && ReloadCount == 0) StartIdle();
            }
            else if (status == SessionStatus.Expired)
            {
                ResetClientStatus();
            }
            else
            {
                picReadingPage.Visible = false;
                lblDrops.Text = localization.strings.ResourceManager.GetString("session_unavailable");
                tmrReadyToGo.Interval = 60000;
                tmrReadyToGo.Start();
            }
        }

        private async void tmrCardDropCheck_Tick(object sender, EventArgs e)
        {
            if (Settings.Default.IdlingModeWhitelist)
            {
                DisableCardDropCheckTimer();
            }
            else if (TimeLeft <= 0)
            {
                DisableCardDropCheckTimer();
                int session = idleSession;
                if (CurrentBadge != null)
                {
                    StartHelper(CurrentBadge);
                    await CheckCardDrops(CurrentBadge);
                }
                else
                {
                    lblDrops.Visible = true;
                    lblDrops.Text = localization.strings.reading_badge_page + ", " + localization.strings.please_wait;
                    lblIdle.Visible = false;
                    picReadingPage.Visible = true;
                    await LoadBadgesAsync();
                    if (session != idleSession || ReloadCount != 0) return;
                    if (GamesRemaining == 0)
                    {
                        StopIdle();
                        lblCurrentStatus.Text = localization.strings.idling_complete;
                        return;
                    }

                    var candidates = QueueCandidates();
                    var ready = candidates.FirstOrDefault(b => b.HoursPlayed >= 2);
                    if (Settings.Default.fastMode && (ready != null || candidates.Count == 1))
                    {
                        var next = ready ?? candidates.First();
                        StartSoloIdle(next);
                    }
                    else
                    {
                        TimeLeft = Settings.Default.fastMode ? 300 : 360;
                        UpdateIdleProcesses();
                    }
                }

                if (session != idleSession) return;
                // Check if user is authenticated and if any badge left to idle
                // There should be check for IsCookieReady, but property is set in timer tick, so it could take some time to be set.
                if (SteamProfile.HasSession && IsSteamReady && CanIdleBadges.Any() && TimeLeft != 0)
                {
                    EnableCardDropCheckTimer();
                }
                else
                {
                    DisableCardDropCheckTimer();
                }
            }
            else
            {
                TimeLeft = TimeLeft - 1;
                lblTimer.Text = TimeSpan.FromSeconds(TimeLeft).ToString(@"mm\:ss");
            }
        }

        private void tmrCheckCookieData_Tick(object sender, EventArgs e)
        {
            foreach (var error in Logger.Drain()) RecordDiagnostic(null, error.Key, error.Value);
            // JN: White icons
            var whiteIcons = Settings.Default.whiteIcons;
            var imgFalse = whiteIcons ? Resources.imgFalse_w : Resources.imgFalse;
            var imgTrue = whiteIcons ? Resources.imgTrue_w : Resources.imgTrue;
            SetTheme();

            if (SteamBrowserSession.Active?.Expired == true) ResetClientStatus();
            var connected = SteamProfile.HasSession;

            var colorGreen = Settings.Default.customTheme ? Settings.Default.colorSteamGreen : Color.Green; // Adjust the green depending on the theme

            lblCookieStatus.Text = connected ? localization.strings.idle_master_connected : localization.strings.idle_master_notconnected;
            lblCookieStatus.ForeColor = connected ? colorGreen : this.ForeColor; // JN: Changed the color of "not connected" message
            picCookieStatus.Image = connected ? imgTrue : imgFalse; // JN: Supports dark theme
            lnkSignIn.Visible = !connected;
            lnkResetCookies.Visible = connected;
            IsCookieReady = connected;
        }

        private void tmrCheckSteam_Tick(object sender, EventArgs e)
        {
            // JN: White icons
            var whiteIcons = Settings.Default.whiteIcons;
            var imgFalse = whiteIcons ? Resources.imgFalse_w : Resources.imgFalse;
            var imgTrue = whiteIcons ? Resources.imgTrue_w : Resources.imgTrue;

            var colorGreen = Settings.Default.customTheme ? Settings.Default.colorSteamGreen : Color.Green; // Adjust the green depending on the theme

            var isSteamRunning = SteamAPI.IsSteamRunning() || Settings.Default.ignoreclient;
            lblSteamStatus.Text = isSteamRunning ? (Settings.Default.ignoreclient ? localization.strings.steam_ignored : localization.strings.steam_running) : localization.strings.steam_notrunning;
            lblSteamStatus.ForeColor = isSteamRunning ? colorGreen : this.ForeColor; // JN: Changed color of the not connected status
            picSteamStatus.Image = isSteamRunning ? imgTrue : imgFalse; // JN: Supports dark theme
            tmrCheckSteam.Interval = isSteamRunning ? 5000 : 500;
            skipGameToolStripMenuItem.Enabled = isSteamRunning;
            pauseIdlingToolStripMenuItem.Enabled = isSteamRunning;
            IsSteamReady = isSteamRunning;

        }

        public void DisableCardDropCheckTimer()
        {
            tmrCardDropCheck.Stop();
            toolStripStatusLabel1.Visible = lblTimer.Visible = false;
        }

        public void EnableCardDropCheckTimer()
        {
            tmrCardDropCheck.Start();
            toolStripStatusLabel1.Visible = lblTimer.Visible = true;
        }
        #endregion

        #region THEME
        /// <summary>
        /// Changes the color of the main window components to match a Steam-like dark theme
        /// </summary>
        private void SetTheme()
        {
            // Icon images
            ApplyIcons();

            // Read settings
            bool customTheme = Settings.Default.customTheme;

            if (IsCurrentThemeCustom != customTheme)
            {
                IsCurrentThemeCustom = customTheme;

                // Define colors
                FlatStyle buttonStyle = customTheme ? FlatStyle.Flat : FlatStyle.Standard;
                Color colorBgd = customTheme ? Settings.Default.colorBgd : Settings.Default.colorBgdOriginal;
                Color colorTxt = customTheme ? Settings.Default.colorTxt : Settings.Default.colorTxtOriginal;

                // --------------------------
                // -- APPLY THEME SETTINGS --
                // --------------------------

                // Main frame window
                this.BackColor = colorBgd;
                this.ForeColor = colorTxt;

                // Link colors
                lnkLatestRelease.LinkColor
                    = lnkSignIn.LinkColor
                    = lnkResetCookies.LinkColor
                    = lblCurrentRemaining.ForeColor
                    = lblGameName.LinkColor
                    = lblCurrentStatus.LinkColor
                    = customTheme ? Color.GhostWhite : Color.Blue;

                // ToolStripMenu Top
                mnuTop.BackColor = colorBgd;
                mnuTop.ForeColor = colorTxt;

                // ToolStripMenuItem and the ToolStripMenuItem dropdowns
                foreach (ToolStripMenuItem item in mnuTop.Items)
                {
                    // Menu item coloring
                    item.BackColor = colorBgd;
                    item.ForeColor = colorTxt;

                    // Dropdown coloring
                    item.DropDown.BackColor = colorBgd;
                    item.DropDown.ForeColor = colorTxt;
                }

                // Game state list (needs to be colored in RefreshGamesStateListView)
                GamesState.BackColor = colorBgd;
                GamesState.ForeColor = colorTxt;

                // lblTimer
                lblTimer.BackColor = colorBgd;
                lblTimer.ForeColor = colorTxt;

                // toolStripStatusLabel1
                toolStripStatusLabel1.BackColor = colorBgd;

                // Footer
                ssFooter.BackColor = colorBgd;

                // Buttons
                btnPause.FlatStyle = btnResume.FlatStyle = btnSkip.FlatStyle = buttonStyle;
                btnPause.BackColor = btnResume.BackColor = btnSkip.BackColor = colorBgd;
                btnPause.ForeColor = btnResume.ForeColor = btnSkip.ForeColor = colorTxt;
            }
        }

        /// <summary>
        /// Replaces the main frame window images with white ones for the dark theme
        /// </summary>
        private void ApplyIcons()
        {
            bool whiteIcons = Settings.Default.whiteIcons;

            if (IsCurrentIconsWhite != whiteIcons)
            {
                IsCurrentIconsWhite = whiteIcons;

                // TOOL STRIP MENU ITEMS
                // File
                settingsToolStripMenuItem.Image = whiteIcons ? Resources.imgSettings_w : Resources.imgSettings;
                blacklistToolStripMenuItem.Image = whiteIcons ? Resources.imgBlacklist_w : Resources.imgBlacklist;
                exitToolStripMenuItem.Image = whiteIcons ? Resources.imgExit_w : Resources.imgExit;
                whitelistToolStripMenuItem.Image = whiteIcons ? Resources.imgTrue_w : Resources.imgTrue;
                donateToolStripMenuItem.Image = whiteIcons ? Resources.imgView_w : Resources.imgView;
                // Game
                pauseIdlingToolStripMenuItem.Image = whiteIcons ? Resources.imgPause_w : Resources.imgPause;
                resumeIdlingToolStripMenuItem.Image = whiteIcons ? Resources.imgPlay_w : Resources.imgPlay;
                skipGameToolStripMenuItem.Image = whiteIcons ? Resources.imgSkip_w : Resources.imgSkip;
                blacklistCurrentGameToolStripMenuItem.Image = whiteIcons ? Resources.imgBlacklist_w : Resources.imgBlacklist;
                // Help
                wikiToolStripMenuItem.Image = whiteIcons ? Resources.imgInfo_w : Resources.imgInfo;
                statisticsToolStripMenuItem.Image = whiteIcons ? Resources.imgStatistics_w : Resources.imgStatistics;
                changelogToolStripMenuItem.Image = whiteIcons ? Resources.imgDocument_w : Resources.imgDocument;
                officialGroupToolStripMenuItem.Image = whiteIcons ? Resources.imgGlobe_w : Resources.imgGlobe;

                // STATUS
                // Handled in respective tick drawing functions

                // BUTTONS
                btnPause.Image = whiteIcons ? Resources.imgPauseSmall_w : Resources.imgPauseSmall;
                btnResume.Image = whiteIcons ? Resources.imgPlaySmall_w : Resources.imgPlaySmall;
                btnSkip.Image = whiteIcons ? Resources.imgSkipSmall_w : Resources.imgSkipSmall;

                // LOADING GIF
                picIdleStatus.Image = whiteIcons ? Resources.imgSpinInv : Resources.imgSpin;
                picReadingPage.Image = whiteIcons ? Resources.imgSpinInv : Resources.imgSpin;
            }
        }
        #endregion
    }
}

internal static class NativeMethods
{
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    internal static extern ExecutionState SetThreadExecutionState(ExecutionState esFlags);
    [FlagsAttribute]
    internal enum ExecutionState : uint
    {
        EsAwaymodeRequired = 0x00000040,
        EsContinuous = 0x80000000,
        EsDisplayRequired = 0x00000002,
        EsSystemRequired = 0x00000001
    }
}
