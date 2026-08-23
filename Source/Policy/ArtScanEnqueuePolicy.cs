namespace Ustas.RimAI.Art.Policy
{
    public enum ArtScanCadenceAction
    {
        ArmFirstTick,
        NotDue,
        ScanDue
    }

    public enum ArtScanEnqueueAction
    {
        SkipNotStarted,
        SkipIneligible,
        SkipNoKey,
        SkipCached,
        SkipDuplicate,
        Enqueue
    }

    /// <summary>
    /// Authoritative background-scan cadence and enqueue decisions.
    /// Map-load scans once per map; daily scans after the first armed tick.
    /// Production-complete uses the same enqueue gate.
    /// </summary>
    public static class ArtScanEnqueuePolicy
    {
        public static ArtScanCadenceAction DecideCadence(int lastScanTick, int currentTick, int intervalTicks)
        {
            if (lastScanTick < 0)
                return ArtScanCadenceAction.ArmFirstTick;
            if (intervalTicks <= 0 || currentTick - lastScanTick < intervalTicks)
                return ArtScanCadenceAction.NotDue;
            return ArtScanCadenceAction.ScanDue;
        }

        public static bool ShouldScanLoadedMap(bool alreadyScanned) =>
            !alreadyScanned;

        public static ArtScanEnqueueAction DecideEnqueue(
            bool started,
            bool eligible,
            bool hasKey,
            bool cached,
            bool alreadyQueued)
        {
            if (!started)
                return ArtScanEnqueueAction.SkipNotStarted;
            if (!eligible)
                return ArtScanEnqueueAction.SkipIneligible;
            if (!hasKey)
                return ArtScanEnqueueAction.SkipNoKey;
            if (cached)
                return ArtScanEnqueueAction.SkipCached;
            if (alreadyQueued)
                return ArtScanEnqueueAction.SkipDuplicate;
            return ArtScanEnqueueAction.Enqueue;
        }
    }
}
