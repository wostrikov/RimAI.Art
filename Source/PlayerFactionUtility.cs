using RimWorld;
using Verse;

namespace Ustas.RimAI.Art
{
    internal static class PlayerFactionUtility
    {
        public static bool TryGetPlayerFaction(out Faction faction)
        {
            faction = null;

            var factionManager = Find.FactionManager;
            if (factionManager == null) return false;

            var factions = factionManager.AllFactionsListForReading;
            if (factions == null) return false;

            for (int i = 0; i < factions.Count; i++)
            {
                var candidate = factions[i];
                if (candidate?.def == FactionDefOf.PlayerColony)
                {
                    faction = candidate;
                    return true;
                }
            }

            return false;
        }

        public static bool IsPlayerFactionPawn(Pawn pawn)
        {
            return pawn?.Faction != null
                && TryGetPlayerFaction(out var playerFaction)
                && pawn.Faction == playerFaction;
        }
    }
}
