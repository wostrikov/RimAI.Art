using Ustas.RimAI.Art.Books;
using Ustas.RimAI.Art.Storage;
using Verse;

namespace Ustas.RimAI.Art.Scanner.Queue
{
    public sealed class PendingBookRecord
    {
        public BookKey Key { get; }
        public BookMeta Meta { get; }
        public Pawn Author { get; }
        public int EnqueuedTick { get; }
        public int Attempts { get; private set; }

        public bool HasAuthor => Author != null;

        public PendingBookRecord(BookKey key, BookMeta meta, Pawn author)
        {
            Key = key;
            Meta = meta;
            Author = author;
            EnqueuedTick = GenTicks.TicksGame;
        }

        public void IncrementAttempts()
        {
            Attempts++;
        }
    }
}
