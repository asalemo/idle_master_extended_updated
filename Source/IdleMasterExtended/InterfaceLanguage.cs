using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace IdleMasterExtended
{
    internal static class InterfaceLanguage
    {
        private static readonly CultureInfo WindowsCulture = CultureInfo.CurrentUICulture;
        private static readonly Dictionary<string, string> Codes = new Dictionary<string, string>
        {
            { "Bulgarian", "bg" },
            { "Chinese (Simplified, China)", "zh-CN" },
            { "Chinese (Traditional, China)", "zh-TW" },
            { "Czech", "cs" },
            { "Danish", "da" },
            { "Dutch", "nl" },
            { "English", "en" },
            { "Finnish", "fi" },
            { "French", "fr" },
            { "German", "de" },
            { "Greek", "el" },
            { "Hungarian", "hu" },
            { "Italian", "it" },
            { "Japanese", "ja" },
            { "Korean", "ko" },
            { "Norwegian", "no" },
            { "Polish", "pl" },
            { "Portuguese", "pt-PT" },
            { "Portuguese (Brazil)", "pt-BR" },
            { "Romanian", "ro" },
            { "Russian", "ru" },
            { "Spanish", "es" },
            { "Swedish", "sv" },
            { "Thai", "th" },
            { "Turkish", "tr" },
            { "Ukrainian", "uk" },
            { "Croatian", "hr" },
        };

        internal static void Apply(string saved)
        {
            string code;
            var culture = !string.IsNullOrEmpty(saved) && Codes.TryGetValue(saved, out code)
                ? CultureInfo.GetCultureInfo(code) : WindowsCulture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
        }

        internal static string DisplayName(string saved)
        {
            string code;
            if (string.IsNullOrEmpty(saved)) return localization.strings.ResourceManager.GetString("windows_language");
            return Codes.TryGetValue(saved, out code) ? CultureInfo.GetCultureInfo(code).NativeName : saved;
        }
    }
}
