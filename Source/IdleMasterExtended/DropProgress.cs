using System;

namespace IdleMasterExtended
{
    internal sealed class DropProgress
    {
        private int lastCount;
        private TimeSpan since;
        internal bool Reported { get; set; }

        internal void Reset()
        {
            lastCount = 0;
            Reported = false;
        }

        internal int NextCheckSeconds(int normalSeconds, TimeSpan now, TimeSpan threshold)
        {
            if (lastCount <= 0) return Math.Min(normalSeconds, (int)threshold.TotalSeconds);
            var remaining = threshold - (now - since);
            if (remaining <= TimeSpan.Zero) return normalSeconds;
            return Math.Min(normalSeconds, (int)Math.Ceiling(remaining.TotalSeconds));
        }

        internal TimeSpan? Observe(int count, TimeSpan now, TimeSpan threshold)
        {
            if (count <= 0 || count != lastCount)
            {
                lastCount = count;
                since = now;
                Reported = false;
                return null;
            }
            var elapsed = now - since;
            return elapsed >= threshold ? elapsed : (TimeSpan?)null;
        }
    }
}
