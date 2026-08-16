/*
 * Purpose:
 * - Queue ideology description flavor after regeneration.
 *
 * Uses:
 * - RimWorld Ideo
 * - IdeoDescriptionRewriter
 *
 * Responsibilities:
 * - Capture regenerated ideology descriptions for LLM append.
 */
using HarmonyLib;
using Ustas.RimAI.Art.events;
using RimWorld;

namespace Ustas.RimAI.Art.patches
{
    [HarmonyPatch(typeof(Ideo), nameof(Ideo.RegenerateDescription))]
    public static class Patch_Ideo_RegenerateDescription
    {
        public static void Postfix(Ideo __instance)
        {
            IdeoDescriptionRewriter.TryQueue(__instance);
        }
    }
}
