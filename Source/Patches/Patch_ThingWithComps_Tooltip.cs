/*
 * Purpose:
 * - Keep tooltip description consistent with the item's DescriptionFlavor.
 */
using System.Text;
using HarmonyLib;
using Ustas.RimAI.Art.Integration;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Art.Patches
{
    [HarmonyPatch(typeof(ThingWithComps), nameof(ThingWithComps.GetTooltip))]
    public static class Patch_ThingWithComps_Tooltip
    {
        public static void Postfix(ThingWithComps __instance, ref TipSignal __result)
        {
            if (__instance == null) return;
            if (!ArtCacheUtil.AllowsDescriptionEdit(__instance)) return;
            if (!ArtCacheUtil.TryGetRecord(__instance, out var record)) return;
            if (!ArtCacheUtil.TryBuildDescription(record, string.Empty, out var description)) return;

            var sb = new StringBuilder();
            sb.Append(__instance.LabelNoParenthesisCap.AsTipTitle());
            sb.Append(GenLabel.LabelExtras(__instance, includeHp: true, includeQuality: true));
            sb.Append("\n\n");
            sb.Append(description);

            if (__instance.def.useHitPoints)
                sb.Append($"\n{__instance.HitPoints} / {__instance.MaxHitPoints}");

            string text = sb.ToString() + "\n";
            if (__instance.AllComps != null)
            {
                for (int i = 0; i < __instance.AllComps.Count; i++)
                {
                    var extra = __instance.AllComps[i]?.CompTipStringExtra();
                    if (!extra.NullOrEmpty())
                        text = $"{text}\n{extra}";
                }
            }

            __result = new TipSignal(text, __instance.thingIDNumber * 251235);
        }
    }
}
