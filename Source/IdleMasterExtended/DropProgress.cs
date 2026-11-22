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

        internal TimeSpan? Observe(int count, TimeSpan now)
        {
            if (count <= 0 || count != lastCount)
            {
                lastCount = count;
                since = now;
                Reported = false;
                return null;
            }
            var elapsed = now - since;
            return elapsed >= TimeSpan.FromHours(2) ? elapsed : (TimeSpan?)null;
        }
    }
}
