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
        public CookieClient() : this(ManualSession.Current) { }

        internal CookieClient(ManualSession session)
        {
            Cookie = GenerateCookies(session);
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
            // Use the same badge language as the private browser session.
            url += url.Contains("?") ? "&l=english" : "?l=english";
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
            return await CheckManualSessionAsync(ManualSession.Current);
        }

        internal static async Task<SessionStatus> CheckManualSessionAsync(ManualSession candidate)
        {
            if (candidate == null || candidate.SteamId.Length != 17 || string.IsNullOrEmpty(candidate.SessionId)) return SessionStatus.Expired;
            using (var client = new CookieClient(candidate))
            {
                try
                {
                    var page = await client.DownloadStringTaskAsync(candidate.Url);
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

        private static CookieContainer GenerateCookies(ManualSession session)
        {
            var cookies = new CookieContainer();
            if (session == null) return cookies;
            var target = new Uri("https://steamcommunity.com");
            cookies.Add(target, new Cookie("sessionid", session.SessionId));
            cookies.Add(target, new Cookie("steamLoginSecure", session.Login));
            if (!string.IsNullOrEmpty(session.Parental))
                cookies.Add(target, new Cookie("steamparental", session.Parental));
            return cookies;
        }
    }
}
