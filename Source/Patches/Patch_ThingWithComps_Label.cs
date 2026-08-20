/*
 * Purpose:
 * - Override labels for non-CompArt items using cached art titles.
 */
using HarmonyLib;
using Ustas.RimAI.Art.Integration;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Art.Patches
{
    [HarmonyPatch(typeof(ThingWithComps), "get_LabelNoCount")]
    public static class Patch_ThingWithComps_Label
    {
        public static void Postfix(ThingWithComps __instance, ref string __result)
        {
            if (__instance == null) return;
            if (__instance.StyleSourcePrecept != null) return;
            if (!ArtCacheUtil.AllowsLabelEdit(__instance)) return;

            if (ArtCacheUtil.TryGetRecord(__instance, out var record) &&
                ArtCacheUtil.TryBuildLabel(__instance, record.Title, out var label))
            {
                __result = label;
            }
        }
    }
}
