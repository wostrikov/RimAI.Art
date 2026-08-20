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
            if (ArtCacheUtil.IsArtTabOverrideSuppressed) return true;
            if (!ArtCacheUtil.AllowsArtTabEdit(__instance.parent)) return true;

            if (ArtCacheUtil.TryGetRecord(__instance.parent, out var record) &&
                !string.IsNullOrWhiteSpace(record.Text))
            {
                __result = record.Text;
                return false;
            }

            return true;
        }
    }
}
