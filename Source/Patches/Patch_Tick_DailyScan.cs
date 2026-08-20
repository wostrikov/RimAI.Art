/*
 * Purpose:
 * - Periodically trigger book scanning (e.g. once per in-game day).
 *
 * Uses:
 * - RimWorld tick system
 * - BookScanScheduler
 * - ArtScanScheduler
 *
 * Responsibilities:
 * - Delegate timing decisions to scheduler.
 *
 * Design notes:
 * - Patch should be extremely lightweight.
 *
 * Do NOT:
 * - Do not scan maps directly.
 * - Do not perform LLM calls here.
 */
using HarmonyLib;
using Ustas.RimAI.Art.Scanner;
using Verse;

namespace Ustas.RimAI.Art.Patches
{
    [HarmonyPatch(typeof(TickManager), nameof(TickManager.DoSingleTick))]
    public static class Patch_Tick_DailyScan
    {
        public static void Postfix()
        {
            BookScanScheduler.TryDailyScan();
            ArtScanScheduler.TryDailyScan();
        }
    }
}
