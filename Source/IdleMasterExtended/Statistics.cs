using System;
using System.Collections.Generic;

namespace IdleMasterExtended
{
    public class Statistics
    {
        private readonly Dictionary<int, int> counts = new Dictionary<int, int>();
        private readonly Dictionary<int, DateTime> confirmed = new Dictionary<int, DateTime>();
        private readonly HashSet<int> completed = new HashSet<int>();
        private uint minutes, drops;
        internal int Failures { get; private set; }
        internal int Completed => completed.Count;
        public uint getSessionMinutesIdled() => minutes;
        public uint getSessionCardIdled() => drops;
        public void increaseMinutesIdled() => minutes++;
        internal void Failed() => Failures++;

        internal DateTime? LastRead(int id)
        {
            DateTime time;
            return confirmed.TryGetValue(id, out time) ? time : (DateTime?)null;
        }

        internal void Observe(int id, int count)
        {
            if (count < 0) return;
            int previous;
            if (counts.TryGetValue(id, out previous) && previous > count)
            {
                drops += (uint)(previous - count);
                if (count == 0) completed.Add(id);
            }
            counts[id] = count;
            confirmed[id] = DateTime.Now;
        }
    }
}
