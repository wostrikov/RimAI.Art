using System;
using Ustas.RimAI.Art.Settings;
using RimWorld;
using Verse;

namespace Ustas.RimAI.Art.Art
{
    [Flags]
    public enum ArtEditTarget
    {
        None = 0,
        ArtTab = 1,
        Label = 2,
        Description = 4
    }

    public static class ArtEditPolicy
    {
        public static ArtEditTarget GetTargets(Thing thing)
        {
            if (thing == null) return ArtEditTarget.None;

            var settings = LiteratureMod.Settings;
            if (settings == null)
                return ArtEditTarget.None;

            var def = thing.def;
            if (!ArtDefFilterPolicy.IsAllowed(def))
                return ArtEditTarget.None;

            bool isArtBenchItem = def?.IsArt == true;
            bool isWeapon = def?.IsWeapon == true;
            bool isApparel = def?.IsApparel == true;
            bool isBuilding = def?.category == ThingCategory.Building;
            bool allowBuildings = settings.allowArtBuildingEdits;
            bool allowWeapons = settings.allowArtWeaponEdits;
            bool allowApparel = settings.allowArtApparelEdits;
            if (!allowBuildings && !allowWeapons && !allowApparel)
                return ArtEditTarget.None;

            var compArt = thing.TryGetComp<CompArt>();
            bool hasArtTag = compArt != null && compArt.CanShowArt;

            var bladelink = thing.TryGetComp<CompBladelinkWeapon>();
            if (bladelink != null)
            {
                if (!allowWeapons) return ArtEditTarget.None;
                if (bladelink.CodedPawn == null) return ArtEditTarget.None;
                return ArtEditTarget.Label | ArtEditTarget.Description;
            }

            if (isArtBenchItem)
            {
                if (!allowBuildings) return ArtEditTarget.None;
                var targets = ArtEditTarget.Label | ArtEditTarget.Description;
                if (hasArtTag)
                    targets |= ArtEditTarget.ArtTab;
                return targets;
            }

            if (!hasArtTag)
                return ArtEditTarget.None;

            if (isWeapon || isApparel)
            {
                if (isWeapon && !allowWeapons) return ArtEditTarget.None;
                if (isApparel && !allowApparel) return ArtEditTarget.None;
                return ArtEditTarget.Label | ArtEditTarget.ArtTab;
            }

            if (isBuilding)
            {
                if (!allowBuildings) return ArtEditTarget.None;
                return ArtEditTarget.ArtTab;
            }

            return ArtEditTarget.ArtTab;
        }

        public static bool Allows(Thing thing, ArtEditTarget target)
        {
            var targets = GetTargets(thing);
            return (targets & target) != 0;
        }

        public static bool ShouldGenerate(Thing thing, bool allowLabelEdits)
        {
            var targets = GetTargets(thing);
            if (targets == ArtEditTarget.None) return false;

            if ((targets & ArtEditTarget.ArtTab) != 0)
                return true;

            if (!allowLabelEdits)
                return false;

            return (targets & (ArtEditTarget.Label | ArtEditTarget.Description)) != 0;
        }
    }
}
