/*
 * File: BookClassifier.cs
 *
 * Purpose:
 * - Central entry point for identifying whether a Thing is a book,
 *   and if so, what type of book it is.
 *
 * Dependencies:
 * - IBookClassifier interface
 * - VanillaBookClassifier
 * - VBEBookClassifier
 * - ChildrensBookClassifier
 * - MOBookClassifier
 * - JournalBookClassifier
 *
 * Core logic:
 * - Accept a Verse.Thing
 * - Iterate classifiers in STRICT ORDER
 *   1. VBEBookClassifier
 *   2. ChildrensBookClassifier
 *   3. MOBookClassifier
 *   4. JournalBookClassifier
 *   5. VanillaBookClassifier
 * - Return BookMeta on first match
 * - Return null if not a book
 *
 * Design notes:
 * - Ordering matters: Children/VBE books also satisfy Vanilla conditions.
 * - This class does NOT scan maps or cache results.
 *
 * Do NOT:
 * - Do not generate book content here.
 * - Do not access LLM or RimTalk services.
 * - Do not guess based on defName or label strings.
 */


using System.Collections.Generic;
using Verse;
using Ustas.RimAI.Art.Books.Children;
using Ustas.RimAI.Art.Books.Journal;
using Ustas.RimAI.Art.Books.MO;
using Ustas.RimAI.Art.Books.Vanilla;
using Ustas.RimAI.Art.Books.VBE;

namespace Ustas.RimAI.Art.Books
{
    public interface IBookClassifier
    {
        /// <summary>
        /// 若能识别则返回 BookMeta，否则返回 null。
        /// </summary>
        BookMeta TryClassify(Thing thing);
    }

    /// <summary>
    /// 分类器入口：按顺序尝试各分类器；全部失败则返回 null（表示不是书）。
    /// </summary>
    public static class BookClassifier
    {
        private static readonly List<IBookClassifier> Classifiers = new List<IBookClassifier>
        {
            // 先识别 VBE 特种（Newspaper / SkillBook）
            new VBEBookClassifier(),

            // 再识别 Children’s Books（否则会被 Vanilla 泛化吃掉）
            new ChildrensBookClassifier(),

            new MOBookClassifier(),

            new JournalBookClassifier(),

            // 最后 Vanilla：CompBook/Book
            new VanillaBookClassifier(),
        };

        public static BookMeta Classify(Thing thing)
        {
            if (thing == null) return null;

            for (int i = 0; i < Classifiers.Count; i++)
            {
                var meta = Classifiers[i].TryClassify(thing);
                if (meta != null) return meta;
            }

            return null;
        }
    }
}
