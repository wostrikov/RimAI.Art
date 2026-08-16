using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Art.settings;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Art.art
{
    public static class ArtDefFilterPolicy
    {
        public static bool IsAllowed(Thing thing)
        {
            return IsAllowed(thing?.def);
        }

        public static bool IsAllowed(ThingDef def)
        {
            if (!SupportsDef(def)) return false;

            var settings = LiteratureMod.Settings;
            if (settings == null) return false;

            settings.EnsureContentFiltersInitialized();
            var allowList = settings.artRewriteAllowList;
            return allowList != null &&
                   allowList.Count > 0 &&
                   !string.IsNullOrWhiteSpace(def.defName) &&
                   allowList.Contains(def.defName);
        }

        public static bool SupportsDef(ThingDef def)
        {
            if (def == null) return false;
            if (def.IsArt) return true;
            if (HasComp<CompArt>(def)) return true;
            if (HasComp<CompBladelinkWeapon>(def)) return true;
            return false;
        }

        public static List<ThingDef> GetEligibleDefs()
        {
            var defs = DefDatabase<ThingDef>.AllDefsListForReading;
            if (defs == null || defs.Count == 0)
                return new List<ThingDef>();

            return defs
                .Where(SupportsDef)
                .Where(def => !string.IsNullOrWhiteSpace(def.defName))
                .OrderBy(GetSortLabel)
                .ThenBy(def => def.defName)
                .ToList();
        }

        public static List<string> GetAllEligibleDefNames()
        {
            return GetEligibleDefs()
                .Select(def => def.defName)
                .ToList();
        }

        public static string GetDisplayLabel(ThingDef def)
        {
            if (def == null) return string.Empty;
            return string.IsNullOrWhiteSpace(def.label)
                ? def.defName
                : $"{def.label} ({def.defName})";
        }

        private static bool HasComp<TComp>(ThingDef def) where TComp : ThingComp
        {
            return def.comps != null &&
                   def.comps.Any(comp => comp?.compClass != null && typeof(TComp).IsAssignableFrom(comp.compClass));
        }

        private static string GetSortLabel(ThingDef def)
        {
            return string.IsNullOrWhiteSpace(def?.label) ? def?.defName ?? string.Empty : def.label;
        }
    }
}
