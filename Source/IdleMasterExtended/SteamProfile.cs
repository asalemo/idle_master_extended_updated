using System;
using System.Net;
using System.Text;
using System.Xml;
using System.Threading.Tasks;
using IdleMasterExtended.Properties;

namespace IdleMasterExtended
{
    internal sealed class ManualSession
    {
        internal static ManualSession Current { get; set; }
        internal readonly string SessionId, Login, Parental, SteamId;
        internal string Url => "https://steamcommunity.com/profiles/" + SteamId;

        internal ManualSession(string sessionId, string login, string parental)
        {
            SessionId = sessionId;
            Login = login;
            Parental = parental;
            SteamId = System.Text.RegularExpressions.Regex.Match(WebUtility.UrlDecode(login),
                @"\A[0-9]{17}(?=\||\z)").Value;
        }
    }

    internal class SteamProfile
    {
        internal static string GetSteamId() => SteamBrowserSession.Active?.SteamId ?? ManualSession.Current?.SteamId;
        internal static string ProfileUrl => SteamBrowserSession.Active?.ProfileUrl ?? ManualSession.Current?.Url;
        internal static bool HasSession => SteamBrowserSession.Active != null || ManualSession.Current != null;

        internal static async Task<string> GetSignedAsAsync()
        {
            var steamUrl = ProfileUrl;
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
                Logger.Exception(ex, "Could not read Steam profile name.");
            }
            return localization.strings.signed_in_as + " " + userName;
        }
    }
}
