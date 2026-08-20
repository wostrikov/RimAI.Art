/*
 * Purpose:
 * - Queue letter text rewrites after letters are received.
 *
 * Uses:
 * - Verse LetterStack
 * - LetterTextRewriter
 *
 * Responsibilities:
 * - Capture letters and enqueue their text for flavor append.
 */
using HarmonyLib;
using Ustas.RimAI.Art.Events;
using Verse;

namespace Ustas.RimAI.Art.Patches
{
    [HarmonyPatch(typeof(LetterStack), nameof(LetterStack.ReceiveLetter), new[] { typeof(Letter), typeof(string), typeof(int), typeof(bool) })]
    public static class Patch_LetterStack_Receive
    {
        public static void Postfix(Letter let)
        {
            LetterTextRewriter.TryQueue(let);
        }
    }
}
