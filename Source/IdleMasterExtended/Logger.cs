using System;
using System.Collections.Generic;
using System.Linq;

namespace IdleMasterExtended
{
    public static class Logger
    {
        private static readonly object Gate = new object();
        private static readonly Dictionary<string, int> Pending = new Dictionary<string, int>();

        internal static void Report(string message)
        {
            lock (Gate)
            {
                int count;
                if (Pending.TryGetValue(message, out count)) Pending[message] = count + 1;
                else if (Pending.Count < 50) Pending.Add(message, 1);
            }
        }

        internal static KeyValuePair<string, int>[] Drain()
        {
            lock (Gate)
            {
                var entries = Pending.ToArray();
                Pending.Clear();
                return entries;
            }
        }

        public static void Exception(Exception ex, params string[] messages)
        {
            string context = string.Join(" ", messages);
            Report(string.IsNullOrEmpty(context) ? ex.GetType().Name : context);
        }
    }
}
