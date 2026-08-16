/*
 * Purpose:
 * - Detect Kiiro textbook production and enqueue for book processing.
 *
 * Uses:
 * - Verse.RecipeWorker
 * - BookProductionTracker
 *
 * Responsibilities:
 * - Hook the Kiiro_Make_TextBook recipe completion.
 * - Notify the literature system that a new book exists.
 *
 * Notes:
 * - Avoids direct Kiiro assembly references by checking recipe defName.
 */
using System.Collections.Generic;
using HarmonyLib;
using Ustas.RimAI.Art.scanner.production;
using Verse;

namespace Ustas.RimAI.Art.patches
{
    [HarmonyPatch(typeof(RecipeWorker), nameof(RecipeWorker.Notify_IterationCompleted))]
    public static class Patch_Kiiro_MakeTextBook
    {
        private const string KiiroRecipeTextBook = "Kiiro_Make_TextBook";
        private const string KiiroRecipeNovel = "Kiiro_Make_Novel";

        public static void Postfix(RecipeWorker __instance, Pawn billDoer, List<Thing> ingredients)
        {
            if (billDoer == null) return;
            var recipe = __instance?.recipe;
            if (recipe == null) return;
            var defName = recipe.defName ?? string.Empty;
            if (!string.Equals(defName, KiiroRecipeTextBook, System.StringComparison.Ordinal) &&
                !string.Equals(defName, KiiroRecipeNovel, System.StringComparison.Ordinal))
                return;

            Log.Message($"[RimAI.Art] Kiiro recipe completed: {defName} by {billDoer.LabelShort}.");
            BookProductionTracker.NotifyProduced(billDoer);
        }
    }
}
