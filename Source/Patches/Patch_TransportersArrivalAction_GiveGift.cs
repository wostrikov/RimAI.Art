
using System.Collections.Generic;
using HarmonyLib;
using Ustas.RimAI.Art.Events.Quests;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Ustas.RimAI.Art.Patches
{
    [HarmonyPatch(typeof(TransportersArrivalAction_GiveGift), nameof(TransportersArrivalAction_GiveGift.Arrived))]
    public static class Patch_TransportersArrivalAction_GiveGift
    {
        public static void Postfix(List<ActiveTransporterInfo> transporters, PlanetTile tile)
        {
            if (transporters == null || transporters.Count == 0) return;
            var worldObjects = Find.WorldObjects;
            if (worldObjects == null) return;
            var settlement = worldObjects.SettlementAt(tile);
            if (settlement == null) return;
            QuestEventScheduler.TryHandleGiftDelivery(settlement, transporters);
        }
    }
}
