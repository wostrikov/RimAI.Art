/*
 * Purpose:
 * - Detect when a pawn finishes producing a book via Bill_Production.
 *
 * Uses:
 * - RimWorld Bill_Production
 * - BookProductionTracker
 *
 * Responsibilities:
 * - Hook the production completion point.
 * - Notify the literature system that a new book exists.
 *
 * Design notes:
 * - Patch MUST be minimal and non-invasive.
 *
 * Do NOT:
 * - Do not generate LLM content here.
 * - Do not modify RimTalk core logic.
 * - Do not assume the produced Thing is always a book.
 */
using System.Collections.Generic;
using HarmonyLib;
using Ustas.RimAI.Art.Scanner.Production;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Art.Patches
{
    [HarmonyPatch(typeof(Bill_Production), "Notify_IterationCompleted")]
    public static class Patch_BillProduction_Finish
    {
        public static void Postfix(Bill_Production __instance, Pawn billDoer, List<Thing> ingredients)
        {
            BookProductionTracker.NotifyProduced(billDoer);
        }
    }
}
