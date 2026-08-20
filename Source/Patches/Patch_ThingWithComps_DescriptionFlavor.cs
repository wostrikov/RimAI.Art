/*
 * Purpose:
 * - Override descriptions for non-CompArt items using cached art text.
 */
using HarmonyLib;
using Ustas.RimAI.Art.Integration;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Art.Patches
{
    [HarmonyPatch(typeof(ThingWithComps), "get_DescriptionFlavor")]
    public static class Patch_ThingWithComps_DescriptionFlavor
    {
        public static void Postfix(ThingWithComps __instance, ref string __result)
        {
            if (__instance == null) return;
            var bladelink = __instance.TryGetComp<CompBladelinkWeapon>();
            if (bladelink == null && __instance.TryGetComp<CompArt>() != null) return;
            if (!ArtCacheUtil.AllowsDescriptionEdit(__instance)) return;

            if (ArtCacheUtil.TryGetRecord(__instance, out var record) &&
                ArtCacheUtil.TryBuildDescription(record, string.Empty, out var description))
            {
                __result = description;
            }
        }
    }
}
