/*
 * Purpose:
 * - Trigger persona weapon LLM update when the weapon is bonded to a pawn.
 */
using HarmonyLib;
using Ustas.RimAI.Art.Art;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Art.Patches
{
    [HarmonyPatch(typeof(CompBladelinkWeapon), "OnCodedFor")]
    public static class Patch_CompBladelinkWeapon_Bonded
    {
        public static void Postfix(CompBladelinkWeapon __instance, Pawn pawn)
        {
            if (__instance?.parent == null || pawn == null) return;
            PersonaWeaponAuthoringPipeline.StartGeneration(__instance.parent, pawn, "bonded");
        }
    }
}
