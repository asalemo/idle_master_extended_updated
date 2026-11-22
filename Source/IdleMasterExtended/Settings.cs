using System.Collections.Generic;

namespace IdleMasterExtended.Properties
{
    internal sealed partial class Settings
    {
        internal void LoadPreferences()
        {
            bool upgrade = updateNeeded;
            bool changed = upgrade;
            // Old releases stored session data and activity totals in these settings.
            var obsolete = new HashSet<string> { "sessionid", "steamLogin", "steamLoginSecure",
                "steamparental", "steamMachineAuth", "steamRememberLogin", "myProfileURL",
                "totalCardIdled", "totalMinutesIdled" };
            foreach (System.Configuration.SettingsProperty property in Properties)
            {
                string name = property.Name;
                if (obsolete.Contains(name))
                {
                    object empty = property.PropertyType == typeof(string) ? (object)string.Empty : 0u;
                    if (!Equals(this[name], empty))
                    {
                        this[name] = empty;
                        changed = true;
                    }
                }
                else if (upgrade && name != "updateNeeded")
                {
                    var previous = GetPreviousVersion(name);
                    if (previous != null) this[name] = previous;
                }
            }
            if (changed)
            {
                updateNeeded = false;
                Save();
            }
        }
    }
}
