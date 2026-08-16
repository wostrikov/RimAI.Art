using Verse;

namespace Ustas.RimAI.Art.book.journal
{
    public sealed class JournalBookClassifier : IBookClassifier
    {
        public BookMeta TryClassify(Thing thing)
        {
            if (thing == null) return null;

            var def = thing.def;
            if (def == null) return null;

            if (def.GetModExtension<JournalBookExtension>() != null)
                return new BookMeta(thing, BookType.Journal);

            return null;
        }
    }
}
