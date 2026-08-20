using RimWorld;
using Verse;

namespace Ustas.RimAI.Art.Books.Vanilla
{
    /// <summary>
    /// Vanilla 可靠识别：
    /// - ThingDef.HasComp<CompBook>()（CompProperties_Book 指定 compClass=CompBook）
    /// - 或 Thing 是 Verse.Book
    /// </summary>
    public sealed class VanillaBookClassifier : IBookClassifier
    {
        public BookMeta TryClassify(Thing thing)
        {
            if (thing == null) return null;

            // Verse.Book（Vanilla 基础）
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
