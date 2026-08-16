/*
 * Purpose:
 * - Override Verse.Book description getters for Medieval Overhaul definable books.
 *
 * Rationale:
 * - Some MO books regenerate description on access, ignoring our cached text.
 * - This patch returns cached synopsis directly for MO definable books only.
 */
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Ustas.RimAI.Art.book;
using Ustas.RimAI.Art.integration;
using Ustas.RimAI.Art.storage;
using Ustas.RimAI.Art.storage.save;
using Verse;

namespace Ustas.RimAI.Art.patches
{
    [HarmonyPatch(typeof(Book))]
    public static class Patch_Book_MO_Description
    {
        private const string MoBookTypeName = "MedievalOverhaul.BookWithAuthor";
        private const string MoDefinableBookCompPropsTypeName = "MedievalOverhaul.CompProperties_DefinableBook";

        [HarmonyPatch(nameof(Book.DescriptionDetailed), MethodType.Getter)]
        [HarmonyPrefix]
        public static bool DescriptionDetailedPrefix(Book __instance, ref string __result)
        {
            return TryOverride(__instance, ref __result);
        }

        [HarmonyPatch(nameof(Book.DescriptionFlavor), MethodType.Getter)]
        [HarmonyPrefix]
        public static bool DescriptionFlavorPrefix(Book __instance, ref string __result)
        {
            return TryOverride(__instance, ref __result);
        }

        private static bool TryOverride(Book book, ref string result)
        {
            if (!IsMoDefinableBook(book)) return true;

            if (TryGetCachedRecord(book, out var record) &&
                !string.IsNullOrWhiteSpace(record.Synopsis))
            {
                result = BookTextApplier.BuildDisplayText(book, record.Synopsis);
                return false;
            }

            return true;
        }

        private static bool IsMoDefinableBook(Book book)
        {
            if (book == null) return false;

            var typeName = book.GetType().FullName;
            if (typeName != null &&
                typeName.Equals(MoBookTypeName, System.StringComparison.Ordinal))
                return true;

            var def = book.def;
            var comps = def?.comps;
            if (comps == null) return false;

            for (int i = 0; i < comps.Count; i++)
            {
                var comp = comps[i];
                if (comp == null) continue;
                var compType = comp.GetType();
                if (compType.FullName == MoDefinableBookCompPropsTypeName)
                    return true;
            }

            return false;
        }

        private static bool TryGetCachedRecord(Thing thing, out BookSynopsisRecord record)
        {
            record = null;
            if (thing == null || thing.DestroyedOrNull()) return false;
            if (!BookFilterPolicy.IsAllowed(thing)) return false;

            var cache = LiteratueSaveData.Current?.SynopsisCache;
            if (cache == null) return false;

            if (!BookKeyProvider.TryGetKey(thing, out var key)) return false;

            return cache.TryGet(key, out record) && record != null;
        }
    }
}
