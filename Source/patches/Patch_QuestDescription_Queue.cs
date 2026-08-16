/*
 * Purpose:
 * - Queue quest description rewrites after quests are generated.
 *
 * Uses:
 * - RimWorld QuestUtility
 * - QuestDescriptionRewriter
 *
 * Responsibilities:
 * - Capture new quests and enqueue their description for rewrite.
 *
 * Design notes:
 * - Does not alter quest data directly here.
 */
using System;
using HarmonyLib;
using Ustas.RimAI.Art.events;
using RimWorld;
using RimWorld.QuestGen;
using Verse;

namespace Ustas.RimAI.Art.patches
{
    [HarmonyPatch(typeof(QuestUtility), nameof(QuestUtility.GenerateQuestAndMakeAvailable), new[] { typeof(QuestScriptDef), typeof(Slate) })]
    public static class Patch_QuestDescription_Queue
    {
        public static void Postfix(Quest __result)
        {
            QuestDescriptionRewriter.TryQueue(__result);
        }
    }
}
