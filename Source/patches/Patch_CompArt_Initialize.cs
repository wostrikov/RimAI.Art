/*
 * Purpose:
 * - Enqueue art for LLM description after art is initialized.
 */
using HarmonyLib;
using Ustas.RimAI.Art.scanner.production;
using RimWorld;

namespace Ustas.RimAI.Art.patches
{
    [HarmonyPatch(typeof(CompArt), "InitializeArtInternal")]
    public static class Patch_CompArt_Initialize
    {
        public static void Postfix(CompArt __instance)
        {
            if (__instance?.parent == null) return;
            ArtProductionTracker.NotifyGenerated(__instance.parent);
        }
    }
}
