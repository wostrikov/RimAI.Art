/*
 * Purpose:
 * - Override art description (outside art tab) using cached art text.
 */
using HarmonyLib;
using Ustas.RimAI.Art.Integration;
using RimWorld;

namespace Ustas.RimAI.Art.Patches
{
    [HarmonyPatch(typeof(CompArt), nameof(CompArt.GetDescriptionPart))]
    public static class Patch_CompArt_DescriptionPart
    {
        public static bool Prefix(CompArt __instance, ref string __result, ref bool __state)
        {
            if (__instance?.parent == null) return true;
            if (ArtCacheUtil.AllowsDescriptionEdit(__instance.parent))
            {
                if (ArtClickSafety.TryGetSafeImageDescription(__instance, out var safe))
                {
                    __result = safe.RawText;
                    return false;
                }

                if (ArtCacheUtil.TryGetRecord(__instance.parent, out var record) &&
                    ArtCacheUtil.TryBuildDescription(record, __instance.AuthorName, out var description))
                {
                    __result = description;
                    return false;
                }

                return true;
            }

            if (ArtCacheUtil.AllowsArtTabEdit(__instance.parent))
            {
                ArtCacheUtil.PushArtTabOverrideSuppression();
                __state = true;
            }

            return true;
        }

        public static void Postfix(bool __state)
        {
            if (__state)
                ArtCacheUtil.PopArtTabOverrideSuppression();
        }
    }
}
