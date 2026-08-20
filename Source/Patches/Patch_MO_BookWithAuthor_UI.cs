/*
 * Purpose:
 * - Override Medieval Overhaul BookWithAuthor UI text using cached synopsis data.
 *
 * Notes:
 * - Avoids direct MO assembly dependency by resolving type by name.
 * - Falls back to original behavior when no cached record exists.
 */
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Ustas.RimAI.Art.Books;
using Ustas.RimAI.Art.Integration;
using Ustas.RimAI.Art.Storage;
using Ustas.RimAI.Art.Storage.Save;
using Verse;

namespace Ustas.RimAI.Art.Patches
{
    [HarmonyPatch]
    public static class Patch_MO_BookWithAuthor_UI
    {
        private const string MoBookTypeName = "MedievalOverhaul.BookWithAuthor";

        public static bool Prepare()
        {
            return AccessTools.TypeByName(MoBookTypeName) != null;
        }

        public static IEnumerable<MethodBase> TargetMethods()
        {
            var type = AccessTools.TypeByName(MoBookTypeName);
            if (type == null) yield break;

            var labelGetter = AccessTools.PropertyGetter(type, "LabelNoCount");
            if (labelGetter != null) yield return labelGetter;

            var labelNoParenGetter = AccessTools.PropertyGetter(type, "LabelNoParenthesis");
            if (labelNoParenGetter != null) yield return labelNoParenGetter;

            var flavorGetter = AccessTools.PropertyGetter(type, "DescriptionFlavor");
            if (flavorGetter != null) yield return flavorGetter;

            var descriptionGetter = AccessTools.PropertyGetter(type, "DescriptionDetailed");
            if (descriptionGetter != null) yield return descriptionGetter;
        }

        public static bool Prefix(object __instance, ref string __result, MethodBase __originalMethod)
        {
            if (__instance is not Thing thing) return true;

            if (!TryGetCachedRecord(thing, out var record)) return true;

            var methodName = __originalMethod.Name ?? string.Empty;
            if (methodName.Contains("get_LabelNoCount") || methodName.Contains("get_LabelNoParenthesis"))
            {
                if (!string.IsNullOrWhiteSpace(record.Title))
                {
                    __result = record.Title;
                    return false;
                }
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(record.Synopsis))
                {
                    if (__instance is Book book)
                        __result = BookTextApplier.BuildDisplayText(book, record.Synopsis);
                    else
                        __result = record.Synopsis;
                    return false;
                }
            }

            return true;
        }

        private static bool TryGetCachedRecord(Thing thing, out BookSynopsisRecord record)
        {
            record = null;
            if (thing == null || thing.DestroyedOrNull()) return false;
            if (!BookFilterPolicy.IsAllowed(thing)) return false;

            var cache = LiteratureSaveData.Current?.SynopsisCache;
            if (cache == null) return false;

            if (!BookKeyProvider.TryGetKey(thing, out var key)) return false;

            return cache.TryGet(key, out record) && record != null;
        }
    }
}
