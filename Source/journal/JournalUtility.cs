using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Ustas.RimAI.Art.journal
{
    public static class JournalUtility
    {
        public const int LogsRequired = 1;

        public static ThingDef GetLogDef()
        {
            return DefDatabase<ThingDef>.GetNamedSilentFail("WoodLog");
        }

        public static int CountAvailableLogs(Map map)
        {
            if (map == null) return 0;
            var def = GetLogDef();
            if (def == null) return 0;

            int count = 0;
            List<Thing> things = map.listerThings.ThingsOfDef(def);
            for (int i = 0; i < things.Count; i++)
            {
                var thing = things[i];
                if (thing == null || thing.DestroyedOrNull()) continue;
                count += thing.stackCount;
                if (count >= LogsRequired) return count;
            }

            return count;
        }

        public static Thing FindClosestLog(Pawn pawn)
        {
            if (pawn == null || pawn.Map == null) return null;

            var def = GetLogDef();
            if (def == null) return null;

            return GenClosest.ClosestThingReachable(
                pawn.Position,
                pawn.Map,
                ThingRequest.ForDef(def),
                PathEndMode.ClosestTouch,
                TraverseParms.For(pawn),
                9999f,
                thing => thing != null && !thing.DestroyedOrNull());
        }

        public static bool IsTable(Thing thing)
        {
            if (thing == null) return false;
            if (thing.def == null) return false;
            return thing.def.IsTable;
        }
    }
}
