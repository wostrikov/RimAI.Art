/*
 * Purpose:
 * - Override CompArt title when cached art description is available.
 */
using HarmonyLib;
using Ustas.RimAI.Art.integration;
using RimWorld;

namespace Ustas.RimAI.Art.patches
{
    [HarmonyPatch(typeof(CompArt), "get_Title")]
    public static class Patch_CompArt_Title
    {
        public static bool Prefix(CompArt __instance, ref string __result)
        {
            if (__instance?.parent == null) return true;
            if (__instance.parent.StyleSourcePrecept != null) return true;
            if (ArtCacheUtil.IsArtTabOverrideSuppressed) return true;
            if (!ArtCacheUtil.AllowsArtTabEdit(__instance.parent)) return true;

            if (ArtCacheUtil.TryGetRecord(__instance.parent, out var record) &&
                !string.IsNullOrWhiteSpace(record.Title))
            {
                __result = record.Title;
                return false;
            }

            return true;
        }
    }
}
