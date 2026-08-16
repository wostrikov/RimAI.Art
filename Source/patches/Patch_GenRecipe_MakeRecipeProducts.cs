/*
 * Purpose:
 * - Track produced books directly from recipe output.
 *
 * Uses:
 * - Verse.GenRecipe.MakeRecipeProducts
 * - BookProductionTracker
 *
 * Responsibilities:
 * - Capture produced Things and enqueue books immediately.
 *
 * Notes:
 * - Uses a wrapper enumerable to avoid double enumeration.
 */
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Ustas.RimAI.Art.scanner.production;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Art.patches
{
    [HarmonyPatch(typeof(GenRecipe), nameof(GenRecipe.MakeRecipeProducts))]
    public static class Patch_GenRecipe_MakeRecipeProducts
    {
        public static void Postfix(
            RecipeDef recipeDef,
            Pawn worker,
            ref IEnumerable<Thing> __result)
        {
            if (__result == null) return;
            var list = __result.ToList();
            BookProductionTracker.NotifyProducts(list, worker, recipeDef);
            __result = list;
        }
    }
}
