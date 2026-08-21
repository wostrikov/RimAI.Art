/*
 * File: BookType.cs
 *
 * Purpose:
 * - Define internal book categories used by RimAI.Art.
 * - This enum is ONLY for classification results, not gameplay logic.
 *
 * Dependencies:
 * - None (pure enum).
 *
 * Design notes:
 * - Only include book types that can be reliably identified from:
 *   - Vanilla RimWorld
 *   - Vanilla Books Expanded (VBE)
 *   - Children's Books
 * - Do NOT invent speculative types (e.g. Novel, Textbook) unless backed by code.
 *
 * Expected values:
 * - VanillaBook
 * - VBE_Newspaper
 * - VBE_SkillBook
 * - CB_ChildrensBook
 * - CB_ColoringBook
 * - MO_DefinableBook
 * - Journal
 *
 * Do NOT:
 * - Do not try to mirror RimWorld BookOutcomeDoer hierarchy.
 * - Do not encode gameplay effects here.
 */


namespace Ustas.RimAI.Art.Books
{
    public enum BookType
    {
        Unknown = 0,

        /// <summary>
        /// Vanilla：ThingDef.HasComp<RimWorld.CompBook>() Verse.Book
        /// </summary>
        VanillaBook = 1,

        Journal = 2,

        /// <summary>
        /// VBE：VanillaBooksExpanded.Newspaper : Verse.Book
        /// </summary>
        VBE_Newspaper = 10,

        VBE_SkillBook = 11,

        /// <summary>
        /// Children’s Books：ThingDef == Childrens_Books.ChildrensBookDefOf.BBLK_ChildrensBook
        /// </summary>
        CB_ChildrensBook = 20,

        /// <summary>
        /// Children’s Books：ThingDef == Childrens_Books.ChildrensBookDefOf.BBLK_ColoringBook
        /// </summary>
        CB_ColoringBook = 21,

        /// <summary>
        /// Medieval Overhaul???ThingDef carries CompProperties_DefinableBook or thingClass BookWithAuthor
        /// </summary>
        MO_DefinableBook = 30
    }
}
