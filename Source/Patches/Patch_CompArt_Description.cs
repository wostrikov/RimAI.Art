/*
 * Purpose:
 * - Override CompArt description text when cached art description is available.
 */
using HarmonyLib;
using Ustas.RimAI.Art.Integration;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Art.Patches
{
    [HarmonyPatch(typeof(CompArt), nameof(CompArt.GenerateImageDescription))]
    public static class Patch_CompArt_Description
    {
        public static bool Prefix(CompArt __instance, ref TaggedString __result)
        {
            if (__instance?.parent == null) return true;
            if (ArtClickSafety.TryGetSafeImageDescription(__instance, out __result))
                return false;
            if (ArtCacheUtil.IsArtTabOverrideSuppressed)
                return ArtClickSafety.IsVanillaDescriptionSafe(__instance);
            return true;
        }
    }
}
