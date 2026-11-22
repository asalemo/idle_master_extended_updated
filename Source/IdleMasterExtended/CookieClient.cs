using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;
using IdleMasterExtended.Properties;

namespace IdleMasterExtended
{
    internal enum SessionStatus { Connected, Expired, Unavailable }

    public class CookieClient : WebClient
    {
        internal CookieContainer Cookie = new CookieContainer();

        private bool sessionExpired;

        /// <summary>
        /// Instantiates a new <c>CookieClient</c> object.
        /// </summary>
        public CookieClient()
        {
            Cookie = GenerateCookies();
            Encoding = Encoding.UTF8;
        }

        /// <summary>
        /// Method <c>GetHttpAsync</c> will asynchronosly use a HTTP GET request (via <c>WebClient</c>) to the provided parameter <c>url</c>
        /// </summary>
        /// <param name="url">URL to send a GET request to</param>
        /// <param name="count">Number of retries to send a GET request</param>
        /// <returns>The HTML content of the HTTP GET response</returns>
        public static async Task<string> GetHttpAsync(string url, int count = 3)
        {
            var session = SteamBrowserSession.Active;
            if (session != null)
            {
                string page = await session.ReadAsync(url);
                return page == "EXPIRED" ? string.Empty : page;
            }
            for (int attempt = 0; attempt <= count; attempt++)
            {
                using (var client = new CookieClient())
                {
                    try
                    {
                        string content = await client.DownloadStringTaskAsync(url);
                        if (!string.IsNullOrWhiteSpace(content)) return content;
                    }
                    catch (WebException ex)
                    {
                        if (attempt == count) Logger.Exception(ex, "Steam Community request failed.");
                    }
                }
                if (attempt < count) await Task.Delay(TimeSpan.FromSeconds(1 << Math.Min(attempt, 4)));
            }
            return string.Empty;
        }

        internal static async Task<SessionStatus> CheckSessionAsync()
        {
            var session = SteamBrowserSession.Active;
            if (session != null)
            {
                var result = await session.ReadAsync(session.ProfileUrl);
                if (result == "EXPIRED") return SessionStatus.Expired;
                return result == "CONNECTED" ? SessionStatus.Connected : SessionStatus.Unavailable;
            }
            using (var client = new CookieClient())
            {
                try
                {
                    var page = await client.DownloadStringTaskAsync(Settings.Default.myProfileURL);
                    if (client.sessionExpired) return SessionStatus.Expired;
                    var document = new HtmlDocument();
                    document.LoadHtml(page);
                    var marker = document.DocumentNode.SelectSingleNode("//span[@class='notification_count']");
                    return marker != null ? SessionStatus.Connected : SessionStatus.Unavailable;
                }
                catch (WebException ex)
                {
                    Logger.Exception(ex, "Steam Community session check unavailable.");
                    return SessionStatus.Unavailable;
                }
            }
        }

        protected override WebRequest GetWebRequest(Uri address)
        {
            var request = base.GetWebRequest(address);
            
            if (request is HttpWebRequest)
                (request as HttpWebRequest).CookieContainer = Cookie;
            
            return request;
        }

        protected override WebResponse GetWebResponse(WebRequest request, System.IAsyncResult result)
        {
            var response = (HttpWebResponse)base.GetWebResponse(request, result);
            var cookie = response.Cookies["steamLoginSecure"];
            sessionExpired = response.ResponseUri.Host == "steamcommunity.com" &&
                (response.ResponseUri.AbsolutePath.StartsWith("/login/", StringComparison.OrdinalIgnoreCase) ||
                 (cookie != null && (cookie.Expired || cookie.Value == "deleted")));
            return response;
        }

        private static CookieContainer GenerateCookies()
        {
            var cookies = new CookieContainer();
            var target = new Uri("https://steamcommunity.com");

            cookies.Add(new Cookie("sessionid", Settings.Default.sessionid) { Domain = target.Host });
            cookies.Add(new Cookie("steamLoginSecure", Settings.Default.steamLoginSecure) { Domain = target.Host });
            cookies.Add(new Cookie("steamparental", Settings.Default.steamparental) { Domain = target.Host });
            cookies.Add(new Cookie("steamRememberLogin", Settings.Default.steamRememberLogin) { Domain = target.Host });
            cookies.Add(new Cookie(GetSteamMachineAuthCookieName(), Settings.Default.steamMachineAuth) { Domain = target.Host });

            return cookies;
        }

        private static string GetSteamMachineAuthCookieName()
        {
            if (Settings.Default.steamLoginSecure != null && Settings.Default.steamLoginSecure.Length > 17)
                return string.Format("steamMachineAuth{0}", Settings.Default.steamLoginSecure.Substring(0, 17));

            return "steamMachineAuth";
        }
    }
}
