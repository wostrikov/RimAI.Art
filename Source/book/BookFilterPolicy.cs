using System.Collections.Generic;
using System.Linq;
using Ustas.RimAI.Art.book.journal;
using Ustas.RimAI.Art.settings;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Art.book
{
    public static class BookFilterPolicy
    {
        public static bool IsAllowed(BookMeta meta)
        {
            return IsAllowed(meta?.Thing?.def);
        }

        public static bool IsAllowed(Thing thing)
        {
            return IsAllowed(thing?.def);
        }

        public static bool IsAllowed(ThingDef def)
        {
            if (!IsBookDef(def)) return false;

            var settings = LiteratureMod.Settings;
            if (settings == null || !settings.enabled) return false;

            settings.EnsureContentFiltersInitialized();
            var allowList = settings.bookRewriteAllowList;
            return allowList != null &&
                   allowList.Count > 0 &&
                   !string.IsNullOrWhiteSpace(def.defName) &&
                   allowList.Contains(def.defName);
        }

        public static bool IsBookDef(ThingDef def)
        {
            if (def == null) return false;
            if (def.GetModExtension<JournalBookExtension>() != null) return true;
            if (def.thingClass != null && typeof(Book).IsAssignableFrom(def.thingClass)) return true;
            if (def.HasComp<CompBook>()) return true;
            return false;
        }

        public static List<ThingDef> GetEligibleDefs()
        {
            var defs = DefDatabase<ThingDef>.AllDefsListForReading;
            if (defs == null || defs.Count == 0)
                return new List<ThingDef>();

            return defs
                .Where(IsBookDef)
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

        private static string GetSortLabel(ThingDef def)
        {
            return string.IsNullOrWhiteSpace(def?.label) ? def?.defName ?? string.Empty : def.label;
        }
    }
}
