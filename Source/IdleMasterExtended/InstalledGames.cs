using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace IdleMasterExtended
{
    internal static class InstalledGames
    {
        private static readonly Regex Fields = new Regex(@"^\s*""(?<key>path|appid|name|StateFlags)""\s+""(?<value>(?:\\.|[^""\\])*)""", RegexOptions.Multiline | RegexOptions.IgnoreCase);

        private static bool Unavailable(Exception ex)
        {
            return ex is IOException || ex is UnauthorizedAccessException || ex is SecurityException;
        }

        internal static List<Badge> Scan()
        {
            string steam = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
            var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { steam };
            try
            {
                string config = File.ReadAllText(Path.Combine(steam, "steamapps", "libraryfolders.vdf"));
                foreach (Match field in Fields.Matches(config))
                    if (field.Groups["key"].Value == "path")
                        libraries.Add(field.Groups["value"].Value.Replace(@"\\", @"\"));
            }
            catch (Exception ex) when (Unavailable(ex)) { /* The default library may still be available. */ }

            var games = new Dictionary<int, Badge>();
            foreach (string library in libraries)
            {
                string[] files;
                try { files = Directory.GetFiles(Path.Combine(library, "steamapps"), "appmanifest_*.acf"); }
                catch (Exception ex) when (Unavailable(ex)) { continue; }

                foreach (string file in files)
                {
                    string text;
                    try { text = File.ReadAllText(file); }
                    catch (Exception ex) when (Unavailable(ex)) { continue; }
                    int id = 0, flags = 0;
                    string name = null;
                    foreach (Match field in Fields.Matches(text))
                    {
                        string value = field.Groups["value"].Value;
                        switch (field.Groups["key"].Value.ToLowerInvariant())
                        {
                            case "appid": int.TryParse(value, out id); break;
                            case "stateflags": int.TryParse(value, out flags); break;
                            case "name": name = value.Replace("\\\"", "\""); break;
                        }
                    }
                    if (id > 0 && (flags & 4) != 0 && !string.IsNullOrWhiteSpace(name))
                        games[id] = new Badge { AppId = id, Name = name };
                }
            }
            return games.Values.OrderBy(b => b.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }
    }
}
