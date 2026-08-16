/*
 * Purpose:
 * - Persist scheduled warning-raid follow-ups.
 *
 * Uses:
 * - Verse.IExposable
 * - RimWorld.Planet.MapParent
 *
 * Responsibilities:
 * - Store raid timing and target details for save/load.
 */
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Ustas.RimAI.Art.events.quests
{
    public sealed class WarningRaidRecord : IExposable
    {
        public int QuestId = -1;
        public int DueTick = -1;
        public int AcceptedTick = -1;
        public float Points;
        public Faction Faction;
        public MapParent TargetParent;

        public void ExposeData()
        {
            Scribe_Values.Look(ref QuestId, "questId", -1);
            Scribe_Values.Look(ref DueTick, "dueTick", -1);
            Scribe_Values.Look(ref AcceptedTick, "acceptedTick", -1);
            Scribe_Values.Look(ref Points, "points", 0f);
            Scribe_References.Look(ref Faction, "faction");
            Scribe_References.Look(ref TargetParent, "targetParent");
        }
    }
}
