using RimWorld;
using Verse;

namespace Ustas.RimAI.Art.journal
{
    [DefOf]
    public static class JournalDefOf
    {
        public static JobDef RimTalk_WriteJournal;
        public static ThingDef RimTalk_JournalBook;

        static JournalDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(JournalDefOf));
        }
    }
}
