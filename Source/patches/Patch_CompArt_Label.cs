/*
 * Purpose:
 * - Override art item labels (outside art tab) using cached art titles.
 */
using HarmonyLib;
using Ustas.RimAI.Art.integration;
using RimWorld;

namespace Ustas.RimAI.Art.patches
{
    [HarmonyPatch(typeof(CompArt), nameof(CompArt.TransformLabel))]
    public static class Patch_CompArt_Label
    {
        public static bool Prefix(CompArt __instance, ref string __result, string label)
        {
            if (__instance?.parent == null) return true;
            if (__instance.parent.StyleSourcePrecept != null) return true;
            if (!ArtCacheUtil.AllowsLabelEdit(__instance.parent)) return true;
            return true;
        }
    }
}
