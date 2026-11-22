using System;
using System.Net;
using System.Text;
using System.Xml;
using System.Threading.Tasks;
using IdleMasterExtended.Properties;

namespace IdleMasterExtended
{
    internal class SteamProfile
    {
        internal static string GetSteamId()
        {
            if (SteamBrowserSession.Active != null) return SteamBrowserSession.Active.SteamId;
            var value = WebUtility.UrlDecode(Settings.Default.steamLoginSecure ?? "");
            var match = System.Text.RegularExpressions.Regex.Match(value, @"\A[0-9]{17}(?=\||\z)");
            return match.Value;
        }

        internal static string ProfileUrl => SteamBrowserSession.Active?.ProfileUrl ?? Settings.Default.myProfileURL;
        internal static bool HasSession => SteamBrowserSession.Active != null ||
            (!string.IsNullOrWhiteSpace(Settings.Default.sessionid) && !string.IsNullOrWhiteSpace(Settings.Default.steamLoginSecure));

        internal static string GetSteamUrl()
        {
            return "https://steamcommunity.com/profiles/" + GetSteamId();
        }

        internal static async Task<string> GetSignedAsAsync()
        {
            var steamUrl = GetSteamUrl();
            var userName = "User " + GetSteamId();
            try
            {
                string xmlRaw;
                using (var client = new WebClient { Encoding = Encoding.UTF8 })
                    xmlRaw = await client.DownloadStringTaskAsync(steamUrl + "/?xml=1");
                var xml = new XmlDocument();
                xml.LoadXml(xmlRaw);
                var nameNode = xml.SelectSingleNode("//steamID");
                if (nameNode != null)
                    userName = WebUtility.HtmlDecode(nameNode.InnerText);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Logger.Exception(ex, "frmMain -> GetSignedAs, for steamUrl = " + steamUrl);
            }
            return localization.strings.signed_in_as + " " + userName;
        }
    }
}
