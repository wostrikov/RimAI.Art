using System.Collections.Generic;
using HarmonyLib;
using Ustas.RimAI.Art.Manual;
using Verse;

namespace Ustas.RimAI.Art.Patches
{
    [HarmonyPatch(typeof(ThingWithComps), nameof(ThingWithComps.GetGizmos))]
    public static class Patch_ThingWithComps_Gizmos
    {
        public static void Postfix(ThingWithComps __instance, ref IEnumerable<Gizmo> __result)
        {
            if (__instance == null) return;

            var gizmo = ManualTextEditService.CreateGizmo(__instance);
            if (gizmo == null) return;

            __result = Append(__result, gizmo);
        }

        private static IEnumerable<Gizmo> Append(IEnumerable<Gizmo> source, Gizmo extra)
        {
            if (source != null)
            {
                foreach (var gizmo in source)
                    yield return gizmo;
            }

            yield return extra;
        }
    }
}
