using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace IdleMasterExtended
{
    public static class Logger
    {
        private static readonly object Gate = new object();
        private static readonly Dictionary<string, int> Pending = new Dictionary<string, int>();
        private static readonly Queue<string> FileQueue = new Queue<string>();
        private static bool writing;

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
            lock (Gate)
            {
                if (FileQueue.Count == 50) FileQueue.Dequeue();
                FileQueue.Enqueue($"{DateTime.Now:u} {context}{Environment.NewLine}{ex}{Environment.NewLine}");
                if (writing) return;
                writing = true;
                ThreadPool.QueueUserWorkItem(WriteLog);
            }
        }

        private static void WriteLog(object state)
        {
            while (true)
            {
                string entry;
                lock (Gate)
                {
                    if (FileQueue.Count == 0) { writing = false; return; }
                    entry = FileQueue.Dequeue();
                }
                try
                {
                    File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log"), entry, Encoding.UTF8);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException ||
                                           ex is System.Security.SecurityException)
                {
                    // In-memory diagnostics remain available in read-only installations.
                }
            }
        }
    }
}
