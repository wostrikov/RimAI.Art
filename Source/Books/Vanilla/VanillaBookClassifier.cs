using RimWorld;
using Verse;

namespace Ustas.RimAI.Art.Books.Vanilla
{
    public sealed class VanillaBookClassifier : IBookClassifier
    {
        public BookMeta TryClassify(Thing thing)
        {
            if (thing == null) return null;

            if (thing is Book)
            {
                return new BookMeta(thing, BookType.VanillaBook);
            }

            // CompBook（Vanilla：CompProperties_Book.compClass = typeof(CompBook)）
            var def = thing.def;
            if (def != null && def.HasComp<CompBook>())
            {
                return new BookMeta(thing, BookType.VanillaBook);
            }

            return null;
        }
    }
}
