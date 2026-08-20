using Ustas.RimAI.Art.Art;
using Ustas.RimAI.Art.Storage;
using Verse;

namespace Ustas.RimAI.Art.Scanner.Queue
{
    public sealed class PendingArtRecord
    {
        public ArtKey Key { get; }
        public ArtMeta Meta { get; }
        public int EnqueuedTick { get; }
        public int Attempts { get; private set; }

        public PendingArtRecord(ArtKey key, ArtMeta meta)
        {
            Key = key;
            Meta = meta;
            EnqueuedTick = GenTicks.TicksGame;
        }

        public void IncrementAttempts()
        {
            Attempts++;
        }
    }
}
