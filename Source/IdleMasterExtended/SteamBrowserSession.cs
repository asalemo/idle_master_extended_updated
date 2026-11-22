using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace IdleMasterExtended
{
    // Steam handles authentication inside an isolated, private browser profile.
    internal sealed class SteamBrowserSession : Form
    {
        private readonly WebView2 browser = new WebView2 { Dock = DockStyle.Fill };
        private readonly Label origin = new Label { Dock = DockStyle.Top, Height = 26 };
        private readonly Button confirm = new Button { Dock = DockStyle.Bottom, Height = 42, Enabled = false };
        private readonly TaskCompletionSource<bool> signedIn = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Dictionary<string, TaskCompletionSource<string>> pending = new Dictionary<string, TaskCompletionSource<string>>();
        private static SteamBrowserSession opening;
        internal static SteamBrowserSession Active { get; private set; }
        internal string SteamId { get; private set; }
        internal string ProfileUrl => "https://steamcommunity.com/profiles/" + SteamId;

        private static string T(string key) => localization.strings.ResourceManager.GetString(key);

        internal static bool AllowedOrigin(Uri uri)
        {
            return uri != null && uri.IsAbsoluteUri && uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort &&
                uri.UserInfo.Length == 0 && (uri.Host == "steamcommunity.com" ||
                uri.Host == "login.steampowered.com" || uri.Host == "store.steampowered.com");
        }

        internal static bool AllowedPage(string profile, string url)
        {
            return Regex.IsMatch(profile ?? "", @"\Ahttps://steamcommunity\.com/profiles/[0-9]{17}\z") &&
                Regex.IsMatch(url ?? "", "\\A" + Regex.Escape(profile) + @"(?:/badges/\?p=[1-9][0-9]*|/gamecards/[1-9][0-9]*)?\z");
        }

        private SteamBrowserSession()
        {
            Text = T("browser_signin");
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(820, 680);
            MinimumSize = new Size(480, 420);
            browser.CreationProperties = new CoreWebView2CreationProperties
            {
                UserDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "IdleMasterExtended", "WebView2"),
                IsInPrivateModeEnabled = true,
                Language = CultureInfo.CurrentUICulture.Name
            };
            confirm.Text = T("browser_use_account");
            origin.Text = T("browser_wait");
            Controls.Add(browser);
            Controls.Add(origin);
            Controls.Add(confirm);
            confirm.Click += async (sender, args) => await ConfirmAsync();
        }

        internal static async Task<bool> ConnectAsync(Form owner)
        {
            if (Active != null) return true;
            if (opening != null) { opening.Activate(); return false; }
            var session = new SteamBrowserSession();
            opening = session;
            try
            {
                session.Show(owner);
                owner.Enabled = false;
                await session.browser.EnsureCoreWebView2Async();
                if (session.IsDisposed) return false;
                session.Configure();
                session.browser.Source = new Uri("https://steamcommunity.com/login/home/?goto=my%2F");
                return await session.signedIn.Task;
            }
            finally
            {
                if (!owner.IsDisposed) owner.Enabled = true;
                if (opening == session) opening = null;
                if (Active != session) session.Dispose();
            }
        }

        private void Configure()
        {
            var core = browser.CoreWebView2;
            core.Settings.AreHostObjectsAllowed = false;
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = false;
            core.Settings.AreDefaultScriptDialogsEnabled = false;
            core.Settings.IsPasswordAutosaveEnabled = false;
            core.Settings.IsGeneralAutofillEnabled = false;
            core.NewWindowRequested += (s, e) => e.Handled = true;
            core.DownloadStarting += (s, e) => e.Cancel = true;
            core.PermissionRequested += (s, e) => e.State = CoreWebView2PermissionState.Deny;
            core.NavigationStarting += (s, e) =>
            {
                confirm.Enabled = false;
                confirm.Text = T("browser_use_account");
                Uri uri;
                e.Cancel = Active == this || !Uri.TryCreate(e.Uri, UriKind.Absolute, out uri) || !AllowedOrigin(uri);
                if (!e.Cancel) origin.Text = new Uri(e.Uri).GetLeftPart(UriPartial.Authority);
            };
            core.NavigationCompleted += async (s, e) =>
            {
                if (!e.IsSuccess || IsDisposed || Active == this) return;
                confirm.Enabled = true;
                if (!IsCommunityPage()) return;
                try
                {
                    string id = await ReadPublicIdAsync();
                    if (IsDisposed || !IsCommunityPage()) return;
                    confirm.Enabled = true;
                    if (id != null) confirm.Text = T("browser_use_account") + " · " + id;
                }
                catch (Exception) { if (!IsDisposed) confirm.Enabled = false; }
            };
            core.WebMessageReceived += (s, e) =>
            {
                Uri uri;
                if (Active != this || !Uri.TryCreate(e.Source, UriKind.Absolute, out uri) ||
                    !AllowedOrigin(uri) || uri.Host != "steamcommunity.com") return;
                string message;
                try { message = e.TryGetWebMessageAsString(); }
                catch (ArgumentException) { return; }
                if (message == null || message.Length < 33 || message[32] != '\n') return;
                TaskCompletionSource<string> reply;
                if (pending.TryGetValue(message.Substring(0, 32), out reply))
                    reply.TrySetResult(message.Substring(33));
            };
        }

        private bool IsCommunityPage()
        {
            var uri = browser.Source;
            return AllowedOrigin(uri) && uri.Host == "steamcommunity.com" &&
                !uri.AbsolutePath.StartsWith("/login", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<string> ReadPublicIdAsync()
        {
            string json = await browser.ExecuteScriptAsync(
                "location.origin === 'https://steamcommunity.com' && document.querySelector('#account_pulldown, .notification_count') ? String(window.g_steamID || '') : ''");
            return Regex.IsMatch(json ?? "", @"\A""[0-9]{17}""\z") ? json.Substring(1, 17) : null;
        }

        private async Task ConfirmAsync()
        {
            try
            {
                string id = IsCommunityPage() ? await ReadPublicIdAsync() : null;
                if (IsDisposed) return;
                if (id == null || !IsCommunityPage())
                {
                    origin.Text = T("browser_wait");
                    browser.CoreWebView2.Navigate("https://steamcommunity.com/my/");
                    return;
                }
                SteamId = id;
                Active = this;
                Owner = null;
                Hide();
                signedIn.TrySetResult(true);
            }
            catch (Exception) { if (!IsDisposed) Logger.Report(T("browser_unavailable")); }
        }

        internal bool Expired { get; private set; }

        internal async Task<string> ReadAsync(string url)
        {
            if (Active != this || !AllowedPage(ProfileUrl, url) || !IsCommunityPage()) return "";
            string key = Guid.NewGuid().ToString("N");
            var reply = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            pending.Add(key, reply);
            try
            {
                string script = BuildReadScript(url, key);
                await browser.ExecuteScriptAsync(script);
                if (await Task.WhenAny(reply.Task, Task.Delay(25000)) != reply.Task) return "";
                string result = await reply.Task;
                if (result == "EXPIRED") Expired = true;
                return result;
            }
            catch (Exception) { return ""; }
            finally { pending.Remove(key); }
        }

        private static string BuildReadScript(string url, string key)
        {
            // Only send back the badge fragments consumed by the existing parser.
            return @"(async () => {
                    let result = '';
                    try {
                        if (location.origin !== 'https://steamcommunity.com') return;
                        const target = new URL('__TARGET__');
                        target.searchParams.set('l', 'english');
                        const r = await fetch(target, { credentials:'same-origin', mode:'same-origin', signal:AbortSignal.timeout(20000) });
                        if (r.status === 401 || new URL(r.url).pathname.startsWith('/login')) result = 'EXPIRED';
                        else if (r.ok) {
                            const doc = new DOMParser().parseFromString(await r.text(), 'text/html');
                            if (doc.querySelector('#account_pulldown, .notification_count')) {
                                result = 'CONNECTED';
                                if (target.pathname.includes('/badges/'))
                                    result = Array.from(doc.querySelectorAll('.badge_row, a.pagelink'), n => n.outerHTML).join('')
                                        || (doc.querySelector('.badges_sheet') ? '<div class=badges_sheet></div>' : '');
                                if (target.pathname.includes('/gamecards/'))
                                    result = doc.querySelector('.badge_title_stats_playtime')?.parentElement?.outerHTML || '';
                            }
                        }
                    } finally { window.chrome.webview.postMessage('KEY\n' + result); }
                })().catch(() => {});".Replace("__TARGET__", url).Replace("KEY", key);
        }

        internal static void Disconnect()
        {
            var current = Active;
            Active = null;
            current?.Dispose();
            opening?.Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                signedIn.TrySetResult(false);
                foreach (var completion in pending.Values) completion.TrySetResult("");
                if (Active == this) Active = null;
            }
            base.Dispose(disposing);
        }
    }
}
