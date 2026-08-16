using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Art.tv
{
    public static class TvFilterPolicy
    {
        private static HashSet<ThingDef> _tvDefs;
        private static List<ThingDef> _tvDefsList;

        public static bool IsTelevision(Thing thing)
        {
            if (thing == null || thing.DestroyedOrNull()) return false;
            EnsureCache();
            return _tvDefs != null && _tvDefs.Contains(thing.def);
        }

        public static List<ThingDef> GetAllEligibleDefs()
        {
            EnsureCache();
            return _tvDefsList ?? new List<ThingDef>();
        }

        public static List<string> GetAllEligibleDefNames()
        {
            var defs = GetAllEligibleDefs();
            var names = new List<string>(defs.Count);
            for (int i = 0; i < defs.Count; i++)
            {
                if (defs[i] != null && !string.IsNullOrWhiteSpace(defs[i].defName))
                    names.Add(defs[i].defName);
            }
            return names;
        }

        private static void EnsureCache()
        {
            if (_tvDefs != null) return;

            _tvDefs = new HashSet<ThingDef>();
            var joyGiverDefs = DefDatabase<JoyGiverDef>.AllDefsListForReading;
            if (joyGiverDefs == null) return;

            for (int i = 0; i < joyGiverDefs.Count; i++)
            {
                var def = joyGiverDefs[i];
                if (def == null) continue;
                if (def.jobDef == null || def.jobDef.defName != "WatchTelevision") continue;

                if (def.thingDefs == null) continue;
                for (int j = 0; j < def.thingDefs.Count; j++)
                {
                    var thingDef = def.thingDefs[j];
                    if (thingDef != null)
                        _tvDefs.Add(thingDef);
                }
            }

            _tvDefsList = new List<ThingDef>(_tvDefs);
        }
    }
}
